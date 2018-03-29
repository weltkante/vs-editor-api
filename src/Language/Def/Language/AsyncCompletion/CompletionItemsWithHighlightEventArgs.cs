using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// This class is used to notify of an operation that affects multiple <see cref="CompletionItemWithHighlight"/>s.
    /// </summary>
    [DebuggerDisplay("EventArgs: {Items.Length} items")]
    public sealed class CompletionItemsWithHighlightEventArgs : EventArgs
    {
        /// <summary>
        /// Relevant items
        /// </summary>
        public ImmutableArray<CompletionItemWithHighlight> Items { get; }

        /// <summary>
        /// Constructs instance of <see cref="CompletionItemEventArgs"/>.
        /// </summary>
        public CompletionItemsWithHighlightEventArgs(ImmutableArray<CompletionItemWithHighlight> items)
        {
            if (items.IsDefault)
                throw new ArgumentException("Array must be initialized", nameof(items));
            Items = items;
        }
    }
}
