using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>要求以语句块明确嵌套控制结构的外层语句体。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NestedControlBracesAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _rule = new(
		"ZS2005",
		new LocalizableResourceString(nameof(Properties.Resources.NestedControlBracesTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.NestedControlBracesMessage), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Style", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs2005");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement, SyntaxKind.ElseClause,
			SyntaxKind.ForStatement, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement,
			SyntaxKind.WhileStatement, SyntaxKind.DoStatement, SyntaxKind.UsingStatement, SyntaxKind.LockStatement, SyntaxKind.FixedStatement);
	}

	private static void Analyze(SyntaxNodeAnalysisContext context)
	{
		if(NestedControlBraces.RequiresBraces(context.Node))
			context.ReportDiagnostic(Diagnostic.Create(_rule, context.Node.GetFirstToken().GetLocation()));
	}
}
