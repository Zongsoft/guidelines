# Zongsoft.CodeAnalysis

[English](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.md) | [简体中文](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.zh-Hans.md)

C# analyzers and shared coding rules for Zongsoft projects, distributed as a development-only NuGet package. Sources, tests, build settings and C# rules are maintained in the guidelines repository; framework and business projects update them by upgrading the package.

## Usage

Add a package reference to an SDK-style C# project:

```xml
<ItemGroup>
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

With Central Package Management, declare the version in `Directory.Packages.props` and omit `Version` from the reference. `PrivateAssets="all"` prevents the analyzer from becoming a dependency of the consuming project's published package; projects that need these rules should reference it explicitly.

NuGet automatically loads the analyzer DLL, SDK code-style build settings and shared Global AnalyzerConfig. No source copies or manual props imports are required. The analyzer targets netstandard2.0 and uses Roslyn 3.8 public APIs; validation covers net8.0, net9.0 and net10.0 with the .NET 10 SDK.

Normal builds report configured style violations as warnings. Set `ZongsoftCodeStyleStrict=true` in the project or pass `-p:ZongsoftCodeStyleStrict=true` to promote selected style warnings to errors. CS4014 and CA2012 remain errors.

## Rules and exceptions

| Component | Behavior |
| --- | --- |
| `UnusedUsingAnalyzer` / `ZS0005` | Uses compiler diagnostic CS8019 to report each unused import; permits ordinary `using System;` only when it resolves to the global System namespace |
| `StyleSuppressor` / `ZSS0055` | Suppresses IDE0055 only for whitespace before `#if`, `#elif`, `#else` and `#endif` |
| `StyleSuppressor` / `ZSS0056`, `ZSS2001` | Permits compact try/catch/finally clauses when every clause occupies one line and contains one simple statement; suppresses block-boundary IDE0055 and block-local IDE2001, while retaining expression-formatting diagnostics |
| `StatementSpacingAnalyzer` / `ZS2003` | Requires blank lines at declaration/expression and declaration/control boundaries in either direction, and between adjacent independent control structures. Applies to blocks, switch sections and top-level statements, including unbraced bodies. Consecutive declarations, short declaration-then-return sequences, related clauses and nested bodies remain together; comments do not replace blank lines |
| SDK `CA1303` | Checks hardcoded Console output, parameters/properties marked `Localizable(true)` and the Text/Message/Caption naming heuristic |
| `ResourceAccessAnalyzer` / `ZS1304` | Reports fixed-key calls to `ResourceManager.GetString/GetObject/GetStream`; use generated Designer properties; generated code and dynamic keys are excluded |

The shared `Zongsoft.CodeAnalysis.Analyzers.globalconfig` defines UPPER_SNAKE_CASE local constants and camelCase ordinary locals through naming rules; no custom analyzer is needed for this distinction.

The built-in IDE0005 can combine consecutive unused imports, so the package disables it and uses ZS0005 to report imports individually. Aliases, `using static`, `global using` and `System.*` are outside the System exception. Like IDE0005, build-time unused-import analysis requires `GenerateDocumentationFile=true`; the package preserves an explicit choice to disable XML documentation. Framework Core already enables it.

Localization requires the neutral `.resx` to use `ResXFileCodeGenerator`, with the generated `*.Designer.cs` committed and its properties used by application code. This analyzer has English neutral resources and a `zh-Hans` translation; its Chinese satellite assembly is packed under `analyzers/dotnet/cs/zh-Hans`. Roslyn diagnostics use `nameof(generated property)` with `LocalizableResourceString` to defer language selection instead of freezing it when descriptors are created.

CA1303 naming heuristics cannot determine every string's purpose. Protocol keys, paths, machine-readable text and custom UI/logging APIs still require review; use `Localizable(false)` on parameters or properties that clearly do not need translation where appropriate. Fixed-key lookup wrappers, ResX/Designer synchronization and generator configuration in other projects also require review. Ordinary `dotnet build` does not run the VS custom tool; CI uses the committed generated files.

## Code fixes

In Visual Studio 2026, place the caret on a ZS0005 or ZS2003 diagnostic and press `Ctrl+.` to select **Remove unused using** or **Insert blank line**. Preview and apply an individual fix, or use Fix All for the same rule in a document, project or solution. The providers load from this package; no separate VSIX is required.

ZS0005 retains ordinary `using System;`, comments and directives. ZS2003 inserts a blank line only at the statement-group boundary, preserving indentation and line endings without formatting unrelated code. SDK rules use their SDK-provided fixes; this package does not yet offer custom resource migration for ZS1304 or CA1303.

From the project directory, restrict CLI fixes to specific files and diagnostics:

```powershell
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 --include ./Example.cs
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 --include ./Example.cs --verify-no-changes
```

The first command changes the selected file; the second only checks it. See the [code fix implementation plan](https://github.com/Zongsoft/Guidelines/blob/main/analysis/CODE_FIXES.md) for full guideline coverage, resource-generation constraints and phased acceptance.

## Configuration

The source file for [the global rules](analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) is maintained in analyzers/src; [props](Zongsoft.CodeAnalysis.props) and [targets](Zongsoft.CodeAnalysis.targets) are maintained in this directory. Their NuGet package paths remain the package root and buildTransitive, respectively.

Keep editor settings such as Tab indentation, CRLF and file-specific exceptions in the repository's `.editorconfig`; a template is included in the package at `.editorconfig`. C# diagnostic settings come from `Zongsoft.CodeAnalysis.Analyzers.globalconfig` at the package root, loaded through `buildTransitive/Zongsoft.CodeAnalysis.props`, and update with the package version. Local EditorConfig settings take precedence: remove obsolete copied C# rules when migrating, while retaining intentional project overrides.

The package contains the analyzer under `analyzers/dotnet/cs` and automatically imported settings under `buildTransitive`. It includes no lib/ref assemblies or Roslyn dependencies and adds no business runtime assembly reference. The analyzer and its tests declare build settings and dependency versions in their own project files, without enabling Central Package Management.

> 💡 `dotnet format whitespace` compares formatted text directly and does not apply diagnostic suppressors, so it may flag permitted layouts. Use loaded-analyzer build diagnostics for these exceptions. Formatting fixes should be limited to changed files and reviewed. IDE0049 requires the editor or a separate `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes` check.

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

The regression suite uses the .NET 10 SDK and xUnit v3 through VSTest. It packs the analyzer, then builds isolated temporary consumer projects using PackageReference, a local package source and separate package caches. Assertions check diagnostic IDs and source locations, exit codes, analyzer loading and unchanged source text. Failures include the build output; temporary directories retain `build.log` for diagnosis.

Code-fix tests load the actual package through MEF and validate exact edits, comments and directives, localized titles, absence of new compiler errors, and document/project/solution batch scopes in Roslyn workspaces.

Both Debug and Release packages are written directly to `analysis` as `Zongsoft.CodeAnalysis.<Version>.nupkg`. A subsequent build of the same version replaces that package; compiler outputs remain separated by configuration, and the publishing workflow always uses Release. Other versions may remain in the directory; publication selects only the current project version.

Publish a validated version to an accessible NuGet feed before using it in other machines or CI. Packing does not publish. Update the version in `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj` for each release, then upgrade consuming projects; do not overwrite published versions. Merge editor-template changes separately when needed.

See the [setup and coverage guide](https://github.com/Zongsoft/Guidelines/blob/main/README.md#code-analysis) for central configuration, local package validation and upgrade steps, and the [coding guidelines](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md) for the complete development rules.
