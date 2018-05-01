using System.Collections.Immutable;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    /// <summary>
    /// Represents an immutable snapshot of state of the async completion feature.
    /// </summary>
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
        /// Snapshot pertinent to this completion model.
        /// </summary>
        public readonly ITextSnapshot Snapshot;

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
        /// <see cref="CompletionItem"/> which overrides regular unique item selection.
        /// When this is null, the single item from <see cref="PresentedItems"/> is used as unique item.
        /// </summary>
        public readonly CompletionItem UniqueItem;

        /// <summary>
        /// This flags prevents <see cref="IAsyncCompletionSession"/> from dismissing when it initially becomes empty.
        /// We dismiss when this flag is set (span is empty) and user attempts to remove characters.
        /// </summary>
        public readonly bool ApplicableSpanWasEmpty;

        /// <summary>
        /// Constructor for the initial model
        /// </summary>
        public CompletionModel(ImmutableArray<CompletionItem> initialItems, ImmutableArray<CompletionItem> sortedItems,
            ITextSnapshot snapshot, ImmutableArray<CompletionFilterWithState> filters, bool useSoftSelection,
            bool useSuggestionMode, bool selectSuggestionItem, CompletionItem suggestionModeItem)
        {
            InitialItems = initialItems;
            SortedItems = sortedItems;
            Snapshot = snapshot;
            Filters = filters;
            SelectedIndex = 0;
            UseSoftSelection = useSoftSelection;
            DisplaySuggestionMode = useSuggestionMode;
            SelectSuggestionItem = selectSuggestionItem;
            SuggestionModeItem = suggestionModeItem;
            UniqueItem = null;
            ApplicableSpanWasEmpty = false;
        }

        /// <summary>
        /// Private constructor for the With* methods
        /// </summary>
        private CompletionModel(ImmutableArray<CompletionItem> initialItems, ImmutableArray<CompletionItem> sortedItems,
            ITextSnapshot snapshot, ImmutableArray<CompletionFilterWithState> filters, ImmutableArray<CompletionItemWithHighlight> presentedItems,
            bool useSoftSelection, bool useSuggestionMode, int selectedIndex, bool selectSuggestionItem, CompletionItem suggestionModeItem,
            CompletionItem uniqueItem, bool applicableSpanWasEmpty)
        {
            InitialItems = initialItems;
            SortedItems = sortedItems;
            Snapshot = snapshot;
            Filters = filters;
            PresentedItems = presentedItems;
            SelectedIndex = selectedIndex;
            UseSoftSelection = useSoftSelection;
            DisplaySuggestionMode = useSuggestionMode;
            SelectSuggestionItem = selectSuggestionItem;
            SuggestionModeItem = suggestionModeItem;
            UniqueItem = uniqueItem;
            ApplicableSpanWasEmpty = applicableSpanWasEmpty;
        }

        public CompletionModel WithPresentedItems(ImmutableArray<CompletionItemWithHighlight> newPresentedItems, int newSelectedIndex)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: newPresentedItems, // Updated
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: newSnapshot, // Updated
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: Snapshot,
                filters: newFilters, // Updated
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: false, // Explicit selection and soft selection are mutually exclusive
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: false, // Explicit selection and soft selection are mutually exclusive
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection | newUseSuggestionMode, // Enabling suggestion mode also enables soft selection
                useSuggestionMode: newUseSuggestionMode, // Updated
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
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: newSoftSelection, // Updated
                useSuggestionMode: DisplaySuggestionMode,
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
                snapshot: snapshot, // Updated
                filters: filters, // Updated
                presentedItems: presentedItems, // Updated
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                selectedIndex: selectedIndex, // Updated
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: suggestionModeItem, // Updated
                uniqueItem: uniqueItem, // Updated
                applicableSpanWasEmpty: ApplicableSpanWasEmpty
            );
        }

        internal CompletionModel WithApplicableSpanEmptyInformation(bool applicableSpanIsEmpty)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                useSuggestionMode: DisplaySuggestionMode,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionModeItem: SuggestionModeItem,
                uniqueItem: UniqueItem,
                applicableSpanWasEmpty: applicableSpanIsEmpty // Updated
            );
        }
    }
}
