using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Strings = Microsoft.VisualStudio.Language.Intellisense.Implementation.Strings;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    /// <summary>
    /// Holds a state of the session
    /// and a reference to the UI element
    /// </summary>
    internal class AsyncCompletionSession : IAsyncCompletionSession, IModelComputationCallbackHandler<CompletionModel>
    {
        // Available data and services
        private readonly IList<(IAsyncCompletionSource Source, SnapshotPoint Point)> _completionSources;
        private readonly IList<(IAsyncCompletionCommitManager, ITextBuffer)> _commitManagers;
        private readonly IAsyncCompletionItemManager _completionItemManager;
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

        // ------------------------------------------------------------------------
        // Fixed completion model data that does not change throughout the session:

        /// <summary>
        /// Span pertinent to this completion.
        /// </summary>
        public ITrackingSpan ApplicableToSpan { get; }

        /// <summary>
        /// Stores the initial reason this session was triggererd.
        /// </summary>
        private InitialTrigger InitialTrigger { get; set; }

        /// <summary>
        /// Text to display in place of suggestion mode when filtered text is empty.
        /// </summary>
        private SuggestionItemOptions SuggestionItemOptions { get; set; }

        /// <summary>
        /// Source that will provide tooltip for the suggestion item.
        /// </summary>
        private IAsyncCompletionSource SuggestionModeCompletionItemSource { get; set; }
        // ------------------------------------------------------------------------

        /// <summary>
        /// Telemetry aggregator for this session
        /// </summary>
        private readonly CompletionSessionTelemetry _telemetry;

        /// <summary>
        /// Self imposed maximum delay for commits due to user double-clicking completion item in the UI
        /// </summary>
        private static readonly TimeSpan MaxCommitDelayWhenClicked = TimeSpan.FromSeconds(1);

        private static SuggestionItemOptions DefaultSuggestionModeOptions = new SuggestionItemOptions(string.Empty, Strings.SuggestionModeDefaultTooltip);

        // Facilitate experience when there are no items to display
        private bool _selectionModeBeforeNoResultFallback;
        private bool _inNoResultFallback;
        private bool _ignoreCaretMovement;

        public event EventHandler<CompletionItemEventArgs> ItemCommitted;
        public event EventHandler Dismissed;
        public event EventHandler<ComputedCompletionItemsEventArgs> ItemsUpdated;

        public ITextView TextView => _textView;

        // When set, UI will no longer be updated
        public bool IsDismissed { get; private set; }

        public PropertyCollection Properties { get; }

        public AsyncCompletionSession(SnapshotSpan initialApplicableToSpan, ImmutableArray<char> potentialCommitChars,
            JoinableTaskContext joinableTaskContext, ICompletionPresenterProvider presenterProvider,
            IList<(IAsyncCompletionSource, SnapshotPoint)> completionSources, IList<(IAsyncCompletionCommitManager, ITextBuffer)> commitManagers,
            IAsyncCompletionItemManager completionService, AsyncCompletionBroker broker, ITextView textView, CompletionSessionTelemetry telemetry,
            IGuardedOperations guardedOperations)
        {
            _potentialCommitChars = potentialCommitChars;
            JoinableTaskContext = joinableTaskContext;
            _presenterProvider = presenterProvider;
            _broker = broker;
            _completionSources = completionSources; // still prorotype at the momemnt.
            _commitManagers = commitManagers;
            _completionItemManager = completionService;
            _textView = textView;
            _guardedOperations = guardedOperations;
            ApplicableToSpan = initialApplicableToSpan.Snapshot.CreateTrackingSpan(initialApplicableToSpan, SpanTrackingMode.EdgeInclusive);
            _telemetry = telemetry;
            PageStepSize = presenterProvider?.Options.ResultsPerPage ?? 1;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            Properties = new PropertyCollection();
        }

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
                    call: () => n.Item1.ShouldCommitCompletion(typeChar, n.Item2.Value, token),
                    valueOnThrow: false));
        }

        bool IAsyncCompletionSession.CommitIfUnique(CancellationToken token)
        {
            if (IsDismissed)
                return false;

            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            // TODO: see what happens when OpenOrUpdate hasn't been called yet
            var lastModel = _computation.WaitAndGetResult(cancelUi: true, token);
            if (lastModel == null)
            {
                return false;
            }
            else if (lastModel.InitiallyUnavailable)
            {
                return false;
            }
            else if (lastModel.UniqueItem != null)
            {
                CommitItem(default, lastModel.UniqueItem, ApplicableToSpan, token);
                return true;
            }
            else if (!lastModel.PresentedItems.IsDefaultOrEmpty && lastModel.PresentedItems.Length == 1)
            {
                CommitItem(default, lastModel.PresentedItems[0].CompletionItem, ApplicableToSpan, token);
                return true;
            }
            else
            {
                // Show the UI, because waitAndGetResult canceled showing the UI.
                UpdateUiInner(lastModel); // We are on the UI thread, so we may call UpdateUiInner
                return false;
            }
        }

        CommitBehavior IAsyncCompletionSession.Commit(char typedChar, CancellationToken token)
        {
            if (IsDismissed)
                return CommitBehavior.None;

            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            var lastModel = _computation.WaitAndGetResult(cancelUi: true, token);
            if (lastModel == null)
            {
                return CommitBehavior.None;
            }
            else if (lastModel.InitiallyUnavailable)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return CommitBehavior.None;
            }
            else if (lastModel.UseSoftSelection && !(typedChar.Equals(default) || typedChar.Equals('\t')) )
            {
                // In soft selection mode, user commits explicitly (click, tab, e.g. not tied to a text change). Otherwise, we dismiss the session
                ((IAsyncCompletionSession)this).Dismiss();
                return CommitBehavior.None;
            }
            else if (lastModel.SelectSuggestionItem && string.IsNullOrWhiteSpace(lastModel.SuggestionItem?.InsertText))
            {
                // When suggestion mode is selected, don't commit empty suggestion
                return CommitBehavior.None;
            }
            else if (lastModel.SelectSuggestionItem)
            {
                // Commit the suggestion mode item
                return CommitItem(typedChar, lastModel.SuggestionItem, ApplicableToSpan, token);
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
                return CommitItem(typedChar, lastModel.PresentedItems[lastModel.SelectedIndex].CompletionItem, ApplicableToSpan, token);
            }
        }

        private CommitBehavior CommitItem(char typedChar, CompletionItem itemToCommit, ITrackingSpan applicableSpan, CancellationToken token)
        {
            CommitBehavior behavior = CommitBehavior.None;
            if (IsDismissed)
                return behavior;

            _telemetry.UiStopwatch.Restart();
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
                InsertIntoBuffer(_textView, applicableSpan, itemToCommit.InsertText);
            }

            _telemetry.UiStopwatch.Stop();
            _guardedOperations.RaiseEvent(this, ItemCommitted, new CompletionItemEventArgs(itemToCommit));
            _telemetry.RecordCommitted(_telemetry.UiStopwatch.ElapsedMilliseconds, managerWhoCommitted);

            Dismiss();

            return behavior;
        }

        private static void InsertIntoBuffer(ITextView view, ITrackingSpan applicableSpan, string insertText)
        {
            var buffer = view.TextBuffer;
            var bufferEdit = buffer.CreateEdit();

            // ApplicableToSpan already contains the typeChar and brace completion. Replacing this span will cause us to lose this data.
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
                        _telemetry.UiStopwatch.Restart();
                        copyOfGui.FiltersChanged -= OnFiltersChanged;
                        copyOfGui.CommitRequested -= OnCommitRequested;
                        copyOfGui.CompletionItemSelected -= OnItemSelected;
                        copyOfGui.CompletionClosed -= OnGuiClosed;
                        copyOfGui.Close();
                        _telemetry.UiStopwatch.Stop();
                        _telemetry.RecordClosing(_telemetry.UiStopwatch.ElapsedMilliseconds);
                        await Task.Yield();
                        _telemetry.Save(_completionItemManager, _presenterProvider);
                    });
                _gui = null;
            }
        }

        void IAsyncCompletionSession.OpenOrUpdate(InitialTrigger trigger, SnapshotPoint triggerLocation, CancellationToken commandToken)
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
            _computation.Enqueue((model, token) => UpdateSnapshot(model, trigger, new UpdateTrigger(FromCompletionTriggerReason(trigger.Reason), trigger.Character), triggerLocation, taskId, token), updateUi: true);
        }

        ComputedCompletionItems IAsyncCompletionSession.GetComputedItems(CancellationToken token)
        {
            if (_computation == null)
                return ComputedCompletionItems.Empty; // Call OpenOrUpdate first to kick off computation

            var model = _computation.WaitAndGetResult(cancelUi: false, token); // We don't want user initiated action to hide UI
            if (model == null)
                return ComputedCompletionItems.Empty;

            return new ComputedCompletionItems(
                    items: model.PresentedItems.Select(n => n.CompletionItem),
                    suggestionItem: model.DisplaySuggestionItem ? model.SuggestionItem : null,
                    selectedItem: model.SelectSuggestionItem
                        ? model.SuggestionItem
                        : model.PresentedItems.IsDefaultOrEmpty && model.SelectedIndex >= 0
                            ? null
                            : model.PresentedItems[model.SelectedIndex].CompletionItem,
                    suggestionItemSelected: model.SelectSuggestionItem,
                    usesSoftSelection: model.UseSoftSelection);
        }

        private static UpdateTriggerReason FromCompletionTriggerReason(InitialTriggerReason reason)
        {
            switch (reason)
            {
                case InitialTriggerReason.Invoke:
                case InitialTriggerReason.InvokeAndCommitIfUnique:
                    return UpdateTriggerReason.Initial;
                case InitialTriggerReason.Insertion:
                    return UpdateTriggerReason.Insertion;
                case InitialTriggerReason.Deletion:
                    return UpdateTriggerReason.Deletion;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        #region Internal methods accessed by the command handlers

        internal void InvokeAndCommitIfUnique(InitialTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
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

        internal void SetSuggestionMode(bool useSuggestionMode)
        {
            _computation.Enqueue((model, token) => ToggleCompletionModeInner(model, useSuggestionMode, token), updateUi: true);
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

        internal void IgnoreCaretMovement(bool ignore)
        {
            if (IsDismissed)
                return; // This method will be called after committing. Don't act on it.

            _ignoreCaretMovement = ignore;
            if (!ignore)
            {
                // Don't let the session exist in invalid state: ensure that the location of the session is still valid
                HandleCaretPositionChanged(_textView.Caret.Position);
            }
        }

        #endregion

        private void OnFiltersChanged(object sender, CompletionFilterChangedEventArgs args)
        {
            var taskId = Interlocked.Increment(ref _lastFilteringTaskId);
            _computation.Enqueue((model, token) => UpdateFilters(model, args.Filters, taskId, token), updateUi: true);
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
                CommitItem(default, args.Item, ApplicableToSpan, expiringTokenSource.Token);
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
            _computation.Enqueue((model, token) => UpdateSelectedItem(model, args.SelectedItem, args.SuggestionItemSelected, token), updateUi: false);
        }

        private void OnGuiClosed(object sender, CompletionClosedEventArgs args)
        {
            Dismiss();
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

            HandleCaretPositionChanged(e.NewPosition);
        }

        async Task IModelComputationCallbackHandler<CompletionModel>.UpdateUI(CompletionModel model, CancellationToken token)
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
            if (model.InitiallyUnavailable)
                return; // Language service wishes to not show completion yet.
            if (!JoinableTaskContext.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            // TODO: Consider building CompletionPresentationViewModel in BG and passing it here
            _telemetry.UiStopwatch.Restart();
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
                            _gui.Open(new CompletionPresentationViewModel(model.PresentedItems, model.Filters,
                                model.SelectedIndex, ApplicableToSpan, model.UseSoftSelection, model.DisplaySuggestionItem,
                                model.SelectSuggestionItem, model.SuggestionItem, SuggestionItemOptions));
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
                    call: () => _gui.Update(new CompletionPresentationViewModel(model.PresentedItems, model.Filters,
                        model.SelectedIndex, ApplicableToSpan, model.UseSoftSelection, model.DisplaySuggestionItem,
                        model.SelectSuggestionItem, model.SuggestionItem, SuggestionItemOptions)));
            }
            _telemetry.UiStopwatch.Stop();
            _telemetry.RecordRendering(_telemetry.UiStopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Creates a new model and populates it with initial data
        /// </summary>
        private async Task<CompletionModel> GetInitialModel(InitialTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            bool sourceUsesSuggestionMode = false;
            SuggestionItemOptions requestedSuggestionItemOptions = null;
            InitialSelectionHint initialSelectionHint = InitialSelectionHint.RegularSelection;
            var initialItemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();

            for (int i = 0; i < _completionSources.Count; i++)
            {
                var index = i; // Capture i, since it will change during the async call

                _telemetry.ComputationStopwatch.Restart();
                var context = await _guardedOperations.CallExtensionPointAsync(
                    errorSource: _completionSources[index].Source,
                    asyncCall: () => _completionSources[index].Source.GetCompletionContextAsync(trigger, _completionSources[index].Point, ApplicableToSpan.GetSpan(ApplicableToSpan.TextBuffer.CurrentSnapshot) /*TODO: just pass ITrackingSpan */, token),
                    valueOnThrow: null
                ).ConfigureAwait(true);
                _telemetry.ComputationStopwatch.Stop();
                _telemetry.RecordObtainingSourceContext(_completionSources[index].Source, _telemetry.ComputationStopwatch.ElapsedMilliseconds);

                if (context == null)
                    continue;

                sourceUsesSuggestionMode |= context.SuggestionItemOptions != null;

                // Set initial selection option, in order of precedence: no selection, soft selection, regular selection
                if (context.SelectionHint == InitialSelectionHint.NoSelection)
                    initialSelectionHint = InitialSelectionHint.NoSelection;
                if (context.SelectionHint == InitialSelectionHint.SoftSelection && initialSelectionHint != InitialSelectionHint.NoSelection)
                    initialSelectionHint = InitialSelectionHint.SoftSelection;

                if (!context.Items.IsDefaultOrEmpty)
                    initialItemsBuilder.AddRange(context.Items);
                // We use SuggestionModeOptions of the first source that provides it
                if (requestedSuggestionItemOptions == null && context.SuggestionItemOptions != null)
                    requestedSuggestionItemOptions = context.SuggestionItemOptions;
            }

            // Do not continue without items
            if (initialItemsBuilder.Count == 0)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return default;
            }

            // If no source provided suggestion item options, provide default options for suggestion mode
            SuggestionItemOptions = requestedSuggestionItemOptions ?? DefaultSuggestionModeOptions;

            // Store the data that won't change throughout the session
            InitialTrigger = trigger;
            SuggestionModeCompletionItemSource = new SuggestionModeCompletionItemSource(SuggestionItemOptions);

            var initialCompletionItems = initialItemsBuilder.ToImmutable();

            var availableFilters = initialCompletionItems
                .SelectMany(n => n.Filters)
                .Distinct()
                .Select(n => new CompletionFilterWithState(n, true))
                .ToImmutableArray();

            var customerUsesSuggestionMode = CompletionUtilities.GetSuggestionModeOption(_textView);
            var viewUsesSuggestionMode = CompletionUtilities.IsDebuggerTextView(_textView);

            var useSuggestionMode = customerUsesSuggestionMode || sourceUsesSuggestionMode || viewUsesSuggestionMode;
            // Select suggestion item only if source explicity provided it. This means that debugger view or ctrl+alt+space won't select the suggestion item.
            var selectSuggestionItem = sourceUsesSuggestionMode;
            // Use soft selection if suggestion item is present, unless source selects that item. Also, use soft selection if source wants to.
            var useSoftSelection = useSuggestionMode && !selectSuggestionItem || initialSelectionHint == InitialSelectionHint.SoftSelection;
            var initiallyUnavailable = initialSelectionHint == InitialSelectionHint.NoSelection;

            _telemetry.ComputationStopwatch.Restart();
            var sortedList = await _guardedOperations.CallExtensionPointAsync(
                errorSource: _completionItemManager,
                asyncCall: () => _completionItemManager.SortCompletionListAsync(
                    session: this,
                    data: new AsyncCompletionSessionInitialDataSnapshot(initialCompletionItems, triggerLocation.Snapshot, InitialTrigger),
                    token: token),
                valueOnThrow: initialCompletionItems).ConfigureAwait(true);
            _telemetry.ComputationStopwatch.Stop();
            _telemetry.RecordProcessing(_telemetry.ComputationStopwatch.ElapsedMilliseconds, initialCompletionItems.Length);
            _telemetry.RecordKeystroke();

            return new CompletionModel(initialCompletionItems, sortedList, triggerLocation.Snapshot,
                availableFilters, useSoftSelection, useSuggestionMode, selectSuggestionItem, suggestionItem: null, initiallyUnavailable: initiallyUnavailable);
        }

        /// <summary>
        /// User has moved the caret. Ensure that the caret is still within the applicable span. If not, dismiss the session.
        /// </summary>
        private void HandleCaretPositionChanged(CaretPosition caretPosition)
        {
            if (!ApplicableToSpan.GetSpan(caretPosition.VirtualBufferPosition.Position.Snapshot).IntersectsWith(new SnapshotSpan(caretPosition.VirtualBufferPosition.Position, 0)))
            {
                ((IAsyncCompletionSession)this).Dismiss();
            }
        }

        /// <summary>
        /// Sets or unsets suggestion mode.
        /// </summary>
#pragma warning disable CA1822 // Member does not access instance data and can be marked as static
#pragma warning disable CA1801 // Parameter token is never used
        private Task<CompletionModel> ToggleCompletionModeInner(CompletionModel model, bool useSuggestionMode, CancellationToken token)
        {
            return Task.FromResult(model.WithSuggestionItemVisibility(useSuggestionMode));
        }
#pragma warning restore CA1822
#pragma warning restore CA1801

        /// <summary>
        /// User has typed. Update the known snapshot, filter the items and update the model.
        /// </summary>
        private async Task<CompletionModel> UpdateSnapshot(CompletionModel model, InitialTrigger initialTrigger, UpdateTrigger updateTrigger, SnapshotPoint updateLocation, int thisId, CancellationToken token)
        {
            // Always record keystrokes, even if filtering is preempted
            _telemetry.RecordKeystroke();

            // Completion got cancelled
            if (token.IsCancellationRequested || model == null)
                return default;

            var instantenousSnapshot = updateLocation.Snapshot;

            // Dismiss if we are outside of the applicable span
            var currentlyApplicableToSpan = ApplicableToSpan.GetSpan(instantenousSnapshot);
            if (updateLocation < currentlyApplicableToSpan.Start
                || updateLocation > currentlyApplicableToSpan.End)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return model;
            }
            // Record the first time the span is empty. If it is empty the second time we're here, and user is deleting, then dismiss
            if (currentlyApplicableToSpan.IsEmpty && model.ApplicableToSpanWasEmpty && initialTrigger.Reason == InitialTriggerReason.Deletion)
            {
                ((IAsyncCompletionSession)this).Dismiss();
                return model;
            }
            model = model.WithApplicableToSpanStatus(currentlyApplicableToSpan.IsEmpty);

            // Filtering got preempted, so store the most recent snapshot for the next time we filter
            if (thisId != _lastFilteringTaskId)
                return model.WithSnapshot(instantenousSnapshot);

            _telemetry.ComputationStopwatch.Restart();

            var filteredCompletion = await _guardedOperations.CallExtensionPointAsync(
                errorSource: _completionItemManager,
                asyncCall: () => _completionItemManager.UpdateCompletionListAsync(
                    session: this,
                    data: new AsyncCompletionSessionDataSnapshot(
                        model.InitialItems,
                        instantenousSnapshot,
                        initialTrigger,
                        updateTrigger,
                        model.Filters,
                        model.UseSoftSelection,
                        model.InitiallyUnavailable),
                    token: token),
                valueOnThrow: null).ConfigureAwait(true);

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

            _telemetry.ComputationStopwatch.Stop();
            _telemetry.RecordProcessing(_telemetry.ComputationStopwatch.ElapsedMilliseconds, returnedItems.Length);

            if (filteredCompletion.SelectionHint == UpdateSelectionHint.SoftSelected)
                model = model.WithSoftSelection(true);
            else if (filteredCompletion.SelectionHint == UpdateSelectionHint.Selected
                && (!model.DisplaySuggestionItem || model.SelectSuggestionItem))
                // Allow the language service wishes to fully select the item if we are not in suggestion mode,
                // or if the item to select is the suggestion item.
                model = model.WithSoftSelection(false);

            // When language service specifies item selection, and completion was unavailable, it will now become available
            if (model.InitiallyUnavailable && filteredCompletion.SelectionHint != UpdateSelectionHint.NoChange)
                model = model.WithInitialAvailability();

            // Prepare the suggestionItem if user ever activates suggestion mode
            var enteredText = currentlyApplicableToSpan.GetText();
            var suggestionItem = new CompletionItem(enteredText, SuggestionModeCompletionItemSource);

            if (ItemsUpdated != null)
            {
                var computedItems = new ComputedCompletionItems(
                    items: returnedItems.Select(n => n.CompletionItem),
                    suggestionItem: model.DisplaySuggestionItem ? suggestionItem : null,
                    selectedItem: model.SelectSuggestionItem ? suggestionItem : returnedItems[selectedIndex].CompletionItem,
                    suggestionItemSelected: model.SelectSuggestionItem,
                    usesSoftSelection: model.UseSoftSelection);
                // Warning: if the event handler throws, and anyone blocks UI thread waiting for UpdateSnapshot,
                // there will be a deadlock. This won't happen for now, because this method is private and nobody waits on it.
                // A good solution is to refactor ExtensionErrorHandler to GetService in constructor and not every time it reports an error.
                _guardedOperations.RaiseEvent(this, ItemsUpdated, new ComputedCompletionItemsEventArgs(computedItems));
            }

            return model.WithSnapshotItemsAndFilters(updateLocation.Snapshot, returnedItems, selectedIndex, filteredCompletion.UniqueItem, suggestionItem, filteredCompletion.Filters);
        }

        /// <summary>
        /// Reacts to user toggling a filter
        /// </summary>
        /// <param name="newFilters">Filters with updated Selected state, as indicated by the user.</param>
        private async Task<CompletionModel> UpdateFilters(CompletionModel model, ImmutableArray<CompletionFilterWithState> newFilters, int thisId, CancellationToken token)
        {
            _telemetry.RecordChangingFilters();
            _telemetry.RecordKeystroke();

            // Filtering got preempted, so store the most updated filters for the next time we filter
            if (token.IsCancellationRequested || thisId != _lastFilteringTaskId)
                return model.WithFilters(newFilters);

            var filteredCompletion = await _guardedOperations.CallExtensionPointAsync(
                errorSource: _completionItemManager,
                asyncCall: () => _completionItemManager.UpdateCompletionListAsync(
                    session: this,
                    data: new AsyncCompletionSessionDataSnapshot(
                        model.InitialItems,
                        model.Snapshot,
                        InitialTrigger,
                        new UpdateTrigger(UpdateTriggerReason.FilterChange),
                        newFilters,
                        model.UseSoftSelection,
                        model.InitiallyUnavailable),
                    token: token),
                valueOnThrow: null).ConfigureAwait(true);

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

            if (ItemsUpdated != null)
            {
                var computedItems = new ComputedCompletionItems(
                    items: filteredCompletion.Items.Select(n => n.CompletionItem),
                    suggestionItem: model.DisplaySuggestionItem ? model.SuggestionItem : null,
                    selectedItem: model.SelectSuggestionItem
                        ? model.SuggestionItem
                        : filteredCompletion.Items.IsDefaultOrEmpty && model.SelectedIndex >= 0
                            ? null
                            : filteredCompletion.Items[model.SelectedIndex].CompletionItem,
                    suggestionItemSelected: model.SelectSuggestionItem,
                    usesSoftSelection: model.UseSoftSelection);
                _guardedOperations.RaiseEvent(this, ItemsUpdated, new ComputedCompletionItemsEventArgs(computedItems));
            }

            return model.WithFilters(filteredCompletion.Filters).WithPresentedItems(filteredCompletion.Items, filteredCompletion.SelectedItemIndex);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CA1801 // Parameter token is never used
        /// <summary>
        /// Reacts to user scrolling the list using keyboard
        /// </summary>
        private async Task<CompletionModel> UpdateSelectedItem(CompletionModel model, int offset, CancellationToken token)
#pragma warning restore CS1998
#pragma warning restore CA1801
        {
            _telemetry.RecordScrolling();
            _telemetry.RecordKeystroke();

            if (!model.PresentedItems.Any())
            {
                // No-op if there are no items
                if (model.DisplaySuggestionItem)
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
                    if (model.DisplaySuggestionItem)
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
                    if (model.DisplaySuggestionItem)
                        return model.WithSuggestionItemSelected();
                    else
                        return model.WithSelectedIndex(lastIndex);
                }
                var newIndex = currentIndex + offset;
                return model.WithSelectedIndex(Math.Max(newIndex, FirstIndex));
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CA1801 // Parameter token is never used
        /// <summary>
        /// Reacts to user selecting a specific item in the list
        /// </summary>
        private async Task<CompletionModel> UpdateSelectedItem(CompletionModel model, CompletionItem selectedItem, bool suggestionItemSelected, CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CA1801
        {
            _telemetry.RecordScrolling();
            if (suggestionItemSelected)
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
    }
}
