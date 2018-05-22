//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//

using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Text.MultiSelection
{
    /// <summary>
    /// Used to get or create a multi caret broker for a given <see cref="ITextView"/>.
    /// There will always be a maximum of one <see cref="IMultiSelectionBroker"/>
    /// for a given <see cref="ITextView"/>.
    /// <remarks>
    /// This is a MEF component part, and should be imported as follows:
    /// [Import]
    /// IMultiSelectionBrokerFactory factory = null;
    /// </remarks>

    public interface IMultiSelectionBrokerFactory
    {
        /// <summary>
        /// Gets an <see cref="IMultiSelectionBroker"/> for the specified <see cref="ITextView"/>.
        /// If there is no <see cref="IMultiSelectionBroker"/> for the view, one
        /// will be created.
        /// </summary>
        /// <param name="textView">The view for which to obtain an <see cref="IMultiSelectionBroker"/>.</param>
        /// <returns>
        /// An <see cref="IMultiSelectionBroker"/> associated with the <see cref="ITextView"/>
        /// <see cref="ITextView"/>.
        /// </returns>
        IMultiSelectionBroker GetOrCreateBroker(ITextView textView);
    }
}
