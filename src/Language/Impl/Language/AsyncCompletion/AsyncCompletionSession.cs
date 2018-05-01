using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Strings = Microsoft.VisualStudio.Language.Intellisense.Implementation.Strings;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    /// <summary>
    /// Holds a state of the session
    /// and a reference to the UI element
    /// </summary>
    internal class AsyncCompletionSession : IAsyncCompletionSession, IModelComputationCallbackHandler<CompletionModel>, IPropertyOwner
    {
        // Available data and services
        private readonly IList<(IAsyncCompletionSource Source, SnapshotPoint Point)> _completionSources;
        private readonly IList<(IAsyncCompletionCommitManager, ITextBuffer)> _commitManagers;
        private readonly IAsyncCompletionItemManager _completionItemManager;
        private readonly SnapshotSpan _initialApplicableSpan;
        private readonly JoinableTaskContext JoinableTaskContext;
        private readonly ICompletionPresenterProvider _presenterProvider;
        private readonly AsyncCompletionBroker _broker;
        private readonly ITextView _textView;
        private readonly IGuardedOperations _guardedOperations;
        private readonly ImmutableArray<char> _potentialCommitChars;

        // Presentation:
        ICompletionPresenter _gui; // Must be accessed from GUI thread
        const int FirstIndex = 0;
        readonly int PageStepSize;

        // Computation state machine
        private ModelComputation<CompletionModel> _computation;
        private readonly CancellationTokenSource _computationCancellation = new CancellationTokenSource();
        int _lastFilteringTaskId;

        // Fixed completion model data that does not change throughout the session:

        /// <summary>
        /// Span pertinent to this completion.
        /// </summary>
        public ITrackingSpan ApplicableSpan { get; private set; }

        /// <summary>
        /// Stores the initial reason this session was triggererd.
        /// </summary>
        private CompletionTriggerReason InitialTriggerReason { get; set; }

        /// <summary>
        /// Text to display in place of suggestion mode when filtered text is empty.
        /// </summary>
        private SuggestionItemOptions SuggestionItemOptions { get; set; }

        private IAsyncCompletionSource SuggestionModeCompletionItemSource { get; set; }

        // Above: Fixed completion model data that does not change throughout the session.

        /// <summary>
        /// Telemetry aggregator for this session
        /// </summary>
        private readonly CompletionSessionTelemetry _telemetry;

        /// <summary>
        /// Self imposed maximum delay for commits due to user double-clicking completion item in the UI
        /// </summary>
        private static readonly TimeSpan MaxCommitDelayWhenClicked = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Tracks time spent on the worker thread - getting data, filtering and sorting. Used for telemetry.
        /// </summary>
        private Stopwatch ComputationStopwatch { get; } = new Stopwatch();

        /// <summary>
        /// Tracks time spent on the UI thread - either rendering or committing. Used for telemetry.
        /// </summary>
        private Stopwatch UiStopwatch { get; } = new Stopwatch();

        private static SuggestionItemOptions DefaultSuggestionModeOptions = new SuggestionItemOptions(string.Empty, Strings.SuggestionModeDefaultTooltip);

        // Facilitate experience when there are no items to display
        private bool _selectionModeBeforeNoResultFallback;
        private bool _inNoResultFallback;
        private bool _ignoreCaretMovement;

        public event EventHandler<CompletionItemEventArgs> ItemCommitted;
        public event EventHandler Dismissed;
        public event EventHandler<CompletionItemsWithHighlightEventArgs> ItemsUpdated;

        public ITextView TextView => _textView;

        // When set, UI will no longer be updated
        public bool IsDismissed { get; private set; }

        public PropertyCollection Properties { get; }

        public AsyncCompletionSession(SnapshotSpan initialApplicableSpan, ImmutableArray<char> potentialCommitChars,
            JoinableTaskContext joinableTaskContext, ICompletionPresenterProvider presenterProvider,
            IList<(IAsyncCompletionSource, SnapshotPoint)> completionSources, IList<(IAsyncCompletionCommitManager, ITextBuffer)> commitManagers,
            IAsyncCompletionItemManager completionService,
            AsyncCompletionBroker broker, ITextView textView, CompletionTelemetryHost telemetryHost,
            IGuardedOperations guardedOperations)
        {
            _initialApplicableSpan = initialApplicableSpan;
            _potentialCommitChars = potentialCommitChars;
            JoinableTaskContext = joinableTaskContext;
            _presenterProvider = presenterProvider;
            _broker = broker;
            _completionSources = completionSources; // still prorotype at the momemnt.
            _commitManagers = commitManagers;
            _completionItemManager = completionService;
            _textView = textView;
            _guardedOperations = guardedOperations;
            _telemetry = new CompletionSessionTelemetry(telemetryHost);
            PageStepSize = presenterProvider?.ResultsPerPage ?? 1;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            Properties = new PropertyCollection();
        }

        bool IAsyncCompletionSession.CommitIfUnique(CancellationToken token)
        {
            if (IsDismissed)
                return false;

            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            // TODO: see what happens when OpenOrUpdate hasn't been called yet
            var lastModel = _computation.WaitAndGetResult(token);
            if (lastModel == null)
            {
                return false;
            }
            if (lastModel.UniqueItem != null)
            {
                CommitItem(default(char), lastModel.UniqueItem, ApplicableSpan, token);
                return true;
            }
            else if (!lastModel.PresentedItems.IsDefaultOrEmpty && lastModel.PresentedItems.Length == 1)
            {
                CommitItem(default(char), lastModel.PresentedItems[0].CompletionItem, ApplicableSpan, token);
                return true;
            }
            else
            {
                // Show the UI, because waitAndGetResult canceled showing the UI.
                UpdateUiInner(lastModel); // We are on the UI thread, so we may call UpdateUiInner
                return false;
            }
        }

        /// <summary>
        /// Entry point for committing. Decides whether to commit or not. The inner commit method calls Dispose to stop this session and hide the UI.
        /// </summary>
        /// <param name="token">Command handler infrastructure provides a token that we should pass to the language service's custom commit method.</param>
        /// <param name="typedChar">It is default(char) when commit was requested by an explcit command (e.g. hitting Tab, Enter or clicking)
        /// and it is not default(char) when commit happens as a result of typing a commit character.</param>
        CommitBehavior IAsyncCompletionSession.Commit(char typedChar, CancellationToken token)
        {
            if (IsDismissed)
                return CommitBehavior.None;

            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            var lastModel = _computation.WaitAndGetResult(token);
            if (lastModel == null)
            {
                return CommitBehavior.None;
            }
            if (lastModel.UseSoftSelection && !(typedChar.Equals(default(char)) || typedChar.Equals('\t')) )
            {
                // In soft selection mode, user commits explicitly (click, tab, e.g. not tied to a text change). Otherwise, we dismiss the session
                ((IAsyncCompletionSession)this).Dismiss();
                return CommitBehavior.None;
            }
            else if (lastModel.SelectSuggestionItem && string.IsNullOrWhiteSpace(lastModel.SuggestionModeItem?.InsertText))
            {
                // When suggestion mode is selected, don't commit empty suggestion
                return CommitBehavior.None;
            }
            else if (lastModel.SelectSuggestionItem)
            {
                // Commit the suggestion mode item
                return CommitItem(typedChar, lastModel.SuggestionModeItem, ApplicableSpan, token);
            }
            else if (lastModel.PresentedItems.IsDefaultOrEmpty)
            {
                // There is nothing to commit
                Dismiss();
                return CommitBehavior.None;
            }
            else
            {
                // Regular commit
                return CommitItem(typedChar, lastModel.PresentedItems[lastModel.SelectedIndex].CompletionItem, ApplicableSpan, token);
            }
        }

        private CommitBehavior CommitItem(char typedChar, CompletionItem itemToCommit, ITrackingSpan applicableSpan, CancellationToken token)
        {
            CommitBehavior behavior = CommitBehavior.None;
            if (IsDismissed)
                return behavior;

            UiStopwatch.Restart();
            IAsyncCompletionCommitManager managerWhoCommitted = null;

            // TODO: Go through commit managers, asking each if they would like to TryCommit
            // and obtaining their CommitBehaviors.
            bool commitHandled = false;
            foreach (var commitManager in _commitManagers)
            {
                var args = _guardedOperations.CallExtensionPoint(
                    errorSource: commitManager,
                    call: () => commitManager.Item1.TryCommit(_textView, commitManager.Item2 /* buffer */, itemToCommit, applicableSpan, typedChar, token),
                    valueOnThrow: CommitResult.Unhandled);

                if (behavior == CommitBehavior.None)
                    behavior = args.Behavior;

                commitHandled |= args.IsHandled;
                if (args.IsHandled)
                {
                    managerWhoCommitted = commitManager.Item1;
                    break;
                }
            }
            if (!commitHandled)
            {
                // Fallback if item is still not committed.
                InsertIntoBuffer(_textView, applicableSpan, itemToCommit.InsertText, typedChar);
            }

            UiStopwatch.Stop();
            _guardedOperations.RaiseEvent(this, ItemCommitted, new CompletionItemEventArgs(itemToCommit));
            Dismiss();

            JoinableTaskContext.Factory.RunAsync(async delegate
            {
                await Task.Yield();
                _telemetry.RecordCommittedSaveAndForget(UiStopwatch.ElapsedMilliseconds, _completionItemManager, _presenterProvider, managerWhoCommitted);
            });
            return behavior;
        }

        private static void InsertIntoBuffer(ITextView view, ITrackingSpan applicableSpan, string insertText, char typeChar)
        {
            var buffer = view.TextBuffer;
            var bufferEdit = buffer.CreateEdit();

            // ApplicableSpan already contains the typeChar and brace completion. Replacing this span will cause us to lose this data.
            // The command handler who invoked this code needs to re-play the type char command, such that we get these changes back.
            bufferEdit.Replace(applicableSpan.GetSpan(buffer.CurrentSnapshot), insertText);
            bufferEdit.Apply();
        }

        public void Dismiss()
        {
            if (IsDismissed)
                return;

            IsDismissed = true;
            _broker.ForgetSession(this);
            _guardedOperations.RaiseEvent(this, Dismissed);
            _textView.Caret.PositionChanged -= OnCaretPositionChanged;
            _computationCancellation.Cancel();

            if (_gui != null)
            {
                var copyOfGui = _gui;
                _guardedOperations.CallExtensionPointAsync(
                    errorSource: _gui,
                    asyncAction: async () =>
                    {
                        await JoinableTaskContext.Factory.SwitchToMainThreadAsync();
                        copyOfGui.FiltersChanged -= OnFiltersChanged;
                        copyOfGui.CommitRequested -= OnCommitRequested;
                        copyOfGui.CompletionItemSelected -= OnItemSelected;
                        copyOfGui.CompletionClosed -= OnGuiClosed;
                        copyOfGui.Close();
                    });
                _gui = null;
            }
        }

        void IAsyncCompletionSession.OpenOrUpdate(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken commandToken)
        {
            if (IsDismissed)
                return;

            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            commandToken.Register(_computationCancellation.Cancel);

            if (_computation == null)
            {
                _computation = new ModelComputation<CompletionModel>(
                    PrioritizedTaskScheduler.AboveNormalInstance,
                    JoinableTaskContext,
                    (model, token) => GetInitialModel(trigger, triggerLocation, token),
                    _computationCancellation.Token,
                    _guardedOperations,
                    this
                    );
            }

            var taskId = Interlocked.Increment(ref _lastFilteringTaskId);
            _computation.Enqueue((model, token) => UpdateSnapshot(model, trigger, FromCompletionTriggerReason(trigger.Reason), triggerLocation, token, taskId), updateUi: true);
        }

        internal void InvokeAndCommitIfUnique(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            if (IsDismissed)
                return;

            if (_computation == null)
            {
                // Compute the unique item.
                // Don't recompute If we already have a model, so that we don't change user's selection.
                ((IAsyncCompletionSession)this).OpenOrUpdate(trigger, triggerLocation, token);
            }

            if (((IAsyncCompletionSession)this).CommitIfUnique(token))
            {
                ((IAsyncCompletionSession)this).Dismiss();
            }
        }

        private static CompletionFilterReason FromCompletionTriggerReason(CompletionTriggerReason reason)
        {
            switch (reason)
            {
                case CompletionTriggerReason.Invoke:
                case CompletionTriggerReason.InvokeAndCommitIfUnique:
                    return CompletionFilterReason.Initial;
                case CompletionTriggerReason.Insertion:
                    return CompletionFilterReason.Insertion;
                case CompletionTriggerReason.Deletion:
                    return CompletionFilterReason.Deletion;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        #region Internal methods accessed by the command handlers

        internal void SetSuggestionMode(bool useSuggestionMode)
        {
            _computation.Enqueue((model, token) => ToggleCompletionModeInner(model, token, useSuggestionMode), updateUi: true);
        }

        internal void SelectDown()
        {
            _computation.Enqueue((model, token) => UpdateSelectedItem(model, +1, token), updateUi: true);
        }

        internal void SelectPageDown()
        {
            _computation.Enqueue((model, token) => UpdateSelectedItem(model, +PageStepSize, token), updateUi: true);
        }

        internal void SelectUp()
        {
            _computation.Enqueue((model, token) => UpdateSelectedItem(model, -1, token), updateUi: true);
        }

        internal void SelectPageUp()
        {
            _computation.Enqueue((model, token) => UpdateSelectedItem(model, -PageStepSize, token), updateUi: true);
        }

        #endregion

        private void OnFiltersChanged(object sender, CompletionFilterChangedEventArgs args)
        {
            var taskId = Interlocked.Increment(ref _lastFilteringTaskId);
            _computation.Enqueue((model, token) => UpdateFilters(model, args.Filters, token, taskId), updateUi: true);
        }

        /// <summary>
        /// Handler for GUI requesting commit, usually through double-clicking.
        /// There is no UI for cancellation, so use self-imposed expiration.
        /// </summary>
        private void OnCommitRequested(object sender, CompletionItemEventArgs args)
        {
            try
            {
                if (_computation == null)
                    return;
                var expiringTokenSource = new CancellationTokenSource(MaxCommitDelayWhenClicked);
                CommitItem(default(char), args.Item, ApplicableSpan, expiringTokenSource.Token);
            }
            catch (Exception ex)
            {
                _guardedOperations.HandleException(this, ex);
            }
        }

        private void OnItemSelected(object sender, CompletionItemSelectedEventArgs args)
        {
            // Note 1: Use this only to react to selection changes initiated by user's mouse\touch operation in the UI, since they cancel the soft selection
            // Note 2: we are not enqueuing a call to update the UI, since this would put us in infinite loop, and the UI is already updated
            _computation.Enqueue((model, token) => UpdateSelectedItem(model, args.SelectedItem, args.SuggestionModeSelected, token), updateUi: false);
        }

        private void OnGuiClosed(object sender, CompletionClosedEventArgs args)
        {
            Dismiss();
        }

        /// <summary>
        /// Determines whether the commit code path should be taken. Since this method is on a typing hot path,
        /// we return quickly if the character is not found in the predefined list of potential commit characters.
        /// Else, we create a mapping point from the top-buffer trigger location to each source's buffer
        /// and return whether any source would like to commit completion.
        /// </summary>
        /// <remarks>This method must run on UI thread because of mapping the point across buffers.
        /// The cancellation token is not used in this case, but provided for future needs.</remarks>
        bool IAsyncCompletionSession.ShouldCommit(char typeChar, SnapshotPoint triggerLocation, CancellationToken token)
        {
            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            if (!_potentialCommitChars.Contains(typeChar))
                return false;

            var mappingPoint = _textView.BufferGraph.CreateMappingPoint(triggerLocation, PointTrackingMode.Negative);
            if (!_commitManagers
                .Select(n => (n.Item1, mappingPoint.GetPoint(n.Item2, PositionAffinity.Predecessor)))
                .Any(n => n.Item2.HasValue))
            {
                // set a breakpoint here, see why there are no available managers
            }
            return _commitManagers
                .Select(n => (n.Item1, mappingPoint.GetPoint(n.Item2, PositionAffinity.Predecessor)))
                .Where(n => n.Item2.HasValue)
                .Any(n => _guardedOperations.CallExtensionPoint(
                    errorSource: n.Item1,
                    call: () => n.Item1.ShouldCommitCompletion(typeChar, n.Item2.Value),
                    valueOnThrow: false));
        }

        /// <summary>
        /// Monitors when user scrolled outside of the applicable span. Note that:
        /// * This event is not raised during regular typing.
        /// * This event is raised by brace completion.
        /// * Typing stretches the applicable span
        /// </summary>
        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            // http://source.roslyn.io/#Microsoft.CodeAnalysis.EditorFeatures/Implementation/IntelliSense/Completion/Controller_CaretPositionChanged.cs,40
            if (_ignoreCaretMovement)
                return;

            _computation.Enqueue((model, token) => HandleCaretPositionChanged(model, e.NewPosition), updateUi: true);
        }

        internal void IgnoreCaretMovement(bool ignore)
        {
            if (IsDismissed)
                return; // This method will be called after committing. Don't act on it.

            _ignoreCaretMovement = ignore;
            if (!ignore)
            {
                // Don't let the session exist in invalid state: ensure that the location of the session is still valid
                _computation?.Enqueue((model, token) => HandleCaretPositionChanged(model, _textView.Caret.Position), updateUi: true);
            }
        }

        async Task IModelComputationCallbackHandler<CompletionModel>.UpdateUi(CompletionModel model, CancellationToken token)
        {
            if (_presenterProvider == null) return;
            await JoinableTaskContext.Factory.SwitchToMainThreadAsync(token);
            if (token.IsCancellationRequested) return;
            UpdateUiInner(model);
        }

        /// <summary>
        /// Opens or updates the UI. Must be called on UI thread.
        /// </summary>
        /// <param name="model"></param>
        private void UpdateUiInner(CompletionModel model)
        {
            if (IsDismissed)
                return;
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            // TODO: Consider building CompletionPresentationViewModel in BG and passing it here
            UiStopwatch.Restart();
            if (_gui == null)
            {
                _gui = _guardedOperations.CallExtensionPoint(errorSource: _presenterProvider, call: () => _presenterProvider.GetOrCreate(_textView), valueOnThrow: null);
                if (_gui != null)
                {
                    _guardedOperations.CallExtensionPoint(
                        errorSource: _gui,
                        call: () =>
                        {
                            _gui = _presenterProvider.GetOrCreate(_textView);
                            _gui.Open(new CompletionPresentationViewModel(model.PresentedItems, model.Filters, ApplicableSpan, model.UseSoftSelection,
                                model.DisplaySuggestionMode, model.SelectSuggestionItem, model.SelectedIndex, model.SuggestionModeItem, SuggestionItemOptions));
                            _gui.FiltersChanged += OnFiltersChanged;
                            _gui.CommitRequested += OnCommitRequested;
                            _gui.CompletionItemSelected += OnItemSelected;
                            _gui.CompletionClosed += OnGuiClosed;
                        });
                }
            }
            else
            {
                _guardedOperations.CallExtensionPoint(
                    errorSource: _gui,
                    call: () => _gui.Update(new CompletionPresentationViewModel(model.PresentedItems, model.Filters, ApplicableSpan, model.UseSoftSelection,
                        model.DisplaySuggestionMode, model.SelectSuggestionItem, model.SelectedIndex, model.SuggestionModeItem, SuggestionItemOptions)));
            }
            UiStopwatch.Stop();
            _telemetry.RecordRendering(UiStopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Creates a new model and populates it with initial data
        /// </summary>
        private async Task<CompletionModel> GetInitialModel(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            bool sourceUsesSuggestionMode = false;
            bool sourceUsesSoftSelection = false;
            SuggestionItemOptions suggestionModeOptions = null;
            var initialItemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();

            for (int i = 0; i < _completionSources.Count; i++)
            {
                var index = i;
                var context = await _guardedOperations.CallExtensionPointAsync(
                    errorSource: _completionSources[index].Source,
                    asyncCall: () => _completionSources[index].Source.GetCompletionContextAsync(trigger, _completionSources[index].Point, _initialApplicableSpan, token),
                    valueOnThrow: null
                );

                if (context == null)
                    continue;

                sourceUsesSuggestionMode |= context.SuggestionModeOptions != null;
                sourceUsesSoftSelection |= context.UseSoftSelection;
                if (context != null && !context.Items.IsDefaultOrEmpty)
                    initialItemsBuilder.AddRange(context.Items);
                if (suggestionModeOptions == null && context.SuggestionModeOptions != null)
                    SuggestionItemOptions = context.SuggestionModeOptions;
            }

            // Do not continue without items
            if (initialItemsBuilder.Count == 0)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return default(CompletionModel);
            }

            // If no source provided suggestion item options, provide default options for suggestion mode
            if (SuggestionItemOptions == null)
                SuggestionItemOptions = DefaultSuggestionModeOptions;

            // Store the data that won't change throughout the session
            InitialTriggerReason = trigger.Reason;
            ApplicableSpan = triggerLocation.Snapshot.CreateTrackingSpan(_initialApplicableSpan, SpanTrackingMode.EdgeInclusive);
            SuggestionModeCompletionItemSource = new SuggestionModeCompletionItemSource(SuggestionItemOptions);

            var initialCompletionItems = initialItemsBuilder.ToImmutable();

            var availableFilters = initialCompletionItems
                .SelectMany(n => n.Filters)
                .Distinct()
                .Select(n => new CompletionFilterWithState(n, true))
                .ToImmutableArray();

            // Starting in suggestion mode implies that we will use soft selection,
            // unless source provided an explicit suggestion item, which we fully select.
            // We also use soft selection if source explicitly ordered us to.
            var customerUsesSuggestionMode = CompletionUtilities.GetSuggestionModeOption(_textView);
            var useSoftSelection = (!sourceUsesSuggestionMode && customerUsesSuggestionMode) || sourceUsesSoftSelection;
            var useSuggestionMode = customerUsesSuggestionMode || sourceUsesSuggestionMode;
            // Don't select suggestion item unless a source explicitly provided it (e.g. User turned on suggestion mode with Ctrl+Alt+Space)
            var selectSuggestionItem = sourceUsesSuggestionMode;

            ComputationStopwatch.Restart();
            var sortedList = await _guardedOperations.CallExtensionPointAsync(
                errorSource: _completionItemManager,
                asyncCall: () => _completionItemManager.SortCompletionListAsync(initialCompletionItems, trigger.Reason, triggerLocation.Snapshot, ApplicableSpan, _textView, token),
                valueOnThrow: initialCompletionItems);
            ComputationStopwatch.Stop();
            _telemetry.RecordProcessing(ComputationStopwatch.ElapsedMilliseconds, initialCompletionItems.Length);
            _telemetry.RecordKeystroke();

            return new CompletionModel(initialCompletionItems, sortedList, triggerLocation.Snapshot,
                availableFilters, useSoftSelection, useSuggestionMode, selectSuggestionItem, suggestionModeItem: null);
        }

        /// <summary>
        /// User has moved the caret. Ensure that the caret is still within the applicable span. If not, dismiss the session.
        /// </summary>
        private Task<CompletionModel> HandleCaretPositionChanged(CompletionModel model, CaretPosition caretPosition)
        {
            if (model == default(CompletionModel))
                return Task.FromResult(model);

            if (!ApplicableSpan.GetSpan(caretPosition.VirtualBufferPosition.Position.Snapshot).IntersectsWith(new SnapshotSpan(caretPosition.VirtualBufferPosition.Position, 0)))
            {
                ((IAsyncCompletionSession)this).Dismiss();
            }
            return Task.FromResult(model);
        }

        /// <summary>
        /// Sets or unsets suggestion mode.
        /// </summary>
        private Task<CompletionModel> ToggleCompletionModeInner(CompletionModel model, CancellationToken token, bool useSuggestionMode)
        {
            // TODO: Call IAsyncCompletionItemManager and  recompute everything.
            // CompletionFilterReason should get SuggestionModeToggled
            // pass suggestion mode setting to UpdateCompletionListAsync
            return Task.FromResult(model.WithSuggestionModeActive(useSuggestionMode));
        }

        /// <summary>
        /// User has typed. Update the known snapshot, filter the items and update the model.
        /// </summary>
        private async Task<CompletionModel> UpdateSnapshot(CompletionModel model, CompletionTrigger trigger, CompletionFilterReason filterReason, SnapshotPoint triggerLocation, CancellationToken token, int thisId)
        {
            // Always record keystrokes, even if filtering is preempted
            _telemetry.RecordKeystroke();

            // Completion got cancelled
            if (token.IsCancellationRequested || model == null)
                return default(CompletionModel);

            // Dismiss if we are outside of the applicable span
            var currentlyApplicableSpan = ApplicableSpan.GetSpan(triggerLocation.Snapshot);
            if (triggerLocation < currentlyApplicableSpan.Start
                || triggerLocation > currentlyApplicableSpan.End)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return model;
            }
            // Record the first time the span is empty. If it is empty the second time we're here, and user is deleting, then dismiss
            if (currentlyApplicableSpan.IsEmpty && model.ApplicableSpanWasEmpty && trigger.Reason == CompletionTriggerReason.Deletion)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return model;
            }
            model = model.WithApplicableSpanEmptyInformation(currentlyApplicableSpan.IsEmpty);

            // Filtering got preempted, so store the most recent snapshot for the next time we filter
            if (thisId != _lastFilteringTaskId)
                return model.WithSnapshot(triggerLocation.Snapshot);

            ComputationStopwatch.Restart();

            var filteredCompletion = await _guardedOperations.CallExtensionPointAsync(
                errorSource: _completionItemManager,
                asyncCall: () => _completionItemManager.UpdateCompletionListAsync(
                    model.InitialItems,
                    InitialTriggerReason,
                    filterReason,
                    triggerLocation.Snapshot, // used exclusively to resolve applicable span
                    ApplicableSpan,
                    model.Filters,
                    _textView, // Roslyn doesn't need it, and likely, can't use it
                    token),
                valueOnThrow: null);

            // Handle error cases by logging the issue and dismissing the session.
            if (filteredCompletion == null)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return model;
            }

            // Special experience when there are no more selected items:
            ImmutableArray<CompletionItemWithHighlight> returnedItems;
            int selectedIndex = filteredCompletion.SelectedItemIndex;
            if (filteredCompletion.Items.IsDefault)
            {
                // Prevent null references when service returns default(ImmutableArray)
                returnedItems = ImmutableArray<CompletionItemWithHighlight>.Empty;
            }
            else if (filteredCompletion.Items.IsEmpty)
            {
                // If there are no results now, show previously visible results, but without highlighting
                if (model.PresentedItems.IsDefaultOrEmpty)
                {
                    returnedItems = ImmutableArray<CompletionItemWithHighlight>.Empty;
                }
                else
                {
                    returnedItems = model.PresentedItems.Select(n => new CompletionItemWithHighlight(n.CompletionItem)).ToImmutableArray();
                    _selectionModeBeforeNoResultFallback = model.UseSoftSelection;
                    selectedIndex = model.SelectedIndex;
                    _inNoResultFallback = true;
                    model = model.WithSoftSelection(true);
                }
            }
            else
            {
                if (_inNoResultFallback)
                {
                    model = model.WithSoftSelection(_selectionModeBeforeNoResultFallback);
                    _inNoResultFallback = false;
                }
                returnedItems = filteredCompletion.Items;
            }

            ComputationStopwatch.Stop();
            _telemetry.RecordProcessing(ComputationStopwatch.ElapsedMilliseconds, returnedItems.Length);

            if (filteredCompletion.SelectionMode == CompletionItemSelection.SoftSelected)
                model = model.WithSoftSelection(true);
            else if (filteredCompletion.SelectionMode == CompletionItemSelection.Selected
                && (!model.DisplaySuggestionMode || model.SelectSuggestionItem))
                // Allow the language service wishes to fully select the item if we are not in suggestion mode,
                // or if the item to select is the suggestion item.
                model = model.WithSoftSelection(false);

            // Prepare the suggestionModeItem if user ever activates suggestion mode
            var enteredText = currentlyApplicableSpan.GetText();
            var suggestionModeItem = new CompletionItem(enteredText, SuggestionModeCompletionItemSource);

            _guardedOperations.RaiseEvent(this, ItemsUpdated, new CompletionItemsWithHighlightEventArgs(returnedItems));

            return model.WithSnapshotItemsAndFilters(triggerLocation.Snapshot, returnedItems, selectedIndex, filteredCompletion.UniqueItem, suggestionModeItem, filteredCompletion.Filters);
        }

        /// <summary>
        /// Reacts to user toggling a filter
        /// </summary>
        /// <param name="newFilters">Filters with updated Selected state, as indicated by the user.</param>
        private async Task<CompletionModel> UpdateFilters(CompletionModel model, ImmutableArray<CompletionFilterWithState> newFilters, CancellationToken token, int thisId)
        {
            _telemetry.RecordChangingFilters();
            _telemetry.RecordKeystroke();

            // Filtering got preempted, so store the most updated filters for the next time we filter
            if (token.IsCancellationRequested || thisId != _lastFilteringTaskId)
                return model.WithFilters(newFilters);

            var filteredCompletion = await _guardedOperations.CallExtensionPointAsync(
                errorSource: _completionItemManager,
                asyncCall: () => _completionItemManager.UpdateCompletionListAsync(
                    model.InitialItems,
                    InitialTriggerReason,
                    CompletionFilterReason.FilterChange,
                    model.Snapshot,
                    ApplicableSpan,
                    newFilters,
                    _textView,
                    token),
                valueOnThrow: null);

            // Handle error cases by logging the issue and discarding the request to filter
            if (filteredCompletion == null)
                return model;
            if (filteredCompletion.Filters.Length != newFilters.Length)
            {
                _guardedOperations.HandleException(
                    errorSource: _completionItemManager,
                    e: new InvalidOperationException("Completion service returned incorrect set of filters."));
                return model;
            }

            return model.WithFilters(filteredCompletion.Filters).WithPresentedItems(filteredCompletion.Items, filteredCompletion.SelectedItemIndex);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        /// <summary>
        /// Reacts to user scrolling the list using keyboard
        /// </summary>
        private async Task<CompletionModel> UpdateSelectedItem(CompletionModel model, int offset, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            _telemetry.RecordScrolling();
            _telemetry.RecordKeystroke();

            if (!model.PresentedItems.Any())
            {
                // No-op if there are no items
                if (model.DisplaySuggestionMode)
                {
                    // Unless there is a suggestion mode item. Select it.
                    return model.WithSuggestionItemSelected();
                }
                return model;
            }

            var lastIndex = model.PresentedItems.Count() - 1;
            var currentIndex = model.SelectSuggestionItem ? -1 : model.SelectedIndex;

            if (offset > 0) // Scrolling down. Stop at last index then go to first index.
            {
                if (currentIndex == lastIndex)
                {
                    if (model.DisplaySuggestionMode)
                        return model.WithSuggestionItemSelected();
                    else
                        return model.WithSelectedIndex(FirstIndex);
                }
                var newIndex = currentIndex + offset;
                return model.WithSelectedIndex(Math.Min(newIndex, lastIndex));
            }
            else // Scrolling up. Stop at first index then go to last index.
            {
                if (currentIndex < FirstIndex)
                {
                    // Suggestion mode item is selected. Go to the last item.
                    return model.WithSelectedIndex(lastIndex);
                }
                else if (currentIndex == FirstIndex)
                {
                    // The first item is selected. If there is a suggestion, select it.
                    if (model.DisplaySuggestionMode)
                        return model.WithSuggestionItemSelected();
                    else
                        return model.WithSelectedIndex(lastIndex);
                }
                var newIndex = currentIndex + offset;
                return model.WithSelectedIndex(Math.Max(newIndex, FirstIndex));
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        /// <summary>
        /// Reacts to user selecting a specific item in the list
        /// </summary>
        private async Task<CompletionModel> UpdateSelectedItem(CompletionModel model, CompletionItem selectedItem, bool suggestionModeSelected, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            _telemetry.RecordScrolling();
            if (suggestionModeSelected)
            {
                return model.WithSuggestionItemSelected();
            }
            else
            {
                for (int i = 0; i < model.PresentedItems.Length; i++)
                {
                    if (model.PresentedItems[i].CompletionItem == selectedItem)
                    {
                        return model.WithSelectedIndex(i);
                    }
                }
                // This item is not in the model
                return model;
            }
        }

        /// <inheritdoc />
        public ImmutableArray<CompletionItem> GetItems(CancellationToken token)
        {
            if (_computation == null)
                return ImmutableArray<CompletionItem>.Empty; // Call OpenOrUpdate first to compute items
            var model = _computation.WaitAndGetResult(token);
            return model.PresentedItems.Select(n => n.CompletionItem).ToImmutableArray();
        }

        /// <inheritdoc />
        public ImmutableArray<CompletionItem> GetBuilderItems(CancellationToken token)
        {
            if (_computation == null)
                return ImmutableArray<CompletionItem>.Empty; // Call OpenOrUpdate first to compute items
            var model = _computation.WaitAndGetResult(token);
            return model.DisplaySuggestionMode
                ? new ImmutableArray<CompletionItem> { model.SuggestionModeItem }
                : ImmutableArray<CompletionItem>.Empty;
        }

        /// <inheritdoc />
        public CompletionItem GetSelectedItem(CancellationToken token)
        {
            if (_computation == null)
                return null; // Call OpenOrUpdate first to compute items
            var model = _computation.WaitAndGetResult(token);
            if (model.SelectSuggestionItem)
                return model.SuggestionModeItem;
            else
                return model.PresentedItems[model.SelectedIndex].CompletionItem;
        }

    }
}
