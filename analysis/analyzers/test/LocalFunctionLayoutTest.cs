using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class LocalFunctionLayoutTest
{
	[Theory]
	[InlineData("static int Get() => 1;\n\t\t_ = Get();", 1)]
	[InlineData("_ = 0;\n\n\t\tstatic int Get() => 1;\n\n\t\t_ = Get();", 1)]
	[InlineData("_ = Get();\n\t\tstatic int Get() => 1;", 0)]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() => 1;", 0)]
	[InlineData("_ = Get();\n\n\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = 0;\n\t\t_ = Get();\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = 0;\n\t\t_ = Get();\n\n\t\tstatic int Get() => 1;", 0)]
	[InlineData("_ = 0;\n\t\t_ = Get();\n\n\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("System.Console.Write(\n\t\t\tGet());\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = Get(); // trailing\n\t\tstatic int Get() => 1;", 0)]
	[InlineData("// body\n\t\t_ = Get();\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = Get();\n\t\t// function\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = Get();\n\n\t\t// function\n\t\tstatic int Get() => 1;", 0)]
	[InlineData("_ = Get();\n\t\t// function\n\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = 0;\n\t\t_ = Get();\n\t\t/* function\n\n\t\t */\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() => Other();\n\t\tstatic int Other() => 1;", 0)]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() => Other();\n\n\t\tstatic int Other() => 1;", 1)]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() => Other();\n\t\tstatic int Other() { return 1; }", 0)]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() => Other();\n\n\t\tstatic int Other() { return 1; }", 0)]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() { return Other(); }\n\t\tstatic int Other() => 1;", 1)]
	[InlineData("_ = Get();\n\n\t\tstatic int Get() { return Other(); }\n\n\t\tstatic int Other() => 1;", 0)]
	[InlineData("static int Get() => 1;", 0)]
	[InlineData("// function\n\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("\n\t\t// function\n\t\tstatic int Get() => 1;", 0)]
	[InlineData("_ = Get();\n\t\t/// <summary>Function.</summary>\n\t\tstatic int Get() => 1;", 1)]
	[InlineData("_ = Get();\n\n\t\t/// <summary>Function.</summary>\n\t\tstatic int Get() => 1;", 0)]
	public async Task ChecksPositionAndExactSpacingAsync(string body, int expected)
	{
		var result = await AnalyzeAsync(Wrap(body));

		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(expected, Regex.Matches(result.Output, @"error ZS2006:").Count);
	}

	[Theory]
	[InlineData("class Sample\n{\n\tpublic Sample()\n\t{\n\t\tstatic int Get() => 1;\n\t\t_ = Get();\n\t}\n}")]
	[InlineData("class Sample\n{\n\tpublic int Value\n\t{\n\t\tget\n\t\t{\n\t\t\tstatic int Get() => 1;\n\t\t\treturn Get();\n\t\t}\n\t}\n}")]
	[InlineData("class Sample { void Run() { System.Action action = () => { static int Get() => 1; _ = Get(); }; action(); } }")]
	[InlineData("class Sample { void Run() { System.Action action = delegate { static int Get() => 1; _ = Get(); }; action(); } }")]
	[InlineData("class Sample { void Run() { { static int Get() => 1; _ = Get(); } } }")]
	[InlineData("class Sample { void Run() { _ = Outer();\n\nstatic int Outer() { static int Inner() => 1; return Inner(); } } }")]
	[InlineData("class Sample { void Run(int value) { switch(value) { case 0: static int Get() => 1; _ = Get(); break; } } }")]
	[InlineData("class Sample { ~Sample() { static int Get() => 1; _ = Get(); } }")]
	[InlineData("class Sample { public static Sample operator +(Sample left, Sample right) { static Sample Get(Sample value) => value; return Get(left); } }")]
	public async Task ChecksEveryHostAsync(string source)
	{
		var result = await AnalyzeAsync(source);

		Assert.DoesNotContain("error CS", result.Output);
		Assert.Single(Regex.Matches(result.Output, @"error ZS2006:"));
	}

	[Fact]
	public async Task ChecksTopLevelStatementListAsync()
	{
		var result = await AnalyzeAsync("static int Get() => 1;\n_ = Get();\nclass Sample { }", executable: true);

		Assert.DoesNotContain("error CS", result.Output);
		Assert.Single(Regex.Matches(result.Output, @"error ZS2006:"));
	}

	[Fact]
	public async Task ReportsFunctionHeaderAndDoesNotDuplicateStatementSpacingAsync()
	{
		var source = Wrap("if(true)\n\t\t{\n\t\t\t_ = Get();\n\t\t}\n\t\tstatic int Get() => 1;");
		var result = await AnalyzeAsync(source, extra: "dotnet_diagnostic.ZS2003.severity = warning\n");
		var line = source[..source.IndexOf("static int Get()")].Count(character => character == '\n') + 1;

		Assert.Contains($"Example.cs({line},3): error ZS2006:", result.Output);
		Assert.DoesNotContain(" ZS2003:", result.Output);
	}

	[Theory]
	[InlineData(false, "warning")]
	[InlineData(true, "error")]
	public async Task HonorsStrictModeAndLocalizationInTestProjectsAsync(bool strict, string severity)
	{
		var result = await AnalyzeAsync(Wrap("static int Get() => 1;\n\t\t_ = Get();"), strict: strict, language: "zh-Hans", testProject: "true");

		Assert.Contains(severity + " ZS2006: 将此局部函数放在所在语句列表末尾并使用规定的空行分隔", result.Output);
	}

	[Fact]
	public async Task HonorsConfigurationAndGeneratedCodeAsync()
	{
		var source = Wrap("static int Get() => 1;\n\t\t_ = Get();");
		var disabled = await AnalyzeAsync(source, extra: "dotnet_diagnostic.ZS2006.severity = none\n");
		var generated = await AnalyzeAsync("// <auto-generated/>\n" + source);

		Assert.DoesNotContain(" ZS2006:", disabled.Output);
		Assert.DoesNotContain(" ZS2006:", generated.Output);
	}

	[Fact]
	public async Task KeepsFunctionsInTheirOwnBlocksAsync()
	{
		var source = Wrap("if(true)\n\t\t{\n\t\t\tvar value = 1;\n\t\t\t_ = Get();\n\n\t\t\tint Get() => value;\n\t\t}\n\n\t\t_ = 0;");
		var result = await AnalyzeAsync(source);

		Assert.DoesNotContain("error CS", result.Output);
		Assert.DoesNotContain(" ZS2006:", result.Output);
	}

	private static string Wrap(string body) => "class Sample\n{\n\tvoid Run()\n\t{\n\t\t" + body + "\n\t}\n}\n";

	private static Task<AnalysisResult> AnalyzeAsync(string source, bool executable = false, bool strict = true, string extra = "", string language = "en-US", string testProject = null) =>
		AnalyzerRunner.AnalyzeAsync(source, true, strict,
			"root = true\n[*]\ndotnet_diagnostic.IDE0055.severity = none\ndotnet_diagnostic.IDE2001.severity = none\ndotnet_diagnostic.ZS2003.severity = none\n" + extra,
			false, executable, language, testProject);
}
