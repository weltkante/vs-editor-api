using System;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Tracks whether the commit occured, and provides instructions for behavior after committing.
    /// </summary>
    public struct CommitResult : IEquatable<CommitResult>
    {
        /// <summary>
        /// Marks this commit as handled. No other <see cref="IAsyncCompletionCommitManager"/> will be asked to commit.
        /// </summary>
        public static CommitResult Handled = new CommitResult(isHandled: true, behavior: CommitBehavior.None);

        /// <summary>
        /// Marks this commit as unhandled. Another <see cref="IAsyncCompletionCommitManager"/> will be asked to commit.
        /// </summary>
        public static CommitResult Unhandled = new CommitResult(isHandled: false, behavior: CommitBehavior.None);

        /// <summary>
        /// Whether the commit occured.
        /// If true, no other <see cref="IAsyncCompletionCommitManager"/> will be asked to commit.
        /// If false, another <see cref="IAsyncCompletionCommitManager"/> will be asked to commit.
        /// </summary>
        public bool IsHandled { get; }

        /// <summary>
        /// Desired behavior after committing, respected even when <see cref="IsHandled"/> is unset.
        /// </summary>
        public CommitBehavior Behavior { get; }

        /// <summary>
        /// Creates a <see cref="CommitResult"/> with specified properties.
        /// </summary>
        /// <param name="isHandled">Whether the commit occured</param>
        /// <param name="behavior">Desired behavior after committing</param>
        public CommitResult(bool isHandled, CommitBehavior behavior)
        {
            IsHandled = isHandled;
            Behavior = behavior;
        }

        bool IEquatable<CommitResult>.Equals(CommitResult other) => IsHandled.Equals(other.IsHandled) && Behavior.Equals(other.Behavior);
    }
}
