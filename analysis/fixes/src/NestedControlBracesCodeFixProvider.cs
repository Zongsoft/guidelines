using System;
using System.Linq;
using System.Threading;
using System.Composition;
using System.Threading.Tasks;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;

namespace Zongsoft.CodeAnalysis.Fixes;

/// <summary>包裹原始嵌入语句，不改变嵌套分支的关联关系。</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NestedControlBracesCodeFixProvider)), Shared]
public sealed class NestedControlBracesCodeFixProvider : CodeFixProvider
{
	private const string EQUIVALENCE_KEY = "ZS2005.AddBraces";
	private static readonly FixAllProvider _fixAll = new NestedFixAllProvider();

	public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ZS2005");
	public override FixAllProvider GetFixAllProvider() => _fixAll;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

		foreach(var diagnostic in context.Diagnostics)
		{
			if(!TryGetChange(root, text, diagnostic.Location.SourceSpan.Start, out var change))
				continue;

			context.RegisterCodeFix(CodeAction.Create(Properties.Resources.AddBraces,
				cancellation => Task.FromResult(context.Document.WithText(text.WithChanges(change))), EQUIVALENCE_KEY), diagnostic);
		}
	}

	private static bool TryGetChange(SyntaxNode root, SourceText text, int position, out TextChange change)
	{
		change = default;
		var node = root?.FindToken(position).Parent?.AncestorsAndSelf()
			.FirstOrDefault(candidate => candidate.SpanStart == position && NestedControlBraces.RequiresBraces(candidate));

		if(node == null || node.ContainsDiagnostics)
			return false;

		var body = NestedControlBraces.GetBody(node);
		var header = body.GetFirstToken().GetPreviousToken();
		var span = TextSpan.FromBounds(header.Span.End, body.FullSpan.End);

		//指令或未启用源码横跨目标范围时，插块可能改变其他配置中的语句归属。
		if(root.DescendantTrivia(span, descendIntoTrivia: true).Any(trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia) || trivia.IsKind(SyntaxKind.SkippedTokensTrivia)))
			return false;

		var indent = SourceTextUtility.GetIndentation(text, node.SpanStart);
		var newline = SourceTextUtility.GetNewLine(text, node.SpanStart);
		var bodyLine = text.Lines.GetLineFromPosition(body.SpanStart);
		var headerLine = text.Lines.GetLineFromPosition(header.Span.End);
		var gap = text.ToString(TextSpan.FromBounds(header.Span.End, body.SpanStart));
		string prefix;
		string content;

		if(headerLine.LineNumber == bodyLine.LineNumber)
		{
			prefix = gap.TrimEnd(' ', '\t') + newline + indent + "{" + newline + indent + "\t";
			content = ShiftIndentation(root, text, TextSpan.FromBounds(body.SpanStart, body.FullSpan.End), "\t");
		}
		else
		{
			var opening = headerLine.EndIncludingLineBreak;

			//跨行尾注释必须完整保留在花括号之前。
			foreach(var trivia in root.DescendantTrivia(TextSpan.FromBounds(header.Span.End, body.SpanStart)))
			{
				if(trivia.SpanStart < opening && trivia.Span.End > opening)
					opening = text.Lines.GetLineFromPosition(trivia.Span.End).EndIncludingLineBreak;
			}

			if(opening > body.SpanStart)
				return false;

			prefix = text.ToString(TextSpan.FromBounds(header.Span.End, opening)) + indent + "{" + newline;
			var shift = SourceTextUtility.GetIndentation(text, body.SpanStart).Length <= indent.Length ? "\t" : "";
			content = ShiftIndentation(root, text, TextSpan.FromBounds(opening, body.FullSpan.End), shift);
		}

		var lastNewline = content.LastIndexOf('\n');
		var tail = lastNewline < 0 ? content : content.Substring(lastNewline + 1);

		if(lastNewline >= 0 && tail.All(character => character == ' ' || character == '\t'))
			content = content.Substring(0, lastNewline + 1) + indent + "}" + newline + tail;
		else
			content = content.TrimEnd(' ', '\t') + newline + indent + "}" + newline + indent;

		change = new TextChange(span, prefix + content);
		return true;
	}

	private static string ShiftIndentation(SyntaxNode root, SourceText text, TextSpan span, string shift)
	{
		if(shift.Length == 0)
			return text.ToString(span);

		var changes = ImmutableArray.CreateBuilder<TextChange>();

		foreach(var line in text.Lines)
		{
			if(line.Start < span.Start || line.Start >= span.End || line.Start == line.End)
				continue;

			var token = root.FindToken(line.Start, findInsideTrivia: true);
			var trivia = root.FindTrivia(line.Start, findInsideTrivia: true);

			//不改写多行字符串、原始字符串或跨行注释的内部内容。
			if(token.SpanStart < line.Start && token.Span.Contains(line.Start) ||
				trivia.SpanStart < line.Start && trivia.Span.Contains(line.Start))
				continue;

			changes.Add(new TextChange(new TextSpan(line.Start - span.Start, 0), shift));
		}

		return SourceText.From(text.ToString(span)).WithChanges(changes).ToString();
	}

	private sealed class NestedFixAllProvider : FixAllProvider
	{
		public override Task<CodeAction> GetFixAsync(FixAllContext context) => Task.FromResult(CodeAction.Create(
			Properties.Resources.AddBraces, cancellation => FixAsync(context, cancellation), EQUIVALENCE_KEY));

		private static async Task<Solution> FixAsync(FixAllContext context, CancellationToken cancellation)
		{
			var solution = context.Solution;
			var ids = context.Scope switch
			{
				FixAllScope.Document => ImmutableArray.Create(context.Document.Id),
				FixAllScope.Project => context.Project.DocumentIds.ToImmutableArray(),
				FixAllScope.Solution => solution.Projects.SelectMany(project => project.DocumentIds).ToImmutableArray(),
				_ => ImmutableArray<DocumentId>.Empty,
			};

			foreach(var id in ids)
			{
				var document = solution.GetDocument(id);
				var diagnostics = await context.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);

				//从内部和后部开始，先前诊断的语句头位置不受新增括号影响。
				foreach(var diagnostic in diagnostics.OrderByDescending(item => item.Location.SourceSpan.Start))
				{
					cancellation.ThrowIfCancellationRequested();
					var root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
					var text = await document.GetTextAsync(cancellation).ConfigureAwait(false);

					if(TryGetChange(root, text, diagnostic.Location.SourceSpan.Start, out var change))
						document = document.WithText(text.WithChanges(change));
				}

				solution = document.Project.Solution;
			}

			return solution;
		}
	}
}
