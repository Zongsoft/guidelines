using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading.Tasks;

using Xunit;

namespace Zongsoft.CodeAnalysis.Analyzers.Tests;

public sealed class EditorConfigIntegrationTest
{
	[Theory]
	[InlineData("net8.0")]
	[InlineData("net9.0")]
	[InlineData("net10.0")]
	public async Task BuildSynchronizesAndSkipsMatchingSizeAndTimestamp(string framework)
	{
		var consumer = await CreateAsync(framework);
		Assert.False(File.Exists(consumer.Config));
		var localConfig = Path.Combine(Path.GetDirectoryName(consumer.Project), ".editorconfig");
		var localContent = "[*.cs]\r\nindent_size = 2\r\n";
		File.WriteAllText(localConfig, localContent);

		var build = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(build.ExitCode == 0, build.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));
		Assert.Equal(localContent, File.ReadAllText(localConfig));

		//大小相同但时间戳不同，即使目标文件更新，也应复制模板。
		var changed = consumer.Template.ToArray();
		changed[Array.IndexOf(changed, (byte)'4')] = (byte)'2';
		File.WriteAllBytes(consumer.Config, changed);
		File.SetLastWriteTimeUtc(consumer.Config, File.GetLastWriteTimeUtc(consumer.PackageTemplate).AddMinutes(1));
		var replace = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(replace.ExitCode == 0, replace.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));

		var timestamp = File.GetLastWriteTimeUtc(consumer.Config);
		var repeat = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(repeat.ExitCode == 0, repeat.Output);
		Assert.Equal(timestamp, File.GetLastWriteTimeUtc(consumer.Config));
		Assert.Equal(localContent, File.ReadAllText(localConfig));
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.PackageTemplate));

		//时间戳相同但大小不同，仍应复制。
		File.WriteAllBytes(consumer.Config, consumer.Template.Concat(Encoding.UTF8.GetBytes("#changed\r\n")).ToArray());
		File.SetLastWriteTimeUtc(consumer.Config, timestamp);
		var resize = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(resize.ExitCode == 0, resize.Output);
		Assert.Equal(consumer.Template, File.ReadAllBytes(consumer.Config));

		//接受 Copy 的边界：大小和时间戳都相同，即使内容不同也跳过。
		File.WriteAllBytes(consumer.Config, changed);
		File.SetLastWriteTimeUtc(consumer.Config, timestamp);
		var skip = await RunAsync(consumer, "build", consumer.Project, "--no-restore");
		Assert.True(skip.ExitCode == 0, skip.Output);
		Assert.Equal(changed, File.ReadAllBytes(consumer.Config));
		Assert.Equal(timestamp, File.GetLastWriteTimeUtc(consumer.Config));
	}

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

	private static async Task<Consumer> CreateAsync(string framework = "net10.0", string directory = "$(MSBuildThisFileDirectory)")
	{
		var metadata = typeof(EditorConfigIntegrationTest).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
		var packageDirectory = metadata.Single(attribute => attribute.Key == "AnalyzerPackageDirectory").Value;
		var version = metadata.Single(attribute => attribute.Key == "AnalyzerPackageVersion").Value;
		var root = Path.Combine(Path.GetTempPath(), "zongsoft-editorconfig-test-" + Guid.NewGuid().ToString("N"), "仓库 with spaces");
		var project = Path.Combine(root, "src", "Example", "Example.csproj");
		var packages = Path.Combine(root, "packages");
		Directory.CreateDirectory(Path.GetDirectoryName(project));

		new XElement("Project", new XElement("PropertyGroup", directory == null ? null : new XElement("ZongsoftGuidelinesSynchronization", directory)))
			.Save(Path.Combine(root, "Directory.Build.props"));
		new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
			new XElement("PropertyGroup",
				new XElement(framework.Contains(';') ? "TargetFrameworks" : "TargetFramework", framework),
				new XElement("RestoreSources", packageDirectory),
				new XElement("RestorePackagesPath", packages),
				new XElement("NuGetAudit", "false")),
			new XElement("ItemGroup", new XElement("PackageReference",
				new XAttribute("Include", "Zongsoft.CodeAnalysis"), new XAttribute("Version", version), new XAttribute("PrivateAssets", "all"))))
			.Save(project);

		byte[] template;
		using(var archive = ZipFile.OpenRead(Path.Combine(packageDirectory, "Zongsoft.CodeAnalysis." + version + ".nupkg")))
		using(var stream = archive.GetEntry(".editorconfig").Open())
		using(var buffer = new MemoryStream())
		{
			await stream.CopyToAsync(buffer);
			template = buffer.ToArray();
		}

		var consumer = new Consumer(root, project, Path.Combine(root, ".editorconfig"),
			Path.Combine(packages, "zongsoft.codeanalysis", version, ".editorconfig"), template);
		var restore = await RunAsync(consumer, "restore", project);
		Assert.True(restore.ExitCode == 0, restore.Output);
		Assert.Equal(template, File.ReadAllBytes(consumer.PackageTemplate));
		return consumer;
	}

	private static async Task<Result> RunAsync(Consumer consumer, params string[] arguments)
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo("dotnet")
			{
				WorkingDirectory = consumer.Root,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		foreach(var argument in arguments)
			process.StartInfo.ArgumentList.Add(argument);

		//多目标构建的复用节点可能继续持有输出管道，测试进程不保留这些节点。
		process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
		process.Start();
		var output = process.StandardOutput.ReadToEndAsync();
		var error = process.StandardError.ReadToEndAsync();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
		try
		{
			await process.WaitForExitAsync(timeout.Token);
			await Task.WhenAll(output, error).WaitAsync(timeout.Token);
		}
		catch(OperationCanceledException)
		{
			if(!process.HasExited)
				process.Kill(entireProcessTree: true);

			throw;
		}

		var result = new Result(process.ExitCode, await output + await error);
		File.WriteAllText(Path.Combine(consumer.Root, "commands." + process.Id + ".log"), string.Join(' ', arguments) + Environment.NewLine + result.Output, new UTF8Encoding(false));
		return result;
	}

	private sealed record Consumer(string Root, string Project, string Config, string PackageTemplate, byte[] Template);
	private sealed record Result(int ExitCode, string Output);
}
