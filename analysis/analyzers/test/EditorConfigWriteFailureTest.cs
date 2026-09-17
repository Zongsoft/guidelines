using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigWriteFailureTest : EditorConfigFixture
{
	[Fact]
	public async Task WriteFailureIsReported()
	{
		var consumer = await CreateAsync();
		Directory.CreateDirectory(consumer.Config);
		var build = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.NotEqual(0, build.ExitCode);
		Assert.Contains("MSB3024", build.Output);
		Assert.Contains(".editorconfig", build.Output);
		Assert.True(Directory.Exists(consumer.Config));
		Assert.Empty(Directory.GetFiles(consumer.Root, ".editorconfig.*.tmp"));
	}
}
