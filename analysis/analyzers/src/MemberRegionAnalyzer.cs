using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>检查超过九个方法和属性的文件是否使用分段。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberRegionAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _rule = new(
		"ZS2004",
		new LocalizableResourceString(nameof(Properties.Resources.MemberRegionTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.MemberRegionMessage), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Style", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs2004");

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

			compilation.RegisterSyntaxTreeAction(Analyze);
		});
	}

	private static void Analyze(SyntaxTreeAnalysisContext context)
	{
		var root = context.Tree.GetRoot(context.CancellationToken);
		var members = root.DescendantNodes().Where(node => node is MethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax).ToArray();
		if(members.Length <= 9)
			return;

		var regions = new List<TextSpan>();
		var openings = new Stack<int>();

		foreach(var trivia in root.DescendantTrivia())
		{
			context.CancellationToken.ThrowIfCancellationRequested();
			if(trivia.GetStructure() is not DirectiveTriviaSyntax directive || !directive.IsActive)
				continue;

			if(directive is RegionDirectiveTriviaSyntax)
				openings.Push(directive.Span.End);
			else if(directive is EndRegionDirectiveTriviaSyntax && openings.Count > 0)
				regions.Add(TextSpan.FromBounds(openings.Pop(), directive.SpanStart));
		}

		//空分段或方法体内部的分段不能替代成员分段；每个文件只报告一次。
		foreach(var member in members)
		{
			if(regions.Any(region => region.Contains(member.Span)))
				continue;

			var token = member switch
			{
				MethodDeclarationSyntax method => method.Identifier,
				PropertyDeclarationSyntax property => property.Identifier,
				IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
				_ => member.GetFirstToken(),
			};
			context.ReportDiagnostic(Diagnostic.Create(_rule, token.GetLocation(), members.Length));
			return;
		}
	}
}
