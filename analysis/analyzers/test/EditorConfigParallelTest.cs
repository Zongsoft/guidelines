using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigParallelTest : EditorConfigFixture
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ParallelMultiTargetBuildsSynchronizeOneDestination(bool exists)
	{
		var consumer = await CreateAsync("net8.0;net9.0;net10.0", "../..");
		var otherProject = Path.Combine(consumer.Root, "src", "Other", "Other.csproj");
		Directory.CreateDirectory(Path.GetDirectoryName(otherProject));
		File.Copy(consumer.Project, otherProject);
		var other = consumer with { Project = otherProject };
		var restore = await RunAsync(other, "restore", other.Project);
		Assert.True(restore.ExitCode == 0, restore.Output);

		if(exists)
			File.WriteAllText(consumer.Config, "root = true\r\n");

		var builds = await Task.WhenAll(
			RunAsync(consumer, "build", consumer.Project, "--no-restore", "-m:2"),
			RunAsync(other, "build", other.Project, "--no-restore", "-m:2"));
		Assert.All(builds, result => Assert.True(result.ExitCode == 0, result.Output));
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.PackageTemplate));
		Assert.Empty(Directory.GetFiles(consumer.Root, ".editorconfig.*.tmp"));
		Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(consumer.Project), ".editorconfig")));
	}
}
