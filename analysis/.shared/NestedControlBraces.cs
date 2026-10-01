using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis;

/// <summary>统一嵌套控制结构的直接语句体及同家族链例外。</summary>
internal static class NestedControlBraces
{
	public static StatementSyntax GetBody(SyntaxNode node) =>
		node is ElseClauseSyntax { Statement: IfStatementSyntax } ? null : ControlStatements.GetBody(node);

	public static bool RequiresBraces(SyntaxNode node)
	{
		var body = GetBody(node);

		if(body == null || !ControlStatements.IsControl(body))
			return false;

		return !((node is UsingStatementSyntax && body is UsingStatementSyntax) ||
			(node is LockStatementSyntax && body is LockStatementSyntax) ||
			(node is FixedStatementSyntax && body is FixedStatementSyntax));
	}
}
