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

public abstract class EditorConfigFixture
{
	//一次测试运行使用全新的包缓存；各消费项目共享只读包，构建输出仍各自隔离。
	private static readonly string _packages = Path.Combine(Path.GetTempPath(), "zongsoft-test-packages-" + Guid.NewGuid().ToString("N"));
	private static readonly string _msbuild = Path.Combine(typeof(EditorConfigFixture).Assembly
		.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "SdkDirectory").Value, "MSBuild.dll");

	protected static async Task<Consumer> CreateAsync(string framework = "net10.0", string directory = "$(MSBuildThisFileDirectory)")
	{
		var metadata = typeof(EditorConfigFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
		var packageDirectory = metadata.Single(attribute => attribute.Key == "AnalyzerPackageDirectory").Value;
		var version = metadata.Single(attribute => attribute.Key == "AnalyzerPackageVersion").Value;
		var root = Path.Combine(Path.GetTempPath(), "zongsoft-editorconfig-test-" + Guid.NewGuid().ToString("N"), "仓库 with spaces");
		var project = Path.Combine(root, "src", "Example", "Example.csproj");
		var packages = _packages;
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

	protected static async Task<Result> RunAsync(Consumer consumer, params string[] arguments)
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

		//直接调用编译测试项目所用 SDK 的 MSBuild，省去每一步的 dotnet CLI SDK 查找。
		process.StartInfo.ArgumentList.Add(_msbuild);
		process.StartInfo.ArgumentList.Add("-nologo");
		process.StartInfo.ArgumentList.Add("-verbosity:minimal");
		if(arguments[0] != "msbuild")
			process.StartInfo.ArgumentList.Add("-t:" + (arguments.Contains("--no-incremental") ? "Rebuild" : arguments[0]));

		if(arguments[0] == "build" && !arguments.Contains("--no-restore"))
			process.StartInfo.ArgumentList.Add("-restore");

		foreach(var argument in arguments.Skip(1).Where(argument => argument != "--no-restore" && argument != "--no-incremental"))
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

	protected sealed record Consumer(string Root, string Project, string Config, string PackageTemplate, byte[] Template);
	protected sealed record Result(int ExitCode, string Output);
}
