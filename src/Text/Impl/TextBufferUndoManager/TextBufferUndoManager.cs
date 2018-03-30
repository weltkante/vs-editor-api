//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//
// This file contain implementations details that are subject to change without notice.
// Use at your own risk.
//
namespace Microsoft.VisualStudio.Text.BufferUndoManager.Implementation
{
    using System;
    using System.Diagnostics;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Operations;

    internal sealed class TextBufferUndoManager : ITextBufferUndoManager, IDisposable
    {
        #region Private Members

        ITextBuffer _textBuffer;
        ITextUndoHistoryRegistry _undoHistoryRegistry;
        ITextUndoHistory2 _undoHistory;
        #endregion

        public TextBufferUndoManager(ITextBuffer textBuffer, ITextUndoHistoryRegistry undoHistoryRegistry)
        {
            if (textBuffer == null)
            {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            if (undoHistoryRegistry == null)
            {
                throw new ArgumentNullException(nameof(undoHistoryRegistry));
            }

            _textBuffer = textBuffer;

            _undoHistoryRegistry = undoHistoryRegistry;

            // Register the undo history
            this.EnsureTextBufferUndoHistory();

            // Listen for the buffer changed events so that we can make them undo/redo-able
            _textBuffer.Changed += TextBufferChanged;
            _textBuffer.Changing += TextBufferChanging;
        }

        #region Private Methods

        private void TextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (e.EditTag is IUndoEditTag)
            {
                Debug.Assert((_undoHistory.State != TextUndoHistoryState.Idle) || (e.EditTag is IBypassUndoEditTag),
                             "Attemping an undo while not inside an undo transaction");
            }
            else if (_undoHistory.State == TextUndoHistoryState.Idle)
            {
                // With projection, we sometimes get Changed events with no changes, or for "" -> "".
                // We don't want to create undo actions for these.
                bool nonNullChange = false;
                foreach (ITextChange c in e.BeforeVersion.Changes)
                {
                    if (c.OldLength != 0 || c.NewLength != 0)
                    {
                        nonNullChange = true;
                        break;
                    }
                }

                if (nonNullChange)
                {

                    // TODO remove this
                    // Hack to allow Cascade's local undo to light up if using v15.7 but behave using the old -- non-local -- undo before if running on 15.6.
                    // Cascade should really be marking its edits with IInvisibleEditTag (and will once it can take a hard requirement of VS 15.7).

                    using (ITextUndoTransaction undoTransaction = ((e.EditTag is IInvisibleEditTag) || ((e.EditTag != null) && (e.EditTag.ToString() == "CascadeRemoteEdit")))
                                                                  ? _undoHistory.CreateInvisibleTransaction("<invisible>")      // This string does not need to be localized (it should never be seen by the end user).
                                                                  : _undoHistory.CreateTransaction(Strings.TextBufferChanged))
                    {
                        TextBufferChangeUndoPrimitive undoPrimitive = new TextBufferChangeUndoPrimitive(_undoHistory, e.BeforeVersion);
                        undoTransaction.AddUndo(undoPrimitive);

                        undoTransaction.Complete();
                    }
                }
            }
            else
            {
                Debug.Fail("Attempting a normal edit while inside an undo transaction");
            }
        }

        void TextBufferChanging(object sender, TextContentChangingEventArgs e)
        {
            if (!(e.EditTag is IBypassUndoEditTag))
            {
                this.EnsureTextBufferUndoHistory();

                if ((_undoHistory.State == TextUndoHistoryState.Idle) == (e.EditTag is IUndoEditTag))
                {
                    Debug.Fail((e.EditTag is IUndoEditTag)
                               ? "Attemping an undo while not inside an undo transaction"
                               : "Attempting a normal edit while inside an undo transaction");

                    e.Cancel();
                }
            }
        }

        #endregion

        #region ITextBufferUndoManager Members

        public ITextBuffer TextBuffer
        {
            get { return _textBuffer; }
        }

        public ITextUndoHistory TextBufferUndoHistory
        {
            // Note, right now, there is no way for us to know if an ITextUndoHistory
            // has been unregistered (ie it can be unregistered by a third party)
            // An issue has been logged with the Undo team, but in the mean time, to ensure that
            // we are robust, always register the undo history.
            get
            {
                this.EnsureTextBufferUndoHistory();
                return _undoHistory;
            }
        }

        public void UnregisterUndoHistory()
        {
            // Unregister the undo history
            if (_undoHistory != null)
            {
                _undoHistoryRegistry.RemoveHistory(_undoHistory);
                _undoHistory = null;
            }
        }

        #endregion

        private void EnsureTextBufferUndoHistory()
        {
            // Note, right now, there is no way for us to know if an ITextUndoHistory
            // has been unregistered (ie it can be unregistered by a third party)
            // An issue has been logged with the Undo team, but in the mean time, to ensure that
            // we are robust, always register the undo history.
            _undoHistory = (ITextUndoHistory2)(_undoHistoryRegistry.RegisterHistory(_textBuffer));
        }

        #region IDisposable Members

        public void Dispose()
        {
            _textBuffer.Changed -= TextBufferChanged;
            _textBuffer.Changing -= TextBufferChanging;

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
