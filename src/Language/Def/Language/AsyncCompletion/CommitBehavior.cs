using System;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Instructs the editor how to behave after invoking the custom commit method.
    /// </summary>
    [Flags]
    public enum CommitBehavior
    {
        /// <summary>
        /// Use the default behavior
        /// </summary>
        None = 0,

        /// <summary>
        /// Surpresses further invocation of the TypeChar command handlers.
        /// By default, editor invoke these command handlers to enable features such as brace completion.
        /// </summary>
        SuppressFurtherCommandHandlers = 1,

        /// <summary>
        /// Raises further invocation of the ReturnKey and Tab command handlers.
        /// By default, editor doesn't invoke ReturnKey and Tab command handlers after committing completion session.
        /// </summary>
        RaiseFurtherCommandHandlers = 2
    }
}
