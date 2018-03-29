using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// This class contains completion items, filters and other pieces of information
    /// used by <see cref="ICompletionPresenter"/> to render the completion UI.
    /// </summary>
    public sealed class CompletionPresentationViewModel
    {
        /// <summary>
        /// Completion items to display with their highlighted spans.
        /// </summary>
        public readonly ImmutableArray<CompletionItemWithHighlight> Items;

        /// <summary>
        /// Completion filters with their available and selected state.
        /// </summary>
        public readonly ImmutableArray<CompletionFilterWithState> Filters;

        /// <summary>
        /// Span pertinent to the completion session.
        /// </summary>
        public readonly ITrackingSpan ApplicableSpan;

        /// <summary>
        /// Controls whether selected item should be soft selected.
        /// </summary>
        public readonly bool UseSoftSelection;

        /// <summary>
        /// Controls whether suggestion mode item is visible.
        /// </summary>
        public readonly bool UseSuggestionMode;

        /// <summary>
        /// Controls whether suggestion mode item is selected.
        /// </summary>
        public readonly bool SelectSuggestionMode;

        /// <summary>
        /// Controls which item is selected. Use -1 in suggestion mode.
        /// </summary>
        public readonly int SelectedItemIndex;

        /// <summary>
        /// Suggestion mode item to display.
        /// </summary>
        public readonly CompletionItem SuggestionModeItem;

        /// <summary>
        /// Text to display when <see cref="SuggestionModeItem"/>'s <see cref="CompletionItem.InsertText"/> is empty.
        /// </summary>
        public readonly string SuggestionModeDescription;

        /// <summary>
        /// Constructs <see cref="CompletionPresentationViewModel"/>
        /// </summary>
        /// <param name="items">Completion items to display with their highlighted spans</param>
        /// <param name="filters">Completion filters with their available and selected state</param>
        /// <param name="applicableSpan">Span pertinent to the completion session</param>
        /// <param name="useSoftSelection">Controls whether selected item should be soft selected</param>
        /// <param name="useSuggestionMode">Controls whether suggestion mode item is visible</param>
        /// <param name="selectSuggestionMode">Controls whether suggestion mode item is selected</param>
        /// <param name="selectedItemIndex">Controls which item is selected. Use -1 in suggestion mode</param>
        /// <param name="suggestionModeItem">Suggestion mode item to display</param>
        public CompletionPresentationViewModel(
            ImmutableArray<CompletionItemWithHighlight> items,
            ImmutableArray<CompletionFilterWithState> filters,
            ITrackingSpan applicableSpan,
            bool useSoftSelection,
            bool useSuggestionMode,
            bool selectSuggestionMode,
            int selectedItemIndex,
            CompletionItem suggestionModeItem,
            string suggestionModeDescription)
        {
            if (selectedItemIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(selectedItemIndex), "Selected index value must be greater than or equal to 0, or -1 to indicate no selection");
            if (items.IsDefault)
                throw new ArgumentException("Array must be initialized", nameof(items));
            if (filters.IsDefault)
                throw new ArgumentException("Array must be initialized", nameof(filters));

            Items = items;
            Filters = filters;
            ApplicableSpan = applicableSpan ?? throw new NullReferenceException(nameof(ApplicableSpan));
            UseSoftSelection = useSoftSelection;
            UseSuggestionMode = useSuggestionMode;
            SelectSuggestionMode = selectSuggestionMode;
            SelectedItemIndex = selectedItemIndex;
            SuggestionModeItem = suggestionModeItem;
            SuggestionModeDescription = suggestionModeDescription;
        }
    }
}
