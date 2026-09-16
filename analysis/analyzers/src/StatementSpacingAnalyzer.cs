using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>检查同一语句列表中声明分组和独立控制结构之间的空行。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StatementSpacingAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _rule = new(
		"ZS2003",
		new LocalizableResourceString(nameof(Properties.Resources.StatementSpacingTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.StatementSpacingMessage), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Style", DiagnosticSeverity.Warning, true);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Block, SyntaxKind.SwitchSection, SyntaxKind.CompilationUnit);
	}

	private static void Analyze(SyntaxNodeAnalysisContext context)
	{
		var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
		StatementSyntax previous = null;

		//仅比较同一列表的直接子语句，不把 else、catch、finally 或循环体拆开。
		foreach(var child in context.Node.ChildNodes())
		{
			var current = child is GlobalStatementSyntax global ? global.Statement : child as StatementSyntax;

			if(current == null)
				continue;

			if(RequiresSeparation(previous, current) && !HasBlankLine(text, previous.Span.End, current.SpanStart))
				context.ReportDiagnostic(Diagnostic.Create(_rule, current.GetFirstToken().GetLocation()));

			previous = current;
		}
	}

	private static bool RequiresSeparation(StatementSyntax previous, StatementSyntax current)
	{
		if(previous == null)
			return false;

		if(IsControl(previous) && IsControl(current))
			return true;

		var previousDeclaration = IsDeclaration(previous);
		var currentDeclaration = IsDeclaration(current);

		if(previousDeclaration == currentDeclaration)
			return false;

		//声明和操作分组；短小的“声明后直接返回”仍可作为同一逻辑段。
		var operation = previousDeclaration ? current : previous;
		return operation is ExpressionStatementSyntax || IsControl(operation);
	}

	private static bool IsDeclaration(StatementSyntax statement) => statement is LocalDeclarationStatementSyntax ||
		statement is ExpressionStatementSyntax expression &&
		expression.Expression is AssignmentExpressionSyntax assignment &&
		assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && IsDeclarationTarget(assignment.Left);

	private static bool IsDeclarationTarget(ExpressionSyntax expression)
	{
		if(expression is DeclarationExpressionSyntax)
			return true;

		if(expression is TupleExpressionSyntax tuple)
		{
			foreach(var argument in tuple.Arguments)
			{
				if(!IsDeclarationTarget(argument.Expression))
					return false;
			}

			return tuple.Arguments.Count > 0;
		}

		return false;
	}

	private static bool IsControl(StatementSyntax statement) => statement is IfStatementSyntax ||
		statement is ForStatementSyntax || statement is CommonForEachStatementSyntax ||
		statement is WhileStatementSyntax || statement is DoStatementSyntax ||
		statement is SwitchStatementSyntax || statement is TryStatementSyntax ||
		statement is UsingStatementSyntax || statement is LockStatementSyntax ||
		statement is CheckedStatementSyntax || statement is UnsafeStatementSyntax || statement is BlockSyntax;

	private static bool HasBlankLine(SourceText text, int start, int end)
	{
		var first = text.Lines.GetLineFromPosition(start).LineNumber + 1;
		var last = text.Lines.GetLineFromPosition(end).LineNumber;

		for(var index = first; index < last; index++)
		{
			if(string.IsNullOrWhiteSpace(text.Lines[index].ToString()))
				return true;
		}

		return false;
	}
}
