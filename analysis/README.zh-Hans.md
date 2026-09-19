# Zongsoft.CodeAnalysis

[English](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.md) | [简体中文](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.zh-Hans.md)

面向 Zongsoft 项目的 C# 分析器和共享编码规则，以开发期 NuGet 包分发。源码、测试、构建配置和 C# 规则只在 guidelines 仓库维护，框架及业务系统通过升级包版本更新。

## 接入方式

在 SDK 风格的 C# 项目中添加包引用：

```xml
<ItemGroup>
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="1.2.0" PrivateAssets="all" />
</ItemGroup>
```

采用中央包版本管理时，在 `Directory.Packages.props` 声明版本，并省略引用中的 `Version`。`PrivateAssets="all"` 防止分析器成为消费项目发布包的依赖；需要执行规范的项目应明确引用。

NuGet 自动加载分析器 DLL、SDK 构建风格检查和共享 Global AnalyzerConfig，无需复制源码或手工导入 props。分析器使用 Roslyn 5.9 API，开发环境要求支持 Roslyn 5.9 的 VS2026 或 .NET 10 SDK。程序集目标为 netstandard2.0，消费项目可面向 net8.0、net9.0、net10.0；消费项目的目标框架与加载分析器的编译器版本是两个独立条件。

普通构建将配置的风格问题报告为警告。在项目中设置 `ZongsoftCodeStyleStrict=true`，或传入 `-p:ZongsoftCodeStyleStrict=true`，可将指定风格警告升级为错误。CS4014 和 CA2012 始终保持为错误。

## 规则与例外

[📋 规则列表](https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#index) 集中说明各诊断的级别、触发条件、例外、示例及修复能力，包括未使用引用、语句空行、成员分段、XML 文档、本地化、命名及格式豁免。

在 VS2026 中点击自定义 ZS 诊断的帮助链接，或在错误列表中选择规则后按 F1，可打开规则对应的在线锚点。SDK 诊断保留微软链接。规则列表同时提供英文版，并以 RULES.md / RULES.zh-Hans.md 随包分发。

本地化检查：ZS1301 检查直接异常消息，ZS1302 检查手写字符串中的非 ASCII 文字，ZS1304 检查资源属性访问；三者均在 `IsTestProject=true` 时停用。CA1303 默认关闭。不追踪变量、异常工厂或构造链，未告警不等于已经本地化；详细边界见规则列表。

## 代码修复

在 Visual Studio 2026 中将光标放在 ZS0005、ZS2003 或 ZS3003 的警告位置，按 `Ctrl+.` 打开快速操作，选择“移除未使用的引用”、“插入空行”或“合并 XML 文档行”。可先预览，再修复单处或同一规则在文档、项目、解决方案内的所有位置。修复器随本包加载，无需另外安装 VSIX。

修复范围与保留行为分别见 [ZS0005](https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs0005)、[ZS2003](https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs2003) 和 [ZS3003](https://github.com/Zongsoft/Guidelines/blob/main/RULES.zh-Hans.md#zs3003)。打开规则帮助不会自动修改源码。

也可在项目目录中限定文件和诊断执行：

```powershell
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 ZS3003 --include ./Example.cs
dotnet format analyzers ./Example.csproj --diagnostics ZS0005 ZS2003 ZS3003 --include ./Example.cs --verify-no-changes
```

第一条修改指定文件，第二条仅验证。其他规则的修复方式见规则列表。

## 配置与边界

[全局规则](analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) 的源文件在 analyzers/src 子目录维护，[props](Zongsoft.CodeAnalysis.props) 和 [targets](Zongsoft.CodeAnalysis.targets) 在当前目录维护；打包后仍分别放在包根目录和 buildTransitive 目录。

Tab 缩进、CRLF 和各类文件例外等编辑器设置保留在仓库的 `.editorconfig`，包内 `.editorconfig` 提供模板。C# 检测规则来自包根目录的 `Zongsoft.CodeAnalysis.Analyzers.globalconfig`，由 `buildTransitive/Zongsoft.CodeAnalysis.props` 加载，并随包版本更新。本地 EditorConfig 同名配置优先；项目仅需声明有意覆盖的规则。

包内分析器位于 `analyzers/dotnet/cs`，自动导入的构建配置位于 `buildTransitive`。包不含 lib/ref 程序集或 Roslyn 依赖，也不增加业务运行时程序集引用。分析器及测试的构建设置和依赖版本直接在各自项目文件中声明，不启用中央包版本管理。

> 💡 `dotnet format whitespace` 直接比较格式化结果，不执行诊断抑制器，仍可能报告规范允许的排版。这些例外以加载分析器的构建诊断为准；自动排版应限定修改范围并检查差异。IDE0049 需要编辑器或单独执行 `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes` 补充检查。

## EditorConfig 同步

在消费仓库根目录的 `Directory.Build.props` 中设置：

```xml
<PropertyGroup>
	<ZongsoftGuidelinesSynchronization>$(MSBuildThisFileDirectory)</ZongsoftGuidelinesSynchronization>
</PropertyGroup>
```

属性值是同步目标目录；未设置或为空时不启用。还原包后，正常构建会在编译准备阶段自动将当前引用包的模板同步为目标目录的 `.editorconfig`，无需单独运行同步或检查命令。

同步完整复制模板并保留编码与换行，不合并本地修改；使用 MSBuild 内置 Copy 任务，缺失时创建，大小或修改时间不同则复制，两者都相同时跳过，不逐字节比较内容。项目例外放在相应子目录的 `.editorconfig`，同步后检查差异并提交根文件，让编辑器在包还原前也能使用配置。

目标目录必须存在，相对路径以消费项目目录为基准。目录无效时报 ZSCFG001，写入失败使构建失败。复制失败最多重试 3 次；支持多目标和解决方案构建，并行项目可能重复复制同一模板，不保证只写入一次。同一仓库应统一分析器包版本。

保留 VS 快速最新检查：VS 判断项目已是最新而跳过 MSBuild 时不同步，需要时执行“重新生成”，或“清理”后再“生成”。单独还原、清理及设计时构建不执行同步，Clean 不删除仓库的 `.editorconfig`。

消费仓库的 `.gitattributes` 应包含 `.editorconfig text eol=crlf`，让 Windows 和 Linux 的 Git 检出保留相同换行。guidelines 和 framework 已配置此项。升级或回退包后，通过下一次实际构建同步对应版本的模板。

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

回归测试使用 .NET 10 SDK 与 xUnit v3，经 VSTest 运行。测试先打包，再通过 PackageReference、本地包源和每次运行的全新包缓存构建临时消费项目，断言诊断编号与源码位置、退出码、分析器加载状态及源码未改写。失败时显示完整构建输出，临时目录保留 `build.log` 供排查。

修复回归测试直接加载真实包中的 MEF 导出，在 Roslyn 工作区验证精确修改、注释与指令保留、中英文标题、无新增编译错误，以及文档／项目／解决方案的批量范围。

Debug 和 Release 的 NuGet 包都直接生成在 `analysis`，文件名为 `Zongsoft.CodeAnalysis.<Version>.nupkg`。相同版本后构建的包覆盖前一次产物；编译输出仍按配置隔离，发布工作流固定使用 Release。发布任务仅选择项目当前版本。

供其他机器或 CI 使用前，应将验证通过的版本发布到可访问的 NuGet 源；打包不会自动发布。每次发布在 `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj` 递增版本，再由消费项目升级；不覆盖已发布版本。启用同步后，编辑器模板随下一次实际构建更新。

中央配置、本地包验证及升级步骤见 [配套说明](https://github.com/Zongsoft/Guidelines/blob/main/README.zh-Hans.md#code-analysis)，完整开发要求见 [开发规范](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md)。

<a id="unicode"></a>
## Unicode 数据

ZS1302 使用固定的 Unicode 17.0 分类表，构建和分析均不联网。维护分类表时，将官方 [DerivedGeneralCategory.txt](https://www.unicode.org/Public/17.0.0/ucd/extracted/DerivedGeneralCategory.txt) 和 [emoji-data.txt](https://www.unicode.org/Public/17.0.0/ucd/emoji/emoji-data.txt) 下载到同一临时目录，从仓库根目录执行：

```powershell
python analysis/analyzers/tool/generate-unicode.py <数据目录>
```

脚本生成 `analyzers/src/UnicodeText.Generated.cs`。Unicode 许可随包分发，见 [Unicode.LICENSE.txt](analyzers/src/Unicode.LICENSE.txt)。
