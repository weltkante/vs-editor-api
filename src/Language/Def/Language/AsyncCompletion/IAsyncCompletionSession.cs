using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Represents a class that tracks completion within a single <see cref="ITextView"/>.
    /// Constructed and managed by an instance of <see cref="IAsyncCompletionBroker"/>
    /// </summary>
    public interface IAsyncCompletionSession
    {
        /// <summary>
        /// Request completion to be opened or updated in a given location, completion items filtered and sorted, and the UI updated.
        /// </summary>
        /// <param name="trigger">What caused completion</param>
        /// <param name="triggerLocation">Location of the trigger on the subject buffer</param>
        /// <param name="commandToken">Token used to cancel this operation and other computations.</param>
        void OpenOrUpdate(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token);

        /// <summary>
        /// Stops the session and hides associated UI.
        /// </summary>
        /// <returns></returns>
        void Dismiss();

        /// <summary>
        /// Returns whether given text edit should result in committing this session.
        /// Since this method is on a typing hot path, it uses <see cref="IAsyncCompletionSource.GetPotentialCommitCharacters"/>
        /// to quickly return if <paramref name="typedChar"/> is not a potential commit character.
        /// Else, we map <paramref name="triggerLocation"/> to subject buffers and query <see cref="IAsyncCompletionSource.ShouldCommitCompletion(char, SnapshotPoint)"/>.
        /// Must be called on UI thread.
        /// <param name="edit">The text edit which caused this action. May be null.</param>
        /// <param name="triggerLocation">Location on the view's top buffer</param>
        /// <returns></returns>
        bool ShouldCommit(char typedChar, SnapshotPoint triggerLocation, CancellationToken token);

        /// <summary>
        /// Commits the currently selected <see cref="CompletionItem"/>.
        /// Must be called on UI thread.
        /// </summary>
        /// <param name="token">Token used to cancel this operation</param>
        /// <param name="char">The text edit which caused this action. May be default(char).</param>
        /// <returns>Instruction for the editor how to proceed after invoking this method</returns>
        CommitBehavior Commit(char typedChar, CancellationToken token);

        /// <summary>
        /// Commits the single <see cref="CompletionItem"/> or opens the completion UI.
        /// Must be called on UI thread.
        /// </summary>
        /// <param name="token">Token used to cancel this operation</param>
        /// <returns>Whether the unique item was committed.</returns>
        bool CommitIfUnique(CancellationToken token);
        
        /// <summary>
        /// Returns the <see cref="ITextView"/> this session is active on.
        /// </summary>
        ITextView TextView { get; }

        bool IsDismissed { get; }

        /// <summary>
        /// Fired when completion item is committed
        /// </summary>
        event EventHandler<CompletionItemEventArgs> ItemCommitted;

        /// <summary>
        /// Fired when completion session is dismissed
        /// </summary>
        event EventHandler Dismissed;

        event EventHandler<CompletionItemsWithHighlightEventArgs> ItemsUpdated;

        /// <summary>
        /// Gets items visible in the UI
        /// </summary>
        ImmutableArray<CompletionItem> GetItems(CancellationToken token);

        /// <summary>
        /// Gets currently selected item
        /// </summary>
        CompletionItem GetSelectedItem(CancellationToken token);
    }
}
