using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class StatementSpacingTest
{
	[Theory]
	[InlineData("if(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("if(enabled)\n\t\t\tGC.KeepAlive(items);\n\t\telse\n\t\t\tGC.KeepAlive(enabled);")]
	[InlineData("if(enabled)\n\t\t\tGC.KeepAlive(items);\n\t\telse if(disabled)\n\t\t\tGC.KeepAlive(enabled);")]
	[InlineData("if(enabled)\n\t\t\twhile(enabled)\n\t\t\t\tenabled = false;")]
	[InlineData("foreach(var item in items)\n\t\t{\n\t\t\tGC.KeepAlive(item);\n\t\t}")]
	[InlineData("foreach(var (first, second) in new[] { (1, 2) })\n\t\t{\n\t\t\tGC.KeepAlive(first + second);\n\t\t}")]
	[InlineData("foreach(var item in items)\n\t\t\tif(enabled)\n\t\t\t\tGC.KeepAlive(item);")]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t{\n\t\t\tGC.KeepAlive(items[index]);\n\t\t}")]
	[InlineData("while(enabled)\n\t\t{\n\t\t\tenabled = false;\n\t\t}")]
	[InlineData("do\n\t\t{\n\t\t\tenabled = false;\n\t\t}\n\t\twhile(enabled);")]
	[InlineData("do\n\t\t\tenabled = false;\n\t\twhile(enabled);")]
	[InlineData("switch(items.Length)\n\t\t{\n\t\t\tcase 0:\n\t\t\t\tbreak;\n\t\t}")]
	[InlineData("start:\n\t\tif(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	public async Task ChecksConditionalPreparationThresholdAsync(string structure)
	{
		foreach(var count in new[] { 0, 1, 2, 3 })
		{
			foreach(var separated in new[] { false, true })
			{
				var preparation = string.Join("\n\t\t", Enumerable.Repeat("GC.KeepAlive(items);", count));
				var source = Wrap(preparation + (count > 0 ? separated ? "\n\n\t\t" : "\n\t\t" : "") + structure)
					.Replace("bool enabled)", "bool enabled, bool disabled)").Replace("\t\tstart:", "\tstart:");
				var result = await AnalyzeAsync(source);

				AssertSpacing(result, source, count >= 2 && !separated ? structure : null);
			}
		}
	}

	[Theory]
	[InlineData("try\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}\n\t\tcatch(Exception)\n\t\t{\n\t\t\tenabled = false;\n\t\t}\n\t\tfinally\n\t\t{\n\t\t\tGC.KeepAlive(enabled);\n\t\t}")]
	[InlineData("try { GC.KeepAlive(items); } catch { }")]
	[InlineData("try { GC.KeepAlive(items); }\n\t\tfinally { GC.KeepAlive(enabled); }")]
	[InlineData("using(var stream = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(stream);\n\t\t}")]
	[InlineData("using(var stream = new global::System.IO.MemoryStream())\n\t\t\tGC.KeepAlive(stream);")]
	[InlineData("lock(items)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("lock(items)\n\t\t\tGC.KeepAlive(items);")]
	[InlineData("checked\n\t\t{\n\t\t\titems[0]++;\n\t\t}")]
	[InlineData("unchecked\n\t\t{\n\t\t\titems[0]++;\n\t\t}")]
	[InlineData("unsafe\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("fixed(int* address = items)\n\t\t{\n\t\t\tGC.KeepAlive((nint)address);\n\t\t}")]
	[InlineData("{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("start:\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	public async Task ChecksBeforeComplexStructureAsync(string structure)
	{
		foreach(var separated in new[] { false, true })
		{
			var source = Wrap("GC.KeepAlive(items);" + (separated ? "\n\n" : "\n") + "\t\t" + structure)
				.Replace("bool enabled)", "bool enabled, bool disabled)").Replace("\t\tstart:", "\tstart:");
			var result = await AnalyzeAsync(source);

			AssertSpacing(result, source, separated ? null : structure);
		}
	}

	[Theory]
	[InlineData("var count = items.Length;")]
	[InlineData("var count = items.Length;\n\t\tvar index = 0;")]
	[InlineData("GC.KeepAlive(items);")]
	[InlineData("enabled = items.Length > 0;")]
	[InlineData("using var stream = new global::System.IO.MemoryStream();")]
	[InlineData("if(items.Length == 0)\n\t\t\treturn;")]
	public async Task DoesNotExemptPreparationBeforeTryAsync(string preparation)
	{
		var structure = "try { GC.KeepAlive(items); } finally { }";
		var source = Wrap(preparation + "\n\t\t" + structure);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, structure);
	}

	[Theory]
	[InlineData("await using(var stream = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(stream);\n\t\t}")]
	[InlineData("await foreach(var item in asyncItems)\n\t\t{\n\t\t\tGC.KeepAlive(item);\n\t\t}")]
	public async Task ChecksBeforeAsyncStructureAsync(string structure)
	{
		var source = Wrap("GC.KeepAlive(items);\n\t\tGC.KeepAlive(enabled);\n\t\t" + structure)
			.Replace("public static unsafe void Run", "public static async global::System.Threading.Tasks.Task Run")
			.Replace("bool enabled)", "bool enabled, global::System.Collections.Generic.IAsyncEnumerable<int> asyncItems)");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, structure);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task CountsUninitializedDeclarationBeforeSwitchAsync(int count)
	{
		var structure = "switch(items.Length)\n\t\t{\n\t\t\tdefault:\n\t\t\t\tcount = items.Length;\n\t\t\t\tbreak;\n\t\t}";
		var source = Wrap("int count;\n\t\t" + (count == 2 ? "GC.KeepAlive(items);\n\t\t" : "") + structure + "\n\n\t\tGC.KeepAlive(count);");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, count == 2 ? structure : null);
	}

	[Theory]
	[InlineData("var first = 1;", 1)]
	[InlineData("int first;", 1)]
	[InlineData("var first = 1;\n\t\tvar second = 2;", 2)]
	[InlineData("GC.KeepAlive(items);\n\t\tGC.KeepAlive(enabled);", 2)]
	[InlineData("enabled = true;\n\t\tenabled = false;", 2)]
	[InlineData("items[0]++;\n\t\titems[0]--;", 2)]
	[InlineData("var first = 1;\n\t\tGC.KeepAlive(first);", 2)]
	[InlineData("GC.KeepAlive(items);\n\t\tenabled = true;\n\t\titems[0]++;", 3)]
	[InlineData("var count = items.Length;\n\t\tcount += 1;\n\t\tGC.KeepAlive(count);\n\t\tcount += 2;", 4)]
	[InlineData("var count = items.Length;\n\t\tcount += 1;\n\t\tint next;\n\t\tcount += 2;", 4)]
	[InlineData("int first = 1, second = 2;", 1)]
	[InlineData("var (first, second) = (1, 2);", 1)]
	[InlineData("var values = new[]\n\t\t{\n\t\t\t1, 2, 3,\n\t\t};", 1)]
	[InlineData("var values = new[]\n\t\t{\n\t\t\t1, 2, 3,\n\t\t};\n\t\tGC.KeepAlive(values);", 2)]
	public async Task CountsOrdinaryStatementsAsync(string preparation, int count)
	{
		foreach(var simple in new[] { false, true })
		{
			var structure = simple ? "if(enabled)\n\t\t\tGC.KeepAlive(items);" : "if(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}";
			var source = Wrap(preparation + "\n\t\t" + structure);
			var result = await AnalyzeAsync(source);

			AssertSpacing(result, source, count >= (simple ? 3 : 2) ? structure : null);
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public async Task CountsAwaitedCallsAsync(int count)
	{
		var structure = "await foreach(var item in asyncItems)\n\t\t{\n\t\t\tGC.KeepAlive(item);\n\t\t}";
		var source = Wrap(string.Concat(Enumerable.Repeat("await global::System.Threading.Tasks.Task.CompletedTask;\n\t\t", count)) + structure)
			.Replace("public static unsafe void Run", "public static async global::System.Threading.Tasks.Task Run")
			.Replace("bool enabled)", "bool enabled, global::System.Collections.Generic.IAsyncEnumerable<int> asyncItems)");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, count >= 2 ? structure : null);
	}

	[Theory]
	[InlineData("\n", false)]
	[InlineData("\n\n", true)]
	[InlineData("\n\t\t//Continue preparation.\n", false)]
	[InlineData("\n\t\t/* Continue\n\n\t\tpreparation. */\n", false)]
	[InlineData("\n#pragma warning disable CS0162\n", false)]
	public async Task ResetsCountOnlyAtRealBlankLineAsync(string separator, bool separated)
	{
		var structure = "if(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}";
		var source = Wrap("GC.KeepAlive(items);" + separator + "\t\tGC.KeepAlive(enabled);\n\t\t" + structure);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, separated ? null : structure);
	}

	[Theory]
	[InlineData("if(enabled)\n\t\t\tGC.KeepAlive(items);")]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t\tGC.KeepAlive(items[index]);")]
	[InlineData("foreach(var item in items)\n\t\t\tGC.KeepAlive(item);")]
	[InlineData("while(enabled)\n\t\t\t;")]
	public async Task PreservesSimpleControlPreparationAsync(string structure)
	{
		foreach(var assignments in new[] { 1, 2, 3 })
		{
			var preparation = string.Concat(Enumerable.Repeat("enabled = true;\n\t\t", assignments));
			var source = Wrap(preparation + structure);
			var result = await AnalyzeAsync(source);

			AssertSpacing(result, source, assignments > 2 ? structure : null);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksResourceGroupStartAsync(bool asynchronous)
	{
		var resource = asynchronous ? "await using" : "using";
		var group = resource + " var buffer = new global::System.IO.MemoryStream();\n\t\t" +
			resource + " global::System.IO.MemoryStream publisher = new();\n\t\t" +
			resource + "(var stream = new global::System.IO.MemoryStream())\n\t\t" +
			resource + "(var nested = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(nested);\n\t\t}";

		foreach(var preparation in new[] { "", "GC.KeepAlive(items);\n\t\t", "GC.KeepAlive(items);\n\n\t\t" })
		{
			var source = Wrap(preparation + group).Replace("public static unsafe void Run", "public static async global::System.Threading.Tasks.Task Run");
			var result = await AnalyzeAsync(source);

			AssertSpacing(result, source, preparation == "GC.KeepAlive(items);\n\t\t" ? group : null);
		}
	}

	[Theory]
	[InlineData("using var first = new global::System.IO.MemoryStream();\n\t\tusing var second = new global::System.IO.MemoryStream();\n\t\tGC.KeepAlive(items);", null)]
	[InlineData("using var first = new global::System.IO.MemoryStream();\n\n\t\tusing(var second = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(second);\n\t\t}", null)]
	[InlineData("using var first = new global::System.IO.MemoryStream();\n\t\tGC.KeepAlive(first);\n\t\tusing(var second = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(second);\n\t\t}", "using(var second")]
	[InlineData("var first = new global::System.IO.MemoryStream();\n\t\tusing(var second = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(second);\n\t\t}", "using(var second")]
	[InlineData("using(var first = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(first);\n\t\t}\n\t\tusing(var second = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(second);\n\t\t}", "using(var second")]
	[InlineData("using(var first = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(first);\n\t\t}\n\t\tusing var second = new global::System.IO.MemoryStream();\n\t\tusing(var third = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(third);\n\t\t}", "using var second")]
	[InlineData("using var first = new global::System.IO.MemoryStream();\n\t\tusing var second = new global::System.IO.MemoryStream();\n\t\tusing var third = new global::System.IO.MemoryStream();\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(items);", "if(enabled)")]
	[InlineData("GC.KeepAlive(items);\n\n\t\tusing var first = new global::System.IO.MemoryStream();\n\t\t/* Resources\n\n\t\tremain grouped. */\n\t\tusing(var second = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(second);\n\t\t}", null)]
	public async Task RestrictsResourceGroupExceptionAsync(string body, string statement)
	{
		var source = Wrap(body);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, statement);
	}

	[Theory]
	[InlineData("\t\t//Process resources.\n", false)]
	[InlineData("\t\t/* Process\n\n\t\tresources. */\n", false)]
	[InlineData("\n\t\t//Process resources.\n", true)]
	[InlineData("#if NET10_0\n", false)]
	public async Task ChecksTriviaBeforeResourceGroupAsync(string trivia, bool separated)
	{
		var group = "using var buffer = new global::System.IO.MemoryStream();\n\t\t" +
			"using(var stream = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(stream);\n\t\t}";
		var source = Wrap("GC.KeepAlive(items);\n" + trivia + "\t\t" + group + (trivia.StartsWith("#if", StringComparison.Ordinal) ? "\n#endif" : ""));
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, separated ? null : group);
	}

	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	public async Task ChecksAssertionBeforeLoopAsync(string testProject)
	{
		var structure = "foreach(var item in items)\n\t\t{\n\t\t\tGC.KeepAlive(item);\n\t\t}";
		foreach(var count in new[] { 1, 2 })
		{
			var source = Wrap(string.Concat(Enumerable.Repeat("global::Xunit.Assert.NotNull(items);\n\t\t", count)) + structure);
			var result = await AnalyzeAsync(source, testProject: testProject);

			AssertSpacing(result, source, count == 2 ? structure : null);
		}
	}

	[Theory]
	[InlineData("try { Console.WriteLine(); } finally { }")]
	[InlineData("using var buffer = new global::System.IO.MemoryStream();\nusing(var stream = new global::System.IO.MemoryStream())\n{\n\tConsole.WriteLine();\n}")]
	[InlineData("await using var buffer = new global::System.IO.MemoryStream();\nawait using(var stream = new global::System.IO.MemoryStream())\n{\n\tConsole.WriteLine();\n}")]
	public async Task ChecksBeforeTopLevelStructureAsync(string structure)
	{
		var source = "using System;\n\nConsole.WriteLine();\n" + structure + "\n";
		var result = await AnalyzeAsync(source, executable: true);

		AssertSpacing(result, source, structure);
	}

	[Theory]
	[InlineData("void Local()")]
	[InlineData("Action action = () =>")]
	[InlineData("Action action = delegate()")]
	public async Task ChecksBeforeStructureInsideFunctionAsync(string declaration)
	{
		var structure = "try { GC.KeepAlive(items); } catch { }";
		var body = declaration + "\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t\t" + structure + "\n\t\t}" +
			(declaration.StartsWith("Action", StringComparison.Ordinal) ? ";\n\t\taction();" : "\n\t\tLocal();");
		var source = Wrap(body);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, structure);
	}

	[Fact]
	public async Task ChecksBeforeStructureInsideSwitchSectionAsync()
	{
		var source = Wrap("switch(items.Length)\n\t\t{\n\t\t\tcase 0:\n\t\t\t\tGC.KeepAlive(items);\n\t\t\t\ttry { GC.KeepAlive(enabled); } finally { }\n\t\t\t\tbreak;\n\t\t}");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, "try {");
	}

	[Theory]
	[InlineData("void Local()")]
	[InlineData("Action action = () =>")]
	[InlineData("Action action = delegate()")]
	public async Task CountsPreparationInsideFunctionsAsync(string declaration)
	{
		foreach(var count in new[] { 1, 2 })
		{
			var structure = "if(enabled)\n\t\t\t{\n\t\t\t\tGC.KeepAlive(items);\n\t\t\t}";
			var body = declaration + "\n\t\t{\n\t\t\t" + string.Concat(Enumerable.Repeat("GC.KeepAlive(items);\n\t\t\t", count)) + structure + "\n\t\t}" +
				(declaration.StartsWith("Action", StringComparison.Ordinal) ? ";\n\t\taction();" : "\n\t\tLocal();");
			var source = Wrap(body);
			var result = await AnalyzeAsync(source);

			AssertSpacing(result, source, count == 2 ? structure : null);
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task CountsPreparationInsideSwitchSectionAsync(int count)
	{
		var structure = "if(enabled)\n\t\t\t\t{\n\t\t\t\t\tGC.KeepAlive(items);\n\t\t\t\t}";
		var source = Wrap("switch(items.Length)\n\t\t{\n\t\t\tcase 0:\n\t\t\t\t" +
			string.Concat(Enumerable.Repeat("GC.KeepAlive(items);\n\t\t\t\t", count)) + structure + "\n\t\t\t\tbreak;\n\t\t}");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, count == 2 ? structure : null);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task CountsTopLevelPreparationAsync(int count)
	{
		var structure = "if(Environment.TickCount > 0)\n{\n\tConsole.WriteLine();\n}";
		var source = "using System;\n\n" + string.Concat(Enumerable.Repeat("Console.WriteLine();\n", count)) + structure + "\n";
		var result = await AnalyzeAsync(source, executable: true);

		AssertSpacing(result, source, count == 2 ? structure : null);
	}

	[Fact]
	public async Task IgnoresGeneratedPreparationBoundaryAsync()
	{
		var source = "// <auto-generated/>\n" + Wrap("GC.KeepAlive(items);\n\t\ttry { } catch { }");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, null);
	}

	[Theory]
	[InlineData("if(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("if(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}\n\t\telse\n\t\t\tenabled = true;")]
	[InlineData("if(enabled)\n\t\t\twhile(enabled)\n\t\t\t{\n\t\t\t\tenabled = false;\n\t\t\t}")]
	[InlineData("foreach(var item in items)\n\t\t{\n\t\t\tGC.KeepAlive(item);\n\t\t}")]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t{\n\t\t\tGC.KeepAlive(items[index]);\n\t\t}")]
	[InlineData("while(enabled)\n\t\t{\n\t\t\tenabled = false;\n\t\t}")]
	[InlineData("do\n\t\t{\n\t\t\tenabled = false;\n\t\t}\n\t\twhile(enabled);")]
	[InlineData("switch(items.Length)\n\t\t{\n\t\t\tcase 0:\n\t\t\t\tbreak;\n\t\t}")]
	[InlineData("try\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}\n\t\tcatch(Exception)\n\t\t{\n\t\t\tenabled = false;\n\t\t}\n\t\tfinally\n\t\t{\n\t\t\tGC.KeepAlive(enabled);\n\t\t}")]
	[InlineData("try { GC.KeepAlive(items); } catch { }")]
	[InlineData("using(var stream = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(stream);\n\t\t}")]
	[InlineData("lock(items)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("checked\n\t\t{\n\t\t\titems[0]++;\n\t\t}")]
	[InlineData("unchecked\n\t\t{\n\t\t\titems[0]++;\n\t\t}")]
	[InlineData("unsafe\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("fixed(int* address = items)\n\t\t{\n\t\t\tGC.KeepAlive((nint)address);\n\t\t}")]
	[InlineData("{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	[InlineData("start:\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}")]
	public async Task ChecksBlockBeforeInvocationAsync(string structure)
	{
		foreach(var separated in new[] { false, true })
		{
			var source = Wrap(structure + (separated ? "\n\n" : "\n") + "\t\tGC.KeepAlive(items);").Replace("\t\tstart:", "\tstart:");
			var result = await AnalyzeAsync(source);

			AssertSpacing(result, source, separated ? null : "GC.KeepAlive(items);");
		}
	}

	[Theory]
	[InlineData("items[0] = 1;")]
	[InlineData("var count = items.Length;")]
	[InlineData("return;")]
	[InlineData("throw new InvalidOperationException();")]
	[InlineData("GC.KeepAlive(enabled);")]
	public async Task DoesNotExemptFollowingStatementsAsync(string statement)
	{
		var source = Wrap("if(enabled)\n\t\t{\n\t\t\titems[0]++;\n\t\t}\n\t\t" + statement);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, statement);
	}

	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	public async Task ChecksTestAndProductionProjectsAsync(string testProject)
	{
		var source = Wrap("foreach(var item in items)\n\t\t{\n\t\t\tGC.KeepAlive(item);\n\t\t}\n\t\tGC.KeepAlive(items);");
		var result = await AnalyzeAsync(source, testProject: testProject);

		AssertSpacing(result, source, "GC.KeepAlive(items);");
	}

	[Theory]
	[InlineData("await using(var stream = new global::System.IO.MemoryStream())\n\t\t{\n\t\t\tGC.KeepAlive(stream);\n\t\t}")]
	[InlineData("await foreach(var item in asyncItems)\n\t\t{\n\t\t\tGC.KeepAlive(item);\n\t\t}")]
	public async Task ChecksAsyncBlocksAsync(string structure)
	{
		var source = Wrap(structure + "\n\t\tGC.KeepAlive(items);")
			.Replace("public static unsafe void Run", "public static async global::System.Threading.Tasks.Task Run")
			.Replace("bool enabled)", "bool enabled, global::System.Collections.Generic.IAsyncEnumerable<int> asyncItems)");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, "GC.KeepAlive(items);");
	}

	[Theory]
	[InlineData("try { GC.KeepAlive(items); } catch { }", "try { GC.KeepAlive(items); } finally { }", true)]
	[InlineData("try { enabled = true; GC.KeepAlive(items); } catch(Exception) { enabled = false; GC.KeepAlive(items); } finally { GC.KeepAlive(enabled); }", "try { } catch { }", true)]
	[InlineData("try { GC.KeepAlive(items); }\n\t\tcatch { }", "try { } catch { }", false)]
	[InlineData("try { } catch { }", "try\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}\n\t\tcatch { }", false)]
	[InlineData("try { enabled = true; enabled = false; GC.KeepAlive(items); } catch { }", "try { } catch { }", false)]
	[InlineData("try { } catch { enabled = true; enabled = false; GC.KeepAlive(items); }", "try { } catch { }", false)]
	[InlineData("try { } finally { enabled = true; enabled = false; GC.KeepAlive(items); }", "try { } catch { }", false)]
	public async Task RestrictsCompactTryExceptionAsync(string first, string second, bool allowed)
	{
		var source = Wrap(first + "\n\t\t" + second);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, allowed ? null : second, checkSuccess: allowed);
	}

	[Fact]
	public async Task DoesNotGroupCompactTriesOnOneLineAsync()
	{
		var source = Wrap("try { GC.KeepAlive(items); } catch { } try { } catch { }");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, "try { } catch { }");
	}

	[Fact]
	public async Task AllowsCompactTryGroupInsideConditionalCompilationAsync()
	{
		var source = Wrap("#if NET10_0\n\t\ttry { GC.KeepAlive(items); } catch { }\n\t\ttry { } catch { }\n#endif");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, null);
	}

	[Theory]
	[InlineData("break;", true)]
	[InlineData("return;", false)]
	[InlineData("throw new InvalidOperationException();", false)]
	[InlineData("goto default;", false)]
	[InlineData("break;\n\t\t\t\t;", false)]
	public async Task RestrictsSwitchBreakExceptionAsync(string statement, bool allowed)
	{
		var source = Wrap("switch(items.Length)\n\t\t{\n\t\t\tcase 0:\n\t\t\t\tif(enabled)\n\t\t\t\t{\n\t\t\t\t\tGC.KeepAlive(items);\n\t\t\t\t}\n\t\t\t\t" + statement + "\n\t\t\tdefault:\n\t\t\t\tbreak;\n\t\t}");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, allowed ? null : statement);
	}

	[Theory]
	[InlineData("break;")]
	[InlineData("continue;")]
	public async Task DoesNotExemptLoopJumpsAsync(string statement)
	{
		var source = Wrap("while(enabled)\n\t\t{\n\t\t\tif(items.Length == 0)\n\t\t\t{\n\t\t\t\tenabled = false;\n\t\t\t}\n\t\t\t" + statement + "\n\t\t}");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, statement);
	}

	[Fact]
	public async Task DoesNotExemptNestedSwitchBreakAsync()
	{
		var source = Wrap("switch(items.Length)\n\t\t{\n\t\t\tcase 0:\n\t\t\t{\n\t\t\t\tif(enabled)\n\t\t\t\t{\n\t\t\t\t\tGC.KeepAlive(items);\n\t\t\t\t}\n\t\t\t\tbreak;\n\t\t\t}\n\t\t}");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, "break;");
	}

	[Theory]
	[InlineData("\t\t//Next statement.\n", false)]
	[InlineData("\t\t/* Next\n\n\t\tstatement. */\n", false)]
	[InlineData("\t\t/* Next\n\n\t\tstatement. */\n\n", true)]
	[InlineData("\n\t\t//Next statement.\n", true)]
	[InlineData("#pragma warning disable CS0162\n", false)]
	[InlineData("#if false\n\nGC.KeepAlive(items);\n#endif\n", false)]
	[InlineData("#if false\n\nGC.KeepAlive(items);\n#endif\n\n", true)]
	public async Task RequiresBlankLineOutsideTriviaAsync(string trivia, bool separated)
	{
		var source = Wrap("if(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}\n" + trivia + "\t\tGC.KeepAlive(items);");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, separated ? null : "GC.KeepAlive(items);");
	}

	[Theory]
	[InlineData("var values = new int[]\n\t\t{\n\t\t\t1, 2, 3,\n\t\t};\n\t\tGC.KeepAlive(values);")]
	[InlineData("Action action = () =>\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t};\n\t\taction();")]
	[InlineData("Action action = delegate()\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t};\n\t\taction();")]
	[InlineData("var count = items.Length switch\n\t\t{\n\t\t\t0 => 0,\n\t\t\t_ => 1,\n\t\t};\n\t\tGC.KeepAlive(count);")]
	[InlineData("if(items is { Length: > 0 })\n\t\t\tGC.KeepAlive(items);\n\t\tGC.KeepAlive(enabled);")]
	[InlineData("void Local()\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}\n\t\tLocal();")]
	public async Task IgnoresExpressionAndLocalFunctionBracesAsync(string body)
	{
		var source = Wrap(body);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, null);
	}

	[Theory]
	[InlineData("void Local()\n\t\t{\n\t\t\tif(enabled)\n\t\t\t{\n\t\t\t\tGC.KeepAlive(items);\n\t\t\t}\n\t\t\treturn;\n\t\t}\n\t\tLocal();")]
	[InlineData("Action action = () =>\n\t\t{\n\t\t\tif(enabled)\n\t\t\t{\n\t\t\t\tGC.KeepAlive(items);\n\t\t\t}\n\t\t\treturn;\n\t\t};\n\t\taction();")]
	[InlineData("try { if(enabled) { GC.KeepAlive(items); } return; } catch { }")]
	public async Task ChecksInsideLocalAndAnonymousFunctionsAsync(string body)
	{
		var source = Wrap(body);
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, "return;");
	}

	[Fact]
	public async Task ChecksTopLevelStatementBoundaryAsync()
	{
		var source = "using System;\n\nif(Environment.TickCount > 0)\n{\n\tConsole.WriteLine();\n}\nConsole.WriteLine();\n";
		var result = await AnalyzeAsync(source, executable: true);

		AssertSpacing(result, source, "Console.WriteLine();");
	}

	[Fact]
	public async Task IgnoresGeneratedBlockBoundaryAsync()
	{
		var source = "// <auto-generated/>\n" + Wrap("if(enabled)\n\t\t{\n\t\t\tGC.KeepAlive(items);\n\t\t}\n\t\treturn;");
		var result = await AnalyzeAsync(source);

		AssertSpacing(result, source, null);
	}

	private static string Wrap(string body) => "using System;\n\nnamespace Samples;\n\npublic static class Sample\n{\n\tpublic static unsafe void Run(int[] items, bool enabled)\n\t{\n\t\t" + body + "\n\t}\n}\n";

	//保留无块嵌套片段以覆盖分隔边界，嵌套花括号要求由对应规则的测试独立验证。
	private static Task<AnalysisResult> AnalyzeAsync(string source, bool executable = false, string testProject = null) =>
		AnalyzerRunner.AnalyzeAsync(source, false, true,
			"root = true\n[*]\nindent_style = tab\nindent_size = 4\ntab_width = 4\nend_of_line = crlf\ndotnet_diagnostic.ZS2005.severity = none\ndotnet_diagnostic.ZS2006.severity = none\n",
			false, executable, "en-US", testProject, unsafeCode: true);

	private static void AssertSpacing(AnalysisResult result, string source, string statement, bool checkSuccess = true)
	{
		Assert.DoesNotContain("error CS", result.Output);

		var actual = Regex.Matches(result.Output, @"Example\.cs\(\d+,\d+\): error ZS2003").Select(match => match.Value).ToArray();

		if(statement == null)
			Assert.Empty(actual);
		else
		{
			var offset = source.LastIndexOf(statement, StringComparison.Ordinal);
			Assert.True(offset >= 0);
			var before = source[..offset];
			var line = before.Count(character => character == '\n') + 1;
			var column = offset - before.LastIndexOf('\n');
			Assert.Equal($"Example.cs({line},{column}): error ZS2003", Assert.Single(actual));
		}

		if(checkSuccess)
			Assert.True(result.Success == (statement == null), result.Output);
	}
}
