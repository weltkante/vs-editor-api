//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//
using System;

namespace Microsoft.VisualStudio.Text.MultiSelection
{
    /// <summary>
    /// Manages the insertion, anchor, and active points for a single caret and its associated
    /// selection.
    /// </summary>
    public struct Selection : IEquatable<Selection>
    {
        /// <summary>
        /// A static instance of a selection that is invalid and can be used to check for instantiation.
        /// </summary>
        public static readonly Selection Invalid = new Selection();

        /// <summary>
        /// Instantiates a new Selection.
        /// </summary>
        /// <param name="insertionPoint">The location where a caret should be rendered and edits performed.</param>
        /// <param name="anchorPoint">The location of the fixed selection endpoint, meaning if a user were to hold shift and click,
        /// this point would remain where it is.</param>
        /// <param name="activePoint">location of the movable selection endpoint, meaning if a user were to hold shift and click,
        /// this point would be changed to the location of the click.</param>
        /// <param name="insertionPointAffinity">
        /// The affinity of the insertion point. This is used in places like word-wrap where one buffer position can represent both the
        /// end of one line and the beginning of the next.
        /// </param>
        public Selection(VirtualSnapshotPoint insertionPoint, VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint, PositionAffinity insertionPointAffinity)
        {
            if (insertionPoint.Position.Snapshot != anchorPoint.Position.Snapshot || insertionPoint.Position.Snapshot != activePoint.Position.Snapshot)
            {
                throw new ArgumentException("All points must be on the same snapshot.");
            }

            InsertionPoint = insertionPoint;
            AnchorPoint = anchorPoint;
            ActivePoint = activePoint;
            InsertionPointAffinity = insertionPointAffinity;
        }

        /// <summary>
        /// Gets whether this selection contains meaningful data.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return this != Invalid && this.InsertionPoint.Position.Snapshot != null;
            }
        }

        /// <summary>
        /// Gets the location where a caret should be rendered and edits performed.
        /// </summary>
        public VirtualSnapshotPoint InsertionPoint { get; }

        /// <summary>
        /// Gets the location of the fixed selection endpoint, meaning if a user were to hold shift and click,
        /// this point would remain where it is. If this is an empty selection, this will be at the
        /// <see cref="InsertionPoint"/>.
        /// </summary>
        public VirtualSnapshotPoint AnchorPoint { get; }

        /// <summary>
        /// Gets the location of the movable selection endpoint, meaning if a user were to hold shift and click,
        /// this point would be changed to the location of the click. If this is an empty selection, this will be at the
        /// <see cref="InsertionPoint"/>.
        /// </summary>
        public VirtualSnapshotPoint ActivePoint { get; }

        /// <summary>
        /// Gets the affinity of the insertion point.
        /// This is used in places like word-wrap where one buffer position can represent both the
        /// end of one line and the beginning of the next.
        /// </summary>
        public PositionAffinity InsertionPointAffinity { get; }

        /// <summary>
        /// True if <see cref="AnchorPoint"/> is later in the document than <see cref="ActivePoint"/>. False otherwise.
        /// </summary>
        public bool IsReversed
        {
            get
            {
                return ActivePoint < AnchorPoint;
            }
        }

        /// <summary>
        /// True if <see cref="AnchorPoint"/> equals <see cref="ActivePoint"/>. False otherwise.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return ActivePoint == AnchorPoint;
            }
        }

        /// <summary>
        /// Returns the smaller of <see cref="ActivePoint"/> and <see cref="AnchorPoint"/>.
        /// </summary>
        public VirtualSnapshotPoint Start
        {
            get
            {
                return IsReversed ? ActivePoint : AnchorPoint;
            }
        }

        /// <summary>
        /// Returns the larger of <see cref="ActivePoint"/> and <see cref="AnchorPoint"/>.
        /// </summary>
        public VirtualSnapshotPoint End
        {
            get
            {
                return IsReversed ? AnchorPoint : ActivePoint;
            }
        }

        /// <summary>
        /// Returns the span from <see cref="Start"/> to <see cref="End"/>.
        /// </summary>
        public VirtualSnapshotSpan Extent
        {
            get
            {
                return new VirtualSnapshotSpan(Start, End);
            }
        }

        /// <summary>
        /// Creates a clone of this object.
        /// </summary>
        /// <returns>The new cloned object.</returns>
        public Selection Clone()
        {
            return new Selection(this.InsertionPoint, this.AnchorPoint, this.ActivePoint, this.InsertionPointAffinity);
        }

        public override int GetHashCode()
        {
            // We are fortunate enough to have 3 interesting points here. If you xor an even number of snapshot point hashcodes
            // together, the snapshot component gets cancelled out.
            return AnchorPoint.GetHashCode() ^ ActivePoint.GetHashCode() ^ InsertionPoint.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is Selection && Equals((Selection)obj);
        }

        public bool Equals(Selection other)
        {
            return this.ActivePoint == other.ActivePoint
                && this.AnchorPoint == other.AnchorPoint
                && this.InsertionPoint == other.InsertionPoint;
        }

        public static bool operator ==(Selection left, Selection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Selection left, Selection right)
        {
            return !left.Equals(right);
        }
    }
}
