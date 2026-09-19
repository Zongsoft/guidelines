# Zongsoft.CodeAnalysis

[English](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.md) | [简体中文](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.zh-Hans.md)

C# analyzers and shared coding rules for Zongsoft projects, distributed as a development-only NuGet package. Sources, tests, build settings and C# rules are maintained in the guidelines repository; framework and business projects update them by upgrading the package.

## Usage

Add a package reference to an SDK-style C# project:

```xml
<ItemGroup>
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="1.2.0" PrivateAssets="all" />
</ItemGroup>
```

With Central Package Management, declare the version in `Directory.Packages.props` and omit `Version` from the reference. `PrivateAssets="all"` prevents the analyzer from becoming a dependency of the consuming project's published package; projects that need these rules should reference it explicitly.

NuGet automatically loads the analyzer DLL, SDK code-style build settings and shared Global AnalyzerConfig. No source copies or manual props imports are required. The analyzer uses Roslyn 5.9 APIs and requires a VS2026 or .NET 10 SDK compiler host with Roslyn 5.9 or later. Its assembly targets netstandard2.0; consuming projects can target net8.0, net9.0 and net10.0. The application target framework and the compiler that loads analyzers are separate requirements.

Normal builds report configured style violations as warnings. Set `ZongsoftCodeStyleStrict=true` in the project or pass `-p:ZongsoftCodeStyleStrict=true` to promote selected style warnings to errors. CS4014 and CA2012 remain errors.

## Rules and exceptions

The [📋 rule catalog](https://github.com/Zongsoft/Guidelines/blob/main/RULES.md#index) centralizes severity, triggers, exceptions, examples and fixes for imports, statement spacing, member regions, XML documentation, localization, naming and formatting exceptions.

In VS2026, follow a custom ZS diagnostic's help link, or select its Error List entry and press F1, to open its online rule anchor. SDK diagnostics retain Microsoft links. Custom links open the Chinese catalog with an English language switch; both RULES.md and RULES.zh-Hans.md are included in the package.

Localization checks: ZS1301 checks direct exception messages, ZS1302 checks non-ASCII text in handwritten strings, and ZS1304 checks resource-property access. All three are disabled when `IsTestProject=true`. CA1303 is disabled by default. Variables, exception factories and constructor chains are not traced; absence of diagnostics does not prove localization. See the rule catalog for precise boundaries.

## Code fixes

In Visual Studio 2026, place the caret on a ZS0005, ZS2003 or ZS3003 diagnostic and press `Ctrl+.` to select **Remove unused using**, **Insert blank line** or **Join XML documentation lines**. Preview and apply an individual fix, or use Fix All for the same rule in a document, project or solution. The providers load from this package; no separate VSIX is required.

See [ZS0005](https://github.com/Zongsoft/Guidelines/blob/main/RULES.md#zs0005), [ZS2003](https://github.com/Zongsoft/Guidelines/blob/main/RULES.md#zs2003) and [ZS3003](https://github.com/Zongsoft/Guidelines/blob/main/RULES.md#zs3003) for fix scope and preservation guarantees. Opening rule help does not change source code.

From the project directory, restrict CLI fixes to specific files and diagnostics:

```powershell
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 ZS3003 --include ./Example.cs
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 ZS3003 --include ./Example.cs --verify-no-changes
```

The first command changes the selected file; the second only checks it. See the rule catalog for other rules and their fixes.

## Configuration

The source file for [the global rules](analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) is maintained in analyzers/src; [props](Zongsoft.CodeAnalysis.props) and [targets](Zongsoft.CodeAnalysis.targets) are maintained in this directory. Their NuGet package paths remain the package root and buildTransitive, respectively.

Keep editor settings such as Tab indentation, CRLF and file-specific exceptions in the repository's `.editorconfig`; a template is included in the package at `.editorconfig`. C# diagnostic settings come from `Zongsoft.CodeAnalysis.Analyzers.globalconfig` at the package root, loaded through `buildTransitive/Zongsoft.CodeAnalysis.props`, and update with the package version. Local EditorConfig settings take precedence: declare only intentional project overrides.

The package contains the analyzer under `analyzers/dotnet/cs` and automatically imported settings under `buildTransitive`. It includes no lib/ref assemblies or Roslyn dependencies and adds no business runtime assembly reference. The analyzer and its tests declare build settings and dependency versions in their own project files, without enabling Central Package Management.

> 💡 `dotnet format whitespace` compares formatted text directly and does not apply diagnostic suppressors, so it may flag permitted layouts. Use loaded-analyzer build diagnostics for these exceptions. Formatting fixes should be limited to changed files and reviewed. IDE0049 requires the editor or a separate `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes` check.

## EditorConfig synchronization

Set one property in the consuming repository's root `Directory.Build.props`:

```xml
<PropertyGroup>
	<ZongsoftGuidelinesSynchronization>$(MSBuildThisFileDirectory)</ZongsoftGuidelinesSynchronization>
</PropertyGroup>
```

The value is the destination directory; unset or empty disables synchronization. After package restore, normal builds automatically synchronize the referenced package's template to that directory's `.editorconfig` before compilation preparation. No separate synchronization or check command is needed.

The complete template is copied with its encoding and line endings, without merging local edits. The built-in MSBuild Copy task creates missing files and copies files whose size or modification time differs; it skips files when both match, without comparing their contents. Put intentional overrides in subdirectory `.editorconfig` files. Review and commit the synchronized root file so editors can use it before package restore.

The destination directory must exist; relative paths resolve against the consuming project directory. Invalid directories report ZSCFG001 and write failures fail the build. Failed copies are retried up to three times. Multi-target and solution builds are supported; parallel projects may copy the same template more than once. Use one analyzer package version throughout a repository.

Visual Studio's fast up-to-date check remains enabled. When VS skips MSBuild, synchronization does not run; use Rebuild or Clean followed by Build when needed. Standalone restore, clean and design-time builds do not synchronize, and Clean does not delete the repository's `.editorconfig`.

Include `.editorconfig text eol=crlf` in the consuming repository's `.gitattributes` for consistent Windows/Linux checkouts; guidelines and framework already do so. After upgrading or rolling back the package, the next actual build synchronizes that version's template.

## Cake workflow

Restore the pinned Cake.Tool version with `dotnet tool restore` from the repository root, then run from the `analysis` directory. [build.cake](https://github.com/Zongsoft/Guidelines/blob/main/analysis/build.cake) follows the deployer/packager conventions: `--edition` selects Debug or Release (default: Release), and `pack` publishes to nuget.org.

```powershell
dotnet cake build.cake
dotnet cake build.cake --target=build
dotnet cake build.cake --target=pack
```

| Target | Behavior |
| --- | --- |
| `clean` | Removes the selected configuration's library/test build outputs and temporary test packages |
| `restore` | Restores both libraries and their two test projects with the selected configuration |
| `build` | Builds both libraries, creates the unified package in `analysis`, then builds both consumer test projects |
| `test` / `default` | Builds the package and runs the consumer regression tests |
| `pack` | Runs tests, then pushes `analysis/Zongsoft.CodeAnalysis.<Version>.nupkg` to nuget.org |

Set `NUGET_API_KEY` in the environment before invoking `pack`; the script reads it without printing its value. Existing published versions are skipped. Both Cake test projects validate the same package in analysis. Standalone dotnet test uses temporary packages under `analysis/obj/packages/<edition>`. Increment the analyzer project's version before publishing changes.

Use `dotnet cake build.cake --target=pack --dryrun` to inspect task order without running tasks. `--target=build` generates a local package for manual publication. Cake runner options are documented in the [official guide](https://cakebuild.net/docs/running-builds/runners/dotnet-tool).

## GitHub Actions publishing

The [Publish Analyzer to NuGet workflow](https://github.com/Zongsoft/Guidelines/blob/main/.github/workflows/publish-nuget.yml) supports manual dispatch only and publishes from `main` using the `release` environment. It restores the pinned Cake tool, builds and tests the Release package, obtains a temporary credential through `NuGet/login@v1`, then runs Cake's `pack --exclusive` to publish that same package. The job grants `contents: read` and `id-token: write`; no long-lived NuGet API key is required in GitHub.

Before the first publication, configure:

1. A GitHub environment named `release` in the guidelines repository, with an environment secret `NUGET_USER` containing your nuget.org **username**, not email.
2. A [nuget.org Trusted Publishing policy](https://www.nuget.org/account/trustedpublishing) with these matching values:

| Policy field | Value |
| --- | --- |
| Repository Owner | `Zongsoft` |
| Repository | `Guidelines` |
| Workflow File | `publish-nuget.yml` |
| Environment | `release` |

Use the workflow filename only, without `.github/workflows/` or the workflow display name. The policy account must have publication rights for `Zongsoft.CodeAnalysis`. See the [official Trusted Publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

Once configuration is complete and the workflow is on the default branch, the manual entry is **Actions → Publish Analyzer to NuGet → Run workflow**, selecting `main`. The package version comes from `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj`; update it before publishing changes. Other branches are skipped. Build or test failure prevents login and publication.

## Build and validation

Each test project defines its own package preparation and version lookup targets directly in its project file, so either project can be tested independently.

Open `Zongsoft.CodeAnalysis.slnx` in Visual Studio 2026 to work with both libraries in `analyzers/src` and `fixes/src`, and their respective `test` projects. The build script and bilingual README files remain in this directory.

Run from the guidelines repository root:

```powershell
dotnet test ./analysis/Zongsoft.CodeAnalysis.slnx
dotnet pack ./analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj -c Release
```

The regression suite uses the .NET 10 SDK and xUnit v3 through VSTest. It packs the analyzer, then builds isolated temporary consumer projects using PackageReference, a local package source and a fresh package cache for each test run. Assertions check diagnostic IDs and source locations, exit codes, analyzer loading and unchanged source text. Failures include the build output; temporary directories retain `build.log` for diagnosis.

Code-fix tests load the actual package through MEF and validate exact edits, comments and directives, localized titles, absence of new compiler errors, and document/project/solution batch scopes in Roslyn workspaces.

Both Debug and Release packages are written directly to `analysis` as `Zongsoft.CodeAnalysis.<Version>.nupkg`. A subsequent build of the same version replaces that package; compiler outputs remain separated by configuration, and the publishing workflow always uses Release. Publication selects only the current project version.

Publish a validated version to an accessible NuGet feed before using it in other machines or CI. Packing does not publish. Update the version in `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj` for each release, then upgrade consuming projects; do not overwrite published versions. When synchronization is enabled, the next actual build updates the editor template.

See the [setup and coverage guide](https://github.com/Zongsoft/Guidelines/blob/main/README.md#code-analysis) for central configuration, local package validation and upgrade steps, and the [coding guidelines](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md) for the complete development rules.

<a id="unicode"></a>
## Unicode data

ZS1302 uses pinned Unicode 17.0 tables; builds and analysis require no network access. To maintain these tables, download the official [DerivedGeneralCategory.txt](https://www.unicode.org/Public/17.0.0/ucd/extracted/DerivedGeneralCategory.txt) and [emoji-data.txt](https://www.unicode.org/Public/17.0.0/ucd/emoji/emoji-data.txt) into one temporary directory, then run from the repository root:

```powershell
python analysis/analyzers/tool/generate-unicode.py <data-directory>
```

The script generates `analyzers/src/UnicodeText.Generated.cs`. The package includes the [Unicode license](analyzers/src/Unicode.LICENSE.txt).
