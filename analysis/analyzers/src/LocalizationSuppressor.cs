using System;
using System.Linq;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>排除字符串视图转换、终端控制序列和纯字符图案的本地化误报。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocalizationSuppressor : DiagnosticSuppressor
{
	private static readonly SuppressionDescriptor _technical = new("ZSS1303", "CA1303", new LocalizableResourceString(nameof(Properties.Resources.TechnicalTextSuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));

	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(_technical);

	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach(var diagnostic in context.ReportedDiagnostics)
		{
			var tree = diagnostic.Location.SourceTree;
			if(tree == null)
				continue;

			var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
			var model = context.GetSemanticModel(tree);
			var expression = node.AncestorsAndSelf().OfType<ExpressionSyntax>().FirstOrDefault();
			if(expression == null)
				continue;

			var constant = model.GetConstantValue(expression, context.CancellationToken);
			if(IsStringView(expression, model) || constant.HasValue && constant.Value is string text && (IsControlSequence(text) || IsDrawing(text)))
				context.ReportSuppression(Suppression.Create(_technical, diagnostic));
		}
	}

	private static bool IsStringView(ExpressionSyntax expression, SemanticModel model)
	{
		var invocation = expression.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
		if(invocation == null || !(model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method))
			return false;

		method = method.ReducedFrom ?? method;
		return (method.Name == "AsSpan" || method.Name == "AsMemory") && method.Parameters.Length > 0 &&
			method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
			SymbolEqualityComparer.Default.Equals(method.ContainingType, model.Compilation.GetTypeByMetadataName("System.MemoryExtensions"));
	}

	private static bool IsControlSequence(string text)
	{
		if(text.Length == 0)
			return false;

		var position = 0;
		while(position < text.Length)
		{
			if(text[position++] != '\u001b' || position >= text.Length || text[position++] != '[')
				return false;

			while(position < text.Length && text[position] >= '\x30' && text[position] <= '\x3f')
				position++;

			while(position < text.Length && text[position] >= '\x20' && text[position] <= '\x2f')
				position++;

			if(position >= text.Length || text[position] < '\x40' || text[position++] > '\x7e')
				return false;
		}

		return true;
	}

	private static bool IsDrawing(string text)
	{
		var lines = 0;
		var hasDrawing = false;

		foreach(var character in text)
		{
			if(character == '\r' || character == '\n')
			{
				if(hasDrawing)
					lines++;

				hasDrawing = false;
			}
			else if(character != ' ' && character != '\t')
			{
				if("_/\\|+-=".IndexOf(character) < 0)
					return false;

				hasDrawing = true;
			}
		}

		return lines + (hasDrawing ? 1 : 0) >= 2;
	}
}
