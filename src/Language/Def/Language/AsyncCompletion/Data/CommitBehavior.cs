using System;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data
{
    /// <summary>
    /// Instructs the editor how to behave after committing a <see cref="CompletionItem"/>.
    /// </summary>
    [Flags]
#pragma warning disable CA1714 // Flags enums should have plural names
    public enum CommitBehavior
#pragma warning restore CA1714 // Flags enums should have plural names
    {
        /// <summary>
        /// Use the default behavior,
        /// that is, to propagate TypeChar command, but surpress ReturnKey and TabKey commands.
        /// </summary>
        None = 0,

        /// <summary>
        /// Surpresses further invocation of the TypeChar command handlers.
        /// By default, editor invokes these command handlers to enable features such as brace completion.
        /// </summary>
        SuppressFurtherTypeCharCommandHandlers = 1,

        /// <summary>
        /// Raises further invocation of the ReturnKey and Tab command handlers.
        /// By default, editor doesn't invoke ReturnKey and Tab command handlers after committing completion session.
        /// </summary>
        RaiseFurtherReturnKeyAndTabKeyCommandHandlers = 2
    }
}
