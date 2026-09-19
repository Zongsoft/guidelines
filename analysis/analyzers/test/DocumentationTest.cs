using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class DocumentationTest
{
	#region 测试方法
	[Fact]
	public async Task ReportsEveryMissingParameterAndReturn()
	{
		var result = await AnalyzeAsync(Wrap("/// <summary>\n\t/// One line.\n\t/// </summary>\n\tpublic int Read(int first, int second) => first + second;"));
		Assert.Equal(4, Diagnostics(result).Length);
		Assert.Contains("Example.cs(8,22): error ZS3001", result.Output);
		Assert.Contains("Example.cs(8,33): error ZS3001", result.Output);
		Assert.Contains("Example.cs(8,9): error ZS3002", result.Output);
		Assert.Contains("Example.cs(5,6): error ZS3003", result.Output);
		Assert.False(result.Success, result.Output);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Theory]
	[InlineData("public")]
	[InlineData("internal")]
	[InlineData("protected")]
	[InlineData("private")]
	public async Task ChecksDocumentedMethodsAtEveryAccessibility(string accessibility)
	{
		var result = await AnalyzeAsync(Wrap("/// <summary>Reads a value.</summary>\n\t" + accessibility + " int Read(int value) => value;"));
		Assert.Equal(2, Diagnostics(result).Length);
		Assert.Single(Diagnostics(result, "ZS3001"));
		Assert.Single(Diagnostics(result, "ZS3002"));
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Theory]
	[InlineData("/// <param name=\"first\">First.</param>", 1)]
	[InlineData("/// <param name=\"First\">Wrong case.</param>", 2)]
	[InlineData("/// <param name=\"first\"/>\n\t/// <param name=\"second\"/>", 0)]
	[InlineData("/// <param name=\"first\">First.</param>\n\t/// <param name=\"second\">Second.</param>", 0)]
	public async Task MatchesEveryParameterByExactName(string parameters, int expected)
	{
		var result = await AnalyzeAsync(Wrap("/// <summary>Reads values.</summary>\n\t" + parameters + "\n\t/// <returns>Sum.</returns>\n\tpublic int Read(int first, int second) => first + second;"));
		Assert.Equal(expected, Diagnostics(result, "ZS3001").Length);
		Assert.Equal(expected, Diagnostics(result).Length);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Fact]
	public async Task AcceptsEscapedParameterNamesAndEmptyReturns()
	{
		var result = await AnalyzeAsync(Wrap("/// <summary>Reads a value.</summary>\n\t/// <param name=\"event\"/>\n\t/// <returns/>\n\tpublic int Read(int @event) => @event;"));
		Assert.True(result.Success, result.Output);
		Assert.Empty(result.Output);
	}

	[Theory]
	[InlineData("public void Run() { }", 0)]
	[InlineData("public int Read() => 0;", 1)]
	[InlineData("public global::System.Threading.Tasks.Task RunAsync() => global::System.Threading.Tasks.Task.CompletedTask;", 1)]
	[InlineData("public ref int Read(ref int value) => ref value;", 1)]
	public async Task RequiresReturnsForEveryNonVoidMethod(string method, int expected)
	{
		var parameter = method.Contains("ref int value", StringComparison.Ordinal) ? "\n\t/// <param name=\"value\">Value.</param>" : "";
		var result = await AnalyzeAsync(Wrap("/// <summary>Performs the operation.</summary>" + parameter + "\n\t" + method));
		Assert.Equal(expected, Diagnostics(result, "ZS3002").Length);
		Assert.Equal(expected, Diagnostics(result).Length);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Theory]
	[InlineData("public int Read(int value) => value;")]
	[InlineData("// Reads a value.\n\tpublic int Read(int value) => value;")]
	[InlineData("/* Reads a value. */\n\tpublic int Read(int value) => value;")]
	public async Task LeavesUndocumentedMethodsAlone(string method)
	{
		var result = await AnalyzeAsync(Wrap(method));
		Assert.True(result.Success, result.Output);
		Assert.Empty(result.Output);
	}

	[Theory]
	[InlineData("/// <inheritdoc/>")]
	[InlineData("/// <include file=\"missing.xml\" path=\"doc/member\"/>")]
	public async Task RequiresExplicitContractForInheritedOrIncludedDocumentation(string documentation)
	{
		var result = await AnalyzeAsync(Wrap(documentation + "\n\tpublic int Read(int value) => value;"));
		Assert.Single(Diagnostics(result, "ZS3001"));
		Assert.Single(Diagnostics(result, "ZS3002"));
		Assert.Equal(2, Diagnostics(result).Length);
	}

	[Theory]
	[InlineData("/// <summary>\n\t/// One line.\n\t/// </summary>", 1)]
	[InlineData("/// <summary>One line.\n\t/// </summary>", 1)]
	[InlineData("/// <summary>\n\t/// One line.</summary>", 1)]
	[InlineData("/// <summary>\n\t///\n\t/// One line.\n\t///\n\t/// </summary>", 1)]
	[InlineData("/// <summary>One line.</summary>", 0)]
	[InlineData("/// <summary>\n\t/// First line.\n\t/// Second line.\n\t/// </summary>", 0)]
	[InlineData("/// <summary>\n\t/// </summary>", 0)]
	[InlineData("/// <summary/>", 0)]
	[InlineData("/// <summary>\n\t/// Calls <see cref=\"Run\"/>.\n\t/// </summary>", 1)]
	[InlineData("/// <summary>Calls <see cref=\"Run\"/>.</summary>", 0)]
	[InlineData("/** <summary>\n\t * One line.\n\t * </summary> */", 1)]
	[InlineData("/** <summary>One line.</summary> */", 0)]
	[InlineData("/** <summary>\n\t * First line.\n\t * Second line.\n\t * </summary> */", 0)]
	public async Task ChecksPhysicalXmlContentLines(string documentation, int expected)
	{
		var result = await AnalyzeAsync(Wrap(documentation + "\n\tpublic void Run() { }"));
		Assert.Equal(expected, Diagnostics(result, "ZS3003").Length);
		Assert.Equal(expected, Diagnostics(result).Length);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Theory]
	[InlineData("param name=\"value\"", "param")]
	[InlineData("returns", "returns")]
	[InlineData("remarks", "remarks")]
	[InlineData("exception cref=\"global::System.Exception\"", "exception")]
	public async Task ChecksLayoutOfEveryXmlElement(string opening, string closing)
	{
		var result = await AnalyzeAsync(Wrap("/// <" + opening + ">\n\t/// One line.\n\t/// </" + closing + ">\n\tpublic void Run(int value) { }"));
		Assert.Single(Diagnostics(result, "ZS3003"));
		Assert.Contains("Example.cs(5,6): error ZS3003", result.Output);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Fact]
	public async Task PreservesMultilineListsAndCode()
	{
		var result = await AnalyzeAsync(Wrap("/// <summary>\n\t/// <list type=\"bullet\">\n\t/// <item>First.</item>\n\t/// <item>Second.</item>\n\t/// </list>\n\t/// </summary>\n\t/// <example>\n\t/// <code>\n\t/// Run();\n\t/// Run();\n\t/// </code>\n\t/// </example>\n\tpublic void Run() { }"));
		Assert.True(result.Success, result.Output);
		Assert.Empty(result.Output);
	}

	[Fact]
	public async Task ChecksBlockDocumentationContract()
	{
		var result = await AnalyzeAsync(Wrap("/** <summary>Reads a value.</summary> */\n\tpublic int Read(int value) => value;"));
		Assert.Equal(2, Diagnostics(result).Length);
		Assert.Contains("Example.cs(6,22): error ZS3001", result.Output);
		Assert.Contains("Example.cs(6,9): error ZS3002", result.Output);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, true)]
	public async Task SkipsDisabledDocumentationAndGeneratedCode(bool documentation, bool generated)
	{
		var source = (generated ? "// <auto-generated/>\n" : "") + Wrap("/// <summary>\n\t/// Reads a value.\n\t/// </summary>\n\tpublic int Read(int value) => value;");
		var result = await AnalyzerRunner.AnalyzeAsync(source, documentation, true, null, false, false, null);
		Assert.True(result.Success, result.Output);
		Assert.Empty(Diagnostics(result));
	}

	[Fact]
	public async Task ChecksXmlLayoutOnTypeDeclarations()
	{
		var result = await AnalyzeAsync("namespace Samples;\n\n/// <summary>\n/// A sample.\n/// </summary>\npublic class Sample { }\n");
		Assert.Single(Diagnostics(result));
		Assert.Contains("Example.cs(3,5): error ZS3003", result.Output);
	}

	[Theory]
	[InlineData(false, null, "warning")]
	[InlineData(true, null, "error")]
	[InlineData(true, "none", null)]
	public async Task HonorsConfiguredSeverity(bool strict, string severity, string expected)
	{
		var configuration = severity == null ? null : "root = true\n[*.cs]\nindent_style = tab\nindent_size = 4\ntab_width = 4\nend_of_line = crlf\ndotnet_diagnostic.ZS3001.severity = " + severity + "\ndotnet_diagnostic.ZS3002.severity = " + severity + "\ndotnet_diagnostic.ZS3003.severity = " + severity + "\n";
		var source = Wrap("/// <summary>\n\t/// Reads a value.\n\t/// </summary>\n\tpublic int Read(int value) => value;");
		var result = await AnalyzerRunner.AnalyzeAsync(source, true, strict, configuration, false, false, null);
		Assert.Equal(expected == "error", !result.Success);
		if(expected == null)
			Assert.Empty(Diagnostics(result));
		else
		{
			Assert.Equal(3, Diagnostics(result).Length);
			foreach(var id in new[] { "ZS3001", "ZS3002", "ZS3003" })
				Assert.Contains(": " + expected + " " + id, Assert.Single(Diagnostics(result, id)));
		}
	}
	#endregion

	#region 辅助方法
	private static string Wrap(string members) => "namespace Samples;\n\npublic class Sample\n{\n\t" + members + "\n}\n";
	private static string[] Diagnostics(AnalysisResult result, string id = "ZS300") => result.Output.Split('\n').Where(line => line.Contains(" " + id, StringComparison.Ordinal)).ToArray();
	private static Task<AnalysisResult> AnalyzeAsync(string source) => AnalyzerRunner.AnalyzeAsync(source, true, true, null, false, false, null);
	#endregion
}
