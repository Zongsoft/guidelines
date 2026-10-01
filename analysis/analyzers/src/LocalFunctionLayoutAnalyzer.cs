using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>检查局部函数的末尾位置及其与主体、相邻函数之间的分隔。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocalFunctionLayoutAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _rule = new(
		"ZS2006",
		new LocalizableResourceString(nameof(Properties.Resources.LocalFunctionLayoutTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.LocalFunctionLayoutMessage), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Style", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs2006");

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

		foreach(var violation in LocalFunctionLayout.GetViolations(context.Node, text))
			context.ReportDiagnostic(Diagnostic.Create(_rule, violation.Function.GetFirstToken().GetLocation()));
	}
}
