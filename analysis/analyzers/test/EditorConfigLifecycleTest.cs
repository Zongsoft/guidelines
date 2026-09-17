using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigLifecycleTest : EditorConfigFixture
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RestoreCleanAndDesignTimeDoNotSynchronize(bool exists)
	{
		var consumer = await CreateAsync();
		var original = "root = true\r\n";
		if(exists)
			File.WriteAllText(consumer.Config, original);

		foreach(var arguments in new[]
		{
			new[] { "restore", consumer.Project },
			new[] { "clean", consumer.Project },
			new[] { "msbuild", consumer.Project, "-t:PrepareForBuild", "-p:DesignTimeBuild=true" },
		})
		{
			var result = await RunAsync(consumer, arguments);
			Assert.True(result.ExitCode == 0, result.Output);
			Assert.Equal(exists, File.Exists(consumer.Config));
			if(exists)
				Assert.Equal(original, File.ReadAllText(consumer.Config));
		}

		var rebuild = await RunAsync(consumer, "msbuild", consumer.Project, "-t:Rebuild");
		Assert.True(rebuild.ExitCode == 0, rebuild.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));
		var clean = await RunAsync(consumer, "clean", consumer.Project);
		Assert.True(clean.ExitCode == 0, clean.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));
	}
}
