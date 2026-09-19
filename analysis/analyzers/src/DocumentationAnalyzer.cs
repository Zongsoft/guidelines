using System.Linq;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>检查方法文档的参数、返回值和 XML 元素布局。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocumentationAnalyzer : DiagnosticAnalyzer
{
	#region 静态字段
	private static readonly DiagnosticDescriptor _parameter = CreateRule("ZS3001", nameof(Properties.Resources.DocumentationParameterTitle), nameof(Properties.Resources.DocumentationParameterMessage));
	private static readonly DiagnosticDescriptor _returns = CreateRule("ZS3002", nameof(Properties.Resources.DocumentationReturnsTitle), nameof(Properties.Resources.DocumentationReturnsMessage));
	private static readonly DiagnosticDescriptor _layout = CreateRule("ZS3003", nameof(Properties.Resources.DocumentationLayoutTitle), nameof(Properties.Resources.DocumentationLayoutMessage));
	#endregion

	#region 公共属性
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_parameter, _returns, _layout);
	#endregion

	#region 公共方法
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		context.RegisterSyntaxTreeAction(AnalyzeLayout);
	}
	#endregion

	#region 私有方法
	private static DiagnosticDescriptor CreateRule(string id, string title, string message) => new(
		id,
		new LocalizableResourceString(title, Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(message, Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Documentation", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#" + id.ToLowerInvariant());

	private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
	{
		var method = (MethodDeclarationSyntax)context.Node;
		var documentation = method.GetLeadingTrivia().Select(trivia => trivia.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().ToArray();
		if(documentation.Length == 0)
			return;

		var elements = documentation.SelectMany(comment => comment.Content).ToArray();
		foreach(var parameter in method.ParameterList.Parameters)
		{
			if(!elements.Any(element => HasParameter(element, parameter.Identifier.ValueText)))
				context.ReportDiagnostic(Diagnostic.Create(_parameter, parameter.Identifier.GetLocation(), parameter.Identifier.ValueText));
		}

		if(method.ReturnType is not PredefinedTypeSyntax type || !type.Keyword.IsKind(SyntaxKind.VoidKeyword))
		{
			if(!elements.Any(element => GetName(element) == "returns"))
				context.ReportDiagnostic(Diagnostic.Create(_returns, method.ReturnType.GetLocation()));
		}
	}

	private static bool HasParameter(XmlNodeSyntax element, string name)
	{
		if(GetName(element) != "param")
			return false;

		var attributes = element is XmlElementSyntax full ? full.StartTag.Attributes : ((XmlEmptyElementSyntax)element).Attributes;
		return attributes.OfType<XmlNameAttributeSyntax>().Any(attribute => attribute.Name.ToString() == "name" && attribute.Identifier.Identifier.ValueText == name);
	}

	private static string GetName(XmlNodeSyntax node) => node switch
	{
		XmlElementSyntax element => element.StartTag.Name.ToString(),
		XmlEmptyElementSyntax element => element.Name.ToString(),
		_ => null,
	};

	private static void AnalyzeLayout(SyntaxTreeAnalysisContext context)
	{
		var root = context.Tree.GetRoot(context.CancellationToken);
		var text = context.Tree.GetText(context.CancellationToken);
		foreach(var trivia in root.DescendantTrivia())
		{
			context.CancellationToken.ThrowIfCancellationRequested();
			if(trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentation || documentation.ContainsDiagnostics)
				continue;

			foreach(var element in documentation.DescendantNodes().OfType<XmlElementSyntax>())
			{
				if(text.Lines.GetLineFromPosition(element.SpanStart).LineNumber != text.Lines.GetLineFromPosition(element.Span.End).LineNumber && HasSingleContentLine(element, text))
					context.ReportDiagnostic(Diagnostic.Create(_layout, element.StartTag.GetLocation(), element.StartTag.Name.ToString()));
			}
		}
	}

	private static bool HasSingleContentLine(XmlElementSyntax element, SourceText text)
	{
		var line = -1;
		foreach(var token in element.Content.SelectMany(content => content.DescendantTokens()))
		{
			//只计算内容 token，排除 ///、块注释前缀和空白行；保留真实多行文本及 XML 结构。
			var value = token.Text;
			var start = 0;
			var end = value.Length;
			while(start < end && char.IsWhiteSpace(value[start]))
				start++;
			while(end > start && char.IsWhiteSpace(value[end - 1]))
				end--;

			if(start == end)
				continue;

			var first = text.Lines.GetLineFromPosition(token.SpanStart + start).LineNumber;
			var last = text.Lines.GetLineFromPosition(token.SpanStart + end - 1).LineNumber;
			if(first != last || line >= 0 && first != line)
				return false;

			line = first;
		}
		return line >= 0;
	}
	#endregion
}
