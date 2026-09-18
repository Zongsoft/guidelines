using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>检查直接异常消息和手写字符串中的非 ASCII 文字，不追踪文本来源。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocalizationAnalyzer : DiagnosticAnalyzer
{
	private static readonly DiagnosticDescriptor _exception = new(
		"ZS1301",
		new LocalizableResourceString(nameof(Properties.Resources.ExceptionMessageTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.ExceptionMessageDiagnostic), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Globalization", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs1301");
	private static readonly DiagnosticDescriptor _text = new(
		"ZS1302",
		new LocalizableResourceString(nameof(Properties.Resources.LocalizedTextTitle), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		new LocalizableResourceString(nameof(Properties.Resources.LocalizedTextDiagnostic), Properties.Resources.ResourceManager, typeof(Properties.Resources)),
		"Globalization", DiagnosticSeverity.Warning, true,
		helpLinkUri: "https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs1302");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_exception, _text);

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(compilation =>
		{
			if(compilation.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.IsTestProject", out var value) &&
				bool.TryParse(value, out var testProject) && testProject)
				return;

			compilation.RegisterSyntaxNodeAction(Analyze,
				SyntaxKind.StringLiteralExpression, SyntaxKind.Utf8StringLiteralExpression,
				SyntaxKind.InterpolatedStringText, SyntaxKind.InterpolationFormatClause);
		});
	}

	private static void Analyze(SyntaxNodeAnalysisContext context)
	{
		var token = context.Node switch
		{
			LiteralExpressionSyntax literal => literal.Token,
			InterpolatedStringTextSyntax text => text.TextToken,
			InterpolationFormatClauseSyntax format => format.FormatStringToken,
			_ => default,
		};

		if(string.IsNullOrWhiteSpace(token.ValueText))
			return;

		var rule = IsExceptionRuleEnabled(context) && IsExceptionText(context, token) ? _exception :
			UnicodeText.ContainsLocalizedText(token.ValueText) ? _text : null;

		if(rule != null)
			context.ReportDiagnostic(Diagnostic.Create(rule, token.GetLocation()));
	}

	private static bool IsExceptionText(SyntaxNodeAnalysisContext context, SyntaxToken token)
	{
		foreach(var syntax in context.Node.Ancestors().OfType<ArgumentSyntax>())
		{
			if(context.SemanticModel.GetOperation(syntax, context.CancellationToken) is IArgumentOperation argument &&
				IsMessage(argument, context.Compilation) && GetMessageText(syntax.Expression, context.SemanticModel).Contains(token))
				return true;
		}

		return false;
	}

	private static bool IsExceptionRuleEnabled(SyntaxNodeAnalysisContext context)
	{
		if(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree).TryGetValue("dotnet_diagnostic.ZS1301.severity", out var value))
			return !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);

		return !context.Compilation.Options.SpecificDiagnosticOptions.TryGetValue("ZS1301", out var severity) || severity != ReportDiagnostic.Suppress;
	}

	private static bool IsMessage(IArgumentOperation argument, Compilation compilation)
	{
		if(argument.IsImplicit || argument.Parameter?.Type.SpecialType != SpecialType.System_String ||
			!string.Equals(argument.Parameter.Name, "message", StringComparison.OrdinalIgnoreCase) ||
			!(argument.Parent is IObjectCreationOperation creation))
			return false;

		var exception = compilation.GetTypeByMetadataName("System.Exception");
		for(var type = creation.Type as INamedTypeSymbol; type != null; type = type.BaseType)
		{
			if(SymbolEqualityComparer.Default.Equals(type, exception))
				return true;
		}

		return false;
	}

	//只查看当前表达式的固定文本，不求常量值、不追踪符号或方法体。
	private static IEnumerable<SyntaxToken> GetMessageText(ExpressionSyntax expression, SemanticModel model)
	{
		switch(expression)
		{
			case LiteralExpressionSyntax literal when literal.Token.Value is string value:
				if(!string.IsNullOrWhiteSpace(value))
					yield return literal.Token;
				break;
			case ParenthesizedExpressionSyntax parenthesized:
				foreach(var token in GetMessageText(parenthesized.Expression, model))
					yield return token;
				break;
			case CastExpressionSyntax cast when model.GetOperation(cast) is IConversionOperation conversion && conversion.OperatorMethod == null:
				foreach(var token in GetMessageText(cast.Expression, model))
					yield return token;
				break;
			case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) &&
				model.GetOperation(binary) is IBinaryOperation operation && operation.OperatorMethod == null && operation.Type?.SpecialType == SpecialType.System_String:
				foreach(var token in GetMessageText(binary.Left, model))
					yield return token;
				foreach(var token in GetMessageText(binary.Right, model))
					yield return token;
				break;
			case InterpolatedStringExpressionSyntax interpolated:
				foreach(var content in interpolated.Contents)
				{
					if(content is InterpolatedStringTextSyntax text && !string.IsNullOrWhiteSpace(text.TextToken.ValueText))
						yield return text.TextToken;
					else if(content is InterpolationSyntax interpolation)
					{
						foreach(var token in GetMessageText(interpolation.Expression, model))
							yield return token;
					}
				}
				break;
			case InvocationExpressionSyntax invocation when model.GetOperation(invocation) is IInvocationOperation call &&
				call.TargetMethod.ContainingType.SpecialType == SpecialType.System_String && call.TargetMethod.Name == "Format":
				foreach(var argument in call.Arguments)
				{
					if(argument.Parameter?.Name == "format" && argument.Syntax is ArgumentSyntax syntax)
					{
						foreach(var token in GetMessageText(syntax.Expression, model))
							yield return token;
					}
				}
				break;
		}
	}
}
