using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class StyleIntegrationTest
{
	[Fact]
	public async Task PreservesUnusedSystem()
	{
		var result = await BuildAsync("using System;\n\nnamespace Samples;\n\npublic class Sample { }\n");
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("ZS0005", result.Output);
	}

	[Fact]
	public async Task PreservesDisabledDocumentation()
	{
		//与原 IDE0005 一样，关闭文档后编译器不提供 CS8019；不擅自覆盖项目配置。
		var result = await BuildAsync("using System;\nusing System.Text;\n\nnamespace Samples;\n\npublic class Sample { }\n", documentation: false);
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("ZS0005", result.Output);
	}

	[Fact]
	public async Task DoesNotExemptGlobalSystem()
	{
		var result = await BuildAsync("global using System;\n\nnamespace Samples;\n\npublic class Sample { }\n");
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("Example.cs(1,1): error ZS0005", result.Output);
	}

	[Theory]
	[InlineData("using System.Text;", "3,1")]
	[InlineData("using Namespace = System;", "3,1")]
	[InlineData("using Text = System.Text.StringBuilder;", "3,1")]
	[InlineData("using static System.Math;", "3,1")]
	public async Task ReportsOtherUnusedImports(string directive, string position)
	{
		var source = "using System;\nusing System.IO;\n" + directive + "\n\nnamespace Samples;\n\npublic class Sample\n{\n\tpublic FileAttributes Attributes => FileAttributes.Normal;\n}\n";
		var result = await BuildAsync(source);
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("Example.cs(" + position + "): error ZS0005", result.Output);
		Assert.DoesNotContain("Example.cs(1,1):", result.Output);
	}

	[Fact]
	public async Task AllowsIndentedDirectives()
	{
		var result = await BuildAsync("namespace Samples;\n\npublic class Sample\n{\n\t#if NET10_0_OR_GREATER\n\tpublic int Value => 1;\n\t#elif NET9_0_OR_GREATER\n\tpublic int Value => 2;\n\t#else\n\tpublic int Value => 3;\n\t#endif\n}\n");
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("IDE0055", result.Output);
	}

	[Fact]
	public async Task AllowsCompactTryFinally()
	{
		var result = await BuildAsync("""
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
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("IDE2001", result.Output);
		Assert.DoesNotContain("IDE0055", result.Output);
	}

	[Fact]
	public async Task AllowsCompactTryCatchFinally()
	{
		var result = await BuildAsync("using System;\n\nnamespace Samples;\n\npublic class Sample\n{\n\tpublic int Read()\n\t{\n\t\ttry { return int.Parse(\"1\"); }\n\t\tcatch(FormatException) { return 0; }\n\t\tfinally { Console.WriteLine(); }\n\t}\n}\n");
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("IDE2001", result.Output);
		Assert.DoesNotContain("IDE0055", result.Output);
	}

	[Theory]
	[InlineData("\t\tif(value == 0) return 0;\n\t\treturn value;", "IDE2001")]
	[InlineData("\t\ttry { value++; return value; }\n\t\tfinally { value++; }", "IDE2001")]
	[InlineData("\t\ttry { global::System.Action action = () => { if(value > 0) value++; }; }\n\t\tfinally { value++; }\n\t\treturn value;", "IDE2001")]
	[InlineData("\t\ttry { return value; }\n\t\tfinally { value++; value++; }", "IDE2001")]
	[InlineData("\t\ttry { return value; }\n\t\tcatch(global::System.Exception) { value++; return value; }", "IDE2001")]
	[InlineData("\t\ttry { return value+1; }\n\t\tfinally { value++; }", "IDE0055")]
	[InlineData("\t\tif (value == 0)\n\t\t\treturn 0;\n\t\treturn value;", "IDE0055")]
	public async Task RejectsNonCompactStatements(string body, string diagnostic)
	{
		var result = await BuildAsync("namespace Samples;\n\npublic class Sample\n{\n\tpublic int Read(int value)\n\t{\n" + body + "\n\t}\n}\n");
		Assert.NotEqual(0, result.ExitCode);
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

		var result = await BuildAsync(source);
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("IDE1006", result.Output);
	}

	[Fact]
	public async Task ReportsLocalVariableNaming()
	{
		var result = await BuildAsync("namespace Samples;\n\npublic class Sample\n{\n\tpublic int Read()\n\t{\n\t\tvar SIZE = 1;\n\t\treturn SIZE;\n\t}\n}\n");
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("error IDE1006", result.Output);
	}

	[Fact]
	public async Task ReportsWarningsWithoutStrictMode()
	{
		var result = await BuildAsync("using System.Text;\n\nnamespace Samples;\n\npublic class Sample { }\n", strict: false);
		Assert.Equal(0, result.ExitCode);
		Assert.Contains("warning ZS0005", result.Output);
	}

	[Fact]
	public async Task RespectsEditorConfigOverride()
	{
		var result = await BuildAsync(
			"using System.Text;\n\nnamespace Samples;\n\npublic class Sample { }\n",
			editorConfig: "root = true\r\n[*.cs]\r\ndotnet_diagnostic.ZS0005.severity = none\r\n");
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("ZS0005", result.Output);
	}

	[Theory]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t\t;\n\t\tif(enabled)\n\t\t\t;\n\t\tforeach(var item in items)\n\t\t\t;", "ZS2003", 2)]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t\titems[index]++;\n\t\tif(enabled)\n\t\t\tArray.Reverse(items);\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);", "ZS2003", 2)]
	[InlineData("if(enabled)\n\t\t{\n\t\t\tArray.Reverse(items);\n\t\t}\n\t\twhile(enabled)\n\t\t\tenabled = false;", "ZS2003", 1)]
	[InlineData("if(enabled)\n\t\t\tArray.Reverse(items);\n\t\t//Next independent statement.\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);", "ZS2003", 1)]
	public async Task ReportsAdjacentControlStatements(string body, string diagnostic, int count)
	{
		var result = await BuildAsync(WrapBody(body));
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("error " + diagnostic, result.Output);
		//MSBuild 会在摘要中重复诊断，只统计不同的源码位置。
		var positions = System.Text.RegularExpressions.Regex.Matches(result.Output, @"Example\.cs\(\d+,\d+\): error " + diagnostic)
			.Select(match => match.Value).Distinct().ToArray();
		Assert.Equal(count, positions.Length);
	}

	[Theory]
	[InlineData("for(var index = 0; index < items.Length; index++)\n\t\t\titems[index]++;\n\n\t\tif(enabled)\n\t\t\tArray.Reverse(items);\n\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);")]
	[InlineData("if(enabled)\n\t\t\tforeach(var item in items)\n\t\t\t\tGC.KeepAlive(item);\n\t\telse\n\t\t\tArray.Reverse(items);")]
	[InlineData("do\n\t\t{\n\t\t\tenabled = false;\n\t\t}\n\t\twhile(enabled);")]
	[InlineData("if(enabled)\n\t\t\tArray.Reverse(items);\n\n\t\t//Next independent statement.\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);")]
	public async Task AllowsSeparatedAndRelatedStatements(string body)
	{
		var result = await BuildAsync(WrapBody(body));
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Fact]
	public async Task ChecksSwitchSectionsAndTopLevelStatements()
	{
		var source = "using System;\n\nvar items = new[] { 1 };\nforeach(var item in items)\n\tGC.KeepAlive(item);\nif(items.Length > 0)\n\tArray.Reverse(items);\n\nswitch(items.Length)\n{\n\tcase 1:\n\t\tif(items.Length > 0)\n\t\t\tArray.Reverse(items);\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);\n\t\tbreak;\n}\n";
		var result = await BuildAsync(source, executable: true);
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("Example.cs(6,1): error ZS2003", result.Output);
		Assert.Contains("Example.cs(14,3): error ZS2003", result.Output);
	}

	[Theory]
	[InlineData("throw new ArgumentException(\"Invalid items.\", nameof(items));")]
	[InlineData("Console.WriteLine(\"Ready.\");")]
	[InlineData("Console.WriteLine(\"Count: {0}\", items.Length);")]
	public async Task ReportsNonLocalizedMessages(string body)
	{
		var result = await BuildAsync(WrapBody(body));
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("Example.cs(9,", result.Output);
		Assert.Contains("error CA1303", result.Output);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RespectsLocalizableAttribute(bool localizable)
	{
		var source = "namespace Samples;\n\npublic static class Sample\n{\n\tpublic static void Show([global::System.ComponentModel.Localizable(" + (localizable ? "true" : "false") + ")] string message) => global::System.GC.KeepAlive(message);\n\tpublic static void Run() => Show(\"Ready.\");\n}\n";
		var result = await BuildAsync(source);
		if(localizable)
		{
			Assert.NotEqual(0, result.ExitCode);
			Assert.Contains("error CA1303", result.Output);
		}
		else
		{
			Assert.True(result.ExitCode == 0, result.Output);
			Assert.DoesNotContain("CA1303", result.Output);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NewRulesRespectSeverityOverrides(bool disabled)
	{
		var source = WrapBody("if(enabled)\n\t\t\tArray.Reverse(items);\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);\n\n\t\tConsole.WriteLine(\"Ready.\");\n\t\tvar manager = new global::System.Resources.ResourceManager(typeof(Sample));\n\t\tGC.KeepAlive(manager.GetString(\"Ready\"));");
		var config = disabled ? "root = true\r\n[*]\r\nindent_style = tab\r\nindent_size = 4\r\ntab_width = 4\r\n[*.cs]\r\ndotnet_diagnostic.ZS2003.severity = none\r\ndotnet_diagnostic.ZS1304.severity = none\r\ndotnet_diagnostic.CA1303.severity = none\r\n" : null;
		var result = await BuildAsync(source, strict: disabled, editorConfig: config);
		Assert.True(result.ExitCode == 0, result.Output);
		foreach(var diagnostic in new[] { "ZS2003", "ZS1304", "CA1303" })
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
		var result = await BuildAsync(WrapBody("var manager = new global::System.Resources.ResourceManager(typeof(Sample));\n\t\tGC.KeepAlive(manager." + call + ");"));
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("Example.cs(10,", result.Output);
		Assert.Contains("error ZS1304", result.Output);
	}

	[Fact]
	public async Task AllowsLocalizedResourcesAndIdentifiers()
	{
		var result = await BuildAsync(WrapBody("ArgumentNullException.ThrowIfNull(items);\n\t\tGC.KeepAlive(\"application/json\");\n\t\tConsole.WriteLine(global::Zongsoft.CodeAnalysis.Analyzers.Properties.Resources.UnusedUsingTitle);\n\t\tthrow new ArgumentException(global::Zongsoft.CodeAnalysis.Analyzers.Properties.Resources.UnusedUsingTitle, nameof(items));"), resources: true);
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("CA1303", result.Output);
		Assert.DoesNotContain("ZS1304", result.Output);
	}

	[Fact]
	public async Task AllowsDynamicResourceKeysAndUnrelatedMethods()
	{
		var source = "namespace Samples;\n\npublic static class Sample\n{\n\tpublic static string Read(global::System.Resources.ResourceManager manager, string key) => manager.GetString(key);\n\tpublic static string GetString(string key) => key;\n\tpublic static string Value => GetString(\"Identifier\");\n}\n";
		var result = await BuildAsync(source);
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("ZS1304", result.Output);
	}

	[Fact]
	public async Task IgnoresGeneratedControlStatements()
	{
		var source = "// <auto-generated/>\n" + WrapBody("if(enabled)\n\t\t\tArray.Reverse(items);\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);");
		var result = await BuildAsync(source);
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	[Theory]
	[InlineData("en-US", "Insert a blank line between statement groups")]
	[InlineData("zh-Hans", "语句组之间应留一个空行")]
	public async Task LocalizesAnalyzerDiagnostics(string language, string message)
	{
		var result = await BuildAsync(WrapBody("if(enabled)\n\t\t\tArray.Reverse(items);\n\t\tforeach(var item in items)\n\t\t\tGC.KeepAlive(item);"), language: language, strict: false);
		Assert.Equal(0, result.ExitCode);
		Assert.Contains("warning ZS2003: " + message, result.Output);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SeparatesDeclarationGroupFromInvocation(bool separated)
	{
		var body = "var x = 1;\n\t\tvar y = 2.0;\n\t\tvar foo = \"...\";\n" + (separated ? "\n" : "") + "\t\tArgumentNullException.ThrowIfNull(items);";
		var result = await BuildAsync(WrapBody(body));
		if(separated)
		{
			Assert.True(result.ExitCode == 0, result.Output);
			Assert.DoesNotContain("ZS2003", result.Output);
		}
		else
		{
			Assert.NotEqual(0, result.ExitCode);
			Assert.Contains("Example.cs(12,3): error ZS2003", result.Output);
			Assert.DoesNotContain("Example.cs(10,3): error ZS2003", result.Output);
			Assert.DoesNotContain("Example.cs(11,3): error ZS2003", result.Output);
		}
	}

	[Theory]
	[InlineData("var count = items.Length;\n\t\titems[0] = count;")]
	[InlineData("GC.KeepAlive(items);\n\t\tvar count = items.Length;")]
	[InlineData("var count = items.Length;\n\t\tif(enabled)\n\t\t\tGC.KeepAlive(count);")]
	public async Task ReportsDeclarationGroupTransitions(string body)
	{
		var result = await BuildAsync(WrapBody(body));
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("Example.cs(10,3): error ZS2003", result.Output);
	}

	[Theory]
	[InlineData("var (count, text) = (1, \"value\");\n\t\tvar next = count + 1;\n\n\t\tGC.KeepAlive(text);\n\t\tGC.KeepAlive(next);")]
	[InlineData("(int count, string text) = (1, \"value\");\n\t\tvar next = count + 1;\n\n\t\tGC.KeepAlive(text);\n\t\tGC.KeepAlive(next);")]
	[InlineData("const int SIZE = 1;\n\t\tusing var stream = new global::System.IO.MemoryStream(SIZE);\n\n\t\tGC.KeepAlive(stream);")]
	[InlineData("var count = items.Length;\n\t\treturn;")]
	[InlineData("items[0] = 1;\n\t\tArray.Reverse(items);\n\t\tGC.KeepAlive(items);")]
	public async Task AllowsCohesiveStatementGroups(string body)
	{
		var result = await BuildAsync(WrapBody(body));
		Assert.True(result.ExitCode == 0, result.Output);
		Assert.DoesNotContain("ZS2003", result.Output);
	}

	private static string WrapBody(string body) => "using System;\n\nnamespace Samples;\n\npublic static class Sample\n{\n\tpublic static void Run(int[] items, bool enabled)\n\t{\n\t\t" + body + "\n\t}\n}\n";

	private static async Task<BuildResult> BuildAsync(string source, bool documentation = true, bool strict = true, string editorConfig = null, bool resources = false, bool executable = false, string language = null)
	{
		var metadata = typeof(StyleIntegrationTest).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
		var packageDirectory = metadata.Single(attribute => attribute.Key == "AnalyzerPackageDirectory").Value;
		var packageVersion = metadata.Single(attribute => attribute.Key == "AnalyzerPackageVersion").Value;
		var directory = Path.Combine(Path.GetTempPath(), "zongsoft-style-test-" + Guid.NewGuid().ToString("N"));

		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, ".editorconfig"), editorConfig ?? "root = true\r\n[*]\r\nindent_style = tab\r\nindent_size = 4\r\ntab_width = 4\r\nend_of_line = crlf\r\n", new UTF8Encoding(false));

		var sourcePath = Path.Combine(directory, "Example.cs");
		source = source.Replace("\r\n", "\n").Replace("\n", "\r\n").TrimEnd('\r', '\n') + "\r\n";
		File.WriteAllText(sourcePath, source, new UTF8Encoding(false));

		var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
			new XElement("PropertyGroup",
				new XElement("TargetFramework", "net10.0"),
				new XElement("OutputType", executable ? "Exe" : "Library"),
				new XElement("PreferredUILang", language ?? "en-US"),
				new XElement("GenerateDocumentationFile", documentation ? "true" : "false"),
				new XElement("NoWarn", "CS1591"),
				new XElement("ZongsoftCodeStyleStrict", strict ? "true" : "false"),
				new XElement("RestoreSources", packageDirectory),
				new XElement("RestorePackagesPath", Path.Combine(directory, "packages"))),
			new XElement("ItemGroup",
				new XElement("PackageReference",
					new XAttribute("Include", "Zongsoft.CodeAnalysis"),
					new XAttribute("Version", packageVersion),
					new XAttribute("PrivateAssets", "all"))));

		if(resources)
		{
			var destination = Path.Combine(directory, "Properties");
			Directory.CreateDirectory(destination);
			foreach(var name in new[] { "Resources.resx", "Resources.Designer.cs" })
				File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), Path.Combine(destination, name));

			project.Add(new XElement("ItemGroup",
				new XElement("EmbeddedResource", new XAttribute("Update", "Properties/Resources.resx"),
					new XElement("LogicalName", "Zongsoft.CodeAnalysis.Analyzers.Properties.Resources.resources"))));
		}

		using(var writer = XmlWriter.Create(Path.Combine(directory, "Example.csproj"), new XmlWriterSettings
		{
			Indent = true,
			IndentChars = "\t",
			NewLineChars = "\r\n",
			Encoding = new UTF8Encoding(false),
			OmitXmlDeclaration = true,
		}))
		{
			project.WriteTo(writer);
			writer.WriteWhitespace("\r\n");
		}

		using(var process = new Process())
		{
			process.StartInfo = new ProcessStartInfo("dotnet")
			{
				WorkingDirectory = directory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			foreach(var argument in new[] { "build", "Example.csproj", "--nologo", "-v:minimal", "-p:NuGetAudit=false", "--ignore-failed-sources" })
				process.StartInfo.ArgumentList.Add(argument);

			process.Start();
			var outputTask = process.StandardOutput.ReadToEndAsync();
			var errorTask = process.StandardError.ReadToEndAsync();
			using(var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
			{
				try { await process.WaitForExitAsync(timeout.Token); }
				catch(OperationCanceledException)
				{
					process.Kill(entireProcessTree: true);
					throw;
				}
			}

			var output = await outputTask + await errorTask;
			File.WriteAllText(Path.Combine(directory, "build.log"), output, new UTF8Encoding(false));
			Assert.DoesNotContain("AD0001", output);
			Assert.DoesNotContain("CS8032", output);
			Assert.DoesNotContain("CS9057", output);
			Assert.DoesNotContain("MultipleGlobalAnalyzerKeys", output);
			Assert.Equal(source, File.ReadAllText(sourcePath));
			return new BuildResult(process.ExitCode, output);
		}
	}

	private sealed record BuildResult(int ExitCode, string Output);
}
