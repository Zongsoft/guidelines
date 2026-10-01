var target = Argument("target", "default");
var edition = Argument("edition", "Release");

var projectFile = "analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj";
var solutionFile = "Zongsoft.CodeAnalysis.slnx";

if(edition != "Debug" && edition != "Release")
	throw new ArgumentOutOfRangeException(nameof(edition), edition, null);

if(!FileExists(projectFile) || !FileExists(solutionFile))
	throw new System.IO.FileNotFoundException(null, !FileExists(projectFile) ? projectFile : solutionFile);

var version = XmlPeek(projectFile, "/Project/PropertyGroup/Version");
var packageFile = $"Zongsoft.CodeAnalysis.{version}.nupkg";
var packageDirectory = MakeAbsolute(Directory(".")).FullPath;

Task("clean")
	.Description("Clean build outputs for the selected configuration")
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
	.Description("Restore analyzer, code-fix and test dependencies")
	.Does(() =>
{
	DotNetRestore(solutionFile, new DotNetRestoreSettings
	{
		MSBuildSettings = new DotNetMSBuildSettings().WithProperty("Configuration", edition),
	});
});

Task("build")
	.Description("Build both libraries, create the package and build consumer tests")
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
	.Description("Run both consumer test suites against the same package")
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
	.Description("Publish the current package to nuget.org")
	.IsDependentOn("test")
	.Does(() =>
{
	var apiKey = EnvironmentVariable("NUGET_API_KEY");

	if(string.IsNullOrWhiteSpace(apiKey))
		throw new ArgumentException(null, "NUGET_API_KEY");

	//只发布项目版本号对应的包，允许当前目录保留其他历史版本。
	if(!FileExists(packageFile))
		throw new System.IO.FileNotFoundException(null, packageFile);

	DotNetNuGetPush(packageFile, new DotNetNuGetPushSettings
	{
		Source = "https://api.nuget.org/v3/index.json",
		ApiKey = apiKey,
		SkipDuplicate = true,
	});
});

Task("default")
	.Description("Build, package and run all tests")
	.IsDependentOn("test");

RunTarget(target);
