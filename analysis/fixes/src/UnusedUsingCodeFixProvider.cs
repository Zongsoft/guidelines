using System;
using System.Linq;
using System.Composition;
using System.Threading.Tasks;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Fixes;

/// <summary>仅移除 ZS0005 指定的引用，保留其注释及条件编译指令。</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnusedUsingCodeFixProvider)), Shared]
public sealed class UnusedUsingCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ZS0005");
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
		var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

		foreach(var diagnostic in context.Diagnostics)
		{
			var directive = root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<UsingDirectiveSyntax>();

			if(directive == null || model == null || directive.ContainsDiagnostics ||
				directive.DescendantTrivia().Any(trivia => trivia.IsDirective && directive.Span.Contains(trivia.Span)) ||
				directive.Span != diagnostic.Location.SourceSpan)
				continue;

			//重新确认编译器诊断，避免对已变更的文档删除正在使用的引用。
			if(!model.Compilation.GetDiagnostics(context.CancellationToken)
				.Any(item => item.Id == "CS8019" && item.Location.SourceTree == model.SyntaxTree && item.Location.SourceSpan == directive.Span))
				continue;

			if(directive.Alias == null && directive.StaticKeyword.IsKind(SyntaxKind.None) &&
				directive.GetFirstToken().IsKind(SyntaxKind.UsingKeyword) &&
				directive.Name != null &&
				model.GetSymbolInfo(directive.Name, context.CancellationToken).Symbol is INamespaceSymbol)
				continue;

			var changes = GetChanges(directive, text);

			context.RegisterCodeFix(CodeAction.Create(Properties.Resources.RemoveUnusedUsing,
				cancellation => Task.FromResult(context.Document.WithText(text.WithChanges(changes))), "ZS0005.RemoveUsing"), diagnostic);
		}
	}

	private static ImmutableArray<TextChange> GetChanges(UsingDirectiveSyntax directive, SourceText text)
	{
		//引用内部的注释不能随节点一起删除，仅删除语法标记并保留原有 trivia。
		if(directive.DescendantTrivia().Any(trivia => directive.Span.Contains(trivia.Span) &&
			!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
			return directive.DescendantTokens().Select(token => new TextChange(token.Span, string.Empty)).ToImmutableArray();

		var first = text.Lines.GetLineFromPosition(directive.SpanStart);
		var last = text.Lines.GetLineFromPosition(directive.Span.End);
		var prefix = text.ToString(TextSpan.FromBounds(first.Start, directive.SpanStart));
		var suffix = text.ToString(TextSpan.FromBounds(directive.Span.End, last.End));

		if(string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(suffix))
			return ImmutableArray.Create(new TextChange(TextSpan.FromBounds(first.Start, last.EndIncludingLineBreak), string.Empty));

		var end = directive.Span.End;

		if(string.IsNullOrWhiteSpace(prefix))
		{
			while(end < last.End && (text[end] == ' ' || text[end] == '\t'))
				end++;
		}

		return ImmutableArray.Create(new TextChange(TextSpan.FromBounds(directive.SpanStart, end), string.Empty));
	}
}
