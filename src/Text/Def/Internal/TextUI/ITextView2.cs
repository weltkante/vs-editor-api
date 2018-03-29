//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//
// This file contain internal APIs that are subject to change without notice.
// Use at your own risk.
//
namespace Microsoft.VisualStudio.Text.Editor
{
    using System;

    /// <summary>
    /// An extension of the ITextView that exposes some internal hooks.
    /// </summary>
    public interface ITextView2 : ITextView
    {
        /// <summary>
        /// The MaxTextRightCoordinate of the view based only on the text contained in the view.
        /// </summary>
        double RawMaxTextRightCoordinate
        {
            get;
        }

        /// <summary>
        /// The minimum value for the view's MaxTextRightCoordinate.
        /// </summary>
        /// <remarks>
        /// If setting this value changes the view's MaxTextRightCoordinate, the view will raise a layout changed event.
        /// </remarks>
        double MinMaxTextRightCoordinate
        {
            get;
            set;
        }

        /// <summary>
        /// Determines whether the view is in the process of being laid out or is preparing to be laid out.
        /// </summary>
        /// <remarks>
        /// As opposed to <see cref="ITextView.InLayout"/>, it is safe to get the <see cref="ITextView.TextViewLines"/>
        /// but attempting to queue another layout will cause a reentrant layout exception.
        /// </remarks>
        bool InOuterLayout
        {
            get;
        }

        /// <summary>
        /// Raised whenever the view's MaxTextRightCoordinate is changed.
        /// </summary>
        /// <remarks>
        /// This event will only be rasied if the MaxTextRightCoordinate is changed by changing the MinMaxTextRightCoordinate property
        /// (it will not be raised as a side-effect of a layout even if the layout does change the MaxTextRightCoordinate).
        /// </remarks>
        event EventHandler MaxTextRightCoordinateChanged;
    }
}
