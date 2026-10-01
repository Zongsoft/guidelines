using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis;

/// <summary>共享控制语句的语法分类及直接语句体，不包含各规则的布局例外。</summary>
internal static class ControlStatements
{
	public static StatementSyntax GetBody(SyntaxNode node) => node switch
	{
		IfStatementSyntax statement => statement.Statement,
		ElseClauseSyntax clause => clause.Statement,
		ForStatementSyntax statement => statement.Statement,
		CommonForEachStatementSyntax statement => statement.Statement,
		WhileStatementSyntax statement => statement.Statement,
		DoStatementSyntax statement => statement.Statement,
		UsingStatementSyntax statement => statement.Statement,
		LockStatementSyntax statement => statement.Statement,
		FixedStatementSyntax statement => statement.Statement,
		_ => null,
	};

	public static bool IsConditionalOrLoop(StatementSyntax statement) => statement is
		IfStatementSyntax or ForStatementSyntax or CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or SwitchStatementSyntax;

	public static bool IsControl(StatementSyntax statement) => IsConditionalOrLoop(statement) || statement is
		UsingStatementSyntax or LockStatementSyntax or FixedStatementSyntax or TryStatementSyntax or CheckedStatementSyntax or UnsafeStatementSyntax;
}
