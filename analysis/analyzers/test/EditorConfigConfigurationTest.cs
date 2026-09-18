using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigConfigurationTest : EditorConfigFixture
{
	[Theory]
	[InlineData(null, false)]
	[InlineData("", true)]
	public async Task UnsetOrEmptyDirectoryDisablesSynchronization(string directory, bool exists)
	{
		var consumer = await CreateAsync(directory: directory);
		var original = "root = true\r\n";
		if(exists)
			File.WriteAllText(consumer.Config, original);

		var build = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(build.ExitCode == 0, build.Output);
		Assert.Equal(exists, File.Exists(consumer.Config));
		if(exists)
			Assert.Equal(original, File.ReadAllText(consumer.Config));

		Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(consumer.Project), ".editorconfig")));
	}

	[Fact]
	public async Task InvalidDirectoryFailsWithoutCreatingIt()
	{
		var consumer = await CreateAsync(directory: "does-not-exist");
		var build = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.NotEqual(0, build.ExitCode);
		Assert.Contains("ZSCFG001", build.Output);
		Assert.False(File.Exists(consumer.Config));
		Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(consumer.Project), "does-not-exist")));
	}

	[Fact]
	public async Task CommandLineDirectoryOverridesRepositorySetting()
	{
		var consumer = await CreateAsync();
		var destination = Path.Combine(consumer.Root, "explicit destination");
		Directory.CreateDirectory(destination);
		var build = await RunAsync(consumer, "build", consumer.Project, "--no-restore",
			"-p:ZongsoftGuidelinesSynchronization=" + destination);
		Assert.True(build.ExitCode == 0, build.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(Path.Combine(destination, ".editorconfig")));
		Assert.False(File.Exists(consumer.Config));
	}
}
