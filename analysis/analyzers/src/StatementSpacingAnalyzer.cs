using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>检查普通语句组与条件或循环之间、独立控制结构之间及执行块前后的空行。</summary>
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

		foreach(var boundary in StatementSpacing.GetMissingBlankLines(context.Node, text))
			context.ReportDiagnostic(Diagnostic.Create(_rule, boundary.Current.GetFirstToken().GetLocation()));
	}
}
