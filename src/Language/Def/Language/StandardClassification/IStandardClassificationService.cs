namespace Microsoft.VisualStudio.Language.StandardClassification
{
    using Microsoft.VisualStudio.Text.Classification;

    public interface IStandardClassificationService
    {
        IClassificationType NaturalLanguage { get; }
        IClassificationType FormalLanguage { get; }
        IClassificationType Comment { get; }
        IClassificationType Identifier { get; }
        IClassificationType Keyword { get; }
        IClassificationType WhiteSpace { get; }
        IClassificationType Operator { get; }
        IClassificationType Literal { get; }
        IClassificationType NumberLiteral { get; }
        IClassificationType StringLiteral { get; }
        IClassificationType CharacterLiteral { get; }
        IClassificationType Other { get; }
        IClassificationType ExcludedCode { get; }
        IClassificationType PreprocessorKeyword { get; }
        IClassificationType SymbolDefinition { get; }
        IClassificationType SymbolReference { get; }
    }
}
