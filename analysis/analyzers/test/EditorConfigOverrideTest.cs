using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigOverrideTest : EditorConfigFixture
{
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
