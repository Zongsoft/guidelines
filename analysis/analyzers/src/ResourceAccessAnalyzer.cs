using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>已知资源键应通过生成的强类型属性访问。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResourceAccessAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _rule = new(
		"ZS1304",
		new LocalizableResourceString(nameof(Properties.Resources.ResourceAccessTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.ResourceAccessMessage), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Globalization", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs1304");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(compilation =>
		{
			if(compilation.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.IsTestProject", out var value) &&
				bool.TryParse(value, out var testProject) && testProject)
				return;

			var manager = compilation.Compilation.GetTypeByMetadataName("System.Resources.ResourceManager");

			if(manager == null)
				return;

			compilation.RegisterOperationAction(operation => Analyze(operation, manager), OperationKind.Invocation);
		});
	}

	private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol manager)
	{
		var invocation = (IInvocationOperation)context.Operation;
		var method = invocation.TargetMethod;

		if(method.Name != "GetString" && method.Name != "GetObject" && method.Name != "GetStream")
			return;

		var type = method.ContainingType;

		while(type != null && !SymbolEqualityComparer.Default.Equals(type, manager))
			type = type.BaseType;

		if(type == null)
			return;

		foreach(var argument in invocation.Arguments)
		{
			//动态资源解析器保留动态键；Designer 生成代码由 Roslyn 的生成代码识别机制排除。
			if(argument.Parameter?.Ordinal == 0 && argument.Value.ConstantValue.HasValue &&
				argument.Value.ConstantValue.Value is string name && !string.IsNullOrEmpty(name))
				context.ReportDiagnostic(Diagnostic.Create(_rule, argument.Syntax.GetLocation(), name));
		}
	}
}
