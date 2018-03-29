using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// This class is used to notify completion's logic of selecting through the UI
    /// </summary>
    [DebuggerDisplay("EventArgs: {SelectedItem}, is suggestion: {SuggestionModeSelected}")]
    public sealed class CompletionItemSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// Selected item. Might be null if there is no selection
        /// </summary>
        public CompletionItem SelectedItem { get; }

        /// <summary>
        /// Whether selected item is a suggestion mode item
        /// </summary>
        public bool SuggestionModeSelected { get; }

        /// <summary>
        /// Constructs instance of <see cref="CompletionItemSelectedEventArgs"/>.
        /// </summary>
        /// <param name="selectedItem">User-selected item</param>
        /// <param name="suggestionModeSelected">Whether the selected item is a suggestion mode item</param>
        public CompletionItemSelectedEventArgs(CompletionItem selectedItem, bool suggestionModeSelected)
        {
            this.SelectedItem = selectedItem;
            this.SuggestionModeSelected = suggestionModeSelected;
        }
    }
}
