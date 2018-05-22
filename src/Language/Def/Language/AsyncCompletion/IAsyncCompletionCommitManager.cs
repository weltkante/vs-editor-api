using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Represents a class that provides means to commit <see cref="CompletionItem"/>s and other information
    /// relevant to the completion feature at a specific <see cref="SnapshotPoint"/>.
    /// </summary>
    /// <remarks>
    /// Instances of this class should be created by <see cref="IAsyncCompletionCommitManagerProvider"/>, which is a MEF part.
    /// </remarks>
    public interface IAsyncCompletionCommitManager
    {
        /// <summary>
        /// Returns characters that may commit completion.
        /// When completion is active and a text edit matches one of these characters,
        /// <see cref="ShouldCommitCompletion(char, SnapshotPoint, CancellationToken)"/> is called to verify that the character
        /// is indeed a commit character at a given location.
        /// Called on UI thread.
        /// </summary>
        IEnumerable<char> PotentialCommitCharacters { get; }

        /// <summary>
        /// Returns whether this character is a commit character in a given location.
        /// If every character returned by <see cref="PotentialCommitCharacters"/> should always commit the active completion session, return true.
        /// Called on UI thread.
        /// </summary>
        /// <param name="typeChar">Character typed by the user</param>
        /// <param name="location">Location in the snapshot of the view's topmost buffer. The character is not inserted into this snapshot.</param>
        /// <param name="token">Token used to cancel this operation</param>
        /// <returns>True if this character should commit the active session.</returns>
        bool ShouldCommitCompletion(char typeChar, SnapshotPoint location, CancellationToken token);

        /// <summary>
        /// Requests commit of specified <see cref="CompletionItem"/>.
        /// Called on UI thread.
        /// </summary>
        /// <param name="view">View that hosts completion and relevant buffers</param>
        /// <param name="buffer">Reference to the buffer with matching content type to perform text edits etc.</param>
        /// <param name="item">Which completion item is to be applied</param>
        /// <param name="applicableSpan">Span augmented by completion, on the view's data buffer: <see cref="ITextView.TextBuffer"/></param>
        /// <param name="typedChar">Text change associated with this commit</param>
        /// <param name="token">Token used to cancel this operation</param>
        /// <returns>Instruction for the editor how to proceed after invoking this method</returns>
        CommitResult TryCommit(ITextView view, ITextBuffer buffer, CompletionItem item, ITrackingSpan applicableSpan, char typedChar, CancellationToken token);
    }
}
