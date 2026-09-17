using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigDisabledTest : EditorConfigFixture
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
}
