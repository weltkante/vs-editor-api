using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    public struct CommitResult
    {
        /// <summary>
        /// Marks this commit as handled.
        /// If true, no other <see cref="IAsyncCompletionCommitManager"/> will be asked to commit.
        /// If false, another <see cref="IAsyncCompletionCommitManager"/> will be asked to commit.
        /// </summary>
        public bool Handled { get; }

        /// <summary>
        /// Desired behavior after committing.
        /// This is respected even when <see cref="Handled"/> is unset.
        /// </summary>
        public CommitBehavior Behavior { get; }

        public CommitResult(bool handled, CommitBehavior behavior = CommitBehavior.None)
        {
            Handled = handled;
            Behavior = behavior;
        }
    }
}
