using System;
using System.Linq;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>仅豁免开发规范明确允许的条件编译缩进与紧凑异常处理布局。</summary>
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
			var statement = node?.AncestorsAndSelf().OfType<TryStatementSyntax>().FirstOrDefault();

			if(statement == null || !IsCompact(statement, text))
				continue;

			foreach(var block in GetBlocks(statement))
			{
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

		var directive = root.FindTrivia(position, findInsideTrivia: false).GetStructure();
		return directive is IfDirectiveTriviaSyntax || directive is ElifDirectiveTriviaSyntax ||
			directive is ElseDirectiveTriviaSyntax || directive is EndIfDirectiveTriviaSyntax;
	}

	private static bool IsCompact(TryStatementSyntax statement, SourceText text)
	{
		if(!IsCompact(statement.Block, statement.TryKeyword, text))
			return false;

		foreach(var clause in statement.Catches)
		{
			if(!IsCompact(clause.Block, clause.CatchKeyword, text))
				return false;
		}

		return statement.Finally == null || IsCompact(statement.Finally.Block, statement.Finally.FinallyKeyword, text);
	}

	private static bool IsCompact(BlockSyntax block, SyntaxToken keyword, SourceText text)
	{
		if(block.Statements.Count != 1 || block.ContainsDiagnostics || block.ContainsDirectives ||
			text.Lines.GetLineFromPosition(keyword.SpanStart).LineNumber != text.Lines.GetLineFromPosition(block.CloseBraceToken.Span.End).LineNumber)
			return false;

		if(block.Statements[0].DescendantNodes().OfType<StatementSyntax>().Any())
			return false;

		return block.Statements[0] is ExpressionStatementSyntax || block.Statements[0] is ReturnStatementSyntax ||
			block.Statements[0] is ThrowStatementSyntax || block.Statements[0] is LocalDeclarationStatementSyntax;
	}

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
		return Contains(previous.Span.End, block.OpenBraceToken.SpanStart, span) ||
			Contains(block.OpenBraceToken.Span.End, block.Statements[0].SpanStart, span) ||
			Contains(block.Statements[0].Span.End, block.CloseBraceToken.SpanStart, span);
	}

	private static bool Contains(int start, int end, TextSpan span) => span.Start >= start && span.End <= end;
}
