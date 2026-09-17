using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

internal static class AnalyzerRunner
{
	private static readonly Lazy<Environment> _environment = new(CreateEnvironment);
	private static readonly Lazy<ImmutableArray<MetadataReference>> _references = new(() =>
		((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
			.Select(path => MetadataReference.CreateFromFile(path)).ToImmutableArray<MetadataReference>());

	public static async Task<AnalysisResult> AnalyzeAsync(string source, bool documentation, bool strict, string editorConfig, bool resources, bool executable, string language)
	{
		var environment = _environment.Value;
		var path = Path.Combine(environment.Directory, "Example.cs");
		var configuration = AnalyzerConfigSet.Create(new[]
		{
			AnalyzerConfig.Parse(environment.Configuration, Path.Combine(environment.Directory, "Zongsoft.globalconfig")),
			AnalyzerConfig.Parse(editorConfig ?? "root = true\n[*]\nindent_style = tab\nindent_size = 4\ntab_width = 4\nend_of_line = crlf\n",
				Path.Combine(environment.Directory, ".editorconfig")),
		});

		var options = new OptionsProvider(configuration);
		var diagnosticOptions = configuration.GlobalConfigOptions.TreeOptions
			.SetItems(configuration.GetOptionsForSourcePath(path).TreeOptions)
			.SetItem("CS1591", ReportDiagnostic.Suppress);

		foreach(var id in strict ? environment.Errors.Concat(environment.StrictErrors) : environment.Errors)
		{
			if(!diagnosticOptions.TryGetValue(id, out var severity) || severity != ReportDiagnostic.Suppress)
				diagnosticOptions = diagnosticOptions.SetItem(id, ReportDiagnostic.Error);
		}

		source = source.Replace("\r\n", "\n").Replace("\n", "\r\n").TrimEnd('\r', '\n') + "\r\n";
		var parseOptions = new CSharpParseOptions(LanguageVersion.Latest,
			documentation ? DocumentationMode.Diagnose : DocumentationMode.None,
			preprocessorSymbols: new[] { "NET", "NET10_0", "NET10_0_OR_GREATER", "NET9_0_OR_GREATER", "NET8_0_OR_GREATER" });
		var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parseOptions, path);
		var compilation = CSharpCompilation.Create("Example", new[] { tree }, _references.Value,
			new CSharpCompilationOptions(executable ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary)
				.WithSpecificDiagnosticOptions(diagnosticOptions));

		if(resources)
		{
			var resourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Resources.Designer.cs");
			compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
				File.ReadAllText(resourcePath), parseOptions, Path.Combine(environment.Directory, "Resources.Designer.cs")));
		}

		var diagnostics = await compilation.WithAnalyzers(environment.Analyzers,
			new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, options))
			.GetAllDiagnosticsAsync(TestContext.Current.CancellationToken);
		var culture = CultureInfo.GetCultureInfo(language ?? "en-US");
		var output = string.Join("\n", diagnostics.Where(diagnostic => !diagnostic.IsSuppressed && diagnostic.Severity >= DiagnosticSeverity.Warning)
			.Select(diagnostic =>
			{
				var position = diagnostic.Location.GetLineSpan().StartLinePosition;
				return $"{Path.GetFileName(diagnostic.Location.GetLineSpan().Path)}({position.Line + 1},{position.Character + 1}): {diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Id}: {diagnostic.GetMessage(culture)}";
			}));

		Assert.False(diagnostics.Any(diagnostic => diagnostic.Id == "AD0001"), output);
		Assert.Empty(configuration.GlobalConfigOptions.Diagnostics);
		Assert.Equal(source, tree.GetText().ToString());

		return new AnalysisResult(!diagnostics.Any(diagnostic => !diagnostic.IsSuppressed && diagnostic.Severity == DiagnosticSeverity.Error), output);
	}

	private static Environment CreateEnvironment()
	{
		var metadata = typeof(AnalyzerRunner).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(item => item.Key, item => item.Value);
		var directory = Path.Combine(Path.GetTempPath(), "zongsoft-analyzer-test-" + Guid.NewGuid().ToString("N"));

		ZipFile.ExtractToDirectory(Path.Combine(metadata["AnalyzerPackageDirectory"], "Zongsoft.CodeAnalysis." + metadata["AnalyzerPackageVersion"] + ".nupkg"), directory);

		var sdk = Path.Combine(metadata["SdkDirectory"], "Sdks", "Microsoft.NET.Sdk");
		var files = new[]
		{
			Path.Combine(directory, "analyzers", "dotnet", "cs", "Zongsoft.CodeAnalysis.Analyzers.dll"),
			Path.Combine(sdk, "codestyle", "cs", "Microsoft.CodeAnalysis.CodeStyle.dll"),
			Path.Combine(sdk, "codestyle", "cs", "Microsoft.CodeAnalysis.CSharp.CodeStyle.dll"),
			Path.Combine(sdk, "analyzers", "Microsoft.CodeAnalysis.NetAnalyzers.dll"),
			Path.Combine(sdk, "analyzers", "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll"),
		};
		var loader = new AnalyzerLoader();
		var configurationText = File.ReadAllText(Path.Combine(directory, "Zongsoft.CodeAnalysis.Analyzers.globalconfig"));
		var configuredRules = AnalyzerConfigSet.Create(new[] { AnalyzerConfig.Parse(configurationText, Path.Combine(directory, "Zongsoft.globalconfig")) }).GlobalConfigOptions.TreeOptions;

		//只运行本规范配置的规则；真实 SDK 默认规则由包消费测试覆盖。
		var analyzers = files.SelectMany(path => new AnalyzerFileReference(path, loader).GetAnalyzers(LanguageNames.CSharp))
			.Where(analyzer => analyzer is DiagnosticSuppressor || analyzer.SupportedDiagnostics.Any(rule => configuredRules.ContainsKey(rule.Id)))
			.ToImmutableArray();

		Assert.Contains(analyzers, analyzer => analyzer is DiagnosticSuppressor);

		foreach(var id in new[] { "ZS0005", "ZS1304", "ZS2003", "IDE0055", "IDE1006", "IDE2001", "CA1303" })
			Assert.Contains(analyzers, analyzer => analyzer.SupportedDiagnostics.Any(rule => rule.Id == id));

		var configuration = configurationText +
			"\nbuild_property.TargetFramework = net10.0\nbuild_property.EffectiveAnalysisLevelStyle = 10.0\n";
		var targets = System.Xml.Linq.XDocument.Load(Path.Combine(directory, "buildTransitive", "Zongsoft.CodeAnalysis.targets"));
		var errors = targets.Descendants("WarningsAsErrors").Where(element => !element.Parent.Attribute("Condition").Value.Contains("ZongsoftCodeStyleStrict")).SelectMany(element => element.Value.Split(';'))
			.Where(value => !value.StartsWith("$", StringComparison.Ordinal)).ToArray();
		var strictErrors = targets.Descendants("WarningsAsErrors").Where(element => element.Parent.Attribute("Condition").Value.Contains("ZongsoftCodeStyleStrict"))
			.SelectMany(element => element.Value.Split(';')).Where(value => !value.StartsWith("$", StringComparison.Ordinal)).ToArray();

		return new Environment(directory, configuration, analyzers, errors, strictErrors);
	}

	private sealed record Environment(string Directory, string Configuration, ImmutableArray<DiagnosticAnalyzer> Analyzers, string[] Errors, string[] StrictErrors);
	private sealed class AnalyzerLoader : IAnalyzerAssemblyLoader
	{
		public void AddDependencyLocation(string fullPath) { }
		public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
	}

	private sealed class OptionsProvider(AnalyzerConfigSet configuration) : AnalyzerConfigOptionsProvider
	{
		public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(configuration.GlobalConfigOptions.AnalyzerOptions);
		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(configuration.GetOptionsForSourcePath(tree.FilePath).AnalyzerOptions);
		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(configuration.GetOptionsForSourcePath(textFile.Path).AnalyzerOptions);
	}

	private sealed class Options(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
	{
		public override IEnumerable<string> Keys => values.Keys;
		public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value);
	}
}

internal sealed record AnalysisResult(bool Success, string Output);
