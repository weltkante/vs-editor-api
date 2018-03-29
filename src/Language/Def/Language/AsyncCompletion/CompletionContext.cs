using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// This type is used to transfer data from <see cref="IAsyncCompletionSource"/>
    /// to <see cref="IAsyncCompletionBroker"/> and further to <see cref="IAsyncCompletionItemManager"/>
    /// </summary>
    [DebuggerDisplay("{Items.Length} items")]
    public sealed class CompletionContext
    {
        /// <summary>
        /// Empty completion context, when <see cref="IAsyncCompletionSource"/> offers no items pertinent to given location.
        /// </summary>
        static CompletionContext Default = new CompletionContext(ImmutableArray<CompletionItem>.Empty);

        /// <summary>
        /// Set of completion items available at a location
        /// </summary>
        public readonly ImmutableArray<CompletionItem> Items;

        /// <summary>
        /// When set to true, the completion list will be initially soft-selected.
        /// Soft selection means that typing commit characters will not commit the item.
        /// The item may be committed only by using TAB or double-clicking.
        /// Soft selection is represented by a border around the item, without a background fill.
        /// Selecting another item automatically disables soft selection and enables full selection.
        /// </summary>
        public readonly bool UseSoftSelection;

        /// <summary>
        /// When set to true, the completion list will be in the "builder" mode,
        /// such that hitting Space will not commit it, but append it to the constructed item.
        /// </summary>
        public readonly bool UseSuggestionMode;

        /// <summary>
        /// Displayed when UI is in suggestion mode, yet there is no code to suggest
        /// </summary>
        public readonly string SuggestionModeDescription;

        /// <summary>
        /// Constructs <see cref="CompletionContext"/> with <see cref="CompletionItem"/> applicable to a <see cref="SnapshotSpan"/>
        /// </summary>
        /// <param name="items">Available completion items</param>
        /// <param name="applicableSpan">Completion list will be filtered by contents of this span</param>
        public CompletionContext(ImmutableArray<CompletionItem> items)
            : this(items, useSoftSelection: false, useSuggestionMode: false, suggestionModeDescription: string.Empty)
        {
        }

        /// <summary>
        /// Constructs <see cref="CompletionContext"/> with <see cref="CompletionItem"/> applicable to a <see cref="SnapshotSpan"/>,
        /// <see cref="CompletionFilter"/> available for these items and instructions on suggestion mode and soft selection.
        /// </summary>
        /// <param name="items">Available completion items</param>
        /// <param name="applicableToSpan">Completion list will be filtered by contents of this span</param>
        /// <param name="availableFilters">Completion filters available for these completion items</param>
        /// <param name="useSoftSelection">Whether UI should use soft selection</param>
        /// <param name="useSuggestionMode">Whether UI should enter suggestion mode</param>
        /// <param name="suggestionModeDescription">Explains why suggestion mode is active. It is displayed when applicableSpan is empty. Otherwise, UI displays content of applicableSpan. May be null.</param>
        public CompletionContext(
            ImmutableArray<CompletionItem> items,
            bool useSoftSelection,
            bool useSuggestionMode,
            string suggestionModeDescription)
        {
            if (items.IsDefault)
                throw new ArgumentException("Array must be initialized", nameof(items));
            Items = items;
            UseSoftSelection = useSoftSelection;
            UseSuggestionMode = useSuggestionMode;
            SuggestionModeDescription = suggestionModeDescription;
        }
    }
}
