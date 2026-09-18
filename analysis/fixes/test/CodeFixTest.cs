using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Composition.Hosting;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace Zongsoft.CodeAnalysis.Fixes.Tests;

public class CodeFixTest
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
	public async Task DiagnosticsLinkToRuleAnchors(string id, string source)
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
		Assert.Equal(new[] { "ZS0005", "ZS1301", "ZS1302", "ZS1304", "ZS2003" }, descriptors.Select(rule => rule.Id).OrderBy(id => id).ToArray());
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
	public async Task FixesUnusedUsingPreservingTrivia(string before, string after)
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
	public async Task InsertsBlankLineWithoutFormatting(string before, string after)
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
	[InlineData(true)]
	[InlineData(false)]
	public async Task FixesSpacingInSwitchAndTopLevelStatements(bool topLevel)
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
	public async Task FixAllMergesEditsOnOneLine(string id, string source, string expected)
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
	public async Task FixAllRespectsScope(string id, FixAllScope scope)
	{
		using var workspace = new AdhocWorkspace();
		var before = id == "ZS0005" ? "using System;\r\nusing Binder = Microsoft.CSharp.RuntimeBinder;\r\nusing CSharp = Microsoft.CSharp;\r\n" :
			Wrap("if(true) { }\n\t\tif(false) { }\n\t\tif(true) { }").Replace("\n", "\r\n");
		var after = id == "ZS0005" ? "using System;\r\n" :
			Wrap("if(true) { }\n\n\t\tif(false) { }\n\n\t\tif(true) { }").Replace("\n", "\r\n");
		var first = AddDocument(workspace.CurrentSolution, before);
		var second = AddDocument(first.Project.Solution, before.Replace("Sample", "Second"), first.Project.Id);
		var third = AddDocument(second.Project.Solution, before);
		var fourth = AddDocument(third.Project.Solution, before.Replace("Sample", "Second"), third.Project.Id);
		var solution = fourth.Project.Solution;

		foreach(var document in solution.Projects.SelectMany(project => project.Documents))
		{
			var diagnostics = await GetDiagnosticsAsync(document, id);
			Assert.Equal(2, diagnostics.Length);

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
				Assert.Equal(2, (await GetDiagnosticsAsync(result, id)).Length);
		}
	}

	[Theory]
	[InlineData("using System;\nclass Sample { }", "ZS0005", "using System;")]
	[InlineData("using System.Threading.Tasks;\nclass Sample { }", "ZS0005", "using System.Threading.Tasks;")]
	[InlineData("using Microsoft.CSharp.RuntimeBinder;\nclass Sample { }", "ZS0005", "using Microsoft.CSharp.RuntimeBinder;")]
	[InlineData("using System.Text;\nclass Sample { StringBuilder Value; }", "ZS0005", "using System.Text;")]
	[InlineData("class Sample { void Run() { if(true) return;\nif(false) return; } }", "ZS2003", "if(false)")]
	[InlineData("class Sample { void Run() { var x = 1;\n\nSystem.GC.KeepAlive(x); } }", "ZS2003", "System")]
	public async Task DoesNotOfferUnsafeOrRedundantFix(string source, string id, string token)
	{
		using var workspace = new AdhocWorkspace();
		var document = AddDocument(workspace.CurrentSolution, source);
		var tree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
		var descriptor = GetAnalyzer(id).SupportedDiagnostics.Single();
		var diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, new TextSpan(source.IndexOf(token, StringComparison.Ordinal), token.Length)));

		Assert.Empty(await GetDiagnosticsAsync(document, id));
		Assert.Empty(await GetActionsAsync(document, diagnostic));
	}

	[Theory]
	[InlineData("en-US", "Remove unused using", "Insert blank line")]
	[InlineData("zh-Hans", "移除未使用的引用", "插入空行")]
	public async Task ExportsLocalizedFixesFromPackage(string language, string usingTitle, string spacingTitle)
	{
		var culture = CultureInfo.CurrentUICulture;

		try
		{
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
			using var workspace = new AdhocWorkspace();
			var document = AddDocument(workspace.CurrentSolution, "using Binder = Microsoft.CSharp.RuntimeBinder;\n" + Wrap("var x = 1;\n\t\tx += 1;\n\t\tx += 2;\n\t\tif(x > 0) { System.GC.KeepAlive(x); }"));

			Assert.Equal(usingTitle, Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS0005")))).Title);
			Assert.Equal(spacingTitle, Assert.Single(await GetActionsAsync(document, Assert.Single(await GetDiagnosticsAsync(document, "ZS2003")))).Title);
			Assert.DoesNotContain(_package.Value.Entries, entry => entry.StartsWith("lib/") || entry.StartsWith("ref/") ||
				entry.Contains("Microsoft.CodeAnalysis.") || entry.Contains("System.Composition."));
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
