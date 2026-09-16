# Zongsoft.CodeAnalysis

[English](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.md) | [简体中文](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.zh-Hans.md)

面向 Zongsoft 项目的 C# 分析器和共享编码规则，以开发期 NuGet 包分发。源码、测试、构建配置和 C# 规则只在 guidelines 仓库维护，框架及业务系统通过升级包版本更新。

## 接入方式

在 SDK 风格的 C# 项目中添加包引用：

```xml
<ItemGroup>
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

采用中央包版本管理时，在 `Directory.Packages.props` 声明版本，并省略引用中的 `Version`。`PrivateAssets="all"` 防止分析器成为消费项目发布包的依赖；需要执行规范的项目应明确引用。

NuGet 自动加载分析器 DLL、SDK 构建风格检查和共享 Global AnalyzerConfig，无需复制源码或手工导入 props。分析器面向 netstandard2.0，使用 Roslyn 3.8 公共 API；已使用 .NET 10 SDK 验证 net8.0、net9.0 和 net10.0。

普通构建将配置的风格问题报告为警告。在项目中设置 `ZongsoftCodeStyleStrict=true`，或传入 `-p:ZongsoftCodeStyleStrict=true`，可将指定风格警告升级为错误。CS4014 和 CA2012 始终保持为错误。

## 规则与例外

| 组件 | 行为 |
| --- | --- |
| `UnusedUsingAnalyzer` / `ZS0005` | 使用编译器 CS8019 逐条报告未使用引用；仅豁免解析为全局 System 命名空间的普通 `using System;` |
| `StyleSuppressor` / `ZSS0055` | 仅豁免 `#if`、`#elif`、`#else`、`#endif` 行前空白上的 IDE0055 |
| `StyleSuppressor` / `ZSS0056`、`ZSS2001` | 当 try/catch/finally 的每个子句都各占一行且仅含一条简单语句时，豁免块边界 IDE0055 和块内 IDE2001；语句内部的格式问题仍会报告 |
| `StatementSpacingAnalyzer` / `ZS2003` | 声明组与调用、赋值或控制结构的双向边界，以及相邻独立控制结构之间应有空行；覆盖语句块、switch 分支、顶层语句和无花括号写法。连续声明、简短的声明后返回、关联子句和嵌套语句体不拆分；注释行不代替空行 |
| SDK `CA1303` | 检查 Console 提示、标记为 `Localizable(true)` 的参数／属性及 Text、Message、Caption 命名启发式识别的硬编码文本 |
| `ResourceAccessAnalyzer` / `ZS1304` | 固定资源键直接调用 `ResourceManager.GetString/GetObject/GetStream` 时提示改用 Designer 生成属性；生成代码与动态键解析不报告 |

共享 `Zongsoft.CodeAnalysis.Analyzers.globalconfig` 通过命名规则区分 UPPER_SNAKE_CASE 局部常量与 camelCase 普通局部变量，无需为此编写自定义分析器。

内置 IDE0005 会合并连续的未使用引用，因此本包禁用它，以 ZS0005 逐条定位。别名、`using static`、`global using` 和 `System.*` 不属于 System 导入例外。与 IDE0005 相同，构建时的未使用引用检查要求 `GenerateDocumentationFile=true`；本包保留项目显式关闭 XML 文档的选择。框架 Core 已开启文档生成。

本地化规则要求默认 `.resx` 使用 `ResXFileCodeGenerator` 生成并提交 `*.Designer.cs`，业务代码通过生成属性访问资源。分析器本身使用英文默认资源和 `zh-Hans` 翻译，中文卫星程序集随包放在 `analyzers/dotnet/cs/zh-Hans`。Roslyn 诊断使用 `nameof(生成属性)` 和 `LocalizableResourceString` 延迟选择语言，避免在创建描述符时固定语言。

CA1303 的命名启发式不能准确识别所有文本用途；协议键、路径、机器文本以及自定义 UI／日志接口需要人工审查，必要时对明确不需翻译的参数或属性使用 `Localizable(false)`。固定键查询包装器、Designer 与 ResX 是否同步及其他项目是否正确设置生成器也需要审查。VS 自定义工具不在普通 `dotnet build` 时运行，CI 使用提交的生成文件。

## 代码修复

在 Visual Studio 2026 中将光标放在 ZS0005 或 ZS2003 的警告位置，按 `Ctrl+.` 打开快速操作，选择“移除未使用的引用”或“插入空行”。可先预览，再修复单处或同一规则在文档、项目、解决方案内的所有位置。修复器随本包加载，无需另外安装 VSIX。

ZS0005 保留普通 `using System;` 和注释、条件编译指令；ZS2003 只在语句组边界补空行，保留现有缩进与换行符，不格式化其他代码。SDK 规则使用 SDK 自带修复；ZS1304 和 CA1303 的资源迁移尚未提供本包自定义修复。

也可在项目目录中限定文件和诊断执行：

```powershell
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 --include ./Example.cs
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 --include ./Example.cs --verify-no-changes
```

第一条修改指定文件，第二条仅验证。全部规范的后续覆盖、资源生成边界和分阶段验收见 [代码修复技术规划](https://github.com/Zongsoft/Guidelines/blob/main/analysis/CODE_FIXES.zh-Hans.md)。

## 配置与边界

[全局规则](analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) 的源文件在 analyzers/src 子目录维护，[props](Zongsoft.CodeAnalysis.props) 和 [targets](Zongsoft.CodeAnalysis.targets) 在当前目录维护；打包后仍分别放在包根目录和 buildTransitive 目录。

Tab 缩进、CRLF 和各类文件例外等编辑器设置保留在仓库的 `.editorconfig`，包内 `.editorconfig` 提供模板。C# 检测规则来自包根目录的 `Zongsoft.CodeAnalysis.Analyzers.globalconfig`，由 `buildTransitive/Zongsoft.CodeAnalysis.props` 加载，并随包版本更新。本地 EditorConfig 同名配置优先；迁移时应移除过时的 C# 规则副本，只保留有意的项目覆盖。

包内分析器位于 `analyzers/dotnet/cs`，自动导入的构建配置位于 `buildTransitive`。包不含 lib/ref 程序集或 Roslyn 依赖，也不增加业务运行时程序集引用。分析器及测试的构建设置和依赖版本直接在各自项目文件中声明，不启用中央包版本管理。

> 💡 `dotnet format whitespace` 直接比较格式化结果，不执行诊断抑制器，仍可能报告规范允许的排版。这些例外以加载分析器的构建诊断为准；自动排版应限定修改范围并检查差异。IDE0049 需要编辑器或单独执行 `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes` 补充检查。

## Cake 构建流程

先在仓库根目录执行 `dotnet tool restore` 还原固定版本的 Cake.Tool，再从 `analysis` 目录运行。[build.cake](https://github.com/Zongsoft/Guidelines/blob/main/analysis/build.cake) 沿用 deployer/packager 的参数和任务约定：`--edition` 可选 Debug 或 Release，默认 Release；`pack` 表示发布到 nuget.org。

```powershell
dotnet cake build.cake
dotnet cake build.cake --target=build
dotnet cake build.cake --target=pack
```

| 任务 | 行为 |
| --- | --- |
| `clean` | 清理所选配置的两个库、两套测试的编译输出及测试临时包 |
| `restore` | 按所选配置还原两个库和两套测试，共四个项目 |
| `build` | 编译两个库，在 `analysis` 生成统一包，再编译两套消费测试 |
| `test` / `default` | 构建包并运行消费回归测试 |
| `pack` | 测试通过后，将 `analysis/Zongsoft.CodeAnalysis.<Version>.nupkg` 推送到 nuget.org |

调用 `pack` 前设置环境变量 `NUGET_API_KEY`，脚本读取密钥而不打印其内容；已发布的重复版本会跳过。Cake 的两套测试共同验证 analysis 目录中的本次包；单独运行 dotnet test 时使用 `analysis/obj/packages/<edition>` 内的测试临时包。发布变更前先递增分析器项目中的版本号。

可用 `dotnet cake build.cake --target=pack --dryrun` 查看任务顺序而不执行任务。手动发布时，使用 `--target=build` 生成本地包即可。Cake 运行器参数见 [官方说明](https://cakebuild.net/docs/running-builds/runners/dotnet-tool)。

## GitHub Actions 发布

[Publish Analyzer to NuGet 工作流](https://github.com/Zongsoft/Guidelines/blob/main/.github/workflows/publish-nuget.yml) 仅支持手动触发，并限定从 `main` 分支、`release` 环境发布。它先还原固定版本的 Cake 工具，构建并测试 Release 包，再通过 `NuGet/login@v1` 获取临时凭据，最后执行 Cake 的 `pack --exclusive` 发布同一个包。作业授予 `contents: read` 和 `id-token: write` 权限，GitHub 中无需保存长期 NuGet API Key。

首次发布前完成以下配置：

1. 在 guidelines 仓库创建名为 `release` 的 GitHub Environment，并添加环境 Secret `NUGET_USER`，值为 nuget.org **用户名**，不是邮箱。
2. 在 [nuget.org Trusted Publishing](https://www.nuget.org/account/trustedpublishing) 创建匹配的信任策略：

| 策略字段 | 值 |
| --- | --- |
| Repository Owner | `Zongsoft` |
| Repository | `Guidelines` |
| Workflow File | `publish-nuget.yml` |
| Environment | `release` |

工作流字段只填写文件名，不包含 `.github/workflows/`，也不填写工作流显示名称。策略所属账户需拥有 `Zongsoft.CodeAnalysis` 的发布权限，详细配置见 [官方 Trusted Publishing 说明](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)。

完成配置且工作流进入默认分支后，手动入口为 **Actions → Publish Analyzer to NuGet → Run workflow**，选择 `main`。发布版本取自 `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj`，发布变更前应先递增版本。选择其他分支时作业会跳过；构建或测试失败时不会登录和发布。

## 构建与验证

两套测试各自在项目文件中定义包准备和版本读取目标，可分别独立构建和运行测试。

使用 Visual Studio 2026 打开 `Zongsoft.CodeAnalysis.slnx`，即可同时加载 `analyzers/src`、`fixes/src` 中的两个库及各自 `test` 目录中的测试项目。构建脚本和中英文 README 保留在当前目录。

从 guidelines 仓库根目录执行：

```powershell
dotnet test ./analysis/Zongsoft.CodeAnalysis.slnx
dotnet pack ./analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj -c Release
```

回归测试使用 .NET 10 SDK 与 xUnit v3，经 VSTest 运行。测试先打包，再通过 PackageReference、本地包源和独立包缓存构建临时消费项目，断言诊断编号与源码位置、退出码、分析器加载状态及源码未改写。失败时显示完整构建输出，临时目录保留 `build.log` 供排查。

修复回归测试直接加载真实包中的 MEF 导出，在 Roslyn 工作区验证精确修改、注释与指令保留、中英文标题、无新增编译错误，以及文档／项目／解决方案的批量范围。

Debug 和 Release 的 NuGet 包都直接生成在 `analysis`，文件名为 `Zongsoft.CodeAnalysis.<Version>.nupkg`。相同版本后构建的包覆盖前一次产物；编译输出仍按配置隔离，发布工作流固定使用 Release。其他历史版本可保留，发布任务仅选择项目当前版本。

供其他机器或 CI 使用前，应将验证通过的版本发布到可访问的 NuGet 源；打包不会自动发布。每次发布在 `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj` 递增版本，再由消费项目升级；不覆盖已发布版本。编辑器模板变化时按需单独合并。

中央配置、本地包验证及升级步骤见 [配套说明](https://github.com/Zongsoft/Guidelines/blob/main/README.zh-Hans.md#code-analysis)，完整开发要求见 [开发规范](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md)。
