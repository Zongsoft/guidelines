using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis;

/// <summary>共享局部函数的末尾位置、紧凑布局及真实空行判定。</summary>
internal static class LocalFunctionLayout
{
	#region 检测入口
	internal static List<StatementSyntax> GetStatements(SyntaxNode container) => container.ChildNodes()
		.Select(child => child is GlobalStatementSyntax global ? global.Statement : child as StatementSyntax)
		.Where(statement => statement != null).ToList();

	internal static SyntaxNode GetContainer(LocalFunctionStatementSyntax function) =>
		function.Parent is GlobalStatementSyntax global ? global.Parent : function.Parent;

	internal static IEnumerable<(LocalFunctionStatementSyntax Function, bool Misplaced)> GetViolations(SyntaxNode container, SourceText text)
	{
		var statements = GetStatements(container);
		var last = statements.FindLastIndex(statement => statement is not LocalFunctionStatementSyntax);

		for(var index = 0; index < statements.Count; index++)
		{
			if(statements[index] is not LocalFunctionStatementSyntax function || function.ContainsDiagnostics)
				continue;

			if(index < last)
				yield return (function, true);
			else if(index == 0 && HasLeadingCommentGap(container, statements, text))
				yield return (function, false);
			else if(index > 0 && !statements[index - 1].ContainsDiagnostics && RequiresSpacing(container, statements, index, text))
				yield return (function, false);
		}
	}

	private static bool HasLeadingCommentGap(SyntaxNode container, List<StatementSyntax> statements, SourceText text)
	{
		var function = statements[0];
		var comment = GetCommentStart(container, text, GetContentStart(container, statements), function.SpanStart);
		return comment < function.SpanStart && GetBlankLines(container, text, comment, function.SpanStart).Count > 0;
	}

	private static bool RequiresSpacing(SyntaxNode container, List<StatementSyntax> statements, int index, SourceText text)
	{
		var previous = statements[index - 1];
		var current = statements[index];
		var (minimum, maximum) = GetSpacing(container, statements, index, text);
		var lines = GetBlankLines(container, text, previous.Span.End, current.SpanStart);
		var comment = GetCommentStart(container, text, previous.Span.End, current.SpanStart);

		return lines.Count < minimum || lines.Count > maximum ||
			comment < current.SpanStart && lines.Any(line => line.Start >= comment);
	}

	internal static (int Minimum, int Maximum) GetSpacing(SyntaxNode container, List<StatementSyntax> statements, int index, SourceText text)
	{
		var function = (LocalFunctionStatementSyntax)statements[index];

		if(statements[index - 1] is LocalFunctionStatementSyntax previous)
			return previous.ExpressionBody == null ? (1, 1) : function.ExpressionBody != null ? (0, 0) : (0, 1);

		var body = statements.Take(index).Where(statement => statement is not LocalFunctionStatementSyntax).ToArray();
		var start = GetContentStart(container, statements);
		var singleLine = body.Length > 0 && SourceTextUtility.IsSingleLine(text, TextSpan.FromBounds(body[0].SpanStart, body[body.Length - 1].Span.End));
		var comments = container.DescendantTrivia(TextSpan.FromBounds(start, function.SpanStart))
			.Any(trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia) || IsStandaloneComment(text, trivia));

		return function.ExpressionBody != null && singleLine && !comments ? (0, 1) : (1, 1);
	}
	#endregion

	#region 文本边界
	internal static int GetContentStart(SyntaxNode container, List<StatementSyntax> statements) => container switch
	{
		BlockSyntax block => block.OpenBraceToken.Span.End,
		SwitchSectionSyntax section => section.Labels.Last().Span.End,
		_ => statements[0].FullSpan.Start,
	};

	internal static bool IsUnsafeTrivia(SyntaxTrivia trivia) => trivia.IsDirective ||
		trivia.IsKind(SyntaxKind.DisabledTextTrivia) || trivia.IsKind(SyntaxKind.SkippedTokensTrivia);

	private static bool IsComment(SyntaxTrivia trivia) => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
		trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
		trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

	private static bool IsStandaloneComment(SourceText text, SyntaxTrivia trivia) => IsComment(trivia) &&
		SourceTextUtility.IsHorizontalWhitespace(text, TextSpan.FromBounds(text.Lines.GetLineFromPosition(trivia.FullSpan.Start).Start, trivia.FullSpan.Start));

	internal static int GetCommentStart(SyntaxNode container, SourceText text, int start, int end)
	{
		foreach(var trivia in container.DescendantTrivia(TextSpan.FromBounds(start, end)))
		{
			if(IsStandaloneComment(text, trivia))
				return text.Lines.GetLineFromPosition(trivia.FullSpan.Start).Start;
		}

		return end;
	}

	internal static int GetStatementEnd(SyntaxNode container, SourceText text, StatementSyntax statement)
	{
		var end = text.Lines.GetLineFromPosition(statement.Span.End).EndIncludingLineBreak;

		//同一行开始的尾注释仍属于前一句，跨行时也不拆开。
		foreach(var trivia in container.DescendantTrivia(TextSpan.FromBounds(statement.Span.End, container.FullSpan.End)))
		{
			if(trivia.FullSpan.Start >= end)
				break;

			if(IsComment(trivia) && trivia.FullSpan.End > end)
				end = text.Lines.GetLineFromPosition(trivia.FullSpan.End - 1).EndIncludingLineBreak;
		}

		return end;
	}

	internal static List<TextLine> GetBlankLines(SyntaxNode container, SourceText text, int start, int end)
	{
		var lines = new List<TextLine>();
		var first = text.Lines.GetLineFromPosition(start).LineNumber + 1;
		var last = text.Lines.GetLineFromPosition(end).LineNumber;

		for(var index = first; index < last; index++)
		{
			var line = text.Lines[index];
			var trivia = container.FindTrivia(line.Start);

			if(string.IsNullOrWhiteSpace(line.ToString()) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
				!trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) && !trivia.IsKind(SyntaxKind.DisabledTextTrivia))
				lines.Add(line);
		}

		return lines;
	}
	#endregion

	#region 空行修复
	internal static bool TryGetSpacingChanges(SyntaxNode container, SourceText text, LocalFunctionStatementSyntax function, out List<TextChange> changes)
	{
		changes = new List<TextChange>();
		var statements = GetStatements(container);
		var index = statements.IndexOf(function);

		if(index < 0)
			return false;

		if(index == 0)
		{
			if(!HasLeadingCommentGap(container, statements, text))
				return false;

			var commentStart = GetCommentStart(container, text, GetContentStart(container, statements), function.SpanStart);

			if(container.DescendantTrivia(TextSpan.FromBounds(commentStart, function.SpanStart), descendIntoTrivia: true).Any(IsUnsafeTrivia))
				return false;

			changes.AddRange(GetBlankLines(container, text, commentStart, function.SpanStart).Select(line => new TextChange(line.SpanIncludingLineBreak, "")));
			return changes.Count > 0;
		}

		if(!RequiresSpacing(container, statements, index, text))
			return false;

		var previous = statements[index - 1];
		var span = TextSpan.FromBounds(previous.Span.End, function.SpanStart);

		if(container.DescendantTrivia(span, descendIntoTrivia: true).Any(IsUnsafeTrivia))
			return false;

		var lines = GetBlankLines(container, text, previous.Span.End, function.SpanStart);
		var (minimum, maximum) = GetSpacing(container, statements, index, text);
		var count = maximum == 0 ? 0 : minimum == 1 || lines.Count > 0 ? 1 : 0;
		var newline = SourceTextUtility.GetNewLine(text, previous.Span.End);
		var start = GetStatementEnd(container, text, previous);
		var comment = GetCommentStart(container, text, previous.Span.End, function.SpanStart);

		if(start > comment)
		{
			//只在两条声明之间都是水平空白时拆行，避免移动行内说明。
			if(!SourceTextUtility.IsHorizontalWhitespace(text, span))
				return false;

			changes.Add(new TextChange(span, newline + (count == 1 ? newline : "") + SourceTextUtility.GetIndentation(text, function.SpanStart)));
			return true;
		}

		foreach(var line in lines)
			changes.Add(new TextChange(line.SpanIncludingLineBreak, ""));

		if(count == 1)
		{
			//合并同一位置的删除与插入，保证多余空行和错位空行都能一次修正。
			var existing = changes.FindIndex(change => change.Span.Start == start);

			if(existing >= 0)
				changes[existing] = new TextChange(changes[existing].Span, newline);
			else
				changes.Add(new TextChange(new TextSpan(start, 0), newline));
		}

		return changes.Count > 0;
	}
	#endregion
}
