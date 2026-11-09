using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis.Text;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class NestedControlBracesTest
{
	[Theory]
	[InlineData("if(flag)")]
	[InlineData("for(var index = 0; index < items.Length; index++)")]
	[InlineData("foreach(var item in items)")]
	[InlineData("foreach(var (first, second) in new[] { (1, 2) })")]
	[InlineData("while(flag)")]
	[InlineData("using(var stream = new global::System.IO.MemoryStream())")]
	[InlineData("lock(items)")]
	[InlineData("fixed(int* address = items)")]
	public async Task RequiresOuterBlockForNestedControlAsync(string header)
	{
		var source = Wrap(header + "\n\tif(items.Length > 0)\n\t\tGC.KeepAlive(items);");
		var result = await AnalyzeAsync(source);

		AssertReports(source, result, header);
	}

	[Theory]
	[InlineData("if(flag)\n\t{\n\t\tGC.KeepAlive(items);\n\t}")]
	[InlineData("switch(items.Length)\n\t{\n\t\tdefault:\n\t\t\tbreak;\n\t}")]
	[InlineData("try { GC.KeepAlive(items); } finally { }")]
	[InlineData("checked { items[0]++; }")]
	[InlineData("unchecked { items[0]++; }")]
	[InlineData("unsafe { GC.KeepAlive(items); }")]
	public async Task InnerBlockDoesNotReplaceOuterBlockAsync(string body)
	{
		var header = "for(var index = 0; index < items.Length; index++)";
		var source = Wrap(header + "\n\t" + body.Replace("\n", "\n\t"));
		var result = await AnalyzeAsync(source);

		AssertReports(source, result, header);
	}

	[Fact]
	public async Task ChecksDoBodyAsync()
	{
		var source = Wrap("do\n\tif(flag)\n\t\tGC.KeepAlive(items);\nwhile(false);");

		AssertReports(source, await AnalyzeAsync(source), "do\n");
	}

	[Fact]
	public async Task ChecksEveryDirectNestingEdgeAsync()
	{
		var source = Wrap("for(var index = 0; index < items.Length; index++)\n\tif(flag)\n\t\twhile(flag)\n\t\t\tflag = false;");

		AssertReports(source, await AnalyzeAsync(source), "for(var index", "if(flag)");
	}

	[Theory]
	[InlineData("using(var first = new global::System.IO.MemoryStream())\nusing(var second = new global::System.IO.MemoryStream())\nusing(var third = new global::System.IO.MemoryStream())")]
	[InlineData("lock(items)\nlock(items)\nlock(items)")]
	[InlineData("fixed(int* first = items)\nfixed(int* second = items)\nfixed(int* third = items)")]
	public async Task AllowsPureSameFamilyChainsAsync(string headers)
	{
		foreach(var body in new[] { "GC.KeepAlive(items);", "{\n\tGC.KeepAlive(items);\n}" })
		{
			var source = Wrap(headers + "\n\t" + body);

			AssertReports(source, await AnalyzeAsync(source));
		}
	}

	[Theory]
	[InlineData("using(var first = new global::System.IO.MemoryStream())\nawait using(var second = new global::System.IO.MemoryStream())")]
	[InlineData("await using(var first = new global::System.IO.MemoryStream())\nusing(var second = new global::System.IO.MemoryStream())")]
	public async Task UsingAndAwaitUsingShareOneFamilyAsync(string headers)
	{
		var source = Wrap(headers + "\n\tGC.KeepAlive(items);", asynchronous: true);

		AssertReports(source, await AnalyzeAsync(source));
	}

	[Theory]
	[InlineData("using(var first = new global::System.IO.MemoryStream())", "lock(items)")]
	[InlineData("lock(items)", "using(var first = new global::System.IO.MemoryStream())")]
	[InlineData("fixed(int* address = items)", "lock(items)")]
	[InlineData("lock(items)", "fixed(int* address = items)")]
	public async Task RejectsMixedChainsAsync(string outer, string inner)
	{
		var source = Wrap(outer + "\n" + inner + "\n\tGC.KeepAlive(items);");

		AssertReports(source, await AnalyzeAsync(source), outer);
	}

	[Fact]
	public async Task DoesNotExtendResourceExceptionToTerminalBranchAsync()
	{
		var source = Wrap("using(var first = new global::System.IO.MemoryStream())\nusing(var second = new global::System.IO.MemoryStream())\n\tif(flag)\n\t\tGC.KeepAlive(items);");

		AssertReports(source, await AnalyzeAsync(source), "using(var second");
	}

	[Theory]
	[InlineData("if(flag)\n\tGC.KeepAlive(items);\nelse if(items.Length > 0)\n\tGC.KeepAlive(flag);\nelse\n\tGC.KeepAlive(items);")]
	[InlineData("for(var index = 0; index < items.Length; index++)\n{\n\tif(flag)\n\t\tGC.KeepAlive(items);\n}")]
	[InlineData("if(flag)\n\tGC.KeepAlive(items);")]
	[InlineData("if(flag) GC.KeepAlive(items);")]
	[InlineData("if(flag)\n\treturn;")]
	[InlineData("if(flag)\n\tthrow new InvalidOperationException();")]
	[InlineData("while(flag)\n\tbreak;")]
	[InlineData("while(flag)\n\tcontinue;")]
	[InlineData("while(flag);")]
	public async Task PreservesSimpleBodiesAndElseIfAsync(string body)
	{
		var source = Wrap(body);

		AssertReports(source, await AnalyzeAsync(source));
	}

	[Fact]
	public async Task ChecksIfBodyIfAndElseBodyLoopSeparatelyAsync()
	{
		var source = Wrap("if(flag)\n\tif(items.Length > 0)\n\t\tGC.KeepAlive(items);\n\telse\n\t\tGC.KeepAlive(flag);");

		AssertReports(source, await AnalyzeAsync(source), "if(flag)");

		source = Wrap("if(flag)\n\tGC.KeepAlive(items);\nelse\n\tforeach(var item in items)\n\t\tGC.KeepAlive(item);");

		AssertReports(source, await AnalyzeAsync(source), "else\n");
	}

	[Theory]
	[InlineData("await using(var stream = new global::System.IO.MemoryStream())")]
	[InlineData("await foreach(var item in values)")]
	public async Task ChecksAsyncBodiesAsync(string header)
	{
		var source = Wrap(header + "\n\tif(flag)\n\t\tGC.KeepAlive(items);", asynchronous: true);

		AssertReports(source, await AnalyzeAsync(source), header);
	}

	[Theory]
	[InlineData("void Local()\n{\n\tfor(var index = 0; index < items.Length; index++)\n\t\tif(flag)\n\t\t\tGC.KeepAlive(items);\n}\nLocal();")]
	[InlineData("Action action = () =>\n{\n\tfor(var index = 0; index < items.Length; index++)\n\t\tif(flag)\n\t\t\tGC.KeepAlive(items);\n};\naction();")]
	[InlineData("Action action = delegate()\n{\n\tfor(var index = 0; index < items.Length; index++)\n\t\tif(flag)\n\t\t\tGC.KeepAlive(items);\n};\naction();")]
	public async Task ChecksNestedFunctionsAsync(string body)
	{
		var source = Wrap(body);

		AssertReports(source, await AnalyzeAsync(source), "for(var index");
	}

	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	public async Task UsesSameRuleInProductionAndTestsAsync(string testProject)
	{
		var source = Wrap("foreach(var item in items)\n\tif(flag)\n\t\tGC.KeepAlive(item);");
		var result = await AnalyzeAsync(source, testProject: testProject);

		AssertReports(source, result, "foreach(var item");
	}

	[Fact]
	public async Task SkipsGeneratedSourceAsync()
	{
		var source = "// <auto-generated />\n" + Wrap("while(flag)\n\tif(flag)\n\t\tflag = false;");

		AssertReports(source, await AnalyzeAsync(source));
	}

	[Theory]
	[InlineData(false, "warning")]
	[InlineData(true, "error")]
	public async Task HonorsSeverityAndLocalizationAsync(bool strict, string severity)
	{
		var source = Wrap("while(flag)\n\tif(flag)\n\t\tflag = false;");
		var result = await AnalyzeAsync(source, strict, language: "zh-Hans");

		AssertReports(source, result, "while(flag)");
		Assert.Contains(severity + " ZS2005: 使用花括号包裹此嵌套控制结构的外层语句体", result.Output);
	}

	[Fact]
	public async Task ConfigurationCanDisableRuleAsync()
	{
		var source = Wrap("while(flag)\n\tif(flag)\n\t\tflag = false;");
		var result = await AnalyzerRunner.AnalyzeAsync(source, false, true,
			"root = true\n[*]\ndotnet_diagnostic.ZS2005.severity = none\n", false, false, null, unsafeCode: true);

		AssertReports(source, result);
	}

	private static Task<AnalysisResult> AnalyzeAsync(string source, bool strict = false, string testProject = null, string language = null) =>
		AnalyzerRunner.AnalyzeAsync(source, false, strict, null, false, false, language, testProject, unsafeCode: true);

	private static string Wrap(string body, bool asynchronous = false) => "using System;\n\nclass Sample\n{\n\tpublic static " +
		(asynchronous ? "async global::System.Threading.Tasks.Task" : "unsafe void") +
		" Run(bool flag, int[] items, global::System.Collections.Generic.IAsyncEnumerable<int> values)\n\t{\n\t\t" +
		body.Replace("\n", "\n\t\t") + "\n\t}\n}\n";

	private static void AssertReports(string source, AnalysisResult result, params string[] headers)
	{
		Assert.DoesNotContain("error CS", result.Output);
		var diagnostics = Regex.Matches(result.Output, @"Example.cs\((\d+),(\d+)\): (?:warning|error) ZS2005:");
		var text = SourceText.From(source);
		var expected = headers.Select(header => text.Lines.GetLinePosition(source.IndexOf(header, StringComparison.Ordinal)))
			.Select(position => (position.Line + 1, position.Character + 1)).OrderBy(position => position.Item1).ToArray();
		var actual = diagnostics.Select(match => (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)))
			.OrderBy(position => position.Item1).ToArray();
		Assert.Equal(expected, actual);
	}
}
