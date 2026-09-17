using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class PackageIntegrationTest : EditorConfigFixture
{
	[Fact]
	public async Task LoadsRulesAndOverridesAcrossTargetFrameworks()
	{
		var consumer = await CreateAsync("net8.0;net9.0;net10.0", directory: null);
		var project = XDocument.Load(consumer.Project);

		project.Root.Element("PropertyGroup").Add(
			new XElement("GenerateDocumentationFile", "true"),
			new XElement("NoWarn", "CS1591"));
		project.Save(consumer.Project);

		var directory = Path.GetDirectoryName(consumer.Project);
		var source = "using System.Text;\r\n\r\nnamespace Samples;\r\n\r\npublic class Sample\r\n{\r\n\tpublic void Run(bool enabled)\r\n\t{\r\n\t\tif (enabled)\r\n\t\t\t;\r\n\t\tif(enabled)\r\n\t\t\t;\r\n\t}\r\n}\r\n";
		var sourcePath = Path.Combine(directory, "Example.cs");

		File.WriteAllText(sourcePath, source);

		var normal = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(normal.ExitCode == 0, normal.Output);

		foreach(var framework in new[] { "net8.0", "net9.0", "net10.0" })
		{
			Assert.True(File.Exists(Path.Combine(directory, "bin", "Debug", framework, "Example.dll")), normal.Output);
			foreach(var id in new[] { "ZS0005", "ZS2003", "IDE0055" })
				Assert.Contains("warning " + id, string.Join('\n', normal.Output.Split('\n').Where(line => line.Contains(framework))));
		}

		var strict = await RunAsync(consumer, "build", consumer.Project, "--no-restore", "--no-incremental", "-p:ZongsoftCodeStyleStrict=true");
		Assert.NotEqual(0, strict.ExitCode);

		foreach(var id in new[] { "ZS0005", "ZS2003", "IDE0055" })
			Assert.Contains("error " + id, strict.Output);

		File.WriteAllText(Path.Combine(directory, ".editorconfig"),
			"root = true\r\n[*.cs]\r\ndotnet_diagnostic.ZS0005.severity = none\r\ndotnet_diagnostic.ZS2003.severity = none\r\ndotnet_diagnostic.IDE0055.severity = none\r\n");

		var overridden = await RunAsync(consumer, "build", consumer.Project, "--no-restore", "--no-incremental", "-p:ZongsoftCodeStyleStrict=true");
		Assert.True(overridden.ExitCode == 0, overridden.Output);

		foreach(var id in new[] { "ZS0005", "ZS2003", "IDE0055", "AD0001", "CS8032", "CS9057", "MultipleGlobalAnalyzerKeys" })
			Assert.DoesNotContain(id, overridden.Output);

		Assert.Equal(source, File.ReadAllText(sourcePath));
	}
}
