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
        ITextUndoHistory _undoHistory;
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
            if (!(e.EditTag is IUndoEditTag))
            {
                if (this.TextBufferUndoHistory.State != TextUndoHistoryState.Idle)
                {
                    Debug.Fail("We are doing a normal edit in a non-idle undo state. This is explicitly prohibited as it would corrupt the undo stack!  Please fix your code.");
                }
                else
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

                        using (ITextUndoTransaction undoTransaction = ((e.EditTag is IInvisibleEditTag) || ((e.EditTag != null) && (string.Equals(e.EditTag.ToString(), "CascadeRemoteEdit", StringComparison.Ordinal))))
                                                                      ? ((ITextUndoHistory2)_undoHistory).CreateInvisibleTransaction("<invisible>")      // This string does not need to be localized (it should never be seen by the end user).
                                                                      : _undoHistory.CreateTransaction(Strings.TextBufferChanged))
                        {
                            TextBufferChangeUndoPrimitive undoPrimitive = new TextBufferChangeUndoPrimitive(_undoHistory, e.BeforeVersion);
                            undoTransaction.AddUndo(undoPrimitive);

                            undoTransaction.Complete();
                        }
                    }
                }
            }
        }

        void TextBufferChanging(object sender, TextContentChangingEventArgs e)
        {
            // Note that VB explicitly forces undo edits to happen while the history is idle so we need to allow this here
            // by always doing nothing for undo edits). This may be a bug in our code (e.g. not properly cleaning up when
            // an undo transaction is cancelled in mid-flight) but changing that will require coordination with Roslyn.
            if (!(e.EditTag is IUndoEditTag))
            {
                if (this.TextBufferUndoHistory.State != TextUndoHistoryState.Idle)
                {
                    Debug.Fail("We are doing a normal edit in a non-idle undo state. This is explicitly prohibited as it would corrupt the undo stack!  Please fix your code.");
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
            _undoHistory = _undoHistoryRegistry.RegisterHistory(_textBuffer);
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
