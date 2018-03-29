using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities;
using Microsoft.VisualStudio.Utilities;
using CommonImplementation = Microsoft.VisualStudio.Language.Intellisense.Implementation;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Implementation
{
    /// <summary>
    /// Reacts to the down arrow command and attempts to scroll the completion list.
    /// </summary>
    [Name(KnownCompletionNames.CompletionCommandHandlers)]
    [ContentType("text")]
    [Export(typeof(ICommandHandler))]
    internal sealed class CompletionCommandHandlers :
        ICommandHandler<DownKeyCommandArgs>,
        ICommandHandler<PageDownKeyCommandArgs>,
        ICommandHandler<PageUpKeyCommandArgs>,
        ICommandHandler<UpKeyCommandArgs>,
        IChainedCommandHandler<BackspaceKeyCommandArgs>,
        ICommandHandler<EscapeKeyCommandArgs>,
        ICommandHandler<InvokeCompletionListCommandArgs>,
        ICommandHandler<CommitUniqueCompletionListItemCommandArgs>,
        ICommandHandler<InsertSnippetCommandArgs>,
        ICommandHandler<ToggleCompletionModeCommandArgs>,
        IChainedCommandHandler<DeleteKeyCommandArgs>,
        ICommandHandler<WordDeleteToEndCommandArgs>,
        ICommandHandler<WordDeleteToStartCommandArgs>,
        ICommandHandler<ReturnKeyCommandArgs>,
        ICommandHandler<RedoCommandArgs>,
        ICommandHandler<TabKeyCommandArgs>,
        IChainedCommandHandler<TypeCharCommandArgs>,
        ICommandHandler<UndoCommandArgs>
    {
        [Import]
        IAsyncCompletionBroker Broker;

        [Import]
        IExperimentationServiceInternal ExperimentationService;

        [Import]
        ITextUndoHistoryRegistry UndoHistoryRegistry;

        [Import]
        IEditorOperationsFactoryService EditorOperationsFactoryService;

        string INamed.DisplayName => CommonImplementation.Strings.CompletionCommandHandlerName;

        /// <summary>
        /// Helper method that returns command state for commands
        /// that are always available - unless the completion feature is available.
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        private CommandState GetAvailability(IContentType contentType)
        {
            return ModernCompletionFeature.GetFeatureState(ExperimentationService)
                && Broker.IsCompletionSupported(contentType)
                ? CommandState.Available
                : CommandState.Unspecified;
        }

        private CommandState GetAvailabilityOfSuggestionMode(IContentType contentType, ITextView textView)
        {
            return new CommandState(
                isAvailable: ModernCompletionFeature.GetFeatureState(ExperimentationService)
                             && Broker.IsCompletionSupported(contentType),
                isChecked: CompletionUtilities.GetSuggestionModeOption(textView));
            // TODO: Once we get a special TextViewRole, detect if we are in debugger and ready the option for suggestion mode in debugger window
        }

        /// <summary>
        /// Helper method that returns command state for commands
        /// that are available when completion is active.
        /// </summary>
        /// <remarks>
        /// Completion might be active only if the feature is available, so we're skipping other checks.
        /// </remarks>
        private CommandState GetAvailabilityIfCompletionIsUp(ITextView textView)
        {
            return Broker.IsCompletionActive(textView)
                ? CommandState.Available
                : CommandState.Unspecified;
        }

        CommandState IChainedCommandHandler<BackspaceKeyCommandArgs>.GetCommandState(BackspaceKeyCommandArgs args, Func<CommandState> nextCommandHandler)
           => GetAvailabilityIfCompletionIsUp(args.TextView);

        void IChainedCommandHandler<BackspaceKeyCommandArgs>.ExecuteCommand(BackspaceKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            // Execute other commands in the chain to see the change in the buffer.
            nextCommandHandler();

            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                var trigger = new CompletionTrigger(CompletionTriggerReason.Deletion);
                var location = args.TextView.Caret.Position.BufferPosition;
                session.OpenOrUpdate(trigger, location, executionContext.OperationContext.UserCancellationToken);
            }
        }

        CommandState ICommandHandler<EscapeKeyCommandArgs>.GetCommandState(EscapeKeyCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<EscapeKeyCommandArgs>.ExecuteCommand(EscapeKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                session.Dismiss();
                return true;
            }
            return false;
        }

        CommandState ICommandHandler<InvokeCompletionListCommandArgs>.GetCommandState(InvokeCompletionListCommandArgs args)
            => GetAvailability(args.SubjectBuffer.ContentType);

        bool ICommandHandler<InvokeCompletionListCommandArgs>.ExecuteCommand(InvokeCompletionListCommandArgs args, CommandExecutionContext executionContext)
        {
            // If the caret is buried in virtual space, we should realize this virtual space before triggering the session.
            if (args.TextView.Caret.InVirtualSpace)
            {
                IEditorOperations editorOperations = EditorOperationsFactoryService.GetEditorOperations(args.TextView);
                // We can realize virtual space by inserting nothing through the editor operations.
                editorOperations?.InsertText("");
            }

            var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke);
            var location = args.TextView.Caret.Position.BufferPosition;
            var session = Broker.TriggerCompletion(args.TextView, location, default(char));
            if (session != null)
            {
                session.OpenOrUpdate(trigger, location, executionContext.OperationContext.UserCancellationToken);
                return true;
            }
            return false;
        }

        CommandState ICommandHandler<CommitUniqueCompletionListItemCommandArgs>.GetCommandState(CommitUniqueCompletionListItemCommandArgs args)
            => GetAvailability(args.SubjectBuffer.ContentType);

        bool ICommandHandler<CommitUniqueCompletionListItemCommandArgs>.ExecuteCommand(CommitUniqueCompletionListItemCommandArgs args, CommandExecutionContext executionContext)
        {
            // If the caret is buried in virtual space, we should realize this virtual space before triggering the session.
            if (args.TextView.Caret.InVirtualSpace)
            {
                IEditorOperations editorOperations = EditorOperationsFactoryService.GetEditorOperations(args.TextView);
                // We can realize virtual space by inserting nothing through the editor operations.
                editorOperations?.InsertText("");
            }

            var trigger = new CompletionTrigger(CompletionTriggerReason.InvokeAndCommitIfUnique);
            var location = args.TextView.Caret.Position.BufferPosition;
            var session = Broker.TriggerCompletion(args.TextView, location, default(char));
            if (session != null)
            {
                var sessionInternal = session as AsyncCompletionSession;
                sessionInternal?.InvokeAndCommitIfUnique(trigger, location, executionContext.OperationContext.UserCancellationToken);
                return true;
            }
            return false;
        }

        CommandState ICommandHandler<InsertSnippetCommandArgs>.GetCommandState(InsertSnippetCommandArgs args)
            => GetAvailability(args.SubjectBuffer.ContentType);

        bool ICommandHandler<InsertSnippetCommandArgs>.ExecuteCommand(InsertSnippetCommandArgs args, CommandExecutionContext executionContext)
        {
            // Room for future implementation.
            return false;
        }

        CommandState ICommandHandler<ToggleCompletionModeCommandArgs>.GetCommandState(ToggleCompletionModeCommandArgs args)
            => GetAvailabilityOfSuggestionMode(args.SubjectBuffer.ContentType, args.TextView);

        bool ICommandHandler<ToggleCompletionModeCommandArgs>.ExecuteCommand(ToggleCompletionModeCommandArgs args, CommandExecutionContext executionContext)
        {
            var toggledValue = !CompletionUtilities.GetSuggestionModeOption(args.TextView);
            CompletionUtilities.SetSuggestionModeOption(args.TextView, toggledValue);

            var session = Broker.GetSession(args.TextView) as AsyncCompletionSession; // we are accessing an internal method
            if (session != null)
            {
                session.SetSuggestionMode(toggledValue);
                return true;
            }
            return false;
        }

        CommandState IChainedCommandHandler<DeleteKeyCommandArgs>.GetCommandState(DeleteKeyCommandArgs args, Func<CommandState> nextCommandHandler)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        void IChainedCommandHandler<DeleteKeyCommandArgs>.ExecuteCommand(DeleteKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            // Execute other commands in the chain to see the change in the buffer.
            nextCommandHandler();

            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                var trigger = new CompletionTrigger(CompletionTriggerReason.Deletion);
                var location = args.TextView.Caret.Position.BufferPosition;
                session.OpenOrUpdate(trigger, location, executionContext.OperationContext.UserCancellationToken);
            }
        }

        CommandState ICommandHandler<WordDeleteToEndCommandArgs>.GetCommandState(WordDeleteToEndCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<WordDeleteToEndCommandArgs>.ExecuteCommand(WordDeleteToEndCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                session.Dismiss();
                return false; // return false so that the editor can handle this event
            }
            return false;
        }

        CommandState ICommandHandler<WordDeleteToStartCommandArgs>.GetCommandState(WordDeleteToStartCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<WordDeleteToStartCommandArgs>.ExecuteCommand(WordDeleteToStartCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                session.Dismiss();
                return false; // return false so that the editor can handle this event
            }
            return false;
        }

        CommandState ICommandHandler<RedoCommandArgs>.GetCommandState(RedoCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<RedoCommandArgs>.ExecuteCommand(RedoCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                session.Dismiss();
                return false; // return false so that the editor can handle this event
            }
            return false;
        }

        CommandState ICommandHandler<ReturnKeyCommandArgs>.GetCommandState(ReturnKeyCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<ReturnKeyCommandArgs>.ExecuteCommand(ReturnKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                var commitBehavior = session.Commit('\n', executionContext.OperationContext.UserCancellationToken);
                session.Dismiss();

                // Mark this command as handled (return true), unless extender set the RaiseFurtherCommandHandlers flag.
                if ((commitBehavior & CommitBehavior.RaiseFurtherCommandHandlers) == 0)
                    return true;
            }
            return false;
        }

        CommandState ICommandHandler<TabKeyCommandArgs>.GetCommandState(TabKeyCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<TabKeyCommandArgs>.ExecuteCommand(TabKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                var commitBehavior = session.Commit('\t', executionContext.OperationContext.UserCancellationToken);
                session.Dismiss();

                // Mark this command as handled (return true), unless extender set the RaiseFurtherCommandHandlers flag.
                if ((commitBehavior & CommitBehavior.RaiseFurtherCommandHandlers) == 0)
                    return true;
            }
            return false;
        }

        CommandState IChainedCommandHandler<TypeCharCommandArgs>.GetCommandState(TypeCharCommandArgs args, Func<CommandState> nextCommandHandler)
            => GetAvailability(args.SubjectBuffer.ContentType);

        void IChainedCommandHandler<TypeCharCommandArgs>.ExecuteCommand(TypeCharCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            var view = args.TextView;
            var location = view.Caret.Position.BufferPosition;
            var initialTextSnapshot = args.SubjectBuffer.CurrentSnapshot;

            // Note regarding undo: When completion and brace completion happen together, completion should be first on the undo stack.
            // Effectively, we want to first undo the completion, leaving brace completion intact. Second undo should undo brace completion.
            // To achieve this, we create a transaction in which we commit and reapply brace completion (via nextCommandHandler).
            // Please read "Note regarding undo" comments in this method that explain the implementation choices.
            // Hopefully an upcoming upgrade of the undo mechanism will allow us to undo out of order and vastly simplify this method.

            // Note regarding undo: In a corner case of typing closing brace over existing closing brace,
            // Roslyn brace completion does not perform an edit. It moves the caret outside of session's applicable span,
            // which dismisses the session. Put the session in a state where it will not dismiss when caret leaves the applicable span.
            var sessionToCommit = Broker.GetSession(args.TextView);
            if (sessionToCommit != null)
            {
                ((AsyncCompletionSession)sessionToCommit).IgnoreCaretMovement(ignore: true);
            }

            // Execute other commands in the chain to see the change in the buffer. This includes brace completion.
            // Note regarding undo: This will be undone second
            nextCommandHandler();

            // if on different version than initialTextSnapshot, we will NOT rollback and we will NOT replay the nextCommandHandler
            // DP to figure out why ShouldCommit returns false or Commit doesn't do anything
            var braceCompletionSpecialHandling = args.SubjectBuffer.CurrentSnapshot.Version == initialTextSnapshot.Version;

            // Pass location from before calling nextCommandHandler
            // so that extenders get the same view of the buffer in both ShouldCommit and Commit
            if (sessionToCommit?.ShouldCommit(args.TypedChar, location, executionContext.OperationContext.UserCancellationToken) == true)
            {
                // Buffer has changed, update the snapshot
                location = view.Caret.Position.BufferPosition;

                // Note regarding undo: this transaction will be undone first
                using (var undoTransaction = new CaretPreservingEditTransaction("Completion", view, UndoHistoryRegistry, EditorOperationsFactoryService))
                {
                    if (!braceCompletionSpecialHandling)
                        UndoUtilities.RollbackToBeforeTypeChar(initialTextSnapshot, args.SubjectBuffer);
                    // Now the buffer doesn't have the commit character nor the matching brace, if any

                    var commitBehavior = sessionToCommit.Commit(args.TypedChar, executionContext.OperationContext.UserCancellationToken);

                    if (!braceCompletionSpecialHandling && (commitBehavior & CommitBehavior.SuppressFurtherCommandHandlers) == 0)
                        nextCommandHandler(); // Replay the key, so that we get brace completion.

                    // Complete the transaction before stopping it.
                    undoTransaction.Complete();
                }
            }

            // Restore the default state where session dismisses when caret is outside of the applicable span.
            if (sessionToCommit != null)
            {
               ((AsyncCompletionSession)sessionToCommit).IgnoreCaretMovement(ignore: false);
            }

            // Buffer might have changed. Update it for when we try to trigger new session.
            location = view.Caret.Position.BufferPosition;

            var trigger = new CompletionTrigger(CompletionTriggerReason.Insertion, args.TypedChar);
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                session.OpenOrUpdate(trigger, location, executionContext.OperationContext.UserCancellationToken);
            }
            else
            {
                var newSession = Broker.TriggerCompletion(args.TextView, location, args.TypedChar);
                if (newSession != null)
                {
                    newSession?.OpenOrUpdate(trigger, location, executionContext.OperationContext.UserCancellationToken);
                }
            }
        }

        CommandState ICommandHandler<UndoCommandArgs>.GetCommandState(UndoCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<UndoCommandArgs>.ExecuteCommand(UndoCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView);
            if (session != null)
            {
                session.Dismiss();
                return false; // return false so that the editor can handle this event
            }
            return false;
        }

        CommandState ICommandHandler<DownKeyCommandArgs>.GetCommandState(DownKeyCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<DownKeyCommandArgs>.ExecuteCommand(DownKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView) as AsyncCompletionSession; // we are accessing an internal method
            if (session != null)
            {
                session.SelectDown();
                return true;
            }
            return false;
        }

        CommandState ICommandHandler<PageDownKeyCommandArgs>.GetCommandState(PageDownKeyCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<PageDownKeyCommandArgs>.ExecuteCommand(PageDownKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView) as AsyncCompletionSession; // we are accessing an internal method
            if (session != null)
            {
                session.SelectPageDown();
                return true;
            }
            return false;
        }

        CommandState ICommandHandler<PageUpKeyCommandArgs>.GetCommandState(PageUpKeyCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<PageUpKeyCommandArgs>.ExecuteCommand(PageUpKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView) as AsyncCompletionSession; // we are accessing an internal method
            if (session != null)
            {
                session.SelectPageUp();
                return true;
            }
            return false;
        }

        CommandState ICommandHandler<UpKeyCommandArgs>.GetCommandState(UpKeyCommandArgs args)
            => GetAvailabilityIfCompletionIsUp(args.TextView);

        bool ICommandHandler<UpKeyCommandArgs>.ExecuteCommand(UpKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            var session = Broker.GetSession(args.TextView) as AsyncCompletionSession; // we are accessing an internal method
            if (session != null)
            {
                session.SelectUp();
                System.Diagnostics.Debug.WriteLine("Completions's UpKey command handler returns true (handled)");
                return true;
            }
            System.Diagnostics.Debug.WriteLine("Completions's UpKey command handler returns false (unhandled)");
            return false;
        }
    }
}
