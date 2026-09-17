using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class AuditRuleTest
{
	[Theory]
	[InlineData("Q001", "public char[] Read(int count) => Random.Shared.GetItems(\"0123456789abcdef\".AsSpan(..10), count);")]
	[InlineData("Q001", "public ReadOnlyMemory<char> Read() => MemoryExtensions.AsMemory(\"0123456789abcdef\", 0, 10);")]
	[InlineData("Q002", "public void Run() => Console.Write(\"\\u001b[3J\\u001b[2J\\u001b[1J\\u001b[0J\");")]
	[InlineData("Q002", "private const string CLEAR = \"\\u001b[2J\";\n\tpublic void Run() => Console.Write(CLEAR);")]
	[InlineData("Q003", "private const string SPLASH = \"  ___ /\\\\\\n /___/  \\n\";\n\tpublic void Run() => Console.Write(SPLASH);")]
	[InlineData("Q009", "public Sample() :\n\t\tthis(1) { }\n\tpublic Sample(int value) { }")]
	[InlineData("Q009", "public Sample() :\n\t\tbase() { }")]
	[InlineData("Q011", "public int Read(Type type)\n\t{\n\t\treturn type.IsGenericType && type.IsValueType ?\n\t\t       1 : 0;\n\t}")]
	[InlineData("Q011", "public bool Read(bool value)\n\t{\n\t\tvalue = value ||\n\t\t      (value && value) ? value : false;\n\t\treturn value;\n\t}")]
	[InlineData("Q012", "public Action<object> Read() => delegate(object sender)\n\t{\n\t\tGC.KeepAlive(sender);\n\t};")]
	[InlineData("Q018", "public int Read(int value) { value++; return value; }")]
	[InlineData("Q018", "public int Read() { return 0; }")]
	[InlineData("Q018", "public int Read()\n\t{\n\t\tint Local(int value) { value++; return value; }\n\t\treturn Local(1);\n\t}")]
	[InlineData("Q019", "public Func<int, int> Read() => value => { value++; return value; };")]
	[InlineData("Q019", "public Action Read() => () => { GC.KeepAlive(1); GC.KeepAlive(2); };")]
	[InlineData("Q019", "public Func<int, int> Read() => delegate(int value) { value++; return value; };")]
	public async Task AllowsApprovedAuditPatterns(string issue, string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.True(result.Success, issue + ": " + result.Output);
		Assert.Empty(result.Output);
	}

	[Theory]
	[InlineData("public void Run() => Console.Write(\"\\u001b[2JReady.\");")]
	[InlineData("public void Run() => Console.Write(\"Ready.\\u001b[2J\");")]
	[InlineData("public void Run() => Console.Write(\"\\u001b[2\");")]
	[InlineData("public void Run() => Console.Write(\"___\\nHello\\n___\");")]
	[InlineData("public void Run() => Console.Write(\"Ready: {0}\", \"012\".AsSpan().ToString());")]
	[InlineData("public string Read() => AsSpan(\"Ready.\");\n\tprivate static string AsSpan(string text) => text;")]
	[InlineData("public void Run() => throw new ArgumentException(\"Invalid value.\");")]
	public async Task RetainsNaturalLanguageAndUnrecognizedTechnicalText(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.Contains("error CA1303", result.Output);
	}

	[Theory]
	[InlineData("Q018", "public int Read(int value) { value++; value++; return value; }", "IDE2001")]
	[InlineData("Q019", "public Action Read() => () => { GC.KeepAlive(1); GC.KeepAlive(2); GC.KeepAlive(3); };", "IDE2001")]
	[InlineData("Q019", "public Action Read() => () => { if(true) GC.KeepAlive(1); };", "IDE2001")]
	[InlineData("Q018", "public int Read(int value) { value+=1; return value; }", "IDE0055")]
	[InlineData("Q019", "public Func<int, int> Read() => value => { value+=1; return value; };", "IDE0055")]
	[InlineData("Q007", "public string Read(string first, string second)\n\t{\n\t\treturn string.Concat(first,\n\t\t                     second);\n\t}", "IDE0055")]
	[InlineData("Q008", "public int Read(int value)\n\t{\n\t\ttry {\n\t\t\treturn value;\n\t\t}\n\t\tfinally { GC.KeepAlive(value); }\n\t}", "IDE0055")]
	[InlineData("Q010", "public Sample() :\n\t\tthis(1)\t{ }\n\tpublic Sample(int value) { }", "IDE0055")]
	[InlineData("Q020", "public global::System.Collections.Generic.IEnumerable<int> Read()\n\t{\n\t\ttry { GC.KeepAlive(1); }\n\t\tcatch { yield break; }\n\t}", "IDE2001")]
	[InlineData("Q013", "private int value = 1;\n\tpublic int Value => value;", "IDE1006")]
	[InlineData("Q014", "private int _Value = 1;\n\tpublic int Value => _Value;", "IDE1006")]
	[InlineData("Q015", "public int Read()\n\t{\n\t\tvar VALUE = 1;\n\t\treturn VALUE;\n\t}", "IDE1006")]
	[InlineData("Q016", "private const int value = 1;\n\tpublic int Value => value;", "IDE1006")]
	[InlineData("Q017", "protected int _value = 1;", "IDE1006")]
	[InlineData("Q022", "public void Run(int value)\n\t{\n\t\tif(value == 0)\n\t\t\treturn;\n\t\telse\n\t\t\tvalue++;\n\t\tif(value == 1)\n\t\t\treturn;\n\t}", "ZS2003")]
	public async Task RetainsAuditWarningsAndExceptionBoundaries(string issue, string members, string diagnostic)
	{
		var result = await AnalyzeAsync(members);
		Assert.False(result.Success, issue + ": " + result.Output);
		Assert.Contains("error " + diagnostic, result.Output);
		Assert.DoesNotContain("error CS", result.Output);
	}

	[Theory]
	[InlineData("Q004", "[Obsolete(\"Use the replacement.\")]\n\tpublic static void Legacy() { }\n\tpublic void Run() => Legacy();", "CS0618")]
	[InlineData("Q005", "public class Entry(int category) { }", "CS9113")]
	public async Task RetainsUnchangedCompilerWarnings(string issue, string members, string diagnostic)
	{
		var result = await AnalyzeAsync(members);
		Assert.True(result.Output.Contains(diagnostic, global::System.StringComparison.Ordinal), issue + ": " + result.Output);
	}

	private static Task<AnalysisResult> AnalyzeAsync(string members) => AnalyzerRunner.AnalyzeAsync(
		"using System;\n\nnamespace Samples;\n\npublic class Sample\n{\n\t" + members + "\n}\n", true, true, null, false, false, null);
}
