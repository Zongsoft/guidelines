using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>检查连续赋值组与条件或循环之间，以及独立控制结构之间的空行。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StatementSpacingAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _rule = new(
		"ZS2003",
		new LocalizableResourceString(nameof(Properties.Resources.StatementSpacingTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.StatementSpacingMessage), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Style", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs2003");

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
		var assignments = 0;

		//仅比较同一列表的直接子语句，不把 else、catch、finally 或循环体拆开。
		foreach(var child in context.Node.ChildNodes())
		{
			var current = child is GlobalStatementSyntax global ? global.Statement : child as StatementSyntax;

			if(current == null)
				continue;

			var separated = previous != null && HasBlankLine(text, previous.Span.End, current.SpanStart);

			if(separated)
				assignments = 0;

			if(previous != null && !separated &&
				(IsControl(previous) && IsControl(current) && !(IsSimpleIf(previous) && IsSimpleIf(current)) || assignments > 2 && IsConditionalOrLoop(current)))
				context.ReportDiagnostic(Diagnostic.Create(_rule, current.GetFirstToken().GetLocation()));

			assignments = IsAssignment(current) ? assignments + 1 : 0;
			previous = current;
		}
	}

	private static bool IsAssignment(StatementSyntax statement)
	{
		if(statement is ExpressionStatementSyntax expression && expression.Expression is AssignmentExpressionSyntax)
			return true;

		if(statement is LocalDeclarationStatementSyntax declaration)
		{
			//按语句计数，多变量声明或解构赋值都只计一次。
			foreach(var variable in declaration.Declaration.Variables)
			{
				if(variable.Initializer != null)
					return true;
			}
		}

		return false;
	}

	private static bool IsSimpleIf(StatementSyntax statement) => statement is IfStatementSyntax condition &&
		condition.Else == null && (condition.Statement is ExpressionStatementSyntax || condition.Statement is ReturnStatementSyntax ||
		condition.Statement is ThrowStatementSyntax || condition.Statement is EmptyStatementSyntax || condition.Statement is BreakStatementSyntax || condition.Statement is ContinueStatementSyntax);

	private static bool IsConditionalOrLoop(StatementSyntax statement) => statement is IfStatementSyntax ||
		statement is ForStatementSyntax || statement is CommonForEachStatementSyntax ||
		statement is WhileStatementSyntax || statement is DoStatementSyntax ||
		statement is SwitchStatementSyntax;

	private static bool IsControl(StatementSyntax statement) => IsConditionalOrLoop(statement) || statement is TryStatementSyntax ||
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
