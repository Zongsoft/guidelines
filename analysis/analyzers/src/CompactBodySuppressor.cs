using System;
using System.Linq;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>允许包含至多两条简单语句的单行方法体和匿名函数体。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompactBodySuppressor : DiagnosticSuppressor
{
	private static readonly SuppressionDescriptor _layout = new("ZSS0058", "IDE0055", new LocalizableResourceString(nameof(Properties.Resources.CompactBodySuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));
	private static readonly SuppressionDescriptor _statement = new("ZSS2003", "IDE2001", new LocalizableResourceString(nameof(Properties.Resources.CompactBodySuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));

	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(_layout, _statement);

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
			var token = root.FindToken(span.Start);
			var next = token.SpanStart >= span.End ? token : token.GetNextToken();
			var block = next.Parent?.AncestorsAndSelf().OfType<BlockSyntax>().FirstOrDefault();

			if(block == null || !IsCompact(block, text))
				continue;

			if(diagnostic.Id == "IDE2001" && block.Span.Contains(span))
				context.ReportSuppression(Suppression.Create(_statement, diagnostic));
			else if(diagnostic.Id == "IDE0055" && IsBoundary(block, text, span))
				context.ReportSuppression(Suppression.Create(_layout, diagnostic));
		}
	}

	private static bool IsCompact(BlockSyntax block, SourceText text)
	{
		if(!(block.Parent is MethodDeclarationSyntax) && !(block.Parent is LocalFunctionStatementSyntax) && !(block.Parent is AnonymousFunctionExpressionSyntax))
			return false;

		if(block.Statements.Count > 2 || block.ContainsDiagnostics || block.ContainsDirectives ||
			text.Lines.GetLineFromPosition(block.OpenBraceToken.SpanStart).LineNumber != text.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart).LineNumber)
			return false;

		return block.Statements.All(statement =>
			(statement is ExpressionStatementSyntax || statement is ReturnStatementSyntax || statement is ThrowStatementSyntax || statement is LocalDeclarationStatementSyntax) &&
			!statement.DescendantNodes().OfType<StatementSyntax>().Any());
	}

	private static bool IsBoundary(BlockSyntax block, SourceText text, TextSpan span)
	{
		for(var position = span.Start; position < span.End; position++)
		{
			if(text[position] != ' ' && text[position] != '\t')
				return false;
		}

		if(Contains(block.OpenBraceToken.GetPreviousToken().Span.End, block.OpenBraceToken.SpanStart, span))
			return true;

		var end = block.OpenBraceToken.Span.End;
		foreach(var statement in block.Statements)
		{
			if(Contains(end, statement.SpanStart, span))
				return true;

			end = statement.Span.End;
		}

		return Contains(end, block.CloseBraceToken.SpanStart, span);
	}

	private static bool Contains(int start, int end, TextSpan span) => span.Start >= start && span.End <= end;
}
