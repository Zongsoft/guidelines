using System;
using System.Linq;
using System.Composition;
using System.Threading.Tasks;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Fixes;

/// <summary>在语句组边界插入空行，不重排注释或格式化其他代码。</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StatementSpacingCodeFixProvider)), Shared]
public sealed class StatementSpacingCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ZS2003");
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

		foreach(var diagnostic in context.Diagnostics)
		{
			var current = root?.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<StatementSyntax>();

			if(current == null || current.SpanStart != diagnostic.Location.SourceSpan.Start || current.ContainsDiagnostics)
				continue;

			var container = current.Parent is GlobalStatementSyntax global ? global.Parent : current.Parent;
			var previous = container == null ? null : StatementSpacing.GetMissingBlankLines(container, text)
				.FirstOrDefault(boundary => boundary.Current == current).Previous;

			if(previous == null)
				continue;

			var first = text.Lines.GetLineFromPosition(previous.Span.End);
			var last = text.Lines.GetLineFromPosition(current.SpanStart);

			var newline = SourceTextUtility.GetNewLine(text, previous.Span.End);
			var boundary = first.EndIncludingLineBreak;

			//跨行尾注释属于上一条语句，空行必须插在注释之外。
			foreach(var trivia in root.DescendantTrivia(TextSpan.FromBounds(previous.Span.End, current.SpanStart)))
			{
				if(trivia.SpanStart < boundary && trivia.Span.End > boundary &&
					(trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)))
					boundary = text.Lines.GetLineFromPosition(trivia.Span.End).EndIncludingLineBreak;
			}

			TextChange change;

			if(first.LineNumber < last.LineNumber && boundary <= last.Start)
				change = new TextChange(new TextSpan(boundary, 0), newline);
			else
			{
				var indent = SourceTextUtility.GetIndentation(text, previous.SpanStart);
				var start = current.SpanStart;

				while(start > previous.Span.End && (text[start - 1] == ' ' || text[start - 1] == '\t'))
					start--;

				change = new TextChange(TextSpan.FromBounds(start, current.SpanStart), newline + newline + indent);
			}

			context.RegisterCodeFix(CodeAction.Create(Properties.Resources.InsertBlankLine,
				cancellation => Task.FromResult(context.Document.WithText(text.WithChanges(change))), "ZS2003.InsertBlankLine"), diagnostic);
		}
	}
}
