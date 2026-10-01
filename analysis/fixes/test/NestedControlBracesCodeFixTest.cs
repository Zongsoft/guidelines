using System;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace Zongsoft.CodeAnalysis.Fixes.Tests;

public partial class CodeFixTest
{
	[Theory]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\tif(flag)\n\t\tGC.KeepAlive(items);", "for(var index = 0; index < items.Length; index++)\n{\n\tif(flag)\n\t\tGC.KeepAlive(items);\n}")]
	[InlineData("if(flag)\n\tforeach(var item in items)\n\t\tGC.KeepAlive(item);", "if(flag)\n{\n\tforeach(var item in items)\n\t\tGC.KeepAlive(item);\n}")]
	[InlineData("foreach(var (first, second) in new[] { (1, 2) })\n\tif(flag)\n\t\tGC.KeepAlive(first + second);", "foreach(var (first, second) in new[] { (1, 2) })\n{\n\tif(flag)\n\t\tGC.KeepAlive(first + second);\n}")]
	[InlineData("while(flag)\n\tif(items.Length > 0)\n\t{\n\t\tflag = false;\n\t}", "while(flag)\n{\n\tif(items.Length > 0)\n\t{\n\t\tflag = false;\n\t}\n}")]
	[InlineData("if(flag)\n\tGC.KeepAlive(items);\nelse\n\tforeach(var item in items)\n\t\tGC.KeepAlive(item);", "if(flag)\n\tGC.KeepAlive(items);\nelse\n{\n\tforeach(var item in items)\n\t\tGC.KeepAlive(item);\n}")]
	[InlineData("do\n\tif(flag)\n\t\tGC.KeepAlive(items);\nwhile(false);", "do\n{\n\tif(flag)\n\t\tGC.KeepAlive(items);\n}\nwhile(false);")]
	[InlineData("for(var index = 0; index < items.Length; index++) // header\n\t// body\n\tif(flag)\n\t\tGC.KeepAlive(items); // trailing", "for(var index = 0; index < items.Length; index++) // header\n{\n\t// body\n\tif(flag)\n\t\tGC.KeepAlive(items); // trailing\n}")]
	[InlineData("for(var index = 0; index < items.Length; index++) /* header\ncontinued */\n\tif(flag)\n\t\tGC.KeepAlive(items);", "for(var index = 0; index < items.Length; index++) /* header\ncontinued */\n{\n\tif(flag)\n\t\tGC.KeepAlive(items);\n}")]
	[InlineData("if(flag) if(items.Length > 0) GC.KeepAlive(items);", "if(flag)\n{\n\tif(items.Length > 0) GC.KeepAlive(items);\n}")]
	[InlineData("if(flag) while(flag)\n\tflag = false;", "if(flag)\n{\n\twhile(flag)\n\t\tflag = false;\n}")]
	[InlineData("lock(items)\nusing(var stream = new System.IO.MemoryStream())\n{\n\tGC.KeepAlive(items);\n}", "lock(items)\n{\n\tusing(var stream = new System.IO.MemoryStream())\n\t{\n\t\tGC.KeepAlive(items);\n\t}\n}")]
	public async Task NestedControlAddsOuterBracesPreservingLayoutAsync(string before, string after)
	{
		foreach(var newline in new[] { "\r\n", "\n" })
		{
			using var workspace = new AdhocWorkspace();
			var document = AddNestedDocument(workspace.CurrentSolution, NestedWrap(before).Replace("\n", newline));
			var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS2005"));
			var action = Assert.Single(await GetActionsAsync(document, diagnostic));

			var result = await ApplyAsync(action, document.Id);

			Assert.Equal("ZS2005.AddBraces", action.EquivalenceKey);

			Assert.Equal(NestedWrap(after).Replace("\n", newline), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
			await AssertCleanAsync(result, "ZS2005");
		}
	}

	[Fact]
	public async Task NestedControlPreservesDanglingElseAssociationAsync()
	{
		using var workspace = new AdhocWorkspace();
		var source = NestedWrap("if(flag)\n\tif(items.Length > 0)\n\t\tGC.KeepAlive(items);\n\telse\n\t\tGC.KeepAlive(flag);");
		var document = AddNestedDocument(workspace.CurrentSolution, source);

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2005")))), document.Id);

		var branches = (await result.GetSyntaxRootAsync(TestContext.Current.CancellationToken)).DescendantNodes().OfType<IfStatementSyntax>().ToArray();
		Assert.Null(branches[0].Else);
		Assert.IsType<BlockSyntax>(branches[0].Statement);
		Assert.NotNull(branches[1].Else);
		Assert.Equal("GC.KeepAlive(flag);", branches[1].Else.Statement.ToString());
		await AssertCleanAsync(result, "ZS2005");
	}

	[Fact]
	public async Task NestedControlKeepsOuterElseAttachedAfterWrappingInnerBranchAsync()
	{
		using var workspace = new AdhocWorkspace();
		var source = NestedWrap("if(flag)\n\tif(items.Length > 0)\n\t\tGC.KeepAlive(items);\n\telse\n\t\tGC.KeepAlive(flag);\nelse\n\tGC.KeepAlive(0);");
		var document = AddNestedDocument(workspace.CurrentSolution, source);

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2005")))), document.Id);

		var branches = (await result.GetSyntaxRootAsync(TestContext.Current.CancellationToken)).DescendantNodes().OfType<IfStatementSyntax>().ToArray();
		Assert.Equal("GC.KeepAlive(0);", branches[0].Else.Statement.ToString());
		Assert.Equal("GC.KeepAlive(flag);", branches[1].Else.Statement.ToString());
		await AssertCleanAsync(result, "ZS2005");
	}

	[Theory]
	[InlineData("while(flag)\n#if true\n\tif(flag)\n\t\tflag = false;\n#endif")]
	[InlineData("while(flag)\n\tif(flag)\n\t{\n#if true\n\t\tflag = false;\n#endif\n\t}")]
	[InlineData("while(flag)\n#if false\n\tGC.KeepAlive(items);\n#else\n\tif(flag)\n\t\tflag = false;\n#endif")]
	[InlineData("while(flag\n\tif(flag)\n\t\tflag = false;")]
	[InlineData("while(flag) /* header\ncontinued */ if(flag)\n\tflag = false;")]
	public async Task NestedControlDoesNotFixUnsafeDirectiveOrSyntaxBoundaryAsync(string body)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddNestedDocument(workspace.CurrentSolution, NestedWrap(body));
		var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS2005"));

		Assert.Empty(await GetActionsAsync(document, diagnostic));
	}

	[Fact]
	public async Task NestedControlAllowsDirectiveOutsideWrappedRangeAsync()
	{
		using var workspace = new AdhocWorkspace();
		var body = "#if true\nwhile(flag)\n\tif(flag)\n\t\tflag = false;\n#endif";
		var document = AddNestedDocument(workspace.CurrentSolution, NestedWrap(body));

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2005")))), document.Id);

		await AssertCleanAsync(result, "ZS2005");
		Assert.Contains("#if true", (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
	}

	[Theory]
	[InlineData("while(flag)\n{\n\tif(flag)\n\t\tflag = false;\n}")]
	[InlineData("while(flag)\n\tflag = false;")]
	[InlineData("using(var first = new System.IO.MemoryStream())\nusing(var second = new System.IO.MemoryStream())\n\tGC.KeepAlive(items);")]
	public async Task NestedControlDoesNotApplyStaleDiagnosticAsync(string body)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddNestedDocument(workspace.CurrentSolution, NestedWrap(body));
		var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
		var descriptor = GetAnalyzer("ZS2005").SupportedDiagnostics.Single();
		var header = root.DescendantNodes().OfType<StatementSyntax>().First(statement => statement is WhileStatementSyntax or UsingStatementSyntax);
		var diagnostic = Diagnostic.Create(descriptor, header.GetFirstToken().GetLocation());

		Assert.Empty(await GetActionsAsync(document, diagnostic));
	}

	[Theory]
	[InlineData(FixAllScope.Document)]
	[InlineData(FixAllScope.Project)]
	[InlineData(FixAllScope.Solution)]
	public async Task NestedControlFixAllRespectsScopeAndFixesDeepEdgesAsync(FixAllScope scope)
	{
		using var workspace = new AdhocWorkspace();
		var source = NestedWrap("for(var index = 0; index < items.Length; index++)\n\tif(flag)\n\t\twhile(flag)\n\t\t\tflag = false;");
		var first = AddNestedDocument(workspace.CurrentSolution, source);
		var second = AddNestedDocument(first.Project.Solution, source.Replace("class Sample", "class Second"), first.Project.Id);
		var third = AddNestedDocument(second.Project.Solution, source.Replace("class Sample", "class Third"));
		var solution = third.Project.Solution;
		first = solution.GetDocument(first.Id);
		var provider = GetProvider("ZS2005");
		var action = Assert.Single(await GetActionsAsync(first, (await GetDiagnosticsAsync(first, "ZS2005"))[0]));
		var context = new FixAllContext(first, provider, scope, action.EquivalenceKey, new[] { "ZS2005" }, new Diagnostics("ZS2005"), TestContext.Current.CancellationToken);

		var result = await ApplyAsync(await provider.GetFixAllProvider().GetFixAsync(context), first.Id);

		foreach(var id in new[] { first.Id, second.Id, third.Id })
		{
			var document = result.Project.Solution.GetDocument(id);
			var shouldChange = scope == FixAllScope.Solution || id == first.Id || scope == FixAllScope.Project && document.Project.Id == first.Project.Id;

			if(shouldChange)
				await AssertCleanAsync(document, "ZS2005");
			else
				Assert.Equal(2, (await GetDiagnosticsAsync(document, "ZS2005")).Length);
		}
	}

	[Fact]
	public async Task NestedControlFixAllHandlesSeveralEdgesOnOneLineAsync()
	{
		using var workspace = new AdhocWorkspace();
		var document = AddNestedDocument(workspace.CurrentSolution, NestedWrap("for(var index = 0; index < items.Length; index++) if(flag) while(flag) flag = false;"));
		var provider = GetProvider("ZS2005");
		var action = Assert.Single(await GetActionsAsync(document, (await GetDiagnosticsAsync(document, "ZS2005"))[0]));
		var context = new FixAllContext(document, provider, FixAllScope.Document, action.EquivalenceKey, new[] { "ZS2005" }, new Diagnostics("ZS2005"), TestContext.Current.CancellationToken);

		var result = await ApplyAsync(await provider.GetFixAllProvider().GetFixAsync(context), document.Id);

		await AssertCleanAsync(result, "ZS2005");
		Assert.Equal(2, (await result.GetSyntaxRootAsync(TestContext.Current.CancellationToken)).DescendantNodes().OfType<BlockSyntax>().Count(block => block.Parent is ForStatementSyntax or IfStatementSyntax));
	}

	[Theory]
	[InlineData("await using(var stream = new System.IO.MemoryStream())")]
	[InlineData("await foreach(var item in values)")]
	public async Task NestedControlFixesAsyncBodiesAsync(string header)
	{
		using var workspace = new AdhocWorkspace();
		var before = header + "\n\tif(flag)\n\t\tGC.KeepAlive(items);";
		var after = header + "\n{\n\tif(flag)\n\t\tGC.KeepAlive(items);\n}";
		var source = NestedWrap(before).Replace("unsafe void", "async System.Threading.Tasks.Task")
			.Replace("int[] items)", "int[] items, System.Collections.Generic.IAsyncEnumerable<int> values)");
		var expected = NestedWrap(after).Replace("unsafe void", "async System.Threading.Tasks.Task")
			.Replace("int[] items)", "int[] items, System.Collections.Generic.IAsyncEnumerable<int> values)");
		var document = AddNestedDocument(workspace.CurrentSolution, source);
		var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS2005"));

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, diagnostic)), document.Id);

		Assert.Equal(expected, (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2005");
	}

	[Theory]
	[InlineData("@\"first\nsecond\"")]
	[InlineData("\"\"\"\nfirst\nsecond\n\"\"\"")]
	public async Task NestedControlPreservesMultilineStringValuesAsync(string literal)
	{
		using var workspace = new AdhocWorkspace();
		var source = NestedWrap("if(flag) while(flag)\n\tGC.KeepAlive(" + literal + ");");
		var document = AddNestedDocument(workspace.CurrentSolution, source);
		var original = (await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken)).DescendantNodes().OfType<LiteralExpressionSyntax>().Single().Token;

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2005")))), document.Id);

		var actual = (await result.GetSyntaxRootAsync(TestContext.Current.CancellationToken)).DescendantNodes().OfType<LiteralExpressionSyntax>().Single().Token;
		Assert.Equal(original.Text, actual.Text);
		Assert.Equal(original.ValueText, actual.ValueText);
		await AssertCleanAsync(result, "ZS2005");
	}

	[Fact]
	public async Task NestedControlPreservesMultilineCommentContentAsync()
	{
		using var workspace = new AdhocWorkspace();
		var before = "lock(items)\nusing(var stream = new System.IO.MemoryStream())\n{\n\t/* first\ncomment content */\n\tGC.KeepAlive(items);\n}";
		var after = "lock(items)\n{\n\tusing(var stream = new System.IO.MemoryStream())\n\t{\n\t\t/* first\ncomment content */\n\t\tGC.KeepAlive(items);\n\t}\n}";
		var document = AddNestedDocument(workspace.CurrentSolution, NestedWrap(before));

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2005")))), document.Id);

		Assert.Equal(NestedWrap(after), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2005");
	}

	[Fact]
	public async Task NestedControlExamplesAgreeWithDocumentationAsync()
	{
		var examples = new string[2][];
		var files = new[] { "RULES.md", "RULES.zh-Hans.md" };

		for(var index = 0; index < files.Length; index++)
		{
			var text = ReadPackageText(files[index]);
			var start = text.IndexOf("<a id=\"zs2005\"></a>", StringComparison.Ordinal);
			var end = text.IndexOf("<a id=\"zs2006\"></a>", start, StringComparison.Ordinal);
			examples[index] = Regex.Matches(text[start..end], @"```csharp\r?\n(.*?)\r?\n```", RegexOptions.Singleline)
				.Select(match => match.Groups[1].Value.Replace("\r\n", "\n")).ToArray();
		}

		Assert.NotEmpty(examples[0]);
		Assert.Equal(examples[0], examples[1]);

		foreach(var body in examples[0])
		{
			using var workspace = new AdhocWorkspace();
			var source = "class Sample\n{\n\tstatic void Process(int value) { }\n\tstatic void Complete() { }\n" +
				"\tstatic unsafe void Run(int[] items, bool enabled, object sync, object firstSync, object secondSync)\n\t{\n\t\t" +
				body.Replace("\n", "\n\t\t") + "\n\t}\n}\n";
			var document = AddNestedDocument(workspace.CurrentSolution, source);
			var expected = SourceText.From(source).Lines.Where(line => line.ToString().Contains("//ZS2005", StringComparison.Ordinal))
				.Select(line => line.Start + line.ToString().Length - line.ToString().TrimStart().Length).ToArray();
			var actual = await GetDiagnosticsAsync(document, "ZS2005");

			Assert.Equal(expected, actual.Select(diagnostic => diagnostic.Location.SourceSpan.Start).ToArray());
		}
	}

	[Theory]
	[InlineData("en-US", "Add braces", "Enclose the outer statement body of this nested control structure in braces")]
	[InlineData("zh-Hans", "添加花括号", "使用花括号包裹此嵌套控制结构的外层语句体")]
	public async Task NestedControlLocalizesFixAndDiagnosticAsync(string language, string title, string message)
	{
		var original = CultureInfo.CurrentUICulture;

		try
		{
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
			using var workspace = new AdhocWorkspace();
			var document = AddNestedDocument(workspace.CurrentSolution, NestedWrap("while(flag)\n\tif(flag)\n\t\tflag = false;"));
			var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS2005"));

			Assert.Equal(title, Assert.Single(await GetActionsAsync(document, diagnostic)).Title);
			Assert.Equal(message, diagnostic.GetMessage(CultureInfo.CurrentUICulture));
		}
		finally
		{
			CultureInfo.CurrentUICulture = original;
		}
	}

	private static string NestedWrap(string body) => "using System;\n\nclass Sample\n{\n\tstatic unsafe void Run(bool flag, int[] items)\n\t{\n\t\t" + body.Replace("\n", "\n\t\t") + "\n\t}\n}\n";

	private static Document AddNestedDocument(Solution solution, string source, ProjectId projectId = null)
	{
		var document = AddDocument(solution, source, projectId);
		return document.Project.WithCompilationOptions(((CSharpCompilationOptions)document.Project.CompilationOptions).WithAllowUnsafe(true)).GetDocument(document.Id);
	}
}
