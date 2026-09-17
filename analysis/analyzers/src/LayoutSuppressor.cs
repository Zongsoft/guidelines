using System;
using System.Linq;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Analyzers;

/// <summary>保留用于对齐、紧凑参数和空循环的局部布局，以及私有字段的命名例外。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LayoutSuppressor : DiagnosticSuppressor
{
	private static readonly SuppressionDescriptor _layout = new("ZSS0057", "IDE0055", new LocalizableResourceString(nameof(Properties.Resources.LayoutSuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));
	private static readonly SuppressionDescriptor _loop = new("ZSS2002", "IDE2001", new LocalizableResourceString(nameof(Properties.Resources.EmptyLoopSuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));
	private static readonly SuppressionDescriptor _name = new("ZSS1006", "IDE1006", new LocalizableResourceString(nameof(Properties.Resources.FieldNameSuppression), Properties.Resources.ResourceManager, typeof(Properties.Resources)));

	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(_layout, _loop, _name);

	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach(var diagnostic in context.ReportedDiagnostics)
		{
			var tree = diagnostic.Location.SourceTree;
			if(tree == null)
				continue;

			var root = tree.GetRoot(context.CancellationToken);
			var text = tree.GetText(context.CancellationToken);
			var span = diagnostic.Location.SourceSpan;
			var token = root.FindToken(span.Start);

			if(diagnostic.Id == "IDE1006" && IsSpecialField(token, context.GetSemanticModel(tree)))
				context.ReportSuppression(Suppression.Create(_name, diagnostic));
			else if(diagnostic.Id == "IDE2001" && IsEmptyLoop(token, text))
				context.ReportSuppression(Suppression.Create(_loop, diagnostic));
			else if(diagnostic.Id == "IDE0055" && IsWhitespace(text, span.Start, span.End))
			{
				//格式诊断可能落在前一个 token 的尾随空白，也可能是后一个 token 前的零宽位置。
				var next = token.SpanStart >= span.End ? token : token.GetNextToken();
				var previous = next.GetPreviousToken();
				var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
				var tabWidth = options.TryGetValue("tab_width", out var width) && int.TryParse(width, out var value) && value > 0 ? value : 4;

				if(previous.Span.End <= span.Start && next.SpanStart >= span.End &&
					(IsBoundary(previous, next, text) || IsContinuation(next, text, tabWidth)))
					context.ReportSuppression(Suppression.Create(_layout, diagnostic));
			}
		}
	}

	private static bool IsBoundary(SyntaxToken previous, SyntaxToken next, SourceText text)
	{
		if(!IsWhitespace(text, previous.Span.End, next.SpanStart))
			return false;

		//初始化器跨行时，仍允许空构造函数的花括号紧接 this/base 调用；Tab 不属于此例外。
		if(next.IsKind(SyntaxKind.OpenBraceToken) && next.Parent is BlockSyntax body && body.Statements.Count == 0 &&
			body.Parent is ConstructorDeclarationSyntax constructor && constructor.Initializer?.GetLastToken() == previous &&
			next.SpanStart == previous.Span.End + 1 && text[previous.Span.End] == ' ' &&
			text.Lines.GetLineFromPosition(next.SpanStart).LineNumber == text.Lines.GetLineFromPosition(body.CloseBraceToken.SpanStart).LineNumber)
			return true;

		if(previous.IsKind(SyntaxKind.DelegateKeyword) && previous.Parent is AnonymousMethodExpressionSyntax &&
			next.IsKind(SyntaxKind.OpenParenToken) && next.Parent?.Parent == previous.Parent && next.SpanStart == previous.Span.End)
			return true;

		//变量名与初始化等号前的额外空白可用于纵向对齐。
		if(next.SpanStart > previous.Span.End && next.Parent is VariableDeclaratorSyntax variable && next == variable.Identifier &&
			variable.Parent is VariableDeclarationSyntax declaration && previous == declaration.Type.GetLastToken())
			return true;

		if(next.SpanStart > previous.Span.End && next.IsKind(SyntaxKind.EqualsToken) && next.Parent is EqualsValueClauseSyntax initializer && initializer.Parent is VariableDeclaratorSyntax)
			return true;

		if(next.SpanStart == previous.Span.End && previous.IsKind(SyntaxKind.CloseBracketToken) && previous.Parent is AttributeListSyntax attributes && attributes.Parent is ParameterSyntax parameter &&
			(next.IsKind(SyntaxKind.OpenBracketToken) && next.Parent?.Parent == parameter || next == parameter.Modifiers.FirstOrDefault() || next == parameter.Type?.GetFirstToken()))
			return true;

		if(next.IsKind(SyntaxKind.ColonToken))
		{
			if(next.Parent is NameColonSyntax name && name.Parent is ArgumentSyntax argument && argument.Parent is TupleExpressionSyntax)
				return true;

			if(next.SpanStart == previous.Span.End && next.Parent is ConditionalExpressionSyntax)
				return IsWhitespace(text, next.Span.End, text.Lines.GetLineFromPosition(next.Span.End).End);
		}

		return next.IsKind(SyntaxKind.SemicolonToken) && next.Parent is EmptyStatementSyntax && IsEmptyLoop(next, text);
	}

	private static bool IsContinuation(SyntaxToken token, SourceText text, int tabWidth)
	{
		var line = text.Lines.GetLineFromPosition(token.SpanStart);
		if(!IsWhitespace(text, line.Start, token.SpanStart) ||
			!token.Parent.AncestorsAndSelf().Any(node =>
				node is BinaryExpressionSyntax binary && (binary.Right.GetFirstToken() == token || binary.Left.GetFirstToken() == token) ||
				node is ConditionalExpressionSyntax conditional && (conditional.Condition.GetFirstToken() == token || conditional.WhenTrue.GetFirstToken() == token || conditional.WhenFalse.GetFirstToken() == token)))
			return false;

		//限定为初始化、赋值或 return 中的表达式续行，不放宽调用参数与任意语句缩进。
		foreach(var expression in token.Parent.AncestorsAndSelf().OfType<ExpressionSyntax>())
		{
			if(!(expression is BinaryExpressionSyntax) && !(expression is ConditionalExpressionSyntax))
				continue;

			var assignment = expression.Parent as AssignmentExpressionSyntax;
			if(!(expression.Parent is EqualsValueClauseSyntax) && !(expression.Parent is ReturnStatementSyntax) && assignment?.Right != expression)
				continue;

			var first = expression.GetFirstToken();
			if(text.Lines.GetLineFromPosition(first.SpanStart).LineNumber < line.LineNumber &&
				(GetColumn(text, first.SpanStart, tabWidth) == GetColumn(text, token.SpanStart, tabWidth) ||
					expression is ConditionalExpressionSyntax && assignment != null &&
					GetColumn(text, assignment.OperatorToken.SpanStart, tabWidth) == GetColumn(text, token.SpanStart, tabWidth)))
				return true;
		}

		return false;
	}

	private static int GetColumn(SourceText text, int position, int tabWidth)
	{
		var column = 0;
		for(var index = text.Lines.GetLineFromPosition(position).Start; index < position; index++)
			column += text[index] == '\t' ? tabWidth - column % tabWidth : 1;

		return column;
	}

	private static bool IsEmptyLoop(SyntaxToken token, SourceText text)
	{
		var loop = token.Parent?.AncestorsAndSelf().OfType<WhileStatementSyntax>().FirstOrDefault();
		return loop?.Statement is EmptyStatementSyntax empty &&
			(token == empty.SemicolonToken || token == loop.WhileKeyword) &&
			text.Lines.GetLineFromPosition(loop.CloseParenToken.Span.End).LineNumber == text.Lines.GetLineFromPosition(empty.SpanStart).LineNumber;
	}

	private static bool IsSpecialField(SyntaxToken token, SemanticModel model)
	{
		if(!(token.Parent is VariableDeclaratorSyntax variable) || !(model.GetDeclaredSymbol(variable) is IFieldSymbol field) ||
			field.DeclaredAccessibility != Accessibility.Private)
			return false;

		var name = field.Name;
		if(name.Length > 0 && name[0] == '_' && name[name.Length - 1] == '_')
			return true;

		var hasLetter = false;
		foreach(var character in name)
		{
			if(char.IsUpper(character))
				hasLetter = true;
			else if(character != '_' && !char.IsDigit(character))
				return false;
		}

		return hasLetter;
	}

	private static bool IsWhitespace(SourceText text, int start, int end)
	{
		for(var index = start; index < end; index++)
		{
			if(text[index] != ' ' && text[index] != '\t')
				return false;
		}

		return true;
	}
}
