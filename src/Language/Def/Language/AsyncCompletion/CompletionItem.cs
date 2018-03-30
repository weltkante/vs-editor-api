using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// This class, returned from <see cref="IAsyncCompletionSource"/>, represents a single entry
    /// to be displayed in the completion UI. This class implements <see cref="IPropertyOwner"/>
    /// </summary>
    [DebuggerDisplay("{DisplayText}")]
    public sealed class CompletionItem : IPropertyOwner
    {
        /// <summary>
        /// Text used in the UI
        /// </summary>
        public string DisplayText { get; }

        /// <summary>
        /// Text inserted when completing this item
        /// </summary>
        public string InsertText { get; }

        /// <summary>
        /// Text used by <see cref="IAsyncCompletionItemManager"/> when sorting against other items
        /// </summary>
        public string SortText { get; }

        /// <summary>
        /// Text used by <see cref="IAsyncCompletionItemManager"/> when matching against the applicable span
        /// </summary>
        public string FilterText { get; }

        /// <summary>
        /// Reference to the instance that will provide tooltip and custom commit method.
        /// Usually it is the same instance as the one that created this <see cref="CompletionItem"/>
        /// </summary>
        public IAsyncCompletionSource Source { get; }

        /// <summary>
        /// <see cref="ImmutableArray"/> of references to <see cref="CompletionFilter"/>s applicable to this item
        /// </summary>
        public ImmutableArray<CompletionFilter> Filters { get; }

        /// <summary>
        /// Image displayed in the UI
        /// </summary>
        public AccessibleImageId Icon { get; }

        /// <summary>
        /// Additional text to display in the UI, after <see cref="DisplayText"/>.
        /// This text has less emphasis than <see cref="DisplayText"/> and is usually right-aligned.
        /// </summary>
        public string Suffix { get; }

        /// <summary>
        /// Additional images to display in the UI.
        /// Usually, these images are displayed on the far right side of the UI.
        /// </summary>
        public ImmutableArray<AccessibleImageId> AttributeIcons { get; }

        /// <summary>
        /// The collection of properties controlled by the property owner. See <see cref="IPropertyOwner.Properties"/>
        /// </summary>
        public PropertyCollection Properties { get; }

        /// <summary>
        /// Creates a completion item whose <see cref="DisplayText"/>, <see cref="InsertText"/>, <see cref="SortText"/> and <see cref="FilterText"/> are all the same,
        /// and there are no icon, filter, suffix nor attribute icons associated with this item.
        /// </summary>
        /// <param name="displayText">Text to use in the UI, when sorting, filtering and completing</param>
        /// <param name="source">Reference to <see cref="IAsyncCompletionSource"/> that created this item</param>
        public CompletionItem(string displayText, IAsyncCompletionSource source)
            : this(displayText, insertText: displayText, sortText: displayText, filterText: displayText,
                  source: source, filters: ImmutableArray<CompletionFilter>.Empty, icon: default(AccessibleImageId),
                  suffix: string.Empty, attributeIcons: ImmutableArray<AccessibleImageId>.Empty)
        {
        }

        /// <summary>
        /// Creates a completion item whose <see cref="DisplayText"/>, <see cref="InsertText"/>, <see cref="SortText"/> and <see cref="FilterText"/> are all the same,
        /// there is an image, and there are no filter, suffix nor attribute images associated with this item.
        /// </summary>
        /// <param name="displayText">Text to use in the UI, when sorting, filtering and completing</param>
        /// <param name="source">Reference to <see cref="IAsyncCompletionSource"/> that created this item</param>
        /// <param name="icon">Image displayed in the UI</param>
        /// <param name="suffix">Additional text to display in the UI</param>
        public CompletionItem(string displayText, IAsyncCompletionSource source, AccessibleImageId icon)
            : this(displayText, insertText: displayText, sortText: displayText, filterText: displayText,
                  source: source, filters: ImmutableArray<CompletionFilter>.Empty, icon: icon,
                  suffix: string.Empty, attributeIcons: ImmutableArray<AccessibleImageId>.Empty)
        {
        }

        /// <summary>
        /// Creates a completion item whose <see cref="DisplayText"/>, <see cref="InsertText"/>, <see cref="SortText"/> and <see cref="FilterText"/> are all the same,
        /// there is an image, filters, and there are no suffix nor attribute images associated with this item.
        /// </summary>
        /// <param name="displayText">Text to use in the UI, when sorting, filtering and completing</param>
        /// <param name="source">Reference to <see cref="IAsyncCompletionSource"/> that created this item</param>
        /// <param name="icon">Image displayed in the UI</param>
        /// <param name="filters"><see cref="ImmutableArray"/> of references to <see cref="CompletionFilter"/>s applicable to this item</param>
        public CompletionItem(string displayText, IAsyncCompletionSource source, AccessibleImageId icon, ImmutableArray<CompletionFilter> filters)
            : this(displayText, insertText: displayText, sortText: displayText, filterText: displayText,
                  source: source, filters: filters, icon: icon,
                  suffix: string.Empty, attributeIcons: ImmutableArray<AccessibleImageId>.Empty)
        {
        }

        /// <summary>
        /// Creates a completion item whose <see cref="DisplayText"/>, <see cref="InsertText"/>, <see cref="SortText"/> and <see cref="FilterText"/> are all the same,
        /// there is an image, filters, suffix, and there are no attribute images associated with this item.
        /// </summary>
        /// <param name="displayText">Text to use in the UI, when sorting, filtering and completing</param>
        /// <param name="source">Reference to <see cref="IAsyncCompletionSource"/> that created this item</param>
        /// <param name="icon">Image displayed in the UI</param>
        /// <param name="filters"><see cref="ImmutableArray"/> of references to <see cref="CompletionFilter"/>s applicable to this item</param>
        /// <param name="suffix">Additional text to display in the UI</param>
        public CompletionItem(string displayText, IAsyncCompletionSource source, AccessibleImageId icon, ImmutableArray<CompletionFilter> filters, string suffix)
            : this(displayText, insertText: displayText, sortText: displayText, filterText: displayText,
                  source: source, filters: ImmutableArray<CompletionFilter>.Empty, icon: icon,
                  suffix: suffix, attributeIcons: ImmutableArray<AccessibleImageId>.Empty)
        {
        }

        /// <summary>
        /// Creates a completion item, allowing customization of all of its properties.
        /// </summary>
        /// <param name="displayText">Text used in the UI</param>
        /// <param name="source">Reference to <see cref="IAsyncCompletionSource"/> that created this item</param>
        /// <param name="icon">Image displayed in the UI</param>
        /// <param name="filters"><see cref="ImmutableArray"/> of references to <see cref="CompletionFilter"/>s applicable to this item</param>
        /// <param name="suffix">Additional text to display in the UI</param>
        /// <param name="insertText">Text inserted when completing this item</param>
        /// <param name="sortText">Text used by <see cref="IAsyncCompletionItemManager"/> when sorting against other items</param>
        /// <param name="filterText">Text used by <see cref="IAsyncCompletionItemManager"/> when matching against the applicable span</param>
        /// <param name="attributeIcons">Additional images to display in the UI</param>
        public CompletionItem(string displayText, IAsyncCompletionSource source, AccessibleImageId icon, ImmutableArray<CompletionFilter> filters,
            string suffix, string insertText, string sortText, string filterText, ImmutableArray<AccessibleImageId> attributeIcons)
        {
            if (displayText == null)
                displayText = String.Empty;
            if (insertText == null)
                insertText = String.Empty;
            if (sortText == null)
                sortText = String.Empty;
            if (filterText == null)
                filterText = String.Empty;
            if (filters.IsDefault)
                throw new ArgumentException("Array must be initialized", nameof(filters));

            DisplayText = displayText;
            InsertText = insertText;
            SortText = sortText;
            FilterText = filterText;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Icon = icon;
            Filters = filters;
            Suffix = suffix;
            AttributeIcons = attributeIcons;
            Properties = new PropertyCollection();
        }

        public override string ToString() => DisplayText;
    }
}
