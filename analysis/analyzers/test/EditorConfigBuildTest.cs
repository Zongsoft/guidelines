using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigBuildTest : EditorConfigFixture
{
	[Fact]
	public async Task BuildSynchronizesAndSkipsMatchingSizeAndTimestamp()
	{
		var consumer = await CreateAsync();
		Assert.False(File.Exists(consumer.Config));
		var localConfig = Path.Combine(Path.GetDirectoryName(consumer.Project), ".editorconfig");
		var localContent = "[*.cs]\r\nindent_size = 2\r\n";
		File.WriteAllText(localConfig, localContent);

		var build = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(build.ExitCode == 0, build.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));
		Assert.Equal(localContent, File.ReadAllText(localConfig));

		//后续边界只执行实际同步所在的 PrepareForBuild，避免重复编译。
		//大小相同但时间戳不同，即使目标文件更新，也应复制模板。
		var changed = consumer.Template.ToArray();
		changed[Array.IndexOf(changed, (byte)'4')] = (byte)'2';
		File.WriteAllBytes(consumer.Config, changed);
		File.SetLastWriteTimeUtc(consumer.Config, File.GetLastWriteTimeUtc(consumer.PackageTemplate).AddMinutes(1));
		var replace = await RunAsync(consumer, "msbuild", consumer.Project, "-t:PrepareForBuild");
		Assert.True(replace.ExitCode == 0, replace.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));

		var timestamp = File.GetLastWriteTimeUtc(consumer.Config);
		var repeat = await RunAsync(consumer, "msbuild", consumer.Project, "-t:PrepareForBuild");
		Assert.True(repeat.ExitCode == 0, repeat.Output);
		Assert.Equal(timestamp, File.GetLastWriteTimeUtc(consumer.Config));
		Assert.Equal(localContent, File.ReadAllText(localConfig));
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.PackageTemplate));

		//时间戳相同但大小不同，仍应复制。
		File.WriteAllBytes(consumer.Config, consumer.Template.Concat(Encoding.UTF8.GetBytes("#changed\r\n")).ToArray());
		File.SetLastWriteTimeUtc(consumer.Config, timestamp);
		var resize = await RunAsync(consumer, "msbuild", consumer.Project, "-t:PrepareForBuild");
		Assert.True(resize.ExitCode == 0, resize.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));

		//接受 Copy 的边界：大小和时间戳都相同，即使内容不同也跳过。
		File.WriteAllBytes(consumer.Config, changed);
		File.SetLastWriteTimeUtc(consumer.Config, timestamp);
		var skip = await RunAsync(consumer, "msbuild", consumer.Project, "-t:PrepareForBuild");
		Assert.True(skip.ExitCode == 0, skip.Output);
		Assert.Equal(changed, File.ReadAllBytes(consumer.Config));
		Assert.Equal(timestamp, File.GetLastWriteTimeUtc(consumer.Config));
	}
}
