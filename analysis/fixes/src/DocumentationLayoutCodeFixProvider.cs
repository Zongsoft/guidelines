using System.Linq;
using System.Composition;
using System.Threading.Tasks;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Fixes;

/// <summary>合并单行 XML 文档元素，保留内容及周围代码。</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationLayoutCodeFixProvider)), Shared]
public sealed class DocumentationLayoutCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ZS3003");
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

		foreach(var diagnostic in context.Diagnostics)
		{
			var element = root?.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true).FirstAncestorOrSelf<XmlElementSyntax>();
			if(element == null || element.StartTag.Span != diagnostic.Location.SourceSpan ||
				element.FirstAncestorOrSelf<DocumentationCommentTriviaSyntax>() is not { ContainsDiagnostics: false } ||
				!SourceTextUtility.IsSingleLine(text, element.StartTag.Span) || !SourceTextUtility.IsSingleLine(text, element.EndTag.Span) ||
				SourceTextUtility.IsSingleLine(text, element.Span) || PreservesWhitespace(element) ||
				!DocumentationLayout.TryGetContentSpan(element, text, out var content))
				continue;

			var replacement = text.ToString(element.StartTag.Span) + text.ToString(content) + text.ToString(element.EndTag.Span);
			var change = new TextChange(element.Span, replacement);

			context.RegisterCodeFix(CodeAction.Create(Properties.Resources.JoinXmlDocumentation,
				cancellation => Task.FromResult(context.Document.WithText(text.WithChanges(change))), "ZS3003.JoinXmlDocumentation"), diagnostic);
		}
	}

	private static bool PreservesWhitespace(XmlElementSyntax element) => element.AncestorsAndSelf().OfType<XmlElementSyntax>()
		.SelectMany(node => node.StartTag.Attributes).OfType<XmlTextAttributeSyntax>()
		.Any(attribute => attribute.Name.ToString() == "xml:space" && string.Concat(attribute.TextTokens.Select(token => token.ValueText)) == "preserve");
}
