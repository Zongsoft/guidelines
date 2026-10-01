using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis;

/// <summary>统一普通命名空间引用的保留条件。</summary>
internal static class UsingDirectives
{
	public static bool IsOrdinaryNamespace(UsingDirectiveSyntax directive, SemanticModel model, CancellationToken cancellation) =>
		directive.Alias == null && directive.StaticKeyword.IsKind(SyntaxKind.None) &&
		directive.GetFirstToken().IsKind(SyntaxKind.UsingKeyword) && directive.Name != null &&
		model.GetSymbolInfo(directive.Name, cancellation).Symbol is INamespaceSymbol;
}
