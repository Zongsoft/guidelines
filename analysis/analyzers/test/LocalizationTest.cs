using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class LocalizationTest
{
	[Theory]
	[InlineData("public void Run(int value) => throw new Exception(\"Failed.\");")]
	[InlineData("public void Run(int value) => throw new Exception(message: \"404\");")]
	[InlineData("public void Run(int value) => throw new Exception(\"❌\");")]
	[InlineData("public void Run(int value) => throw new Exception(\"\\u001b[2J\");")]
	[InlineData("public void Run(int value) => throw new Exception(\"___\\n/___/\");")]
	[InlineData("public void Run(int value) => throw new Exception(\"!\");")]
	[InlineData("public void Run(int value) => throw new Exception(@\"Failed.\");")]
	[InlineData("public void Run(int value) => throw new Exception(\"\"\"Failed.\"\"\");")]
	[InlineData("public void Run(int value) => throw new Exception($\"Failed: {value}\");")]
	[InlineData("public void Run(int value) => throw new Exception(\"Failed: \" + value);")]
	[InlineData("public void Run(int value) => throw new Exception(string.Format(\"Failed: {0}\", value));")]
	[InlineData("public void Run(int value) => throw new Exception(string.Format(global::System.Globalization.CultureInfo.InvariantCulture, \"Failed: {0}\", value));")]
	[InlineData("public void Run(int value) => throw new Exception(string.Format(format: \"Failed: {0}\", arg0: value));")]
	[InlineData("public void Run() => throw new Exception($\"{\"Failed.\"}\");")]
	[InlineData("public void Run(int value) => throw new Exception((string)(\"Failed.\"));")]
	[InlineData("public void Run(int value) => throw new ArgumentException(\"Failed.\", \"value\");")]
	[InlineData("public Exception Read() => new(\"Failed.\");")]
	[InlineData("public object Read() => new Exception(\"Failed.\");")]
	[InlineData("public void Run() => throw new Failure(\"Failed.\"); public class Failure(string message) : Exception(message) { }")]
	[InlineData("public void Run() => throw new Failure(\"Failed.\"); public class Failure(string Message) : Exception(Message) { }")]
	[InlineData("public void Run() => throw new Failure(\"Failed.\"); public class Failure([global::System.ComponentModel.Localizable(false)] string message) : Exception(message) { }")]
	[InlineData("public void Run() => throw new ArgumentException(paramName: \"value\", message: \"Failed.\");")]
	[InlineData("public void Run() => throw new Exception(Resources.Message + \"!\"); public static class Resources { public static string Message => \"Failed.\"; }")]
	[InlineData("public void Run() => throw new Exception(string.Format(Resources.Message, \"CSV\") + \"!\"); public static class Resources { public static string Message => \"{0}\"; }")]
	public async Task ExceptionMessages(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(new[] { "ZS1301" }, Diagnostics(result));
	}

	[Theory]
	[InlineData("public void Run() => throw new ArgumentNullException(\"value\");")]
	[InlineData("public void Run() => throw new Failure(\"Failed.\"); public class Failure(string reason) : Exception(reason) { }")]
	[InlineData("public void Run() => throw new Failure(\"Failed.\"); public class Failure(string errorMessage) : Exception(errorMessage) { }")]
	[InlineData("public void Run() => throw new Failure(\"Failed.\"); public class Failure(string text) : Exception(text) { }")]
	[InlineData("public object Run() => new Other.Exception(\"Failed.\"); public static class Other { public class Exception(string message) { } }")]
	[InlineData("public void Run(string message) => throw new Exception(message);")]
	[InlineData("public void Run() { var message = \"Failed.\"; throw new Exception(message); }")]
	[InlineData("public void Run() { const string MESSAGE = \"Failed.\"; throw new Exception(MESSAGE); }")]
	[InlineData("private readonly string _message = \"Failed.\"; public void Run() => throw new Exception(_message);")]
	[InlineData("private string Message => \"Failed.\"; public void Run() => throw new Exception(this.Message);")]
	[InlineData("public void Run() => throw Fail(\"Failed.\"); private static Exception Fail(string message) => new Exception(message);")]
	[InlineData("public void Run() => throw new Exception(Read()); private static string Read() => \"Failed.\";")]
	[InlineData("public void Run(Exception error) => throw error;")]
	[InlineData("public void Run() => Console.WriteLine(\"Ready.\");")]
	[InlineData("[global::System.ComponentModel.Localizable(true)] public void Show(string message) { } public void Run() => this.Show(\"Ready.\");")]
	[InlineData("public void Run() => throw new Exception((string)null);")]
	[InlineData("public void Run() => throw new Exception(\"\");")]
	[InlineData("public void Run() => throw new Exception(\" \\t\\r\\n\");")]
	[InlineData("public void Run(int value) => throw new Exception($\"{value}\");")]
	[InlineData("public void Run() => throw new Exception(nameof(Run));")]
	[InlineData("public void Run() => throw new Exception(string.Format(Resources.Message, \"CSV\")); public static class Resources { public static string Message => \"{0}\"; }")]
	[InlineData("public void Run(bool value) => throw new Exception(value ? \"Failed.\" : \"Ready.\");")]
	[InlineData("public Exception Read() => new Failure(); public class Failure(string message = \"Failed.\") : Exception(message) { }")]
	[InlineData("public void Run() => throw new Exception(new string('x', 4));")]
	[InlineData("public void Run() => throw new Exception(new Text() + \"resource-key\"); public class Text { public static string operator +(Text left, string right) => right; }")]
	[InlineData("public void Run() => throw new Exception((string)(Text)\"resource-key\"); public class Text { public static explicit operator Text(string value) => new Text(); public static explicit operator string(Text value) => \"\"; }")]
	public async Task UntrackedExpressions(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(new string[0], Diagnostics(result));
	}

	[Theory]
	[InlineData("public string Text => \"你好\";")]
	[InlineData("public string Text => \"かな\";")]
	[InlineData("public string Text => \"한글\";")]
	[InlineData("public string Text => \"ไทย\";")]
	[InlineData("public string Text => \"é\";")]
	[InlineData("public string Text => \"e\\u0301\";")]
	[InlineData("public string Text => \"q\\u0307\";")]
	[InlineData("public string Text => \"Русский\";")]
	[InlineData("public string Text => \"Ελληνικά\";")]
	[InlineData("public string Text => \"العربية\";")]
	[InlineData("public string Text => \"\\u4F60\\u597D\";")]
	[InlineData("public string Text => \"\\U00020000\";")]
	[InlineData("public string Text => \"\\U00031350\";")]
	[InlineData("public string Text => \"👩🏽‍💻你好\";")]
	[InlineData("public string Text => \"ℹ️你好\";")]
	public async Task UnicodeText(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(new[] { "ZS1302" }, Diagnostics(result));
	}

	[Theory]
	[InlineData("public string Text => \"Ready.\";")]
	[InlineData("public string Text => \"123 !\";")]
	[InlineData("public string Text => \"❌\";")]
	[InlineData("public string Text => \"ℹ\";")]
	[InlineData("public string Text => \"ℹ️\";")]
	[InlineData("public string Text => \"Ⓜ️\";")]
	[InlineData("public string Text => \"㊗️\";")]
	[InlineData("public string Text => \"👩🏽‍💻\";")]
	[InlineData("public string Text => \"🇨🇳\";")]
	[InlineData("public string Text => \"1️⃣\";")]
	[InlineData("public string Text => \"→≠∞\";")]
	[InlineData("public string Text => \"\\uE000\";")]
	[InlineData("public string Text => \"\\u001b[2J\";")]
	[InlineData("public string Text => \"___\\n/___/\";")]
	[InlineData("public string Text => \"\\u0301\";")]
	[InlineData("public string Text => \"e\\uFE0F\";")]
	[InlineData("public string Text => \"\\uD800\";")]
	public async Task Symbols(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(new string[0], Diagnostics(result));
	}

	[Theory]
	[InlineData("public string Text { get; set; } = \"你好\";")]
	[InlineData("public const string TEXT = \"你好\";")]
	[InlineData("private readonly string _text = \"你好\"; public string Read() => _text;")]
	[InlineData("public void Run(string text = \"你好\") { }")]
	[InlineData("[global::System.ComponentModel.Description(\"你好\")] public void Run() { }")]
	[InlineData("public string Read() => @\"C:\\你好\";")]
	[InlineData("public string Read() => \"{\\\"你好\\\":1}\";")]
	[InlineData("public string Read() => @\"[你好]\";")]
	[InlineData("public void Run() { var text = \"你好\"; GC.KeepAlive(text); }")]
	[InlineData("public string Read(int value) => $\"你好{value}\";")]
	[InlineData("public string Read(DateTime value) => $\"{value:yyyy年}\";")]
	[InlineData("public string Read() => \"\"\"你好\"\"\";")]
	[InlineData("public ReadOnlySpan<byte> Read() => \"你好\"u8;")]
	[InlineData("public ReadOnlySpan<byte> Read() => \"\"\"你好\"\"\"u8;")]
	[InlineData("public string Read(int value) => $$\"\"\"你好{{value}}\"\"\";")]
	[InlineData("public void Run() => throw new Failure(\"你好\"); public class Failure(string reason) : Exception(reason) { }")]
	[InlineData("public void Run() => throw new Exception(Read(\"你好\")); private static string Read(string key) => key;")]
	[InlineData("public void Run() => throw new Exception(string.Format(Resources.Message, \"你好\")); public static class Resources { public static string Message => \"{0}\"; }")]
	public async Task SourcePositions(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(new[] { "ZS1302" }, Diagnostics(result));
	}

	[Theory]
	[InlineData("public void Run() => throw new Exception(\"失败\");")]
	[InlineData("public void Run() => throw new Exception(message: \"失败\");")]
	[InlineData("public void Run(int value) => throw new Exception($\"失败{value}\");")]
	[InlineData("public void Run(int value) => throw new Exception(string.Format(\"失败{0}\", value));")]
	public async Task ExceptionPriority(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(new[] { "ZS1301" }, Diagnostics(result));
	}

	[Theory]
	[InlineData("// 中文注释\npublic char 字符 => '中';")]
	[InlineData("public string Read() => nameof(中文); public int 中文 => 1;")]
	public async Task AllowsNonTextSyntax(string members)
	{
		var result = await AnalyzeAsync(members);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(new string[0], Diagnostics(result));
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData("false", false)]
	[InlineData("true", false)]
	[InlineData("TRUE", true)]
	[InlineData("false", true)]
	public async Task TestProjects(string testProject, bool strict)
	{
		var result = await AnalyzeAsync("public void Run() { Console.WriteLine(\"你好\"); GC.KeepAlive(new Exception(\"Failed.\")); GC.KeepAlive(new global::System.Resources.ResourceManager(typeof(Sample)).GetString(\"Name\")); }", strict: strict, testProject: testProject);
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(string.Equals(testProject, "true", global::System.StringComparison.OrdinalIgnoreCase) ? new string[0] : new[] { "ZS1301", "ZS1302", "ZS1304" }, Diagnostics(result));
		if(!string.Equals(testProject, "true", global::System.StringComparison.OrdinalIgnoreCase))
			Assert.Contains((strict ? "error" : "warning") + " ZS1301", result.Output);
	}

	[Theory]
	[InlineData("ZS1301", "public void Run() => throw new Exception(\"失败\");", "ZS1302")]
	[InlineData("ZS1302", "public string Text => \"你好\";", null)]
	[InlineData("ZS1304", "public object Read() => new global::System.Resources.ResourceManager(typeof(Sample)).GetString(\"Name\");", null)]
	public async Task SeverityOverrides(string disabled, string members, string remaining)
	{
		var result = await AnalyzeAsync(members, config: "root = true\n[*.cs]\ndotnet_diagnostic." + disabled + ".severity = none\n");
		Assert.DoesNotContain("error CS", result.Output);
		Assert.Equal(remaining == null ? new string[0] : new[] { remaining }, Diagnostics(result));
	}

	[Fact]
	public async Task GeneratedCode()
	{
		var result = await AnalyzeAsync("public void Run() => throw new Exception(\"失败\");", generated: true);
		Assert.Empty(Diagnostics(result));
		Assert.DoesNotContain("error CS", result.Output);
		result = await AnalyzeAsync("[global::System.CodeDom.Compiler.GeneratedCode(\"generator\", \"1\")] public void Run() => throw new Exception(\"失败\");");
		Assert.Empty(Diagnostics(result));
	}

	[Theory]
	[InlineData("en-US", "Provide fixed exception message text")]
	[InlineData("zh-Hans", "通过生成的资源属性提供异常消息")]
	public async Task LocalizedDiagnostics(string language, string message)
	{
		var result = await AnalyzerRunner.AnalyzeAsync("class Sample { void Run() => throw new System.Exception(\"Failed.\"); }", true, false, null, false, false, language);
		Assert.Equal(new[] { "ZS1301" }, Diagnostics(result));
		Assert.Contains(message, result.Output);
	}

	private static string[] Diagnostics(AnalysisResult result) => result.Output.Split('\n')
		.Where(line => line.Contains(" ZS130"))
		.Select(line => line.Substring(line.IndexOf(" ZS130") + 1, 6)).OrderBy(id => id).ToArray();

	private static Task<AnalysisResult> AnalyzeAsync(string members, bool strict = false, string testProject = null, string config = null, bool generated = false) =>
		AnalyzerRunner.AnalyzeAsync((generated ? "// <auto-generated/>\n" : "") + "using System;\nnamespace Samples;\npublic class Sample\n{\n" + members + "\n}\n", true, strict, config, false, false, null, testProject);
}
