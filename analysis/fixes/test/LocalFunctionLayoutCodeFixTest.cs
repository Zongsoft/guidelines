using System;
using System.Linq;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Zongsoft.CodeAnalysis.Fixes.Tests;

public partial class CodeFixTest
{
	[Theory]
	[InlineData("_ = 0;\n\n\t\tstatic int Get() => 1;\n\n\t\t_ = Get();", "_ = 0;\n\t\t_ = Get();\n\n\t\tstatic int Get() => 1;")]
	[InlineData("_ = 0;\n\n\t\t// function\n\t\tstatic int Get() => 1; // tail\n\n\t\t_ = Get();", "_ = 0;\n\t\t_ = Get();\n\n\t\t// function\n\t\tstatic int Get() => 1; // tail")]
	[InlineData("static int Get() => Other();\n\t\t_ = Get();\n\n\t\tstatic int Other() => 1;\n\t\t_ = Other();", "_ = Get();\n\t\t_ = Other();\n\n\t\tstatic int Get() => Other();\n\t\tstatic int Other() => 1;")]
	[InlineData("var value = 1;\n\t\tint Get() => value;\n\t\t_ = Get();", "var value = 1;\n\t\t_ = Get();\n\n\t\tint Get() => value;")]
	[InlineData("_ = 0;\n\t\t_ = Get();\n\t\tstatic int Get() => 1;", "_ = 0;\n\t\t_ = Get();\n\n\t\tstatic int Get() => 1;")]
	[InlineData("_ = Get();\n\n\n\t\tstatic int Get() => 1;", "_ = Get();\n\n\t\tstatic int Get() => 1;")]
	[InlineData("_ = Get();\n\t\t// function\n\n\t\tstatic int Get() => 1;", "_ = Get();\n\n\t\t// function\n\t\tstatic int Get() => 1;")]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() => Other();\n\n\t\tstatic int Other() => 1;", "_ = Get();\n\n\t\tstatic int Get() => Other();\n\t\tstatic int Other() => 1;")]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() { return Other(); }\n\t\tstatic int Other() => 1;", "_ = Get();\n\n\t\tstatic int Get() { return Other(); }\n\n\t\tstatic int Other() => 1;")]
	[InlineData("_ = 0; /* tail\n\t\tcontinued */\n\t\t_ = Get();\n\t\tstatic int Get() => 1;", "_ = 0; /* tail\n\t\tcontinued */\n\t\t_ = Get();\n\n\t\tstatic int Get() => 1;")]
	[InlineData("_ = Get(); /* first\n\t\tcontinued */ /* second\n\t\tcontinued */\n\t\tstatic int Get() { return 1; }", "_ = Get(); /* first\n\t\tcontinued */ /* second\n\t\tcontinued */\n\n\t\tstatic int Get() { return 1; }")]
	[InlineData("static int Get() => 1; /* first\n\t\tcontinued */ /* second\n\t\tcontinued */\n\t\t_ = Get();", "_ = Get();\n\n\t\tstatic int Get() => 1; /* first\n\t\tcontinued */ /* second\n\t\tcontinued */")]
	[InlineData("// function\n\n\t\tstatic int Get() => 1;", "// function\n\t\tstatic int Get() => 1;")]
	[InlineData("_ = Get();\n\t\t/// <summary>Function.</summary>\n\t\tstatic int Get() => 1;", "_ = Get();\n\n\t\t/// <summary>Function.</summary>\n\t\tstatic int Get() => 1;")]
	[InlineData("/// <summary>Function.</summary>\n\n\t\tstatic int Get() => 1;", "/// <summary>Function.</summary>\n\t\tstatic int Get() => 1;")]
	[InlineData("/// <summary>Function.</summary>\n\t\tstatic int Get() => 1;\n\t\t_ = Get();", "_ = Get();\n\n\t\t/// <summary>Function.</summary>\n\t\tstatic int Get() => 1;")]
	public async Task LocalFunctionFixPreservesExactSourceAsync(string before, string after)
	{
		foreach(var newline in new[] { "\r\n", "\n" })
		{
			using var workspace = new AdhocWorkspace();
			var document = AddDocument(workspace.CurrentSolution, Wrap(before).Replace("\n", newline));
			var diagnostic = (await GetDiagnosticsAsync(document, "ZS2006"))[0];
			var action = Assert.Single(await GetActionsAsync(document, diagnostic));
			var result = await ApplyAsync(action, document.Id);

			Assert.Equal("ZS2006.ArrangeLocalFunctions", action.EquivalenceKey);
			Assert.Equal(Wrap(after).Replace("\n", newline), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
			await AssertCleanAsync(result, "ZS2006");
		}
	}

	[Theory]
	[InlineData("#if true\n\t\tstatic int Get() => 1;\n\t\t_ = Get();\n#endif")]
	[InlineData("static int Get() => 1;\n\t\tusing var stream = new System.IO.MemoryStream();\n\t\t_ = Get();")]
	[InlineData("// uncertain\n\n\t\tstatic int Get() => 1;\n\t\t_ = Get();")]
	[InlineData("static int Get() => 1;\n\t\t_ = ;")]
	public async Task LocalFunctionFixRejectsUnsafeMovesAsync(string body)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, Wrap(body));
		var diagnostics = await GetDiagnosticsAsync(document, "ZS2006");
		Assert.NotEmpty(diagnostics);

		foreach(var diagnostic in diagnostics)
			Assert.Empty(await GetActionsAsync(document, diagnostic));

		Assert.Equal(Wrap(body), (await document.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
	}

	[Fact]
	public async Task LocalFunctionFixKeepsCapturedResourceInItsOriginalScopeAsync()
	{
		var before = "using var stream = new System.IO.MemoryStream();\n\t\tlong Get() => stream.Length;\n\t\t_ = Get();";
		var after = "using var stream = new System.IO.MemoryStream();\n\t\t_ = Get();\n\n\t\tlong Get() => stream.Length;";
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, Wrap(before));
		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006")))), document.Id);

		Assert.Equal(Wrap(after), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2006");
	}

	[Theory]
	[InlineData("static string Get() => @\"first\n  second\";")]
	[InlineData("static string Get() =>\n\t\t\t\"\"\"\n\t\t\tfirst\n\t\t\t  second\n\t\t\t\"\"\";")]
	public async Task LocalFunctionFixPreservesMultilineStringContentsAsync(string function)
	{
		var before = function + "\n\t\t_ = Get();";
		var after = "_ = Get();\n\n\t\t" + function;
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, Wrap(before));
		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006")))), document.Id);

		Assert.Equal(Wrap(after), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2006");
	}

	[Fact]
	public async Task LocalFunctionFixKeepsTopLevelFunctionsBeforeTypeDeclarationsAsync()
	{
		var before = "static int Get() => 1;\n_ = 0;\n_ = Get();\nclass Sample { }\n";
		var after = "_ = 0;\n_ = Get();\n\nstatic int Get() => 1;\nclass Sample { }\n";
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, before);
		document = document.Project.Solution.WithProjectCompilationOptions(document.Project.Id, new CSharpCompilationOptions(OutputKind.ConsoleApplication)).GetDocument(document.Id);
		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006")))), document.Id);

		Assert.Equal(after, (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2006");
	}

	[Fact]
	public async Task LocalFunctionFixRejectsImplicitCallerLineNumberAsync()
	{
		var source = Wrap("static int Get() => Capture();\n\t\t_ = Get();") +
			"static class Helpers { public static int Capture([System.Runtime.CompilerServices.CallerLineNumber] int line = 0) => line; }";
		source = source.Replace("=> Capture()", "=> Helpers.Capture()");
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);

		Assert.Empty(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006"))));
	}

	[Theory]
	[InlineData("static Marker Get() => new Marker();", "class Marker { public Marker([System.Runtime.CompilerServices.CallerLineNumber] int line = 0) { } }")]
	[InlineData("static Marker Get() => new();", "class Marker { public Marker([System.Runtime.CompilerServices.CallerLineNumber] int line = 0) { } }")]
	[InlineData("static int Get() => new Marker()[1];", "class Marker { public int this[int index, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0] => line; }")]
	[InlineData("static Marker Get() => new Marker { [1] = 1 };", "class Marker { public int this[int index, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0] { get => line; set { } } }")]
	public async Task LocalFunctionFixRejectsCallerLineNumberInConstructorsAndIndexersAsync(string function, string type)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, Wrap(function + "\n\t\t_ = Get();") + type);
		var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS2006"));

		Assert.Empty(await GetActionsAsync(document, diagnostic));
		Assert.DoesNotContain((await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken)).GetDiagnostics(TestContext.Current.CancellationToken), item => item.Severity == DiagnosticSeverity.Error);
	}

	[Fact]
	public async Task LocalFunctionFixRejectsChangingCallerLineNumberAfterAnInnerBlockAsync()
	{
		var source = Wrap("{\n\t\t\tstatic int Get() => 1;\n\t\t\t_ = Get();\n\t\t}\n\n\t\t_ = Helpers.Capture();") +
			"static class Helpers { public static int Capture([System.Runtime.CompilerServices.CallerLineNumber] int line = 0) => line; }";
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);

		Assert.Empty(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006"))));
	}

	[Fact]
	public async Task LocalFunctionFixSeparatesNewOrdinaryStatementGroupsAsync()
	{
		var before = "var a = 1;\n\t\tstatic int Get() => 1;\n\t\tvar b = 2;\n\t\tvar c = 3;\n\t\tif(a > 0)\n\t\t\t_ = Get() + b + c;";
		var after = "var a = 1;\n\t\tvar b = 2;\n\t\tvar c = 3;\n\n\t\tif(a > 0)\n\t\t\t_ = Get() + b + c;\n\n\t\tstatic int Get() => 1;";
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, Wrap(before));
		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006")))), document.Id);

		Assert.Equal(Wrap(after), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2006");
		await AssertCleanAsync(result, "ZS2003");
	}

	[Theory]
	[InlineData("var value = 1;\n\t\tstatic int Get() => 1;\n\t\tif(value > 0)\n\t\t{\n\t\t\t_ = Get();\n\t\t}", "var value = 1;\n\t\tif(value > 0)\n\t\t{\n\t\t\t_ = Get();\n\t\t}\n\n\t\tstatic int Get() => 1;")]
	[InlineData("var value = 1;\n\t\t_ = value;\n\t\tstatic int Get() => 1;\n\t\tif(value > 0)\n\t\t{\n\t\t\t_ = Get();\n\t\t}", "var value = 1;\n\t\t_ = value;\n\n\t\tif(value > 0)\n\t\t{\n\t\t\t_ = Get();\n\t\t}\n\n\t\tstatic int Get() => 1;")]
	[InlineData("_ = 0;\n\t\t_ = 1;\n\t\tstatic int Get() => 1;\n\t\tif(true)\n\t\t\t_ = Get();", "_ = 0;\n\t\t_ = 1;\n\t\tif(true)\n\t\t\t_ = Get();\n\n\t\tstatic int Get() => 1;")]
	public async Task LocalFunctionMoveRespectsConditionalPreparationThresholdsAsync(string before, string after)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, Wrap(before));
		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006")))), document.Id);

		Assert.Equal(Wrap(after), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2006");
		await AssertCleanAsync(result, "ZS2003");
	}

	[Theory]
	[InlineData("en-US", "Arrange local functions")]
	[InlineData("zh-Hans", "整理局部函数")]
	public async Task LocalFunctionFixUsesLocalizedTitleAsync(string language, string title)
	{
		var culture = CultureInfo.CurrentUICulture;

		try
		{
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
			using var workspace = new AdhocWorkspace();
			var document = AddDocument(workspace.CurrentSolution, Wrap("static int Get() => 1;\n\t\t_ = Get();"));

			Assert.Equal(title, Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2006")))).Title);
		}
		finally { CultureInfo.CurrentUICulture = culture; }
	}

	[Theory]
	[InlineData(FixAllScope.Document)]
	[InlineData(FixAllScope.Project)]
	[InlineData(FixAllScope.Solution)]
	public async Task LocalFunctionFixAllHandlesNestedListsAndBatchScopesAsync(FixAllScope scope)
	{
		var source = Wrap("static int Outer()\n\t\t{\n\t\t\tstatic int Inner() => 1;\n\t\t\treturn Inner();\n\t\t}\n\t\t_ = Outer();\n\n\t\tstatic int Other() => 2;\n\t\t_ = Other();");
		using var workspace = new AdhocWorkspace();
		var first = AddDocument(workspace.CurrentSolution, source);
		var second = AddDocument(first.Project.Solution, source.Replace("class Sample", "class Second"), first.Project.Id);
		var third = AddDocument(second.Project.Solution, source);
		first = third.Project.Solution.GetDocument(first.Id);
		var provider = GetProvider("ZS2006");
		var context = new FixAllContext(first, provider, scope, "ZS2006.ArrangeLocalFunctions", new[] { "ZS2006" }, new Diagnostics("ZS2006"), TestContext.Current.CancellationToken);
		var result = await ApplyAsync(await provider.GetFixAllProvider().GetFixAsync(context), first.Id);

		foreach(var id in new[] { first.Id, second.Id, third.Id })
		{
			var document = result.Project.Solution.GetDocument(id);
			var changed = scope == FixAllScope.Solution || scope == FixAllScope.Project && document.Project.Id == first.Project.Id || id == first.Id;

			if(changed)
				await AssertCleanAsync(document, "ZS2006");
			else
				Assert.Equal(id == second.Id ? source.Replace("class Sample", "class Second") : source, (await document.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		}
	}

	[Fact]
	public async Task LocalFunctionDocumentationExamplesCompileAndMatchDiagnosticsAsync()
	{
		string[] examples = null;

		foreach(var file in new[] { "RULES.md", "RULES.zh-Hans.md" })
		{
			var text = ReadPackageText(file);
			var start = text.IndexOf("<a id=\"zs2006\"></a>", StringComparison.Ordinal);
			var end = text.IndexOf("<a id=\"zs3001\"></a>", start, StringComparison.Ordinal);
			var fragments = System.Text.RegularExpressions.Regex.Matches(text[start..end], @"```csharp\r?\n(.*?)\r?\n```", System.Text.RegularExpressions.RegexOptions.Singleline)
				.Select(match => match.Groups[1].Value.Replace("\r\n", "\n")).ToArray();

			Assert.NotEmpty(fragments);

			if(examples != null)
				Assert.Equal(examples, fragments);

			examples = fragments;
		}

		foreach(var fragment in examples)
		{
			using var workspace = new AdhocWorkspace();
			var source = "using System;\nclass Sample\n{\n" + fragment + "\n}\n";
			var document = AddDocument(workspace.CurrentSolution, source);
			var expected = SourceText.From(source).Lines.Where(line => line.ToString().Contains("//ZS2006", StringComparison.Ordinal))
				.Select(line => (line.LineNumber, line.ToString().TakeWhile(char.IsWhiteSpace).Count())).ToArray();
			var diagnostics = await GetDiagnosticsAsync(document, "ZS2006");
			var actual = diagnostics.Select(item => item.Location.GetLineSpan().StartLinePosition).Select(position => (position.Line, position.Character)).ToArray();

			Assert.Equal(expected, actual);
			Assert.DoesNotContain((await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken)).GetDiagnostics(TestContext.Current.CancellationToken), item => item.Severity == DiagnosticSeverity.Error);
		}
	}

	[Fact]
	public async Task LocalFunctionFixAllUsesOriginalDiagnosticSnapshotsAsync()
	{
		var source = Wrap("static int Outer()\n\t\t{\n\t\t\tstatic int Inner() => 1;\n\t\t\treturn Inner();\n\t\t}\n\t\t_ = Outer();\n\n\t\tstatic int Other() => 2;\n\t\t_ = Other();");
		using var workspace = new AdhocWorkspace();
		var first = AddDocument(workspace.CurrentSolution, source);
		var second = AddDocument(first.Project.Solution, source.Replace("class Sample", "class Second"), first.Project.Id);
		first = second.Project.Solution.GetDocument(first.Id);
		var snapshots = new Dictionary<DocumentId, ImmutableArray<Diagnostic>>
		{
			[first.Id] = await GetDiagnosticsAsync(first, "ZS2006"),
			[second.Id] = await GetDiagnosticsAsync(second, "ZS2006"),
		};
		var provider = GetProvider("ZS2006");
		var context = new FixAllContext(first, provider, FixAllScope.Project, "ZS2006.ArrangeLocalFunctions", new[] { "ZS2006" },
			new LocalFunctionSnapshotDiagnostics(first.Project, snapshots), TestContext.Current.CancellationToken);
		var result = await ApplyAsync(await provider.GetFixAllProvider().GetFixAsync(context), first.Id);

		await AssertCleanAsync(result, "ZS2006");
		await AssertCleanAsync(result.Project.Solution.GetDocument(second.Id), "ZS2006");
	}

	private sealed class LocalFunctionSnapshotDiagnostics(Project original, Dictionary<DocumentId, ImmutableArray<Diagnostic>> diagnostics) : FixAllContext.DiagnosticProvider
	{
		public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
			Task.FromResult<IEnumerable<Diagnostic>>(document.Project == original ? diagnostics[document.Id] : ImmutableArray<Diagnostic>.Empty);
		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) => Task.FromResult(Enumerable.Empty<Diagnostic>());
		public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
			Task.FromResult(project == original ? diagnostics.Values.SelectMany(items => items) : Enumerable.Empty<Diagnostic>());
	}
}
