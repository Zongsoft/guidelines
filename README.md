# Zongsoft Development Guidelines

[English](README.md) | [简体中文](README.zh-Hans.md)

## Development Guidelines

- [Zongsoft C# Development Guidelines](zongsoft.csharp.guidelines.md)
- [.NET/C# rule catalog](RULES.md)
- [Zongsoft REST API Guidelines](zongsoft.rest-api.guidelines.md)
- [AI Development Collaboration Guidelines](AGENTS.md)

<a id="code-analysis"></a>

## .NET/C# Code Analysis

[Tooling files](#tooling-files) · [Project setup](#project-setup) · [Packaging and validation](#packaging-validation-and-upgrades) · [Automatic fixes](#automatic-fixes) · [Check commands](#check-commands) · [Rule coverage](#rule-coverage-and-limitations) · [Tool references](#tool-references)

This tooling packages the enforceable rules from the [coding guidelines](zongsoft.csharp.guidelines.md) as the `Zongsoft.CodeAnalysis` NuGet analyzer. Sources, tests, build switches and C# rules are maintained only in guidelines; framework and business projects update them through the package version. Passing these checks does not establish that design, compatibility or resource ownership has been reviewed.

### Tooling files

| File | Responsibility |
| --- | --- |
| [`.editorconfig`](.editorconfig) | Tab indentation, CRLF, file-type exceptions and existing VB preferences; C# diagnostic rules are no longer duplicated here |
| [`Zongsoft.CodeAnalysis.Analyzers.globalconfig`](analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) | C# style, naming and diagnostic severity, loaded automatically from the package |
| [`Zongsoft.CodeAnalysis.props`](analysis/Zongsoft.CodeAnalysis.props) / [`.targets`](analysis/Zongsoft.CodeAnalysis.targets) | Automatically imported NuGet build settings for SDK switches, global configuration registration and strict checks |
| [`analysis`](analysis/README.md) | Analyzer sources, the packaging project and regression tests using actual package consumers |
| [`.gitattributes`](.gitattributes) | LF for `.sh`, CRLF for `.cmd` |

The package places its DLL under the standard `analyzers/dotnet/cs` path and imports settings through `buildTransitive`. It contains no business runtime assemblies or Roslyn runtime dependencies. It supports SDK-style C# projects using PackageReference; validation uses the .NET 10 SDK and covers net8.0, net9.0 and net10.0. SDK-provided rules change with the SDK and should be revalidated after upgrades.

### Project setup

For projects without Central Package Management, add:

```xml
<ItemGroup>
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="0.2.0" PrivateAssets="all" />
</ItemGroup>
```

With Central Package Management, declare the version in `Directory.Packages.props`:

```xml
<PackageVersion Include="Zongsoft.CodeAnalysis" Version="0.2.0" />
```

Add the versionless reference to the C# project or shared `Directory.Build.props`:

```xml
<ItemGroup Condition="'$(MSBuildProjectExtension)' == '.csproj'">
	<PackageReference Include="Zongsoft.CodeAnalysis" PrivateAssets="all" />
</ItemGroup>
```

After restore, NuGet automatically loads the analyzer and shared rules. No manual props imports, source copies or submodule initialization are needed. `PrivateAssets="all"` prevents rules from flowing to downstream consumers through business libraries. Each project that needs the checks should reference the package explicitly; a repository can add that reference in its shared build file.

Keep editor settings in the root EditorConfig. For initial setup, use this repository's file or the `.editorconfig` template at the package root as a reference. Merge existing settings as needed, preserving project exceptions. C# rules come from the packaged Global AnalyzerConfig, while matching local EditorConfig settings take precedence. When migrating, remove copied C# rules and retain only intentional project overrides so stale settings do not block package upgrades. Framework has completed this migration, and both repositories retain identical root EditorConfig files.

From version 0.2.0, set `ZongsoftGuidelinesSynchronization` to the repository root to enable [automatic build-time synchronization](analysis/README.md#editorconfig-synchronization). The guidelines file is the maintained source; consumers commit synchronized copies and keep intentional overrides in subdirectory configs. Use Rebuild if VS skips the build.

### Packaging, validation and upgrades

From the `analysis` directory, the [Cake script](analysis/build.cake) handles building, testing, packaging and publishing. See the [Cake workflow](analysis/README.md#cake-workflow) for arguments and publication credentials.

Manual GitHub Actions publication uses [publish-nuget.yml](.github/workflows/publish-nuget.yml). Initial setup requires the `release` environment, `NUGET_USER` and a nuget.org trusted publishing policy; see [GitHub Actions publishing](analysis/README.md#github-actions-publishing).

For direct .NET CLI usage, run from the guidelines repository root:

```powershell
dotnet test ./analysis/Zongsoft.CodeAnalysis.slnx
dotnet pack ./analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj -c Release
```

Release packages are generated in `analysis` by default. Retrieve the `.nupkg` there for publication.

Tests first create the package, then build isolated temporary consumer projects through PackageReference. Each project uses a separate package cache to avoid false results from stale versions. The package version is maintained in `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj`; increment it for each new release instead of overwriting a published version.

Before the first publication, validate locally from framework:

```powershell
dotnet restore ./Zongsoft.Core/src/Zongsoft.Core.csproj --source ../guidelines/analysis --source 'https://api.nuget.org/v3/index.json'
dotnet build ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore -p:GeneratePackageOnBuild=false
```

This local package source is only for pre-publication validation and is not added to the consuming repository's configuration. Before normal use, publish the package to a NuGet source accessible to business projects. An unpublished version cannot be restored solely from public feeds in a fresh checkout or CI. Packing does not publish; publication sources and permissions follow the maintainer's process.

Maintain, validate and publish new versions in guidelines. Consumers update PackageReference or the central version, then restore and build. C# diagnostic rules update with the package version; when synchronization is enabled, actual builds update the editor template. Roll back by selecting a previous package version.

> 💡 **File format:** The configuration defaults new files to CRLF, but editors may apply that setting to existing files too. Preserve existing LF files with a nearby `.editorconfig` override. The configuration deliberately omits `charset` to avoid rewriting existing BOMs; save new files as UTF-8 without BOM according to the guidelines. Markdown retains trailing double spaces used for hard line breaks, YAML uses spaces for indentation, and generated files remain under their generator's control.

### Automatic fixes

ZS0005 (unused imports) and ZS2003 (statement-group blank lines) now have code-fix providers distributed with the NuGet package. Press `Ctrl+.` at a diagnostic in VS2026 to preview an individual fix or apply Fix All for the same rule across a document, project or solution. See [usage](analysis/README.md#code-fixes) and the [full implementation plan](analysis/CODE_FIXES.md). Other custom fixes, including localization migration, remain planned work.

### Check commands

Run the following commands from the framework repository root. They check Core; replace the paths for other projects. Restore project dependencies before the first check, then use `--no-restore` for subsequent builds.

```powershell
dotnet restore ./Zongsoft.Core/src/Zongsoft.Core.csproj
dotnet build ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore -p:GeneratePackageOnBuild=false
```

Normal builds report configured style warnings. `CS4014` (some unawaited tasks) and `CA2012` (some ValueTask misuse) remain errors. Suggestions for `var`, expression bodies, readonly members and modern syntax are not promoted wholesale to errors. The configuration appends to existing `WarningsAsErrors`; a project's own stricter policy still applies.

Strict checks promote selected style diagnostics to errors and are suitable for projects or CI where existing violations have been resolved:

```powershell
dotnet build ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore -p:GeneratePackageOnBuild=false -p:ZongsoftCodeStyleStrict=true
```

See the [rule index](RULES.md#index) for the diagnostics covered by strict mode. It checks the project's entire compilation input and any project references actually built, not just Git changes. Existing projects may have many outstanding diagnostics; a successful normal build does not establish that strict checks pass.

`IDE0049` (using type keywords such as `int`) does not run during ordinary `dotnet build`, even with build-time code-style analysis enabled. Add this read-only check instead of relying on strict builds alone:

```powershell
dotnet format style ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore --verify-no-changes --diagnostics IDE0049
```

To inspect the standard formatter's suggestions for a changed file, use:

```powershell
dotnet format whitespace ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore --verify-no-changes --include ./Zongsoft.Core/src/Components/Handler.cs
```

**Note:** `dotnet format whitespace` compares formatted text directly and does not execute diagnostic suppressors. It can still report differences for permitted directive indentation, alignment or compact blocks. Loaded-analyzer build diagnostics govern these exceptions; do not use the formatter's exit code as an unconditional CI gate for them.

`--include` paths are relative to the working directory. Replace the example file with the actual changed files; multiple paths are supported. `--verify-no-changes` does not rewrite source and returns a nonzero exit code when formatting differs. CI must check build and IDE0049 verification exit codes; review the standard formatter's known exception differences separately. Multi-target builds cover the configured target frameworks, and formatting checks do not replace compilation and analysis for each target.

> ⚠️ **Automatic fixes:** Do not run unrestricted `dotnet format` or an editor's import organizer across the repository. Standard import organizers sort alphabetically and cannot implement the guideline's namespace-length ordering within groups. Limit fixes to changed files with `dotnet format whitespace --include ...`, then inspect the diff. Do not use bulk renaming or syntax conversion to automatically modify existing public APIs.

### Rule coverage and limitations

The [📋 rule catalog](RULES.md#index) is the single reference for diagnostic IDs, default and strict severity, triggers, exceptions, examples and fixes. Custom ZS diagnostic help links in VS open the corresponding rule anchor; SDK diagnostics retain their official links. See [manual review](RULES.md#review) for requirements beyond automated checks.

### Tool references

See the [official references in the rule catalog](RULES.md#references), including Microsoft's code-style index, diagnostic help links, configuration and formatting documentation.
