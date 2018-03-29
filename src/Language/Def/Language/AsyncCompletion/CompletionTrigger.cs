using System;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// What triggered the completion, but not where it happened.
    /// The reason we don't expose location is that for each extension,
    /// we map the point to a buffer with matching content type.
    /// </summary>
    [DebuggerDisplay("{Reason} {Character}")]
    public struct CompletionTrigger
    {
        /// <summary>
        /// The reason that completion was started.
        /// </summary>
        public CompletionTriggerReason Reason { get; }

        /// <summary>
        /// The text edit associated with the triggering action.
        /// </summary>
        public char Character { get; }

        /// <summary>
        /// Creates a <see cref="CompletionTrigger"/> associated with a text edit
        /// </summary>
        /// <param name="reason">The kind of action that triggered completion to start</param>
        /// <param name="character">Character that triggered completion</param>
        public CompletionTrigger(CompletionTriggerReason reason, char character)
        {
            this.Reason = reason;
            this.Character = character;
        }

        /// <summary>
        /// Creates a <see cref="CompletionTrigger"/> not associated with a text edit
        /// </summary>
        /// <param name="reason">The kind of action that triggered completion to start</param>
        public CompletionTrigger(CompletionTriggerReason reason) : this(reason, default(char))
        { }
    }
}
