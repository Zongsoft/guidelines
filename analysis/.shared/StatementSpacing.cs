using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis;

/// <summary>供分析器与修复器共同判断语句组的分隔边界。</summary>
internal static class StatementSpacing
{
	#region 检测入口
	internal static IEnumerable<(StatementSyntax Previous, StatementSyntax Current)> GetMissingBlankLines(SyntaxNode container, SourceText text)
	{
		var statements = new List<StatementSyntax>();

		foreach(var child in container.ChildNodes())
		{
			var statement = child is GlobalStatementSyntax global ? global.Statement : child as StatementSyntax;

			if(statement != null)
				statements.Add(statement);
		}

		StatementSyntax previous = null;
		var ordinaryStatements = 0;

		//只比较同一列表的直接子语句，不拆开关联子句或嵌套语句体。
		for(var index = 0; index < statements.Count; index++)
		{
			var current = statements[index];
			var last = GetResourceGroupEnd(statements, index, container, text);
			var separated = previous != null && HasBlankLine(container, text, previous.Span.End, current.SpanStart);

			if(separated)
				ordinaryStatements = 0;

			if(previous != null && !separated && !previous.ContainsDiagnostics && !current.ContainsDiagnostics &&
				RequiresSeparation(previous, statements[last], text, ordinaryStatements))
				yield return (previous, current);

			for(var position = index; position <= last; position++)
				ordinaryStatements = IsOrdinaryStatement(statements[position]) ? ordinaryStatements + 1 : 0;

			previous = statements[last];
			index = last;
		}
	}

	private static int GetResourceGroupEnd(List<StatementSyntax> statements, int index, SyntaxNode container, SourceText text)
	{
		if(!IsResourceDeclaration(statements[index]))
			return index;

		//无分隔空行的资源声明及其后直接嵌套的 using 链，在组首检查前置分隔。
		while(index + 1 < statements.Count && !statements[index].ContainsDiagnostics && !statements[index + 1].ContainsDiagnostics &&
			!HasBlankLine(container, text, statements[index].Span.End, statements[index + 1].SpanStart))
		{
			if(IsResourceDeclaration(statements[index + 1]))
				index++;
			else
				return statements[index + 1] is UsingStatementSyntax ? index + 1 : index;
		}

		return index;
	}

	private static bool HasBlankLine(SyntaxNode container, SourceText text, int start, int end)
	{
		var first = text.Lines.GetLineFromPosition(start).LineNumber + 1;
		var last = text.Lines.GetLineFromPosition(end).LineNumber;

		for(var index = first; index < last; index++)
		{
			var line = text.Lines[index];

			if(!string.IsNullOrWhiteSpace(line.ToString()))
				continue;

			//块注释内部和未激活的条件编译文本中的空白行不是语句分隔。
			var trivia = container.FindTrivia(line.Start);

			if(!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) &&
				!trivia.IsKind(SyntaxKind.DisabledTextTrivia))
				return true;
		}

		return false;
	}
	#endregion

	#region 语句分类
	//按语句计数，多变量声明、解构赋值及跨行表达式均只计一次。
	private static bool IsOrdinaryStatement(StatementSyntax statement) =>
		statement is LocalDeclarationStatementSyntax or ExpressionStatementSyntax;

	private static bool IsControl(StatementSyntax statement) => statement is BlockSyntax || ControlStatements.IsControl(statement);

	private static bool IsComplexControl(StatementSyntax statement) => statement is LabeledStatementSyntax label ?
		IsComplexControl(label.Statement) : IsControl(statement) && !IsSimpleControl(statement);

	private static bool IsResourceDeclaration(StatementSyntax statement) =>
		statement is LocalDeclarationStatementSyntax declaration && declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword);

	private static bool HasBlock(StatementSyntax statement) => statement switch
	{
		BlockSyntax or SwitchStatementSyntax or TryStatementSyntax or CheckedStatementSyntax or UnsafeStatementSyntax => true,
		IfStatementSyntax condition => HasBlock(condition.Statement) || condition.Else != null && HasBlock(condition.Else.Statement),
		LabeledStatementSyntax label => HasBlock(label.Statement),
		_ => ControlStatements.GetBody(statement) is StatementSyntax body && HasBlock(body),
	};
	#endregion

	#region 布局例外
	internal static bool RequiresSeparation(StatementSyntax previous, StatementSyntax current, SourceText text, int ordinaryStatements)
	{
		//局部函数的分隔及紧凑例外统一由 ZS2006 判定。
		if(current is LocalFunctionStatementSyntax)
			return false;

		if(IsCompactTryPair(previous, current, text))
			return false;

		if(HasBlock(previous))
			return !IsSwitchBreak(current);

		while(current is LabeledStatementSyntax label)
			current = label.Statement;

		//条件和循环按前置普通语句数量分隔，资源、同步及异常处理结构保留前置边界。
		return (ControlStatements.IsConditionalOrLoop(current) ? ordinaryStatements >= (IsSimpleControl(current) ? 3 : 2) : IsComplexControl(current)) ||
			IsControl(previous) && IsControl(current) && !IsSimpleControlPair(previous, current);
	}

	private static bool IsSimpleControlPair(StatementSyntax first, StatementSyntax second) =>
		IsSimpleControl(first) && IsSimpleControl(second) &&
		(first.RawKind == second.RawKind || first is CommonForEachStatementSyntax && second is CommonForEachStatementSyntax);

	private static bool IsSimpleControl(StatementSyntax statement)
	{
		var body = statement is IfStatementSyntax { Else: null } or ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax ?
			ControlStatements.GetBody(statement) : null;

		return body is ExpressionStatementSyntax or ReturnStatementSyntax or ThrowStatementSyntax or
			EmptyStatementSyntax or BreakStatementSyntax or ContinueStatementSyntax or YieldStatementSyntax;
	}

	private static bool IsCompactTryPair(StatementSyntax first, StatementSyntax second, SourceText text) =>
		first is TryStatementSyntax previous && second is TryStatementSyntax current && IsCompactTry(previous, text) && IsCompactTry(current, text) &&
		text.Lines.GetLineFromPosition(previous.Span.End).LineNumber < text.Lines.GetLineFromPosition(current.SpanStart).LineNumber;

	private static bool IsCompactTry(TryStatementSyntax statement, SourceText text)
	{
		//单行完整结构不会包含内部指令；结构之外的条件编译指令不影响此例外。
		if(statement.ContainsDiagnostics || statement.Block.Statements.Count > 2 ||
			!SourceTextUtility.IsSingleLine(text, statement.Span))
			return false;

		foreach(var clause in statement.Catches)
		{
			if(clause.Block.Statements.Count > 2)
				return false;
		}

		return statement.Finally == null || statement.Finally.Block.Statements.Count <= 2;
	}

	private static bool IsSwitchBreak(StatementSyntax statement) =>
		statement is BreakStatementSyntax &&
		statement.Parent is SwitchSectionSyntax section &&
		section.Statements[section.Statements.Count - 1] == statement;
	#endregion
}
