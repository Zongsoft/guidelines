# Zongsoft Development Guidelines

[English](README.md) | [简体中文](README.zh-Hans.md)

## 📚 Development Guidelines

- [Zongsoft C# Development Guidelines](zongsoft.csharp.guidelines.md)
- [Zongsoft REST API Guidelines](zongsoft.rest-api.guidelines.md)
- [AI Development Collaboration Guidelines](AGENTS.md)

<a id="code-analysis"></a>

## 🛠️ .NET/C# Code Analysis

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
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

With Central Package Management, declare the version in `Directory.Packages.props`:

```xml
<PackageVersion Include="Zongsoft.CodeAnalysis" Version="0.1.0" />
```

Add the versionless reference to the C# project or shared `Directory.Build.props`:

```xml
<ItemGroup Condition="'$(MSBuildProjectExtension)' == '.csproj'">
	<PackageReference Include="Zongsoft.CodeAnalysis" PrivateAssets="all" />
</ItemGroup>
```

After restore, NuGet automatically loads the analyzer and shared rules. No manual props imports, source copies or submodule initialization are needed. `PrivateAssets="all"` prevents rules from flowing to downstream consumers through business libraries. Each project that needs the checks should reference the package explicitly; a repository can add that reference in its shared build file.

Keep editor settings in the root EditorConfig. For initial setup, use this repository's file or the `.editorconfig` template at the package root as a reference. Merge existing settings as needed, preserving project exceptions. C# rules come from the packaged Global AnalyzerConfig, while matching local EditorConfig settings take precedence. When migrating, remove copied C# rules and retain only intentional project overrides so stale settings do not block package upgrades. Framework has completed this migration, and both repositories retain identical root EditorConfig files.

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

Maintain, validate and publish new versions in guidelines. Consumers update PackageReference or the central version, then restore and build. C# diagnostic rules update with the package version; merge the editor template separately only when common editor settings change. Roll back by selecting a previous package version.

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

Strict mode covers `ZS0005`, `ZS2003`, `ZS1304`, `CA1303`, `IDE0009`, `IDE0055`, `IDE0065`, `IDE0161`, `IDE1006` and `IDE2001`. It checks the project's entire compilation input and any project references actually built, not just Git changes. Existing projects may have many outstanding diagnostics; a successful normal build does not establish that strict checks pass.

`IDE0049` (using type keywords such as `int`) does not run during ordinary `dotnet build`, even with build-time code-style analysis enabled. Add this read-only check instead of relying on strict builds alone:

```powershell
dotnet format style ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore --verify-no-changes --diagnostics IDE0049
```

To inspect the standard formatter's suggestions for a changed file, use:

```powershell
dotnet format whitespace ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore --verify-no-changes --include ./Zongsoft.Core/src/Components/Handler.cs
```

**Note:** `dotnet format whitespace` compares formatted text directly and does not execute diagnostic suppressors. It can still report differences for permitted conditional directive indentation or compact blocks. Loaded-analyzer build diagnostics govern these exceptions; do not use the formatter's exit code as an unconditional CI gate for them.

`--include` paths are relative to the working directory. Replace the example file with the actual changed files; multiple paths are supported. `--verify-no-changes` does not rewrite source and returns a nonzero exit code when formatting differs. CI must check build and IDE0049 verification exit codes; review the standard formatter's known exception differences separately. Multi-target builds cover the configured target frameworks, and formatting checks do not replace compilation and analysis for each target.

> ⚠️ **Automatic fixes:** Do not run unrestricted `dotnet format` or an editor's import organizer across the repository. Standard import organizers sort alphabetically and cannot implement the guideline's namespace-length ordering within groups. Limit fixes to changed files with `dotnet format whitespace --include ...`, then inspect the diff. Do not use bulk renaming or syntax conversion to automatically modify existing public APIs.

### Rule coverage and limitations

| Guideline | Tooling behavior | Limitations |
| --- | --- | --- |
| 2.1 Casing, interface `I` prefix, `_camelCase` fields, constants and generic parameters | `IDE1006`, differentiated by member kind, visibility and `const` | Cannot assess domain meaning or validate the full namespace design; names required by external interfaces need contract review |
| 3.1 / 3.4 Indentation, line breaks, spacing and Allman braces | EditorConfig editor settings, packaged global rules and `IDE0055`; suppressors exempt whitespace before conditional directives and compact exception-handling block boundaries | C# formatting checks do not cover XML or Markdown; multiline blocks and logical grouping still require review |
| 3.2 File-scoped namespaces, import placement and unused imports | `IDE0161`, `IDE0065`, `ZS0005` | `ZS0005` uses the compiler's unused-import diagnostics and still requires `GenerateDocumentationFile=true` during builds; ordinary `using System;` is permitted; IDE0005 is disabled to avoid its merging of consecutive imports |
| 3.2 No aliases, cautious production `global using`, test `Global.cs`, dependency groups and namespace-length ordering | Code review | Built-in SDK rules cannot express these requirements fully; automated enforcement needs dedicated Roslyn analyzers, not regex substitutes or invented configuration keys |
| 3.3 Chinese `#region` labels, responsibility ordering and adjacent overloads | Code review | Standard formatters cannot determine responsibility boundaries |
| 3.4 Optional braces for simple branches, with statements on separate lines | Braces are a suggestion; `IDE2001` checks same-line embedded statements | `IDE2001` uses an experimental option that needs rechecking after SDK upgrades; suppressors allow try/catch/finally clauses that each contain one simple statement on one line; dangling `else` and complex expressions need review |
| 3.4 Statement groups and independent control structures | `ZS2003` | Checks declaration/expression and declaration/control boundaries in both directions, plus adjacent independent control structures in blocks, switch sections and top-level code. Consecutive declarations (including complete deconstructions), short declaration-then-return sequences and related clauses remain together; other logical grouping requires review |
| 7.4 Localization and strongly typed resources | `CA1303`, `ZS1304` | CA1303 checks Console, Localizable(true) and Text/Message/Caption naming heuristics; ZS1304 checks direct fixed-key ResourceManager reads. Dynamic keys and generated code are excluded; text purpose, custom lookup wrappers, generator settings and Designer synchronization require review |
| 5.1 `this.` for instance properties, methods and events | `IDE0009` | The built-in field option cannot distinguish visibility; review must enforce `this.` for public fields and omit it for private fields |
| 3.2 / 5.1 Preserve fully qualified names used to resolve conflicts | Disable automatic simplification diagnostics `IDE0001`, `IDE0002` and `IDE0003` | This also removes some harmless simplification hints; it does not permit unrestricted use of fully qualified names |
| 5.1 Type keywords | `IDE0049` in the editor and `dotnet format style`, not during builds | `var` remains a suggestion; explicit types may convey precision, an interface view or other intent |
| 5.2 Target-typed `new`, collection expressions and expression bodies | SDK style suggestions; collection conversions are suggested only when target types match exactly | Review constructor arguments, comparers, enumeration timing and lifetimes; do not encourage bulk conversion to primary constructors or top-level statements |
| 7.1 Some unawaited tasks and ValueTask misuse | `CS4014` and `CA2012` are errors | Cannot fully check the `Async` suffix, `async void`, cancellation propagation, synchronous blocking or background-task ownership |
| 4 / 6 / 7 / 8 Documentation, contracts, composition, exceptions, resources and test quality | Some compiler and SDK diagnostics; otherwise review and testing | Adds no repository-wide documentation-warning suppression and does not change existing `NoWarn`; passing checks cannot replace behavioral validation |

Naming-rule `severity` and build diagnostic severity are separate configuration layers, so `IDE1006` is explicitly configured as a warning too. In the packaged global configuration, constant rules with more specific visibility or modifier requirements take precedence over ordinary private-field or local-variable rules. Valid `DEFAULT_CAPACITY` constants are not required to become `_defaultCapacity`, and a method-local `const int SIZE` is not required to become `size`.

### Tool references

- [Analyzer configuration files](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files): responsibilities of editor and analyzer configuration.
- [Naming rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules): symbol matching, precedence and build severity.
- [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format): check scope, subcommands and exit codes.
- [DiagnosticSuppressor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticsuppressor): exempt diagnostics at specific syntax locations; direct formatting is not the same as executing a suppressor.
- [IDE0049](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0049): type-keyword checks do not run during builds.
- [CA1303](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1303): localizable parameters, Console text, naming heuristics and limitations.
