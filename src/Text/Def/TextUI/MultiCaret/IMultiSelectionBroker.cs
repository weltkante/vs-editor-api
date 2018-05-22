//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Microsoft.VisualStudio.Text.MultiSelection
{
    /// <summary>
    /// Manages all the caret and selecting behavior for an <see cref="ITextView"/>.
    /// Handles multiple selections, and box selection. Throughout this namespace carets
    /// are considered to be part of Selections, and are represented by <see cref="Selection.InsertionPoint"/>.
    /// </summary>
    public interface IMultiSelectionBroker
    {
        /// <summary>
        /// Gets the view for which this broker manages selections.
        /// </summary>
        ITextView TextView { get; }

        /// <summary>
        /// Gets the current <see cref="ITextSnapshot"/> that is associated with anchor,
        /// active, and insertion points for everything managed by this broker. This snapshot
        /// will always be based in the <see cref="ITextViewModel.EditBuffer"/> for the associated
        /// <see cref="ITextView"/>.
        /// </summary>
        ITextSnapshot CurrentSnapshot { get; }

        #region Add/Remove/Get Selections
        /// <summary>
        /// Gets a list of all selections associated with <see cref="TextView" />. They will
        /// be sorted in the order of appearence in the underlying snapshot. This property is
        /// intended for edit operations and may be computationally expensive. If not all
        /// selections are required, use <see cref="GetSelectionsIntersectingSpan(SnapshotSpan)"/> instead.
        ///
        /// This returns a selection as an <see cref="Selection"/>.
        /// </summary>
        IReadOnlyList<Selection> AllSelections { get; }

        /// <summary>
        /// Gets a list of all the selections that intersect the given span. Virtual whitespace is ignored for this method.
        /// </summary>
        /// <param name="span">The span of interest.</param>
        /// <returns>The list of <see cref="Selection"/> objects.</returns>
        IReadOnlyList<Selection> GetSelectionsIntersectingSpan(SnapshotSpan span);

        /// <summary>
        /// Gets a list of all the selections that intersect the given span collection. Virtual whitespace is ignored for this method.
        /// </summary>
        /// <param name="spanCollection"></param>
        /// <returns></returns>
        IReadOnlyList<Selection> GetSelectionsIntersectingSpans(NormalizedSnapshotSpanCollection spanCollection);

        /// <summary>
        /// Adds a new <see cref="Selection"/> with the given characteristics.
        /// </summary>
        /// <param name="anchorPoint">The stationary endpoint of a selection.</param>
        /// <param name="activePoint">The mobile endpoint of a selection. It also defines the insertion point of the new selection.</param>
        /// <returns>
        /// An instantiated <see cref="Selection"/> object matching the provided characteristics, which
        /// can be further manipulated by the caller if desired.
        /// </returns>
        /// <remarks>
        /// This will throw if the provided points are on a different snapshot than <see cref="CurrentSnapshot"/>.
        /// </remarks>
        Selection AddSelection(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint);

        /// <summary>
        /// Adds a new <see cref="Selection"/> with the given characteristics.
        /// </summary>
        /// <param name="anchorPoint">The stationary endpoint of a selection.</param>
        /// <param name="activePoint">The mobile endpoint of a selection. It also defines the insertion point of the new selection.</param>
        /// <param name="insertionPointAffinity">
        /// The affinity of the insertion point. This is used in places like word-wrap where one buffer position can represent both the
        /// end of one line and the beginning of the next.
        /// </param>
        /// <returns>
        /// An instantiated <see cref="Selection"/> object matching the provided characteristics, which
        /// can be further manipulated by the caller if desired.
        /// </returns>
        /// <remarks>
        /// This will throw if the provided points are on a different snapshot than <see cref="CurrentSnapshot"/>.
        /// </remarks>
        Selection AddSelection(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint, PositionAffinity insertionPointAffinity);

        /// <summary>
        /// Adds a new <see cref="Selection"/> with the given characteristics.
        /// </summary>
        /// <param name="anchorPoint">The stationary endpoint of a selection.</param>
        /// <param name="activePoint">The mobile endpoint of a selection. It also defines the insertion point of the new selection.</param>
        /// <param name="insertionPoint">The location where characters will be inserted.</param>
        /// <param name="insertionPointAffinity">
        /// The affinity of the insertion point. This is used in places like word-wrap where one buffer position can represent both the
        /// end of one line and the beginning of the next.
        /// </param>
        /// <returns>
        /// An instantiated <see cref="Selection"/> object matching the provided characteristics, which
        /// can be further manipulated by the caller if desired.
        /// </returns>
        /// <remarks>
        /// This will throw if the provided points are on a different snapshot than <see cref="CurrentSnapshot"/>.
        /// </remarks>
        Selection AddSelection(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint, VirtualSnapshotPoint insertionPoint, PositionAffinity insertionPointAffinity);

        /// <summary>
        /// Clears all selections and creates a new one with the given characteristics.
        /// </summary>
        /// <param name="anchorPoint">The stationary endpoint of a selection.</param>
        /// <param name="activePoint">The mobile endpoint of a selection. It also defines the insertion point of the new selection.</param>
        /// <returns>
        /// /// An instantiated <see cref="Selection"/> object matching the provided characteristics, which
        /// can be further manipulated by the caller if desired.
        /// </returns>
        Selection SetSelection(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint);

        /// <summary>
        /// Clears all selections and creates a new one with the given characteristics.
        /// </summary>
        /// <param name="anchorPoint">The stationary endpoint of a selection.</param>
        /// <param name="activePoint">The mobile endpoint of a selection. It also defines the insertion point of the new selection.</param>
        /// <param name="insertionPointAffinity">
        /// The affinity of the insertion point. This is used in places like word-wrap where one buffer position can represent both the
        /// end of one line and the beginning of the next.
        /// </param>
        /// <returns>
        /// /// An instantiated <see cref="Selection"/> object matching the provided characteristics, which
        /// can be further manipulated by the caller if desired.
        /// </returns>
        Selection SetSelection(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint, PositionAffinity insertionPointAffinity);

        /// <summary>
        /// Clears all selections and creates a new one with the given characteristics.
        /// </summary>
        /// <param name="anchorPoint">The stationary endpoint of a selection.</param>
        /// <param name="activePoint">The mobile endpoint of a selection. It also defines the insertion point of the new selection.</param>
        /// <param name="insertionPoint">The location where characters will be inserted.</param>
        /// <param name="insertionPointAffinity">
        /// The affinity of the insertion point. This is used in places like word-wrap where one buffer position can represent both the
        /// end of one line and the beginning of the next.
        /// </param>
        /// <returns>
        /// /// An instantiated <see cref="Selection"/> object matching the provided characteristics, which
        /// can be further manipulated by the caller if desired.
        /// </returns>
        Selection SetSelection(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint, VirtualSnapshotPoint insertionPoint, PositionAffinity insertionPointAffinity);

        /// <summary>
        /// Removes a selection from the view.
        /// </summary>
        /// <param name="selection">The selection to remove.</param>
        /// <returns><c>true</c> if successful. <c>false</c> otherwise. This can fail if either the selection passed in does not exist in the view, or
        /// it is the last one.</returns>
        bool TryRemoveSelection(Selection selection);

        /// <summary>
        /// Gets the primary selection which should remain after invoking <see cref="ClearSecondarySelections"/>.
        /// </summary>
        Selection PrimarySelection { get; }

        /// <summary>
        /// Attempts to set the provided selection to be the new <see cref="PrimarySelection"/>.
        /// </summary>
        /// <param name="candidate">The new candidate for primary selection.</param>
        /// <returns>Whether the set operation was successful. This will return <c>false</c> if the candidate is not
        /// found in <see cref="AllSelections"/>.</returns>
        bool TrySetAsPrimarySelection(Selection candidate);

        /// <summary>
        /// Removes all but the <see cref="PrimarySelection"/> from the session.
        /// </summary>
        void ClearSecondarySelections();

        /// <summary>
        /// Performs a predefined manipulation on all <see cref="Selection"/>s contained by <see cref="TextView"/>.
        /// </summary>
        /// <param name="action">The manipulation to perform.</param>
        /// <remarks>Overlapping selections will be merged after all manipulations have been applied.</remarks>
        void PerformActionOnAllSelections(PredefinedSelectionTransformations action);

        /// <summary>
        /// Performs a custom action on all <see cref="Selection"/>s contained by <see cref="TextView"/>.
        /// </summary>
        /// <param name="action">The action to perform. This will be called once per Selection
        /// and the supplied <see cref="ISelectionTransformer"/> contains methods to adjust an individual Selection.</param>
        /// <remarks>Overlapping selections will be merged after all actions have been performed.</remarks>
        void PerformActionOnAllSelections(Action<ISelectionTransformer> action);

        /// <summary>
        /// Attempts to perform a predefined action on a single <see cref="Selection"/>.
        /// </summary>
        /// <param name="before">The selection on which to perform the manipulation</param>
        /// <param name="action">The manipulation to perform.</param>
        /// <param name="after">Overlapping selections will be merged after the manipulation has been performed.
        /// This parameter reports back the Selection post manipulation and post merge.</param>
        /// <returns><c>true</c> if the manipulation was performed. <c>false</c> otherwise. Typically, <c>false</c> implies that the
        /// before Selection did not exist in <see cref="AllSelections"/>.</returns>
        bool TryPerformActionOnSelection(Selection before, PredefinedSelectionTransformations action, out Selection after);

        /// <summary>
        /// Attempts to perform a custom action on a single <see cref="Selection"/>.
        /// </summary>
        /// <param name="before">The selection on which to perform the action.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="after">Overlapping selections will be merged after the action has been performed.
        /// This parameter reports back the Selection post action and post merge.</param>
        /// <returns><c>true</c> if the action was performed. <c>false</c> otherwise. Typically, <c>false</c> implies that the
        /// beforeSelection did not exist in <see cref="AllSelections"/>.</returns>
        bool TryPerformActionOnSelection(Selection before, Action<ISelectionTransformer> action, out Selection after);

        /// <summary>
        /// Attempts to make the given Selection visible in the view.
        /// </summary>
        /// <param name="selection">The selection to ensure visiblity on.</param>
        /// <param name="options">How the selection span should be made visible.</param>
        /// <returns><c>true</c> if the selection was in <see cref="AllSelections"/> and is now in view. <c>false</c> otherwise.</returns>
        /// <remarks>
        /// This will first ensure that the selection span is visible, erring on the side of showing the <see cref="Selection.ActivePoint"/>.
        /// Then if the <see cref="Selection.InsertionPoint"/> is different than the <see cref="Selection.ActivePoint"/>, the
        /// <see cref="Selection.InsertionPoint"/> will be ensured visible.
        ///
        /// <see cref="EnsureSpanVisibleOptions.ShowStart"/> will be ignored by this method.
        /// </remarks>
        bool TryEnsureVisible(Selection selection, EnsureSpanVisibleOptions options);


        /// <summary>
        /// Adds a box of selections with the given points as its corners.
        /// </summary>
        /// <param name="anchorPoint">The stationary corner of the box.</param>
        /// <param name="activePoint">
        /// The movable corner of the box, also defines which selection
        /// is set to be the primary selection.
        /// </param>
        /// <returns>
        /// A <see cref="Selection"/> that describes the box itself, which can be further manipulated
        /// by the caller as desired.
        /// </returns>
        /// <remarks>
        /// Calling this method will clear all existing selections.
        /// </remarks>
        Selection SetBoxSelection(VirtualSnapshotPoint anchorPoint, VirtualSnapshotPoint activePoint);

        /// <summary>
        /// If <see cref="IsBoxSelection"/> is <c>true</c>, returns an instantiated <see cref="Selection"/>
        /// which the caller can interrogate or manipulate to work with the box itself. Calls to
        /// <see cref="AllSelections"/> or <see cref="GetSelectionsIntersectingSpan(SnapshotSpan)"/> will return individual
        /// per line entries rather than the full box.
        ///
        /// If <see cref="IsBoxSelection"/> is <c>false</c>, this will return null.
        /// </summary>
        Selection BoxSelection { get; }

        /// <summary>
        /// Returns <c>true</c> if <see cref="SetBoxSelection(VirtualSnapshotPoint, VirtualSnapshotPoint)"/> has been
        /// called, and selections are being managed by the box geometry, instead of manually by the user. <see cref="ClearSecondarySelections"/>
        /// and <see cref="BreakBoxSelection"/> will both revert this to <c>false</c>, and several other methods like
        /// <see cref="AddSelection(VirtualSnapshotPoint, VirtualSnapshotPoint)"/> will indirectly also set this back to <c>false</c>.
        /// </summary>
        bool IsBoxSelection { get; }

        /// <summary>
        /// Clears <see cref="BoxSelection"/>, but retains the current state of selections. This is a useful utility when performing gestures like End and Home
        /// where each selection moves, but the result is not necessarily a box.
        /// </summary>
        void BreakBoxSelection();

        #endregion

        #region Get Selections

        /// <summary>
        /// Gets the list of spans within <see cref="CurrentSnapshot"/> that are selected. While two selections cannot
        /// overlap, they may inhabit virtual space, and selections may be adjacent. This will merge those spans and return
        /// the minimum set of spans that could be used to describe the selection. This can be a costly operation
        /// and should only be run when needed.
        /// </summary>
        NormalizedSnapshotSpanCollection SelectedSpans { get; }

        /// <summary>
        /// Gives the set of spans selected. There is exactly one span per selection, but it may be empty.
        /// They will be sorted in the order of appearence in the document.
        /// </summary>
        IReadOnlyList<VirtualSnapshotSpan> VirtualSelectedSpans { get; }

        /// <summary>
        /// Gets the span containing all selections, complete with virtual space.
        /// </summary>
        VirtualSnapshotSpan SelectionExtent { get; }

        #endregion

        #region Environment Integration

        /// <summary>
        /// Whether or not selections are active within <see cref="TextView"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If <see cref="ActivationTracksFocus"/> is <c>true</c>, this property is automatically
        /// updated when the <see cref="ITextView"/> gains and loses aggregate focus.  You can still
        /// override it while <see cref="ActivationTracksFocus"/> is <c>false</c>, but the value will change
        /// whenever focus changes.
        /// </para>
        /// </remarks>
        bool AreSelectionsActive { get; set; }

        /// <summary>
        /// Determines whether <see cref="AreSelectionsActive"/> should track when the <see cref="ITextView"/> gains and
        /// loses aggregate focus.  The default is <c>true</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// While the value of this property is <c>true</c>, the value of <see cref="AreSelectionsActive"/> will track
        /// <see cref="ITextView.HasAggregateFocus"/>.  When the value of this property changes to <c>true</c>,
        /// the value of <see cref="AreSelectionsActive"/> will be immediately updated.
        /// </para>
        /// </remarks>
        bool ActivationTracksFocus { get; set; }

        /// <summary>
        /// Occurs when selections are added/removed/updated. Also when the primary selection is changed, and when
        /// box selection mode is entered/exited. <see cref="MultiSelectionChangedEventArgs"/> contains more
        /// information about the specific change details.
        /// </summary>
        event EventHandler<MultiSelectionChangedEventArgs> MultiSelectionSessionChanged;

        /// <summary>
        /// Temporarily disables <see cref="MultiSelectionSessionChanged"/>, but instead queues up all actions
        /// to be included in the resultant <see cref="MultiSelectionChangedEventArgs"/> once the operation
        /// is completed. Selection merges will also be deferred until the end of batch operations.
        /// </summary>
        /// <returns>An object that should be disposed once the batch operation is complete.</returns>
        /// </param>
        IDisposable BeginBatchOperation();

        #endregion

        /// <summary>
        /// Trys to get the UI properties associated with the given Selection.
        /// </summary>
        /// <param name="selection">The selection of interest.</param>
        /// <param name="properties">Returns out the properties if successful.</param>
        /// <returns><c>true</c> if the supplied selection was found and the properties returned. <c>false</c> otherwise.</returns>
        bool TryGetSelectionPresentationProperties(Selection selection, out AbstractSelectionPresentationProperties properties);
    }
}
