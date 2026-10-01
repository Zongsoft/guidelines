using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Composition;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Zongsoft.CodeAnalysis.Fixes;

/// <summary>在原语句列表内整理局部函数，保留源码片段、注释及普通语句顺序。</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LocalFunctionLayoutCodeFixProvider)), Shared]
public sealed class LocalFunctionLayoutCodeFixProvider : CodeFixProvider
{
	private const string EQUIVALENCE_KEY = "ZS2006.ArrangeLocalFunctions";
	private static readonly FixAllProvider _fixAll = new LocalFunctionFixAllProvider();

	public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ZS2006");
	public override FixAllProvider GetFixAllProvider() => _fixAll;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		foreach(var diagnostic in context.Diagnostics)
		{
			var document = await FixAsync(context.Document, diagnostic.Location.SourceSpan.Start, context.CancellationToken).ConfigureAwait(false);

			if(document != null)
				context.RegisterCodeFix(CodeAction.Create(Properties.Resources.ArrangeLocalFunctions,
					cancellation => Task.FromResult(document), EQUIVALENCE_KEY), diagnostic);
		}
	}

	private static async Task<Document> FixAsync(Document document, int position, CancellationToken cancellation)
	{
		var root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
		var text = await document.GetTextAsync(cancellation).ConfigureAwait(false);
		var function = root?.FindToken(position).Parent?.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();

		if(function == null || function.SpanStart != position)
			return null;

		var container = LocalFunctionLayout.GetContainer(function);
		var violation = LocalFunctionLayout.GetViolations(container, text).FirstOrDefault(item => item.Function == function);

		if(violation.Function == null)
			return null;

		if(!violation.Misplaced)
			return LocalFunctionLayout.TryGetSpacingChanges(container, text, function, out var changes) ? document.WithText(text.WithChanges(changes)) : null;

		if(!TryMove(container, text, out var change))
			return null;

		var model = await document.GetSemanticModelAsync(cancellation).ConfigureAwait(false);

		//移位会改变物理行号；不自动改变隐式 CallerLineNumber 实参。
		foreach(var expression in root.DescendantNodes().Where(node => node is InvocationExpressionSyntax or
			ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax or ElementAccessExpressionSyntax or ImplicitElementAccessSyntax))
		{
			cancellation.ThrowIfCancellationRequested();
			var arguments = model.GetOperation(expression, cancellation) switch
			{
				IInvocationOperation invocation => invocation.Arguments,
				IObjectCreationOperation creation => creation.Arguments,
				IPropertyReferenceOperation property => property.Arguments,
				_ => ImmutableArray<IArgumentOperation>.Empty,
			};

			if(arguments.Any(argument => argument.IsImplicit && argument.Parameter != null && argument.Parameter.GetAttributes().Any(attribute =>
					attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.CallerLineNumberAttribute")))
				return null;
		}

		var result = document.WithText(text.WithChanges(change));
		var compilation = await result.Project.GetCompilationAsync(cancellation).ConfigureAwait(false);

		//作用域、捕获和确定赋值由真实编译核对，原本无法编译的项目不猜测移动结果。
		if(model.Compilation.GetDiagnostics(cancellation).Any(item => item.Severity == DiagnosticSeverity.Error) ||
			compilation.GetDiagnostics(cancellation).Any(item => item.Severity == DiagnosticSeverity.Error))
			return null;

		return result;
	}

	private static bool TryMove(SyntaxNode container, SourceText text, out TextChange change)
	{
		change = default;
		var statements = LocalFunctionLayout.GetStatements(container);
		var firstFunction = statements.FindIndex(statement => statement is LocalFunctionStatementSyntax);

		if(firstFunction < 0 || container.ContainsDiagnostics || container.DescendantTrivia(descendIntoTrivia: true).Any(LocalFunctionLayout.IsUnsafeTrivia) ||
			statements.Skip(firstFunction).Any(statement => statement is LocalDeclarationStatementSyntax declaration && declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)))
			return false;

		var units = new List<(StatementSyntax Statement, int Index, int Start, int End, string Content)>();
		var previousEnd = LocalFunctionLayout.GetContentStart(container, statements);

		for(var index = 0; index < statements.Count; index++)
		{
			var statement = statements[index];
			var line = text.Lines.GetLineFromPosition(statement.SpanStart);
			var start = LocalFunctionLayout.GetCommentStart(container, text, previousEnd, statement.SpanStart);

			if(!SourceTextUtility.IsHorizontalWhitespace(text, TextSpan.FromBounds(line.Start, statement.SpanStart)))
				return false;

			start = start == statement.SpanStart ? line.Start : start;
			var end = LocalFunctionLayout.GetStatementEnd(container, text, statement);

			//说明与声明之间已有空行时，无法可靠判断注释属于哪一段。
			if(start < line.Start && LocalFunctionLayout.GetBlankLines(container, text, start, statement.SpanStart).Count > 0 ||
				index > 0 && start < units[index - 1].End || end > statement.GetLastToken().GetNextToken().SpanStart)
				return false;

			units.Add((statement, index, start, end, text.ToString(TextSpan.FromBounds(start, end))));
			previousEnd = end;
		}

		var builder = new StringBuilder();
		var newline = SourceTextUtility.GetNewLine(text, statements[0].SpanStart);
		var body = units.Where(unit => unit.Statement is not LocalFunctionStatementSyntax).ToArray();

		for(var index = 0; index < body.Length; index++)
		{
			if(index > 0)
			{
				if(body[index - 1].Index + 1 == body[index].Index)
					builder.Append(text.ToString(TextSpan.FromBounds(body[index - 1].End, body[index].Start)));
				else if(StatementSpacing.RequiresSeparation(body[index - 1].Statement, body[index].Statement, text, 1))
					builder.Append(newline);
			}

			builder.Append(body[index].Content);
		}

		LocalFunctionStatementSyntax previous = null;

		if(builder.Length > 0 && builder[builder.Length - 1] != '\n')
			builder.Append(newline);

		foreach(var unit in units.Where(unit => unit.Statement is LocalFunctionStatementSyntax))
		{
			var current = (LocalFunctionStatementSyntax)unit.Statement;

			if(previous == null ? body.Length > 0 : previous.ExpressionBody == null)
				builder.Append(newline);

			builder.Append(unit.Content);
			previous = current;
		}

		change = new TextChange(TextSpan.FromBounds(units[0].Start, units[units.Count - 1].End), builder.ToString());

		//局部函数原本会中断普通语句计数；移走后重新计数，只补齐新形成的语句边界。
		var originalMissing = StatementSpacing.GetMissingBlankLines(container, text).Select(boundary => boundary.Current).ToArray();
		var movedText = text.WithChanges(change);
		var movedRoot = CSharpSyntaxTree.ParseText(movedText, (CSharpParseOptions)container.SyntaxTree.Options).GetRoot();
		var movedContainer = container is CompilationUnitSyntax ? movedRoot : movedRoot.DescendantNodes()
			.FirstOrDefault(node => node.RawKind == container.RawKind && node.SpanStart == container.SpanStart);

		if(movedContainer == null)
			return false;

		var movedStatements = LocalFunctionLayout.GetStatements(movedContainer);
		var separators = new List<TextChange>();

		foreach(var boundary in StatementSpacing.GetMissingBlankLines(movedContainer, movedText))
		{
			var index = movedStatements.IndexOf(boundary.Current);

			if(index >= 0 && index < body.Length && !originalMissing.Contains(body[index].Statement))
				separators.Add(new TextChange(new TextSpan(LocalFunctionLayout.GetStatementEnd(movedContainer, movedText, boundary.Previous), 0), newline));
		}

		if(separators.Count > 0)
			change = new TextChange(change.Span, movedText.WithChanges(separators).ToString(new TextSpan(change.Span.Start, change.NewText.Length + separators.Sum(separator => separator.NewText.Length))));

		return text.ToString(change.Span) != change.NewText;
	}

	private sealed class LocalFunctionFixAllProvider : FixAllProvider
	{
		public override Task<CodeAction> GetFixAsync(FixAllContext context) => Task.FromResult(CodeAction.Create(
			Properties.Resources.ArrangeLocalFunctions, cancellation => FixAllAsync(context, cancellation), EQUIVALENCE_KEY));

		private static async Task<Solution> FixAllAsync(FixAllContext context, CancellationToken cancellation)
		{
			var solution = context.Solution;
			var ids = context.Scope switch
			{
				FixAllScope.Document => ImmutableArray.Create(context.Document.Id),
				FixAllScope.Project => context.Project.DocumentIds.ToImmutableArray(),
				FixAllScope.Solution => solution.Projects.SelectMany(project => project.DocumentIds).ToImmutableArray(),
				_ => ImmutableArray<DocumentId>.Empty,
			};

			//先读取整个范围的原始诊断；VS 与 dotnet format 的提供器允许返回固定快照。
			var work = new List<(DocumentId Id, ImmutableArray<Diagnostic> Diagnostics)>();

			foreach(var id in ids)
			{
				var diagnostics = await context.GetDocumentDiagnosticsAsync(context.Solution.GetDocument(id)).ConfigureAwait(false);
				work.Add((id, diagnostics));
			}

			foreach(var item in work)
			{
				var document = solution.GetDocument(item.Id);
				var root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
				var pending = item.Diagnostics.Select(diagnostic => (Position: diagnostic.Location.SourceSpan.Start,
					Depth: root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.Ancestors().Count()))
					.OrderByDescending(target => target.Depth).ThenByDescending(target => target.Position).ToList();

				while(pending.Count > 0)
				{
					cancellation.ThrowIfCancellationRequested();
					var target = pending[0];
					pending.RemoveAt(0);
					var result = await FixAsync(document, target.Position, cancellation).ConfigureAwait(false);

					if(result == null)
						continue;

					var previousText = await document.GetTextAsync(cancellation).ConfigureAwait(false);
					var currentText = await result.GetTextAsync(cancellation).ConfigureAwait(false);
					var changes = currentText.GetTextChanges(previousText);

					//整组移位已经处理同列表函数；其他授权位置按实际文本变更继续跟踪。
					for(var index = pending.Count - 1; index >= 0; index--)
					{
						var position = pending[index].Position;

						if(changes.Any(change => change.Span.Contains(position)))
							pending.RemoveAt(index);
						else
							pending[index] = (position + changes.Where(change => change.Span.End <= position)
								.Sum(change => change.NewText.Length - change.Span.Length), pending[index].Depth);
					}

					document = result;
				}

				solution = document.Project.Solution;
			}

			return solution;
		}
	}
}
