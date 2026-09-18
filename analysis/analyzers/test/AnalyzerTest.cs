using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class AnalyzerTest
{
	[Fact]
	public async Task PreservesUnusedSystem()
	{
		var result = await AnalyzeAsync("using System;\n\nnamespace Samples;\n\npublic class Sample { }\n");
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS0005", result.Output);
	}

	[Fact]
	public async Task PreservesDisabledDocumentation()
	{
		//与原 IDE0005 一样，关闭文档后编译器不提供 CS8019；不擅自覆盖项目配置。
		var result = await AnalyzeAsync("using System;\nusing Binder = Microsoft.CSharp.RuntimeBinder;\n\nnamespace Samples;\n\npublic class Sample { }\n", documentation: false);
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS0005", result.Output);
	}

	[Fact]
	public async Task DoesNotExemptGlobalSystem()
	{
		var result = await AnalyzeAsync("global using System;\n\nnamespace Samples;\n\npublic class Sample { }\n");
		Assert.False(result.Success, result.Output);
		Assert.Contains("Example.cs(1,1): error ZS0005", result.Output);
	}

	[Theory]
	[InlineData("using Binder = Microsoft.CSharp.RuntimeBinder;", "3,1")]
	[InlineData("using Namespace = System;", "3,1")]
	[InlineData("using Text = System.Text.StringBuilder;", "3,1")]
	[InlineData("using static System.Math;", "3,1")]
	public async Task ReportsOtherUnusedImports(string directive, string position)
	{
		var source = "using System;\nusing System.IO;\n" + directive + "\n\nnamespace Samples;\n\npublic class Sample\n{\n\tpublic FileAttributes Attributes => FileAttributes.Normal;\n}\n";
		var result = await AnalyzeAsync(source);
		Assert.False(result.Success, result.Output);
		Assert.Contains("Example.cs(" + position + "): error ZS0005", result.Output);
		Assert.DoesNotContain("Example.cs(1,1):", result.Output);
	}

	[Fact]
	public async Task AllowsIndentedDirectives()
	{
		var result = await AnalyzeAsync("namespace Samples;\n\npublic class Sample\n{\n\t#if NET10_0_OR_GREATER\n\tpublic int Value => 1;\n\t#elif NET9_0_OR_GREATER\n\tpublic int Value => 2;\n\t#else\n\tpublic int Value => 3;\n\t#endif\n}\n");
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("IDE0055", result.Output);
	}

	[Fact]
	public async Task AllowsCompactTryFinally()
	{
		var result = await AnalyzeAsync("""
		using System;
		using System.Threading;
		using System.Collections.Generic;

		namespace Samples;

		public class Sample
		{
			private readonly ReaderWriterLockSlim _lock = new();
			private readonly List<int> _list = new();

			public int Count
			{
				get
				{
					_lock.EnterReadLock();
					try { return _list.Count; }
					finally { _lock.ExitReadLock(); }
				}
			}
		}
		""");
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("IDE2001", result.Output);
		Assert.DoesNotContain("IDE0055", result.Output);
	}

	[Fact]
	public async Task AllowsCompactTryCatchFinally()
	{
		var result = await AnalyzeAsync("using System;\n\nnamespace Samples;\n\npublic class Sample\n{\n\tpublic int Read()\n\t{\n\t\ttry { return int.Parse(\"1\"); }\n\t\tcatch(FormatException) { return 0; }\n\t\tfinally { Console.WriteLine(); }\n\t}\n}\n");
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("IDE2001", result.Output);
		Assert.DoesNotContain("IDE0055", result.Output);
	}

	[Theory]
	[InlineData("\t\tif(value == 0) return 0;\n\t\treturn value;", "IDE2001")]
	[InlineData("\t\ttry { value++; value++; return value; }\n\t\tfinally { value++; }", "IDE2001")]
	[InlineData("\t\ttry { return value; }\n\t\tfinally { value++; value++; value++; }", "IDE2001")]
	[InlineData("\t\ttry { return value; }\n\t\tcatch(global::System.Exception) { value++; value++; return value; }", "IDE2001")]
	[InlineData("\t\ttry { return value+1; }\n\t\tfinally { value++; }", "IDE0055")]
	[InlineData("\t\tif (value == 0)\n\t\t\treturn 0;\n\t\treturn value;", "IDE0055")]
	public async Task RejectsNonCompactStatements(string body, string diagnostic)
	{
		var result = await AnalyzeAsync("namespace Samples;\n\npublic class Sample\n{\n\tpublic int Read(int value)\n\t{\n" + body + "\n\t}\n}\n");
		Assert.False(result.Success, result.Output);
		Assert.Contains("error " + diagnostic, result.Output);
	}

	[Fact]
	public async Task AllowsLocalConstants()
	{
		var source = """
		using System;
		using System.Buffers;
		using System.Buffers.Binary;

		namespace Samples;

		public static class SequenceExtension
		{
			public static bool TryGetInt16BigEndian(this ReadOnlySequence<byte> buffer, out short value)
			{
				const int SIZE = sizeof(short);

				if(buffer.Length < SIZE)
				{
					value = 0;
					return false;
				}

				if(buffer.First.Length >= SIZE)
					return BinaryPrimitives.TryReadInt16BigEndian(buffer.FirstSpan, out value);

				Span<byte> local = stackalloc byte[SIZE];

				buffer.Slice(0, SIZE).CopyTo(local);
				return BinaryPrimitives.TryReadInt16BigEndian(local, out value);
			}
		}
		""";

		var result = await AnalyzeAsync(source);
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("IDE1006", result.Output);
	}

	[Fact]
	public async Task ReportsLocalVariableNaming()
	{
		var result = await AnalyzeAsync("namespace Samples;\n\npublic class Sample\n{\n\tpublic int Read()\n\t{\n\t\tvar SIZE = 1;\n\t\treturn SIZE;\n\t}\n}\n");
		Assert.False(result.Success, result.Output);
		Assert.Contains("error IDE1006", result.Output);
	}

	[Fact]
	public async Task ReportsWarningsWithoutStrictMode()
	{
		var result = await AnalyzeAsync("using Binder = Microsoft.CSharp.RuntimeBinder;\n\nnamespace Samples;\n\npublic class Sample { }\n", strict: false);
		Assert.True(result.Success, result.Output);
		Assert.Contains("warning ZS0005", result.Output);
	}

	[Fact]
	public async Task RespectsEditorConfigOverride()
	{
		var result = await AnalyzeAsync(
			"using Binder = Microsoft.CSharp.RuntimeBinder;\n\nnamespace Samples;\n\npublic class Sample { }\n",
			editorConfig: "root = true\r\n[*.cs]\r\ndotnet_diagnostic.ZS0005.severity = none\r\n");
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS0005", result.Output);
	}

	[Theory]
	[InlineData("if(enabled)\n\t\t{\n\t\t\tArray.Reverse(items);\n\t\t}\n\t\twhile(enabled)\n\t\t\tenabled = false;", "ZS2003", 1)]
	[InlineData("if(enabled)\n\t\t{\n\t\t\tArray.Reverse(items);\n\t\t}\n\t\t//Next independent statement.\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);", "ZS2003", 1)]
	public async Task ReportsAdjacentControlStatements(string body, string diagnostic, int count)
	{
		var result = await AnalyzeAsync(WrapBody(body));
		Assert.False(result.Success, result.Output);
		Assert.Contains("error " + diagnostic, result.Output);
		//核对不同源码位置的诊断数量。
		var positions = System.Text.RegularExpressions.Regex.Matches(result.Output, @"Example\.cs\(\d+,\d+\): error " + diagnostic)
			.Select(match => match.Value).Distinct().ToArray();
		Assert.Equal(count, positions.Length);
	}

	[Theory]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t\titems[index]++;\n\n\t\tif(enabled)\n\t\t\tArray.Reverse(items);\n\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);")]
	[InlineData("if(enabled)\n\t\t\tforeach(var item in items)\n\t\t\t\tGC.KeepAlive(item);\n\t\telse\n\t\t\tArray.Reverse(items);")]
	[InlineData("do\n\t\t{\n\t\t\tenabled = false;\n\t\t}\n\t\twhile(enabled);")]
	[InlineData("if(enabled)\n\t\t\tArray.Reverse(items);\n\n\t\t//Next independent statement.\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);")]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t\t;\n\t\tif(enabled)\n\t\t\t;\n\t\tforeach(var item in items)\n\t\t\t;")]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t\titems[index]++;\n\t\tif(enabled)\n\t\t\tArray.Reverse(items);\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);")]
	public async Task AllowsSeparatedAndRelatedStatements(string body)
	{
		var result = await AnalyzeAsync(WrapBody(body));
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Fact]
	public async Task ChecksSwitchSectionsAndTopLevelStatements()
	{
		var source = "using System;\n\nvar items = new[] { 1 };\nforeach(var item in items)\n{ GC.KeepAlive(item); }\nif(items.Length > 0)\n\tArray.Reverse(items);\n\nswitch(items.Length)\n{\n\tcase 1:\n\t\tif(items.Length > 0)\n\t\t{ Array.Reverse(items); }\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);\n\t\tbreak;\n}\n";
		var result = await AnalyzeAsync(source, executable: true);
		Assert.False(result.Success, result.Output);
		Assert.Contains("Example.cs(6,1): error ZS2003", result.Output);
		Assert.Contains("Example.cs(14,3): error ZS2003", result.Output);
	}

	[Theory]
	[InlineData("throw new ArgumentException(\"Invalid items.\", nameof(items));")]
	public async Task ReportsNonLocalizedMessages(string body)
	{
		var result = await AnalyzeAsync(WrapBody(body));
		Assert.False(result.Success, result.Output);
		Assert.Contains("Example.cs(9,", result.Output);
		Assert.Contains("error ZS1301", result.Output);
	}


	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NewRulesRespectSeverityOverrides(bool disabled)
	{
		var source = WrapBody("if(enabled)\n\t\t{\n\t\t\tArray.Reverse(items);\n\t\t}\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);\n\n\t\tGC.KeepAlive(new Exception(\"Ready.\"));\n\t\tvar manager = new global::System.Resources.ResourceManager(typeof(Sample));\n\t\tGC.KeepAlive(manager.GetString(\"Ready\"));");
		var config = disabled ? "root = true\r\n[*]\r\nindent_style = tab\r\nindent_size = 4\r\ntab_width = 4\r\n[*.cs]\r\ndotnet_diagnostic.ZS2003.severity = none\r\ndotnet_diagnostic.ZS1304.severity = none\r\ndotnet_diagnostic.ZS1301.severity = none\r\n" : null;
		var result = await AnalyzeAsync(source, strict: disabled, editorConfig: config);
		Assert.True(result.Success, result.Output);
		foreach(var diagnostic in new[] { "ZS2003", "ZS1304", "ZS1301" })
		{
			if(disabled)
				Assert.DoesNotContain(diagnostic, result.Output);
			else
				Assert.Contains("warning " + diagnostic, result.Output);
		}
	}

	[Theory]
	[InlineData("GetString(\"UnusedUsingTitle\")")]
	[InlineData("GetObject(name: nameof(items))")]
	[InlineData("GetStream(\"Logo\")")]
	public async Task ReportsDirectResourceAccess(string call)
	{
		var result = await AnalyzeAsync(WrapBody("var manager = new global::System.Resources.ResourceManager(typeof(Sample));\n\t\tGC.KeepAlive(manager." + call + ");"));
		Assert.False(result.Success, result.Output);
		Assert.Contains("Example.cs(10,", result.Output);
		Assert.Contains("error ZS1304", result.Output);
	}

	[Fact]
	public async Task AllowsLocalizedResourcesAndIdentifiers()
	{
		var result = await AnalyzeAsync(WrapBody("ArgumentNullException.ThrowIfNull(items);\n\t\tGC.KeepAlive(\"application/json\");\n\t\tConsole.WriteLine(global::Zongsoft.CodeAnalysis.Analyzers.Properties.Resources.UnusedUsingTitle);\n\t\tthrow new ArgumentException(global::Zongsoft.CodeAnalysis.Analyzers.Properties.Resources.UnusedUsingTitle, nameof(items));"), resources: true);
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS1301", result.Output);
		Assert.DoesNotContain("ZS1304", result.Output);
	}

	[Fact]
	public async Task AllowsDynamicResourceKeysAndUnrelatedMethods()
	{
		var source = "namespace Samples;\n\npublic static class Sample\n{\n\tpublic static string Read(global::System.Resources.ResourceManager manager, string key) => manager.GetString(key);\n\tpublic static string GetString(string key) => key;\n\tpublic static string Value => GetString(\"Identifier\");\n}\n";
		var result = await AnalyzeAsync(source);
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS1304", result.Output);
	}

	[Fact]
	public async Task IgnoresGeneratedControlStatements()
	{
		var source = "// <auto-generated/>\n" + WrapBody("if(enabled)\n\t\t{\n\t\t\tArray.Reverse(items);\n\t\t}\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);");
		var result = await AnalyzeAsync(source);
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Theory]
	[InlineData("en-US", "Insert a blank line between statement groups")]
	[InlineData("zh-Hans", "语句组之间应留一个空行")]
	public async Task LocalizesAnalyzerDiagnostics(string language, string message)
	{
		var result = await AnalyzeAsync(WrapBody("if(enabled)\n\t\t{\n\t\t\tArray.Reverse(items);\n\t\t}\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);"), language: language, strict: false);
		Assert.True(result.Success, result.Output);
		Assert.Contains("warning ZS2003: " + message, result.Output);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AllowsDeclarationGroupBeforeInvocation(bool separated)
	{
		var body = "var x = 1;\n\t\tvar y = 2.0;\n\t\tvar foo = \"...\";\n" + (separated ? "\n" : "") + "\t\tArgumentNullException.ThrowIfNull(items);";
		var result = await AnalyzeAsync(WrapBody(body));
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Theory]
	[InlineData("var count = items.Length;\n\t\titems[0] = count;")]
	[InlineData("GC.KeepAlive(items);\n\t\tvar count = items.Length;")]
	[InlineData("var count = items.Length;\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(count);")]
	[InlineData("var count = items.Length;\n\t\tcount += 1;\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(count);")]
	[InlineData("int x = 1, y = 2, z = 3;\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(x + y + z);")]
	[InlineData("var count = items.Length;\n\t\tcount += 1;\n\n\t\tcount += 2;\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(count);")]
	[InlineData("var count = items.Length;\n\t\tcount += 1;\n\t\tGC.KeepAlive(count);\n\t\tcount += 2;\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(count);")]
	[InlineData("var count = items.Length;\n\t\tcount += 1;\n\t\tint next;\n\t\tcount += 2;\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(count);")]
	public async Task AllowsShortOrInterruptedAssignmentGroups(string body)
	{
		var result = await AnalyzeAsync(WrapBody(body));
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Theory]
	[InlineData(1, false)]
	[InlineData(2, false)]
	[InlineData(3, false)]
	[InlineData(4, false)]
	[InlineData(3, true)]
	public async Task RequiresBlankLineAfterMoreThanTwoAssignments(int count, bool separated)
	{
		var body = string.Join("\n\t\t", Enumerable.Repeat("items[0] = 1;", count)) +
			(separated ? "\n\n" : "\n") + "\t\tif(enabled)\n\t\t\tArray.Reverse(items);";
		var result = await AnalyzeAsync(WrapBody(body));
		if(count > 2 && !separated)
		{
			Assert.False(result.Success, result.Output);
			var positions = System.Text.RegularExpressions.Regex.Matches(result.Output, @"Example\.cs\(\d+,\d+\): error ZS2003")
				.Select(match => match.Value).Distinct().ToArray();
			Assert.Equal($"Example.cs({9 + count},3): error ZS2003", Assert.Single(positions));
		}
		else
		{
			Assert.True(result.Success, result.Output);
			Assert.DoesNotContain("ZS2003", result.Output);
		}
	}

	[Theory]
	[InlineData("if(enabled)\n\t\t\tGC.KeepAlive(count);", true)]
	[InlineData("switch(count)\n\t\t{\n\t\t\tcase 1: break;\n\t\t}", true)]
	[InlineData("for(var index = 0; index < count; index++)\n\t\t\tGC.KeepAlive(index);", true)]
	[InlineData("foreach(var item in items)\n\t\t\tGC.KeepAlive(item);", true)]
	[InlineData("foreach(var (first, second) in new[] { (1, 2) })\n\t\t\tGC.KeepAlive(first + second);", true)]
	[InlineData("while(enabled)\n\t\t\tenabled = false;", true)]
	[InlineData("do { enabled = false; } while(enabled);", true)]
	[InlineData("GC.KeepAlive(count);", false)]
	[InlineData("return;", false)]
	[InlineData("try { GC.KeepAlive(count); }\n\t\tfinally { GC.KeepAlive(items); }", false)]
	[InlineData("lock(items)\n\t\t\tGC.KeepAlive(count);", false)]
	[InlineData("using(var stream = new global::System.IO.MemoryStream())\n\t\t\tGC.KeepAlive(stream);", false)]
	public async Task ChecksAssignmentGroupBeforeConditionsAndLoopsOnly(string next, bool expected)
	{
		var result = await AnalyzeAsync(WrapBody("var (count, text) = (1, \"value\");\n\t\tcount += 1;\n\t\t(count, text) = (2, text.Trim());\n\t\t" + next));
		if(expected)
		{
			Assert.False(result.Success, result.Output);
			Assert.Contains("Example.cs(12,3): error ZS2003", result.Output);
			Assert.DoesNotContain("Example.cs(10,3): error ZS2003", result.Output);
			Assert.DoesNotContain("Example.cs(11,3): error ZS2003", result.Output);
		}
		else
		{
			Assert.True(result.Success, result.Output);
			Assert.DoesNotContain("ZS2003", result.Output);
		}
	}

	[Fact]
	public async Task AllowsDocumentedAssignmentGroup()
	{
		var source = "using System;\n\nnamespace Samples;\n\npublic static class Sample\n{\n\tpublic static void Run(int[] items)\n\t{\n\t\tvar count = items.Length;\n\t\tvar index = 0;\n\t\tvar enabled = count > 0;\n\n\t\tif(enabled)\n\t\t\tProcess(items[index]);\n\t}\n\n\tprivate static void Process(int value) => GC.KeepAlive(value);\n}\n";
		var result = await AnalyzeAsync(source);
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Fact]
	public async Task AllowsEntityCreationMappingAndReturn()
	{
		var source = "using System;\n\nnamespace Samples;\n\npublic static class Sample\n{\n\tpublic static object Create(Type type, Action<object, object> map, object state)\n\t{\n\t\tvar entity = GetCreator(type)();\n\t\tmap?.Invoke(entity, state);\n\t\treturn entity;\n\t}\n\n\tprivate static Func<object> GetCreator(Type type) => () => Activator.CreateInstance(type);\n}\n";
		var result = await AnalyzeAsync(source);
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Theory]
	[InlineData("var (count, text) = (1, \"value\");\n\t\tvar next = count + 1;\n\n\t\tGC.KeepAlive(text);\n\t\tGC.KeepAlive(next);")]
	[InlineData("(int count, string text) = (1, \"value\");\n\t\tvar next = count + 1;\n\n\t\tGC.KeepAlive(text);\n\t\tGC.KeepAlive(next);")]
	[InlineData("const int SIZE = 1;\n\t\tusing var stream = new global::System.IO.MemoryStream(SIZE);\n\n\t\tGC.KeepAlive(stream);")]
	[InlineData("var count = items.Length;\n\t\treturn;")]
	[InlineData("items[0] = 1;\n\t\tArray.Reverse(items);\n\t\tGC.KeepAlive(items);")]
	public async Task AllowsCohesiveStatementGroups(string body)
	{
		var result = await AnalyzeAsync(WrapBody(body));
		Assert.True(result.Success, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	private static string WrapBody(string body) => "using System;\n\nnamespace Samples;\n\npublic static class Sample\n{\n\tpublic static void Run(int[] items, bool enabled)\n\t{\n\t\t" + body + "\n\t}\n}\n";

	private static Task<AnalysisResult> AnalyzeAsync(string source, bool documentation = true, bool strict = true, string editorConfig = null, bool resources = false, bool executable = false, string language = null) =>
		AnalyzerRunner.AnalyzeAsync(source, documentation, strict, editorConfig, resources, executable, language);
}
