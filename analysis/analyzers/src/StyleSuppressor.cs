using System;
using System.Linq;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>仅豁免开发规范明确允许的指令缩进与紧凑异常处理布局。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StyleSuppressor : DiagnosticSuppressor
{
	private static readonly SuppressionDescriptor _directive = new("ZSS0055", "IDE0055", new LocalizableResourceString(nameof(Properties.Resources.DirectiveSuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));
	private static readonly SuppressionDescriptor _block = new("ZSS0056", "IDE0055", new LocalizableResourceString(nameof(Properties.Resources.BlockSuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));
	private static readonly SuppressionDescriptor _statement = new("ZSS2001", "IDE2001", new LocalizableResourceString(nameof(Properties.Resources.BlockSuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));

	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(_directive, _block, _statement);

	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach(var diagnostic in context.ReportedDiagnostics)
		{
			var tree = diagnostic.Location.SourceTree;

			if(tree == null)
				continue;

			var root = tree.GetRoot(context.CancellationToken);
			var text = tree.GetText(context.CancellationToken);
			var span = diagnostic.Location.SourceSpan;

			if(diagnostic.Id == "IDE0055" && IsDirectiveIndentation(root, text, span))
			{
				context.ReportSuppression(Suppression.Create(_directive, diagnostic));
				continue;
			}

			var node = root.FindToken(span.Start).Parent;
			var statement = node?.AncestorsAndSelf()
				.TakeWhile(ancestor => !(ancestor is AnonymousFunctionExpressionSyntax) && !(ancestor is LocalFunctionStatementSyntax))
				.OfType<TryStatementSyntax>().FirstOrDefault();

			if(statement == null)
				continue;

			foreach(var block in GetBlocks(statement))
			{
				if(!IsCompact(block, text))
					continue;

				if(diagnostic.Id == "IDE2001" && block.Span.Contains(span))
					context.ReportSuppression(Suppression.Create(_statement, diagnostic));
				else if(diagnostic.Id == "IDE0055" && IsBlockBoundary(block, text, span))
					context.ReportSuppression(Suppression.Create(_block, diagnostic));
			}
		}
	}

	private static bool IsDirectiveIndentation(SyntaxNode root, SourceText text, TextSpan span)
	{
		var line = text.Lines.GetLineFromPosition(span.Start);
		var position = line.Start;

		while(position < line.End && (text[position] == ' ' || text[position] == '\t'))
			position++;

		if(position == line.End || text[position] != '#' || span.End > position)
			return false;

		return root.FindTrivia(position, findInsideTrivia: false).GetStructure() is DirectiveTriviaSyntax;
	}

	private static bool IsCompact(BlockSyntax block, SourceText text) =>
		block.Statements.Count <= 2 && !block.ContainsDiagnostics && !block.ContainsDirectives &&
		text.Lines.GetLineFromPosition(block.Parent.GetFirstToken().SpanStart).LineNumber ==
		text.Lines.GetLineFromPosition(block.CloseBraceToken.Span.End).LineNumber;

	private static ImmutableArray<BlockSyntax> GetBlocks(TryStatementSyntax statement)
	{
		var blocks = ImmutableArray.CreateBuilder<BlockSyntax>();

		blocks.Add(statement.Block);

		foreach(var clause in statement.Catches)
			blocks.Add(clause.Block);

		if(statement.Finally != null)
			blocks.Add(statement.Finally.Block);

		return blocks.ToImmutable();
	}

	private static bool IsBlockBoundary(BlockSyntax block, SourceText text, TextSpan span)
	{
		//不抑制语句内部的运算符空格、缩进等其他格式诊断。
		for(var position = span.Start; position < span.End; position++)
		{
			if(text[position] != ' ' && text[position] != '\t')
				return false;
		}

		var previous = block.OpenBraceToken.GetPreviousToken();
		var keyword = block.Parent.GetFirstToken();
		var preceding = keyword.GetPreviousToken();
		return Contains(previous.Span.End, block.OpenBraceToken.SpanStart, span) ||
			Contains(block.OpenBraceToken.Span.End, block.Statements.FirstOrDefault()?.SpanStart ?? block.CloseBraceToken.SpanStart, span) ||
			Contains(block.Statements.LastOrDefault()?.Span.End ?? block.OpenBraceToken.Span.End, block.CloseBraceToken.SpanStart, span) ||
			(preceding.IsKind(SyntaxKind.CloseBraceToken) && (keyword.IsKind(SyntaxKind.CatchKeyword) || keyword.IsKind(SyntaxKind.FinallyKeyword)) &&
				Contains(preceding.Span.End, keyword.SpanStart, span));
	}

	private static bool Contains(int start, int end, TextSpan span) => span.Start >= start && span.End <= end;
}
