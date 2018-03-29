using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    sealed class CompletionModel
    {
        /// <summary>
        /// All items, as provided by completion item sources.
        /// </summary>
        public readonly ImmutableArray<CompletionItem> InitialItems;

        /// <summary>
        /// Sorted array of all items, as provided by the completion service.
        /// </summary>
        public readonly ImmutableArray<CompletionItem> SortedItems;

        /// <summary>
        /// Span pertinent to this completion model.
        /// </summary>
        public readonly ITrackingSpan ApplicableSpan;

        /// <summary>
        /// Snapshot pertinent to this completion model.
        /// </summary>
        public readonly ITextSnapshot Snapshot;

        /// <summary>
        /// Stores the initial reason this session was triggererd.
        /// </summary>
        public readonly CompletionTriggerReason InitialTriggerReason;

        /// <summary>
        /// Filters involved in this completion model, including their availability and selection state.
        /// </summary>
        public readonly ImmutableArray<CompletionFilterWithState> Filters;

        /// <summary>
        /// Items to be displayed in the UI.
        /// </summary>
        public readonly ImmutableArray<CompletionItemWithHighlight> PresentedItems;

        /// <summary>
        /// Index of item to select. Use -1 to select nothing, when suggestion mode item should be selected.
        /// </summary>
        public readonly int SelectedIndex;

        /// <summary>
        /// Whether selection should be displayed as soft selection.
        /// </summary>
        public readonly bool UseSoftSelection;

        /// <summary>
        /// Whether suggestion mode item should be visible.
        /// </summary>
        public readonly bool DisplaySuggestionMode;

        /// <summary>
        /// Whether suggestion mode item should be selected.
        /// </summary>
        public readonly bool SelectSuggestionItem;

        /// <summary>
        /// <see cref="CompletionItem"/> which contains user-entered text.
        /// Used to display and commit the suggestion mode item
        /// </summary>
        public readonly CompletionItem SuggestionModeItem;

        /// <summary>
        /// Text to display in place of suggestion mode when filtered text is empty.
        /// </summary>
        public readonly string SuggestionModeDescription;

        /// <summary>
        /// <see cref="CompletionItem"/> which overrides regular unique item selection.
        /// When this is null, the single item from <see cref="PresentedItems"/> is used as unique item.
        /// </summary>
        public readonly CompletionItem UniqueItem;

        /// <summary>
        /// When completion starts, its <see cref="ApplicableSpan"/> may be empty. Initially, don't dismiss.
        /// Further, the span is empty if user removes characters. We would like to dismiss then.
        /// </summary>
        public readonly bool ApplicableSpanWasEmpty;

        /// <summary>
        /// Constructor for the initial model
        /// </summary>
        public CompletionModel(ImmutableArray<CompletionItem> initialItems, ImmutableArray<CompletionItem> sortedItems,
            ITrackingSpan applicableSpan, CompletionTriggerReason initialTriggerReason, ITextSnapshot snapshot,
            ImmutableArray<CompletionFilterWithState> filters, bool useSoftSelection, bool useSuggestionMode, bool selectSuggestionItem, string suggestionModeDescription, CompletionItem suggestionModeItem)
        {
            InitialItems = initialItems;
            SortedItems = sortedItems;
            ApplicableSpan = applicableSpan;
            InitialTriggerReason = initialTriggerReason;
            Snapshot = snapshot;
            Filters = filters;
            SelectedIndex = 0;
            UseSoftSelection = useSoftSelection;
            DisplaySuggestionMode = useSuggestionMode;
            SelectSuggestionItem = selectSuggestionItem;
            SuggestionModeDescription = suggestionModeDescription;
            SuggestionModeItem = suggestionModeItem;
            UniqueItem = null;
            ApplicableSpanWasEmpty = false;
        }

        /// <summary>
        /// Private constructor for the With* methods
        /// </summary>
        private CompletionModel(ImmutableArray<CompletionItem> initialItems, ImmutableArray<CompletionItem> sortedItems, ITrackingSpan applicableSpan, CompletionTriggerReason initialTriggerReason,
            ITextSnapshot snapshot, ImmutableArray<CompletionFilterWithState> filters, ImmutableArray<CompletionItemWithHighlight> presentedItems, bool useSoftSelection, bool useSuggestionMode,
            string suggestionModeDescription, int selectedIndex, bool selectSuggestionItem, CompletionItem suggestionModeItem, CompletionItem uniqueItem, bool applicableSpanWasEmpty)
        {
            InitialItems = initialItems;
            SortedItems = sortedItems;
            ApplicableSpan = applicableSpan;
            InitialTriggerReason = initialTriggerReason;
            Snapshot = snapshot;
            Filters = filters;
            PresentedItems = presentedItems;
            SelectedIndex = selectedIndex;
            UseSoftSelection = useSoftSelection;
            DisplaySuggestionMode = useSuggestionMode;
            SelectSuggestionItem = selectSuggestionItem;
            SuggestionModeDescription = suggestionModeDescription;
            SuggestionModeItem = suggestionModeItem;
            UniqueItem = uniqueItem;
            ApplicableSpanWasEmpty = applicableSpanWasEmpty;
        }

        public CompletionModel WithPresentedItems(ImmutableArray<CompletionItemWithHighlight> newPresentedItems, int newSelectedIndex)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: newPresentedItems, // Updated
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: newSelectedIndex, // Updated
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        public CompletionModel WithSnapshot(ITextSnapshot newSnapshot)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: newSnapshot, // Updated
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        public CompletionModel WithFilters(ImmutableArray<CompletionFilterWithState> newFilters)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: newFilters, // Updated
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        public CompletionModel WithSelectedIndex(int newIndex)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: false, // Explicit selection and soft selection are mutually exclusive
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: newIndex, // Updated
                selectSuggestionItem: false, // Explicit selection of regular item
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        public CompletionModel WithSuggestionItemSelected()
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: false, // Explicit selection and soft selection are mutually exclusive
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: -1, // Deselect regular item
                selectSuggestionItem: true, // Explicit selection of suggestion item
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        public CompletionModel WithSuggestionModeActive(bool newUseSuggestionMode)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection | newUseSuggestionMode, // Enabling suggestion mode also enables soft selection
                useSuggestionMode: newUseSuggestionMode, // Updated
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        /// <summary>
        /// </summary>
        /// <param name="newSuggestionModeItem">It is ok to pass in null when there is no suggestion. UI will display SuggestsionModeDescription instead.</param>
        internal CompletionModel WithSuggestionModeItem(CompletionItem newSuggestionModeItem)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: newSuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        /// <summary>
        /// </summary>
        /// <param name="newUniqueItem">Overrides typical unique item selection.
        /// Pass in null to use regular behavior: treating single <see cref="PresentedItems"/> item as the unique item.</param>
        internal CompletionModel WithUniqueItem(CompletionItem newUniqueItem)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: newUniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        internal CompletionModel WithSoftSelection(bool newSoftSelection)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: newSoftSelection, // Updated
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        internal CompletionModel WithSnapshotItemsAndFilters(ITextSnapshot snapshot, ImmutableArray<CompletionItemWithHighlight> presentedItems,
            int selectedIndex, CompletionItem uniqueItem, CompletionItem suggestionModeItem, ImmutableArray<CompletionFilterWithState> filters)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: snapshot, // Updated
                filters: filters, // Updated
                presentedItems: presentedItems, // Updated
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: selectedIndex, // Updated
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: suggestionModeItem, // Updated
                uniqueItem: uniqueItem, // Updated
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        internal CompletionModel WithApplicableSpanEmptyRecord(bool applicableSpanIsEmpty)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                applicableSpan: ApplicableSpan,
                initialTriggerReason: InitialTriggerReason,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                suggestionModeDescription: SuggestionModeDescription,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: applicableSpanIsEmpty // Updated
            );
        }
    }

    sealed class ModelComputation<TModel> where TModel : class
    {
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly TaskScheduler _computationTaskScheduler;
        private readonly CancellationToken _token;
        private readonly IGuardedOperations _guardedOperations;
        private readonly ICompletionComputationCallbackHandler<TModel> _callbacks;

        private bool _terminated;
        private JoinableTask<TModel> _lastJoinableTask;
        private CancellationTokenSource _uiCancellation;

        internal TModel RecentModel { get; private set; } = default(TModel);

        public ModelComputation(
            TaskScheduler computationTaskScheduler,
            JoinableTaskContext joinableTaskContext,
            Func<TModel, CancellationToken, Task<TModel>> initialTransformation,
            CancellationToken token,
            IGuardedOperations guardedOperations,
            ICompletionComputationCallbackHandler<TModel> callbacks)
        {
            _joinableTaskFactory = joinableTaskContext.Factory;
            _computationTaskScheduler = computationTaskScheduler;
            _token = token;
            _guardedOperations = guardedOperations;
            _callbacks = callbacks;

            // Start dummy tasks so that we don't need to check for null on first Enqueue
            _lastJoinableTask = _joinableTaskFactory.RunAsync(() => Task.FromResult(default(TModel)));
            _uiCancellation = new CancellationTokenSource();

            // Immediately run the first transformation, to operate on proper TModel.
            Enqueue(initialTransformation, updateUi: false);
        }

        /// <summary>
        /// Schedules work to be done on the background,
        /// potentially preempted by another piece of work scheduled in the future,
        /// <paramref name="updateUi" /> indicates whether a single piece of work should occue once all background work is completed.
        /// </summary>
        public void Enqueue(Func<TModel, CancellationToken, Task<TModel>> transformation, bool updateUi)
        {
            // The integrity of our sequential chain depends on this method not being called concurrently.
            // So we require the UI thread.
            if (!_joinableTaskFactory.Context.IsOnMainThread)
                throw new InvalidOperationException($"This method must be callled on the UI thread.");

            if (_token.IsCancellationRequested || _terminated)
                return; // Don't enqueue after computation has stopped.

            // Attempt to commit (CommitIfUnique) will cancel the UI updates. If the commit failed, we still want to update the UI.
            if (_uiCancellation.IsCancellationRequested)
                _uiCancellation = new CancellationTokenSource();

            var previousTask = _lastJoinableTask;
            JoinableTask<TModel> currentTask = null;
            currentTask = _joinableTaskFactory.RunAsync(async () =>
            {
                await Task.Yield(); // Yield to guarantee that currentTask is assigned.
                await _computationTaskScheduler; // Go to the above priority thread. Main thread will return as soon as possible.
                try
                {
                    var previousModel = await previousTask;
                    // Previous task finished processing. We are ready to execute next piece of work.
                    if (_token.IsCancellationRequested || _terminated)
                        return previousModel;

                    var transformedModel = await transformation(await previousTask, _token);
                    RecentModel = transformedModel;

                    // TODO: update UI even if updateUi is false but it wasn't updated yet.
                    if (_lastJoinableTask == currentTask && updateUi)
                    {
                        // update UI because we're the latest task
                        if (!_uiCancellation.IsCancellationRequested)
                            _callbacks.UpdateUi(transformedModel, _uiCancellation.Token).Forget();
                    }

                    return transformedModel;
                }
                catch (Exception ex)
                {
                    _terminated = true;
                    _guardedOperations.HandleException(this, ex);
                    _callbacks.Dismiss();
                    return await previousTask;
                }
            });

            _lastJoinableTask = currentTask;
        }

        /// <summary>
        /// Blocks, waiting for all background work to finish.
        /// Ignores the last piece of work a.k.a. "updateUi"
        /// </summary>
        public TModel WaitAndGetResult(CancellationToken token)
        {
            _uiCancellation.Cancel();
            try
            {
                return _lastJoinableTask.Join(token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }
}
