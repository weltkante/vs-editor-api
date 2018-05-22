//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//

using System;

namespace Microsoft.VisualStudio.Text.MultiSelection
{
    /// <summary>
    /// Describes interesting events generated from an <see cref="IMultiSelectionBroker"/>.
    /// </summary>
    public sealed class MultiSelectionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Denotes a change in the current value of <see cref="IMultiSelectionBroker.PrimarySelection"/>.
        /// </summary>
        public bool PrimarySelectionChanged { get; internal set; }

        /// <summary>
        /// Denotes a change in the current value of <see cref="IMultiSelectionBroker.BoxSelection"/>
        /// and associated properties.
        /// </summary>
        public bool BoxSelectionChanged { get; internal set; }

        /// <summary>
        /// Denotes a change in the current value of <see cref="IMultiSelectionBroker.AreSelectionsActive"/>.
        /// </summary>
        public bool IsActiveChanged { get; internal set; }

        /// <summary>
        /// Denotes a change of <see cref="IMultiSelectionBroker.AllSelections"/> or a change within one
        /// of the contained <see cref="Selection"/> objects.
        /// </summary>
        public bool SelectionsChanged { get; internal set; }

        /// <summary>
        /// Denotes an event that intentionally invalidates cached values returned
        /// from <see cref="IMultiSelectionBroker.TryGetSelectionPresentationProperties(Selection, out AbstractSelectionPresentationProperties)"/>.
        /// </summary>
        public bool PresentationPropertiesInvalidated { get; internal set; }
    }
}
