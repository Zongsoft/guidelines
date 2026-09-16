using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>逐条检查未使用的导入，允许保留普通的 System 命名空间导入。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedUsingAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _rule = new(
		"ZS0005",
		new LocalizableResourceString(nameof(Properties.Resources.UnusedUsingTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.UnusedUsingMessage), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Style", DiagnosticSeverity.Warning, true);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSemanticModelAction(Analyze);
	}

	private static void Analyze(SemanticModelAnalysisContext context)
	{
		var root = context.SemanticModel.SyntaxTree.GetRoot(context.CancellationToken);

		//编译器负责判断真实使用情况，包括 XML 文档引用、条件编译及同名类型解析。
		foreach(var diagnostic in context.SemanticModel.GetDiagnostics(cancellationToken: context.CancellationToken))
		{
			if(diagnostic.Id != "CS8019")
				continue;

			var directive = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<UsingDirectiveSyntax>();

			if(directive == null)
				continue;

			//仅放宽 using System;，不包括别名、using static、global using 或其他 System.*。
			if(directive.Alias == null && directive.StaticKeyword.IsKind(SyntaxKind.None) &&
				directive.GetFirstToken().IsKind(SyntaxKind.UsingKeyword) &&
				directive.Name is IdentifierNameSyntax name && name.Identifier.ValueText == "System" &&
				context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is INamespaceSymbol space &&
				space.Name == "System" && space.ContainingNamespace.IsGlobalNamespace)
				continue;

			context.ReportDiagnostic(Diagnostic.Create(_rule, directive.GetLocation(), directive.Name?.ToString()));
		}
	}
}
