using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigInvalidDirectoryTest : EditorConfigFixture
{
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
}
