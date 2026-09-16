var target = Argument("target", "default");
var edition = Argument("edition", "Release");

var projectFile = "analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj";
var solutionFile = "Zongsoft.CodeAnalysis.slnx";

if(edition != "Debug" && edition != "Release")
	throw new ArgumentException("The edition must be Debug or Release.");

if(!FileExists(projectFile) || !FileExists(solutionFile))
	throw new Exception("Run this script from the analysis directory.");

var version = XmlPeek(projectFile, "/Project/PropertyGroup/Version");
var packageFile = $"Zongsoft.CodeAnalysis.{version}.nupkg";
var packageDirectory = MakeAbsolute(Directory(".")).FullPath;

Task("clean")
	.Description("清理所选配置的编译输出")
	.Does(() =>
{
	foreach(var project in new[] { "analyzers/src", "analyzers/test", "fixes/src", "fixes/test" })
	{
		CleanDirectories($"{project}/bin/{edition}");
		CleanDirectories($"{project}/obj/{edition}");
	}

	CleanDirectories($"obj/packages/{edition}");
});

Task("restore")
	.Description("还原分析器、修复器和两套测试依赖")
	.Does(() =>
{
	DotNetRestore(solutionFile, new DotNetRestoreSettings
	{
		MSBuildSettings = new DotNetMSBuildSettings().WithProperty("Configuration", edition),
	});
});

Task("build")
	.Description("编译两个库，在 analysis 目录生成统一包，再编译两套消费测试")
	.IsDependentOn("clean")
	.IsDependentOn("restore")
	.Does(() =>
{
	DotNetBuild(projectFile, new DotNetBuildSettings
	{
		Configuration = edition,
		NoRestore = true,
	});

	DotNetPack(projectFile, new DotNetPackSettings
	{
		Configuration = edition,
		NoBuild = true,
		NoRestore = true,
	});

	DotNetBuild(solutionFile, new DotNetBuildSettings
	{
		Configuration = edition,
		NoRestore = true,
		MSBuildSettings = new DotNetMSBuildSettings()
			.WithProperty("CodeAnalysisPackagePrepared", "true")
			.WithProperty("AnalyzerPackageDirectory", packageDirectory),
	});
});

Task("test")
	.Description("用同一个 NuGet 包运行两套消费回归测试")
	.IsDependentOn("build")
	.Does(() =>
{
	DotNetTest(solutionFile, new DotNetTestSettings
	{
		Configuration = edition,
		NoBuild = true,
		NoRestore = true,
	});
});

Task("pack")
	.Description("将当前版本的统一包发布到 nuget.org")
	.IsDependentOn("test")
	.Does(() =>
{
	var apiKey = EnvironmentVariable("NUGET_API_KEY");

	if(string.IsNullOrWhiteSpace(apiKey))
		throw new Exception("Set NUGET_API_KEY before publishing.");

	//只发布项目版本号对应的包，允许当前目录保留其他历史版本。
	if(!FileExists(packageFile))
		throw new Exception($"Package not found: {packageFile}");

	DotNetNuGetPush(packageFile, new DotNetNuGetPushSettings
	{
		Source = "https://api.nuget.org/v3/index.json",
		ApiKey = apiKey,
		SkipDuplicate = true,
	});
});

Task("default")
	.Description("编译、打包并运行全部测试")
	.IsDependentOn("test");

RunTarget(target);
