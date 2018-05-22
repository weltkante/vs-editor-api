using System;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    /// <summary>
    /// Represents an immutable snapshot of state of the async completion feature.
    /// </summary>
    internal sealed class CompletionModel
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
        public readonly bool DisplaySuggestionItem;

        /// <summary>
        /// Whether suggestion mode item should be selected.
        /// </summary>
        public readonly bool SelectSuggestionItem;

        /// <summary>
        /// <see cref="CompletionItem"/> which contains user-entered text.
        /// Used to display and commit the suggestion mode item
        /// </summary>
        public readonly CompletionItem SuggestionItem;

        /// <summary>
        /// <see cref="CompletionItem"/> which overrides regular unique item selection.
        /// When this is null, the single item from <see cref="PresentedItems"/> is used as unique item.
        /// </summary>
        public readonly CompletionItem UniqueItem;

        /// <summary>
        /// This flags prevents <see cref="IAsyncCompletionSession"/> from dismissing when it initially becomes empty.
        /// We dismiss when this flag is set (span is empty) and user attempts to remove characters.
        /// </summary>
        public readonly bool ApplicableToSpanWasEmpty;

        /// <summary>
        /// Whether completion is unavailable (hidden and impossible to commit).
        /// This allows language to not display completion when certain initial conditions are met.
        /// For more info, ask Roslyn devs about completion behavior with method parameters that are not of enum type.
        /// </summary>
        public readonly bool InitiallyUnavailable;

        /// <summary>
        /// Constructor for the initial model
        /// </summary>
        public CompletionModel(ImmutableArray<CompletionItem> initialItems, ImmutableArray<CompletionItem> sortedItems,
            ITextSnapshot snapshot, ImmutableArray<CompletionFilterWithState> filters, bool useSoftSelection,
            bool displaySuggestionItem, bool selectSuggestionItem, CompletionItem suggestionItem, bool initiallyUnavailable)
        {
            InitialItems = initialItems;
            SortedItems = sortedItems;
            Snapshot = snapshot;
            Filters = filters;
            SelectedIndex = 0;
            UseSoftSelection = useSoftSelection;
            DisplaySuggestionItem = displaySuggestionItem;
            SelectSuggestionItem = selectSuggestionItem;
            SuggestionItem = suggestionItem;
            UniqueItem = null;
            ApplicableToSpanWasEmpty = false;
            InitiallyUnavailable = initiallyUnavailable;
        }

        /// <summary>
        /// Private constructor for the With* methods
        /// </summary>
        private CompletionModel(ImmutableArray<CompletionItem> initialItems, ImmutableArray<CompletionItem> sortedItems,
            ITextSnapshot snapshot, ImmutableArray<CompletionFilterWithState> filters, ImmutableArray<CompletionItemWithHighlight> presentedItems,
            bool useSoftSelection, bool displaySuggestionItem, int selectedIndex, bool selectSuggestionItem, CompletionItem suggestionItem,
            CompletionItem uniqueItem, bool applicableToSpanWasEmpty, bool initiallyUnavailable)
        {
            InitialItems = initialItems;
            SortedItems = sortedItems;
            Snapshot = snapshot;
            Filters = filters;
            PresentedItems = presentedItems;
            SelectedIndex = selectedIndex;
            UseSoftSelection = useSoftSelection;
            DisplaySuggestionItem = displaySuggestionItem;
            SelectSuggestionItem = selectSuggestionItem;
            SuggestionItem = suggestionItem;
            UniqueItem = uniqueItem;
            ApplicableToSpanWasEmpty = applicableToSpanWasEmpty;
            InitiallyUnavailable = initiallyUnavailable;
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
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: newSelectedIndex, // Updated
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
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
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
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
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
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
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: newIndex, // Updated
                selectSuggestionItem: false, // Explicit selection of regular item
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
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
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: -1, // Deselect regular item
                selectSuggestionItem: true, // Explicit selection of suggestion item
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
            );
        }

        public CompletionModel WithSuggestionItemVisibility(bool newDisplaySuggestionItem)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection | newDisplaySuggestionItem, // Enabling suggestion mode also enables soft selection
                displaySuggestionItem: newDisplaySuggestionItem, // Updated
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
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
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: newUniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
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
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
            );
        }

        internal CompletionModel WithSnapshotItemsAndFilters(ITextSnapshot snapshot, ImmutableArray<CompletionItemWithHighlight> presentedItems,
            int selectedIndex, CompletionItem uniqueItem, CompletionItem suggestionItem, ImmutableArray<CompletionFilterWithState> filters)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                snapshot: snapshot, // Updated
                filters: filters, // Updated
                presentedItems: presentedItems, // Updated
                useSoftSelection: UseSoftSelection,
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: selectedIndex, // Updated
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: suggestionItem, // Updated
                uniqueItem: uniqueItem, // Updated
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: InitiallyUnavailable
            );
        }

        internal CompletionModel WithApplicableToSpanStatus(bool applicableSpanIsEmpty)
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: applicableSpanIsEmpty, // Updated
                initiallyUnavailable: InitiallyUnavailable
            );
        }

        internal CompletionModel WithInitialAvailability()
        {
            return new CompletionModel(
                initialItems: InitialItems,
                sortedItems: SortedItems,
                snapshot: Snapshot,
                filters: Filters,
                presentedItems: PresentedItems,
                useSoftSelection: UseSoftSelection,
                displaySuggestionItem: DisplaySuggestionItem,
                selectedIndex: SelectedIndex,
                selectSuggestionItem: SelectSuggestionItem,
                suggestionItem: SuggestionItem,
                uniqueItem: UniqueItem,
                applicableToSpanWasEmpty: ApplicableToSpanWasEmpty,
                initiallyUnavailable: false // Updated
            );
        }
    }
}
