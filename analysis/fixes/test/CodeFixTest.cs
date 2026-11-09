using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Globalization;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Composition.Hosting;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Zongsoft.CodeAnalysis.Fixes.Tests;

public partial class CodeFixTest
{
	private static readonly Lazy<(Assembly Analyzer, Assembly Fixes, string[] Entries)> _package = new(LoadPackage);
	private static readonly Lazy<ImmutableArray<MetadataReference>> _references = new(() =>
		((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
			.Select(path => MetadataReference.CreateFromFile(path)).ToImmutableArray<MetadataReference>());

	[Theory]
	[InlineData("ZS0005", "using Binder = Microsoft.CSharp.RuntimeBinder;\nclass Sample { }")]
	[InlineData("ZS1301", "class Sample { void Run() => throw new System.Exception(\"Failed.\"); }")]
	[InlineData("ZS1302", "class Sample { string Text => \"你好\"; }")]
	[InlineData("ZS1304", "class Sample { string Read() => new System.Resources.ResourceManager(typeof(Sample)).GetString(\"Name\"); }")]
	[InlineData("ZS2003", "class Sample { void Run() { if(true) { } if(false) { } } }")]
	[InlineData("ZS2005", "class Sample { void Run() { for(var index = 0; index < 1; index++) if(true) System.GC.KeepAlive(index); } }")]
	[InlineData("ZS2006", "class Sample { void Run() { static int Get() => 1; _ = Get(); } }")]
	[InlineData("ZS3003", "/// <summary>\n/// One line.\n/// </summary>\nclass Sample { }")]
	public async Task DiagnosticsLinkToRuleAnchorsAsync(string id, string source)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);
		var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, id));
		var anchor = id.ToLowerInvariant();

		Assert.Equal("https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#" + anchor, diagnostic.Descriptor.HelpLinkUri);

		foreach(var file in new[] { "RULES.md", "RULES.zh-Hans.md" })
			Assert.Contains("<a id=\"" + anchor + "\"></a>", ReadPackageText(file));
	}

	[Fact]
	public void RuleCatalogCoversPackagedDiagnosticsAndConfiguration()
	{
		var english = ReadPackageText("RULES.md");
		var chinese = ReadPackageText("RULES.zh-Hans.md");
		var englishAnchors = System.Text.RegularExpressions.Regex.Matches(english, "<a id=\"([^\"]+)\"></a>")
			.Select(match => match.Groups[1].Value).ToArray();
		var chineseAnchors = System.Text.RegularExpressions.Regex.Matches(chinese, "<a id=\"([^\"]+)\"></a>")
			.Select(match => match.Groups[1].Value).ToArray();

		Assert.NotEmpty(englishAnchors);
		Assert.Equal(englishAnchors, chineseAnchors);
		Assert.Equal(englishAnchors.Length, englishAnchors.Distinct().Count());

		var ids = System.Text.RegularExpressions.Regex.Matches(ReadPackageText("Zongsoft.CodeAnalysis.Analyzers.globalconfig"), @"dotnet_diagnostic\.(\w+)\.severity")
			.Select(match => match.Groups[1].Value).ToArray();
		Assert.NotEmpty(ids);
		Assert.All(ids, id => Assert.Contains(id.ToLowerInvariant(), englishAnchors));

		var analyzers = _package.Value.Analyzer.GetTypes().Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
			.Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)).ToArray();
		var descriptors = analyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics).ToArray();

		Assert.Equal(new[] { "ZS0005", "ZS1301", "ZS1302", "ZS1304", "ZS2003", "ZS2004", "ZS2005", "ZS2006", "ZS3001", "ZS3002", "ZS3003" }, descriptors.Select(rule => rule.Id).OrderBy(id => id).ToArray());
		Assert.All(descriptors, rule => Assert.Equal("https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#" + rule.Id.ToLowerInvariant(), rule.HelpLinkUri));

		Assert.All(analyzers.OfType<DiagnosticSuppressor>().SelectMany(analyzer => analyzer.SupportedSuppressions),
			rule => Assert.Contains(rule.Id.ToLowerInvariant(), englishAnchors));

		var configuration = System.Text.RegularExpressions.Regex.Matches(ReadPackageText("buildTransitive/Zongsoft.CodeAnalysis.targets"), "Code=\"(ZSCFG[0-9]+)\"")
			.Select(match => match.Groups[1].Value).Distinct().ToArray();
		Assert.Equal(new[] { "ZSCFG001" }, configuration);
		Assert.All(configuration, id => Assert.Contains(id.ToLowerInvariant(), englishAnchors));

		Assert.Contains("/RULES.md#index", ReadPackageText("README.md"));
		Assert.Contains("/RULES.zh-Hans.md#index", ReadPackageText("README.zh-Hans.md"));
	}

	[Fact]
	public async Task StatementSpacingExamplesAgreeWithDocumentationAsync()
	{
		var examples = new List<string[]>();

		foreach(var file in new[] { "RULES.md", "RULES.zh-Hans.md" })
		{
			var text = ReadPackageText(file);
			var start = text.IndexOf("<a id=\"zs2003\"></a>", StringComparison.Ordinal);
			var end = text.IndexOf("<a id=\"zs2004\"></a>", start, StringComparison.Ordinal);
			var fragments = System.Text.RegularExpressions.Regex.Matches(text[start..end], @"```csharp\r?\n(.*?)\r?\n```", System.Text.RegularExpressions.RegexOptions.Singleline)
				.Select(match => match.Groups[1].Value.Replace("\r\n", "\n")).ToArray();
			Assert.NotEmpty(fragments);
			examples.Add(fragments);
		}

		Assert.Equal(examples[0], examples[1]);

		foreach(var body in examples[0])
		{
			using var workspace = new AdhocWorkspace();
			var resultType = body.Contains("return entity;", StringComparison.Ordinal) ? "object" : "void";
			var enabledParameter = body.Contains("var enabled =", StringComparison.Ordinal) ? "" : ", bool enabled";
			var source = "using System;\nusing Xunit;\n\nclass Sample\n{\n" +
				"\tstatic void Process(int value) { }\n\tstatic void Complete() { }\n\tstatic void NotifyChanged() { }\n" +
				"\tstatic Func<object> GetCreator(Type type) => () => new object();\n\n" +
				$"\tstatic {resultType} Run(int[] items, bool disabled{enabledParameter}, Type type, Action<object, object> map, object state)\n\t{{\n\t\t" +
				body.Replace("\n", "\n\t\t") + "\n\t}\n}\n";
			var document = AddDocument(workspace.CurrentSolution, source);
			var compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken);
			Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken), item => item.Severity == DiagnosticSeverity.Error);

			var expected = SourceText.From(source).Lines.Where(line => line.ToString().Contains("//ZS2003", StringComparison.Ordinal))
				.Select(line => line.Start + line.ToString().Length - line.ToString().TrimStart().Length).ToArray();
			var diagnostics = await GetDiagnosticsAsync(document, "ZS2003");

			Assert.Equal(expected, diagnostics.Select(item => item.Location.SourceSpan.Start).ToArray());

			if(diagnostics.Length > 0)
			{
				var fixedDocument = await ApplyAsync(Assert.Single(await GetActionsAsync(document, Assert.Single(diagnostics))), document.Id);

				await AssertCleanAsync(fixedDocument, "ZS2003");
			}
		}

		foreach(var file in new[] { "RULES.md", "RULES.zh-Hans.md" })
		{
			var text = ReadPackageText(file);
			var start = text.IndexOf("<a id=\"zss0056\"></a>", StringComparison.Ordinal);
			var end = text.IndexOf("<a id=\"zss0057\"></a>", start, StringComparison.Ordinal);
			var member = System.Text.RegularExpressions.Regex.Match(text[start..end], @"```csharp\r?\n(.*?)\r?\n```", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
			var source = "class Sample\n{\n\treadonly System.Threading.ReaderWriterLockSlim _lock = new();\n\treadonly System.Collections.Generic.List<int> _list = new();\n" + member + "\n}\n";
			using var workspace = new AdhocWorkspace();
			var document = AddDocument(workspace.CurrentSolution, source);

			Assert.NotEmpty(member);
			await AssertCleanAsync(document, "ZS2003");
		}
	}

	[Theory]
	[InlineData("using Binder = Microsoft.CSharp.RuntimeBinder;\n", "")]
	[InlineData("using Text = System.Text.StringBuilder;\n", "")]
	[InlineData("using static System.Math;\n", "")]
	[InlineData("// heading\nusing Binder = Microsoft.CSharp.RuntimeBinder; // reason\n", "// heading\n// reason\n")]
	[InlineData("#if true\nusing Binder = Microsoft.CSharp.RuntimeBinder;\n#endif\n", "#if true\n#endif\n")]
	[InlineData("#region Imports\nusing Binder = Microsoft.CSharp.RuntimeBinder;\n#endregion\n", "#region Imports\n#endregion\n")]
	[InlineData("using /* keep */ Binder = Microsoft.CSharp.RuntimeBinder;\n", " /* keep */   \n")]
	[InlineData("/* keep */ using Binder = Microsoft.CSharp.RuntimeBinder;\n", "/* keep */ \n")]
	[InlineData("using\n\tBinder = Microsoft.CSharp.RuntimeBinder;\n", "")]
	public async Task FixesUnusedUsingPreservingTriviaAsync(string before, string after)
	{
		foreach(var newline in new[] { "\r\n", "\n" })
		{
			using var workspace = new AdhocWorkspace();
			var source = ("using System;\n" + before + "class Sample { }\n").Replace("\n", newline);
			var expected = ("using System;\n" + after + "class Sample { }\n").Replace("\n", newline);
			var document = AddDocument(workspace.CurrentSolution, source);
			var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS0005"));
			var actions = await GetActionsAsync(document, diagnostic);

			var result = await ApplyAsync(Assert.Single(actions), document.Id);

			Assert.Equal(expected, (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
			await AssertCleanAsync(result, "ZS0005");
		}
	}

	[Theory]
	[InlineData("var x = 1;\n\t\tvar y = 2;\n\t\tif(x > 0) { System.GC.KeepAlive(y); }", "var x = 1;\n\t\tvar y = 2;\n\n\t\tif(x > 0) { System.GC.KeepAlive(y); }")]
	[InlineData("var x = 1;\n\t\tSystem.GC.KeepAlive(x);\n\t\tif(x > 0) { }", "var x = 1;\n\t\tSystem.GC.KeepAlive(x);\n\n\t\tif(x > 0) { }")]
	[InlineData("System.GC.KeepAlive(1);\n\t\tSystem.GC.KeepAlive(2);\n\t\tSystem.GC.KeepAlive(3);\n\t\tif(true)\n\t\t\treturn;", "System.GC.KeepAlive(1);\n\t\tSystem.GC.KeepAlive(2);\n\t\tSystem.GC.KeepAlive(3);\n\n\t\tif(true)\n\t\t\treturn;")]
	[InlineData("var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1;\n\t\tif(x > 0) { System.GC.KeepAlive(x); }", "var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1;\n\n\t\tif(x > 0) { System.GC.KeepAlive(x); }")]
	[InlineData("var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; // value\n\t\t// consume\n\t\tif(x > 0) { System.GC.KeepAlive(x); }", "var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; // value\n\n\t\t// consume\n\t\tif(x > 0) { System.GC.KeepAlive(x); }")]
	[InlineData("var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1;\n#if true\n\t\tif(x > 0) { System.GC.KeepAlive(x); }\n#endif", "var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1;\n\n#if true\n\t\tif(x > 0) { System.GC.KeepAlive(x); }\n#endif")]
	[InlineData("if(true) { }\n\t\tforeach(var item in new int[0]) { }", "if(true) { }\n\n\t\tforeach(var item in new int[0]) { }")]
	[InlineData("var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; if(x > 0) { System.GC.KeepAlive(x); }", "var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1;\n\n\t\tif(x > 0) { System.GC.KeepAlive(x); }")]
	[InlineData("var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; /* keep */ if(x > 0) { System.GC.KeepAlive(x); }", "var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; /* keep */\n\n\t\tif(x > 0) { System.GC.KeepAlive(x); }")]
	[InlineData("var z = 3;\n\t\tz += 1;\n\t\tvar (x, y) = (1, 2);\n\t\tif(x > 0) { System.GC.KeepAlive(x + y); }", "var z = 3;\n\t\tz += 1;\n\t\tvar (x, y) = (1, 2);\n\n\t\tif(x > 0) { System.GC.KeepAlive(x + y); }")]
	[InlineData("var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; /* multi\nline */\n\t\tif(x > 0) { System.GC.KeepAlive(x); }", "var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; /* multi\nline */\n\n\t\tif(x > 0) { System.GC.KeepAlive(x); }")]
	[InlineData("var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; /* multi\nline */ if(x > 0) { System.GC.KeepAlive(x); }", "var y = 2;\n\t\tvar z = 3;\n\t\tvar x = 1; /* multi\nline */\n\n\t\tif(x > 0) { System.GC.KeepAlive(x); }")]
	[InlineData("if(true)\n\t\t\tSystem.GC.KeepAlive(1);\n\t\tif(true)\n\t\t\tSystem.GC.KeepAlive(2);\n\t\tforeach(var item in new int[0])\n\t\t\tSystem.GC.KeepAlive(item);", "if(true)\n\t\t\tSystem.GC.KeepAlive(1);\n\t\tif(true)\n\t\t\tSystem.GC.KeepAlive(2);\n\n\t\tforeach(var item in new int[0])\n\t\t\tSystem.GC.KeepAlive(item);")]
	[InlineData("if(true) { }\n\t\treturn;", "if(true) { }\n\n\t\treturn;")]
	[InlineData("try { } catch { }\n\t\tSystem.GC.KeepAlive(1);", "try { } catch { }\n\n\t\tSystem.GC.KeepAlive(1);")]
	[InlineData("try { } catch { }\n\t\ttry { } catch { }\n\t\treturn;", "try { } catch { }\n\t\ttry { } catch { }\n\n\t\treturn;")]
	[InlineData("do { } while(false);\n\t\treturn;", "do { } while(false);\n\n\t\treturn;")]
	[InlineData("if(true) { } else System.GC.KeepAlive(1);\n\t\treturn;", "if(true) { } else System.GC.KeepAlive(1);\n\n\t\treturn;")]
	[InlineData("if(true) { }\n\t\t//Next statement.\n\t\treturn;", "if(true) { }\n\n\t\t//Next statement.\n\t\treturn;")]
	[InlineData("if(true) { }\n\t\t/* Next\n\nstatement. */\n\t\treturn;", "if(true) { }\n\n\t\t/* Next\n\nstatement. */\n\t\treturn;")]
	[InlineData("if(true) { } /* Previous\n\nstatement. */\n\t\treturn;", "if(true) { } /* Previous\n\nstatement. */\n\n\t\treturn;")]
	[InlineData("if(true) { }\n#if false\n\nSystem.GC.KeepAlive(1);\n#endif\n\t\treturn;", "if(true) { }\n\n#if false\n\nSystem.GC.KeepAlive(1);\n#endif\n\t\treturn;")]
	[InlineData("var x = 100;\n\t\ttry { System.GC.KeepAlive(x); } catch { }", "var x = 100;\n\n\t\ttry { System.GC.KeepAlive(x); } catch { }")]
	[InlineData("System.GC.KeepAlive(1);\n\t\tSystem.GC.KeepAlive(2);\n\t\tif(true)\n\t\t\tSystem.GC.KeepAlive(2);\n\t\telse\n\t\t\tSystem.GC.KeepAlive(3);", "System.GC.KeepAlive(1);\n\t\tSystem.GC.KeepAlive(2);\n\n\t\tif(true)\n\t\t\tSystem.GC.KeepAlive(2);\n\t\telse\n\t\t\tSystem.GC.KeepAlive(3);")]
	[InlineData("System.GC.KeepAlive(1);\n\t\t//Prepare resources.\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream())\n\t\tusing(var writer = new System.IO.StreamWriter(stream))\n\t\t{\n\t\t\twriter.Write(1);\n\t\t}", "System.GC.KeepAlive(1);\n\n\t\t//Prepare resources.\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream())\n\t\tusing(var writer = new System.IO.StreamWriter(stream))\n\t\t{\n\t\t\twriter.Write(1);\n\t\t}")]
	[InlineData("System.GC.KeepAlive(1); /* Previous\n\nstatement. */\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream()) { }", "System.GC.KeepAlive(1); /* Previous\n\nstatement. */\n\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream()) { }")]
	[InlineData("System.GC.KeepAlive(1);\n#if true\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream()) { }\n#endif", "System.GC.KeepAlive(1);\n\n#if true\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream()) { }\n#endif")]
	[InlineData("System.GC.KeepAlive(1); using var buffer = new System.IO.MemoryStream(); using(var stream = new System.IO.MemoryStream()) { }", "System.GC.KeepAlive(1);\n\n\t\tusing var buffer = new System.IO.MemoryStream(); using(var stream = new System.IO.MemoryStream()) { }")]
	[InlineData("using var buffer = new System.IO.MemoryStream();\n\t\tSystem.GC.KeepAlive(buffer);\n\t\tusing(var stream = new System.IO.MemoryStream()) { }", "using var buffer = new System.IO.MemoryStream();\n\t\tSystem.GC.KeepAlive(buffer);\n\n\t\tusing(var stream = new System.IO.MemoryStream()) { }")]
	[InlineData("using(var first = new System.IO.MemoryStream()) { }\n\t\tusing(var second = new System.IO.MemoryStream()) { }", "using(var first = new System.IO.MemoryStream()) { }\n\n\t\tusing(var second = new System.IO.MemoryStream()) { }")]
	public async Task InsertsBlankLineWithoutFormattingAsync(string before, string after)
	{
		foreach(var newline in new[] { "\r\n", "\n" })
		{
			using var workspace = new AdhocWorkspace();
			var document = AddDocument(workspace.CurrentSolution, Wrap(before).Replace("\n", newline));
			var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS2003"));

			var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, diagnostic)), document.Id);

			Assert.Equal(Wrap(after).Replace("\n", newline), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
			await AssertCleanAsync(result, "ZS2003");
		}
	}

	[Theory]
	[InlineData("\r\n")]
	[InlineData("\n")]
	public async Task FixesAsyncResourceGroupStartAsync(string newline)
	{
		var source = Wrap("System.GC.KeepAlive(1);\n\t\tawait using var buffer = new System.IO.MemoryStream();\n\t\tawait using(var stream = new System.IO.MemoryStream()) { }")
			.Replace("void Run()", "async System.Threading.Tasks.Task Run()").Replace("\n", newline);
		var expected = source.Replace("System.GC.KeepAlive(1);" + newline, "System.GC.KeepAlive(1);" + newline + newline);
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);
		var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS2003"));

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, diagnostic)), document.Id);

		Assert.Equal(source.IndexOf("await using var", StringComparison.Ordinal), diagnostic.Location.SourceSpan.Start);
		Assert.Equal(expected, (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2003");
	}

	[Fact]
	public async Task DoesNotFixInsideResourceGroupAsync()
	{
		var source = Wrap("System.GC.KeepAlive(1);\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream()) { }");
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);
		var tree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
		var rule = GetAnalyzer("ZS2003").SupportedDiagnostics.Single();
		var diagnostic = Diagnostic.Create(rule, Location.Create(tree, new TextSpan(source.IndexOf("using(var", StringComparison.Ordinal), 5)));

		Assert.Equal(source.IndexOf("using var", StringComparison.Ordinal), Assert.Single(await GetDiagnosticsAsync(document, "ZS2003")).Location.SourceSpan.Start);

		Assert.Empty(await GetActionsAsync(document, diagnostic));
	}

	[Theory]
	[InlineData("/// <summary>\n\t/// One line.\n\t/// </summary>", "/// <summary>One line.</summary>")]
	[InlineData("/// <summary>One line.\n\t/// </summary>", "/// <summary>One line.</summary>")]
	[InlineData("/// <summary>\n\t/// One line.</summary>", "/// <summary>One line.</summary>")]
	[InlineData("/// <summary>\n\t///\n\t///  One  line.  \n\t///\n\t/// </summary>", "/// <summary>One  line.</summary>")]
	[InlineData("/// <summary>\n\t/// Calls <see cref=\"Run\"/> &amp; keeps &#32; entities.\n\t/// </summary>", "/// <summary>Calls <see cref=\"Run\"/> &amp; keeps &#32; entities.</summary>")]
	[InlineData("/// <summary>\n\t/// <![CDATA[a < b && b > c]]>\n\t/// </summary>", "/// <summary><![CDATA[a < b && b > c]]></summary>")]
	[InlineData("/// <summary>\n\t/// Before <!-- keep --> after.\n\t/// </summary>", "/// <summary>Before <!-- keep --> after.</summary>")]
	[InlineData("/// <summary>\n\t/// <para>One line.</para>\n\t/// </summary>", "/// <summary><para>One line.</para></summary>")]
	[InlineData("/// <remarks xml:space=\"default\">\n\t/// One line.\n\t/// </remarks>", "/// <remarks xml:space=\"default\">One line.</remarks>")]
	[InlineData("/** <summary>\n\t * One line.\n\t * </summary> */", "/** <summary>One line.</summary> */")]
	[InlineData("/** <summary>\n\tOne line.\n\t</summary> */", "/** <summary>One line.</summary> */")]
	[InlineData("/**\n\t * <summary>\n\t * Calls <see cref=\"Run\"/>.\n\t * </summary>\n\t */", "/**\n\t * <summary>Calls <see cref=\"Run\"/>.</summary>\n\t */")]
	public async Task FixesSingleLineXmlContentWithoutFormattingAsync(string before, string after)
	{
		foreach(var newline in new[] { "\r\n", "\n" })
		{
			using var workspace = new AdhocWorkspace();
			var source = "class Sample\n{\n\t// keep heading\n\t" + before + "\n\tvoid Run() { } // keep trailing\n}\n";
			var expected = "class Sample\n{\n\t// keep heading\n\t" + after + "\n\tvoid Run() { } // keep trailing\n}\n";
			var document = AddDocument(workspace.CurrentSolution, source.Replace("\n", newline));
			var diagnostic = Assert.Single(await GetDiagnosticsAsync(document, "ZS3003"));
			var action = Assert.Single(await GetActionsAsync(document, diagnostic));

			Assert.Equal("ZS3003.JoinXmlDocumentation", action.EquivalenceKey);

			var result = await ApplyAsync(action, document.Id);

			Assert.Equal(expected.Replace("\n", newline), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
			await AssertCleanAsync(result, "ZS3003");
		}
	}

	[Theory]
	[InlineData("/// <summary>One line.</summary>", "<summary>", 0)]
	[InlineData("/// <summary>\n/// </summary>", "<summary>", 0)]
	[InlineData("/// <summary>\n/// First line.\n/// Second line.\n/// </summary>", "<summary>", 0)]
	[InlineData("/// <summary>\n/// One line.\n/// </summary>", "One line.", 1)]
	[InlineData("/// <summary>\n/// One line.\n/// </remarks>", "<summary>", 0)]
	[InlineData("/// <summary\n/// >One line.\n/// </summary>", "<summary", 1)]
	[InlineData("/// <summary>\n/// One line.\n/// </summary\n/// >", "<summary>", 1)]
	[InlineData("/// <summary xml:space=\"preserve\">\n/// One line.\n/// </summary>", "<summary", 1)]
	[InlineData("/// <remarks xml:space=\"preserve\">\n/// <summary>\n/// One line.\n/// </summary>\n/// </remarks>", "<summary>", 1)]
	public async Task DoesNotOfferUnsafeXmlLayoutFixAsync(string documentation, string token, int expected)
	{
		using var workspace = new AdhocWorkspace();
		var source = documentation + "\nclass Sample { }\n";
		var document = AddDocument(workspace.CurrentSolution, source);
		var tree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
		var descriptor = GetAnalyzer("ZS3003").SupportedDiagnostics.Single(rule => rule.Id == "ZS3003");
		var diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, new TextSpan(source.IndexOf(token, StringComparison.Ordinal), token.Length)));

		var diagnostics = await GetDiagnosticsAsync(document, "ZS3003");
		Assert.Equal(expected, diagnostics.Length);

		if(expected > 0 && token != "One line.")
			diagnostic = Assert.Single(diagnostics);

		Assert.Empty(await GetActionsAsync(document, diagnostic));
		Assert.Equal(source, (await document.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task FixesSpacingInSwitchAndTopLevelStatementsAsync(bool topLevel)
	{
		using var workspace = new AdhocWorkspace();
		var body = "var x = 1;\r\nx += 1;\r\nx += 2;\r\nif(x > 0) { System.GC.KeepAlive(x); }";
		var source = topLevel ? body : Wrap("switch(1)\n\t\t{\n\t\t\tcase 1:\n" + body + "\n\t\t\t\tbreak;\n\t\t}");
		var document = AddDocument(workspace.CurrentSolution, source);

		if(topLevel)
			document = document.Project.WithCompilationOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication)).GetDocument(document.Id);

		var diagnostics = await GetDiagnosticsAsync(document, "ZS2003");
		Assert.Single(diagnostics.Select(item => item.Location.SourceSpan).Distinct());

		var result = await ApplyAsync(Assert.Single(await GetActionsAsync(document, diagnostics[0])), document.Id);

		Assert.Equal(source.Replace("x += 2;\r\n", "x += 2;\r\n\r\n"), (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, "ZS2003");
	}

	[Theory]
	[InlineData("ZS0005", "using System; using Binder = Microsoft.CSharp.RuntimeBinder; using CSharp = Microsoft.CSharp;", "using System;  ")]
	[InlineData("ZS2003", "class Sample { void Run() { if(true) { } if(false) { } if(true) { } } }", "class Sample { void Run() { if(true) { }\r\n\r\nif(false) { }\r\n\r\nif(true) { } } }")]
	public async Task FixAllMergesEditsOnOneLineAsync(string id, string source, string expected)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);
		var diagnostics = await GetDiagnosticsAsync(document, id);
		Assert.Equal(2, diagnostics.Length);

		var provider = GetProvider(id);
		var action = Assert.Single(await GetActionsAsync(document, diagnostics[0]));
		var context = new FixAllContext(document, provider, FixAllScope.Document, action.EquivalenceKey, new[] { id }, new Diagnostics(id), TestContext.Current.CancellationToken);

		var result = await ApplyAsync(await provider.GetFixAllProvider().GetFixAsync(context), document.Id);

		Assert.Equal(expected, (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());
		await AssertCleanAsync(result, id);
	}

	[Theory]
	[InlineData("ZS0005", FixAllScope.Document)]
	[InlineData("ZS0005", FixAllScope.Project)]
	[InlineData("ZS0005", FixAllScope.Solution)]
	[InlineData("ZS2003", FixAllScope.Document)]
	[InlineData("ZS2003", FixAllScope.Project)]
	[InlineData("ZS2003", FixAllScope.Solution)]
	[InlineData("ZS3003", FixAllScope.Document)]
	[InlineData("ZS3003", FixAllScope.Project)]
	[InlineData("ZS3003", FixAllScope.Solution)]
	public async Task FixAllRespectsScopeAsync(string id, FixAllScope scope)
	{
		using var workspace = new AdhocWorkspace();
		var before = id == "ZS0005" ? "using System;\r\nusing Binder = Microsoft.CSharp.RuntimeBinder;\r\nusing CSharp = Microsoft.CSharp;\r\n" :
			id == "ZS3003" ? "/// <summary>\r\n/// Summary.\r\n/// </summary>\r\n/// <remarks>\r\n/// Remarks.\r\n/// </remarks>\r\nclass Sample { }\r\n" :
			Wrap("System.GC.KeepAlive(0);\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream()) { }\n\t\tSystem.GC.KeepAlive(1);\n\t\ttry { } finally { }\n\t\treturn;").Replace("\n", "\r\n");
		var after = id == "ZS0005" ? "using System;\r\n" :
			id == "ZS3003" ? "/// <summary>Summary.</summary>\r\n/// <remarks>Remarks.</remarks>\r\nclass Sample { }\r\n" :
			Wrap("System.GC.KeepAlive(0);\n\n\t\tusing var buffer = new System.IO.MemoryStream();\n\t\tusing(var stream = new System.IO.MemoryStream()) { }\n\n\t\tSystem.GC.KeepAlive(1);\n\n\t\ttry { } finally { }\n\n\t\treturn;").Replace("\n", "\r\n");
		var expectedCount = id == "ZS2003" ? 4 : 2;
		var first = AddDocument(workspace.CurrentSolution, before);
		var second = AddDocument(first.Project.Solution, before.Replace("Sample", "Second"), first.Project.Id);
		var third = AddDocument(second.Project.Solution, before);
		var fourth = AddDocument(third.Project.Solution, before.Replace("Sample", "Second"), third.Project.Id);
		var solution = fourth.Project.Solution;

		foreach(var document in solution.Projects.SelectMany(project => project.Documents))
		{
			var diagnostics = await GetDiagnosticsAsync(document, id);
			Assert.Equal(expectedCount, diagnostics.Length);

			foreach(var diagnostic in diagnostics)
				Assert.Single(await GetActionsAsync(document, diagnostic));
		}

		first = solution.GetDocument(first.Id);
		var provider = GetProvider(id);
		var action = Assert.Single(await GetActionsAsync(first, (await GetDiagnosticsAsync(first, id)).First()));
		var context = new FixAllContext(first, provider, scope, action.EquivalenceKey, new[] { id }, new Diagnostics(id), TestContext.Current.CancellationToken);
		var fix = await provider.GetFixAllProvider().GetFixAsync(context);
		Assert.NotNull(fix);

		var changed = (await ApplyAsync(fix, first.Id)).Project.Solution;

		foreach(var document in solution.Projects.SelectMany(project => project.Documents))
		{
			var shouldChange = scope == FixAllScope.Solution || document.Id == first.Id ||
				scope == FixAllScope.Project && document.Project.Id == first.Project.Id;
			var expected = shouldChange ? after : before;

			if(document.Id == second.Id || document.Id == fourth.Id)
				expected = expected.Replace("Sample", "Second");

			var result = changed.GetDocument(document.Id);
			Assert.Equal(expected, (await result.GetTextAsync(TestContext.Current.CancellationToken)).ToString());

			if(shouldChange)
				await AssertCleanAsync(result, id);
			else
				Assert.Equal(expectedCount, (await GetDiagnosticsAsync(result, id)).Length);
		}
	}

	[Theory]
	[InlineData("using System;\nclass Sample { }", "ZS0005", "using System;")]
	[InlineData("using System.Threading.Tasks;\nclass Sample { }", "ZS0005", "using System.Threading.Tasks;")]
	[InlineData("using Microsoft.CSharp.RuntimeBinder;\nclass Sample { }", "ZS0005", "using Microsoft.CSharp.RuntimeBinder;")]
	[InlineData("using System.Text;\nclass Sample { StringBuilder Value; }", "ZS0005", "using System.Text;")]
	[InlineData("class Sample { void Run() { if(true) return;\nif(false) return; } }", "ZS2003", "if(false)")]
	[InlineData("class Sample { void Run() { var x = 1;\nif(x > 0) { } } }", "ZS2003", "if(x")]
	[InlineData("class Sample { void Run() { var x = 1;\n\nif(x > 0) { } } }", "ZS2003", "if(x")]
	[InlineData("class Sample { void Run() { var x = 1;\nvar y = 2;\nif(x > y) return; } }", "ZS2003", "if(x")]
	[InlineData("class Sample { void Run() { System.GC.KeepAlive(1);\nif(true) return; else return; } }", "ZS2003", "if(true)")]
	[InlineData("class Sample { void Run() { var x = 1;\n\nSystem.GC.KeepAlive(x); } }", "ZS2003", "System")]
	[InlineData("class Sample { void Run() { try { } catch { }\ntry { } finally { } } }", "ZS2003", "try { } finally")]
	[InlineData("class Sample { void Run() { switch(0) { case 0: if(true) { }\nbreak; } } }", "ZS2003", "break;")]
	public async Task DoesNotOfferUnsafeOrRedundantFixAsync(string source, string id, string token)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);
		var tree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
		var descriptor = GetAnalyzer(id).SupportedDiagnostics.Single(rule => rule.Id == id);
		var diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, new TextSpan(source.IndexOf(token, StringComparison.Ordinal), token.Length)));

		Assert.Empty(await GetDiagnosticsAsync(document, id));
		Assert.Empty(await GetActionsAsync(document, diagnostic));
	}

	[Theory]
	[InlineData("en-US", "Remove unused using", "Insert blank line", "Join XML documentation lines")]
	[InlineData("zh-Hans", "移除未使用的引用", "插入空行", "合并 XML 文档行")]
	public async Task ExportsLocalizedFixesFromPackageAsync(string language, string usingTitle, string spacingTitle, string documentationTitle)
	{
		var culture = CultureInfo.CurrentUICulture;

		try
		{
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
			using var workspace = new AdhocWorkspace();
			var document = AddDocument(workspace.CurrentSolution, "using Binder = Microsoft.CSharp.RuntimeBinder;\n" + Wrap("var x = 1;\n\t\tx += 1;\n\t\tx += 2;\n\t\tif(x > 0) { System.GC.KeepAlive(x); }"));

			Assert.Equal(usingTitle, Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS0005")))).Title);
			Assert.Equal(spacingTitle, Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2003")))).Title);

			var documentation = AddDocument(document.Project.Solution, "/// <summary>\n/// One line.\n/// </summary>\nclass Documented { }\n", document.Project.Id);

			Assert.Equal(documentationTitle, Assert.Single(await GetActionsAsync(documentation, Assert.Single(await GetDiagnosticsAsync(documentation, "ZS3003")))).Title);

			Assert.DoesNotContain(_package.Value.Entries, entry => entry.StartsWith("lib/", StringComparison.Ordinal) || entry.StartsWith("ref/", StringComparison.Ordinal) ||
				entry.Contains("Microsoft.CodeAnalysis.", StringComparison.Ordinal) || entry.Contains("System.Composition.", StringComparison.Ordinal));
			Assert.Contains("analyzers/dotnet/cs/zh-Hans/Zongsoft.CodeAnalysis.Fixes.resources.dll", _package.Value.Entries);
		}
		finally { CultureInfo.CurrentUICulture = culture; }
	}

	private static string Wrap(string body) => "class Sample\n{\n\tvoid Run()\n\t{\n\t\t" + body + "\n\t}\n}\n";

	private static Document AddDocument(Solution solution, string source, ProjectId projectId = null)
	{
		if(projectId == null)
		{
			projectId = ProjectId.CreateNewId();
			solution = solution.AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), projectId.ToString(), "Sample", LanguageNames.CSharp,
				parseOptions: new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose),
				compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), metadataReferences: _references.Value));
		}

		var id = DocumentId.CreateNewId(projectId);
		return solution.AddDocument(id, id + ".cs", SourceText.From(source), filePath: Path.Combine(Path.GetTempPath(), id.Id + ".cs")).GetDocument(id);
	}

	private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document, string id)
	{
		var compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken);
		var tree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
		var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create(GetAnalyzer(id))).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

		Assert.DoesNotContain(diagnostics, item => item.Id == "AD0001");

		return diagnostics.Where(item => item.Id == id && item.Location.SourceTree == tree).OrderBy(item => item.Location.SourceSpan.Start).ToImmutableArray();
	}

	private static async Task AssertCleanAsync(Document document, string id)
	{
		Assert.Empty(await GetDiagnosticsAsync(document, id));
		Assert.DoesNotContain((await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken)).GetDiagnostics(), item => item.Severity == DiagnosticSeverity.Error);
	}

	private static async Task<List<CodeAction>> GetActionsAsync(Document document, Diagnostic diagnostic)
	{
		var actions = new List<CodeAction>();

		await GetProvider(diagnostic.Id).RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), TestContext.Current.CancellationToken));

		return actions;
	}

	private static async Task<Document> ApplyAsync(CodeAction action, DocumentId id)
	{
		var operation = Assert.Single((await action.GetOperationsAsync(TestContext.Current.CancellationToken)).OfType<ApplyChangesOperation>());
		return operation.ChangedSolution.GetDocument(id);
	}

	private static CodeFixProvider GetProvider(string id)
	{
		using var container = new ContainerConfiguration().WithAssembly(_package.Value.Fixes).CreateContainer();
		return container.GetExports<CodeFixProvider>().Single(provider => provider.FixableDiagnosticIds.Contains(id));
	}

	private static DiagnosticAnalyzer GetAnalyzer(string id) => (DiagnosticAnalyzer)Activator.CreateInstance(
		_package.Value.Analyzer.GetType("Zongsoft.CodeAnalysis.Analyzers." + (id switch
		{
			"ZS0005" => "UnusedUsingAnalyzer",
			"ZS1301" or "ZS1302" => "LocalizationAnalyzer",
			"ZS1304" => "ResourceAccessAnalyzer",
			"ZS2003" => "StatementSpacingAnalyzer",
			"ZS2005" => "NestedControlBracesAnalyzer",
			"ZS2006" => "LocalFunctionLayoutAnalyzer",
			"ZS3001" or "ZS3002" or "ZS3003" => "DocumentationAnalyzer",
			_ => throw new ArgumentOutOfRangeException(nameof(id)),
		}), true));

	private static string ReadPackageText(string name)
	{
		var metadata = typeof(CodeFixTest).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(item => item.Key, item => item.Value);
		var path = Path.Combine(metadata["AnalyzerPackageDirectory"], $"Zongsoft.CodeAnalysis.{metadata["AnalyzerPackageVersion"]}.nupkg");

		using var archive = ZipFile.OpenRead(path);
		var entry = archive.GetEntry(name);
		Assert.NotNull(entry);

		using var reader = new StreamReader(entry.Open());
		return reader.ReadToEnd();
	}

	private static (Assembly, Assembly, string[]) LoadPackage()
	{
		var metadata = typeof(CodeFixTest).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(item => item.Key, item => item.Value);
		var path = Path.Combine(metadata["AnalyzerPackageDirectory"], $"Zongsoft.CodeAnalysis.{metadata["AnalyzerPackageVersion"]}.nupkg");
		var directory = Path.Combine(Path.GetTempPath(), "zongsoft-codefix-test-" + Guid.NewGuid().ToString("N"));

		ZipFile.ExtractToDirectory(path, directory);

		using var archive = ZipFile.OpenRead(path);
		return (Assembly.LoadFrom(Path.Combine(directory, "analyzers/dotnet/cs/Zongsoft.CodeAnalysis.Analyzers.dll")),
			Assembly.LoadFrom(Path.Combine(directory, "analyzers/dotnet/cs/Zongsoft.CodeAnalysis.Fixes.dll")),
			archive.Entries.Select(entry => entry.FullName).ToArray());
	}

	private sealed class Diagnostics(string id) : FixAllContext.DiagnosticProvider
	{
		public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) => await GetDiagnosticsAsync(document, id);
		public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) => Task.FromResult(Enumerable.Empty<Diagnostic>());
		public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
		{
			var diagnostics = new List<Diagnostic>();

			foreach(var document in project.Documents)
				diagnostics.AddRange(await GetDiagnosticsAsync(document, id));

			return diagnostics;
		}
	}
}
