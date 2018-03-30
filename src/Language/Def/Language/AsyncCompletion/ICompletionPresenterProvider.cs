using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
{
    /// <summary>
    /// Represents a class that produces instances of <see cref="ICompletionPresenter"/>
    /// </summary>
    /// <remarks>
    /// This is a MEF component and should be exported with [ContentType] and [Name] attributes
    /// and optional [Order] attribute.
    /// An instance of <see cref="ICompletionPresenterProvider"/> is selected
    /// first by matching ContentType with content type of the view's top buffer, and then by Order.
    /// Only one <see cref="ICompletionPresenterProvider"/> is used in a given view.
    /// </remarks>
    /// <example>
    ///     [Export(typeof(ICompletionPresenterProvider))]
    ///     [Name(nameof(MyCompletionPresenterProvider))]
    ///     [ContentType("any")]
    ///     [TextViewRoles(PredefinedTextViewRoles.Editable)]
    ///     [Order(Before = KnownCompletionNames.DefaultCompletionPresenter)]
    ///     public class MyCompletionPresenterProvider : ICompletionPresenterProvider
    /// </example>
    public interface ICompletionPresenterProvider
    {
        /// <summary>
        /// Returns instance of <see cref="ICompletionPresenter"/> that will host completion for given <see cref="ITextView"/>
        /// </summary>
        /// <remarks>It is encouraged to reuse the UI over creating new UI each time this method is called.</remarks>
        /// <param name="textView">Text view that will host the completion. Completion acts on buffers of this view.</param>
        /// <returns>Instance of <see cref="ICompletionPresenter"/></returns>
        ICompletionPresenter GetOrCreate(ITextView textView);

        /// <summary>
        /// Declares size of the jump when user presses PageUp and PageDown keys.
        /// </summary>
        /// <remarks>This value needs to be known before the UI is created, hence it is defined on this class instead of <see cref="ICompletionPresenter"/>.
        /// Note that <see cref="ICompletionPresenter"/> is just a view that doesn't participate in keyboard scrolling</remarks>
        int ResultsPerPage { get; }
    }
}
