# Zongsoft 开发规范

[English](README.md) | [简体中文](README.zh-Hans.md)

## 开发规范

- [《Zongsoft C# 开发规范》](zongsoft.csharp.guidelines.md)
- [《.NET/C# 规则列表》](RULES.zh-Hans.md)
- [《Zongsoft REST API 规范》](zongsoft.rest-api.guidelines.md)
- [《AI 开发协作规范》](AGENTS.md)

<a id="code-analysis"></a>

## .NET/C# 代码规范检查

[配套文件](#配套文件) · [接入项目](#接入项目) · [打包与验证](#打包验证与升级) · [自动修复](#自动修复) · [检查命令](#检查命令) · [规则覆盖](#规则覆盖与边界) · [工具依据](#工具依据)

本配套将 [开发规范](zongsoft.csharp.guidelines.md) 中可检测的规则打包为 `Zongsoft.CodeAnalysis` NuGet 分析器。源码、测试、构建开关和 C# 规则只在 guidelines 维护；框架和业务系统通过包版本更新。检查通过不代表设计、兼容性和资源所有权已审查。

### 配套文件

| 文件 | 职责 |
| --- | --- |
| [`.editorconfig`](.editorconfig) | 编辑器的 Tab、CRLF、文件类型例外和既有 VB 偏好；不再复制 C# 检测规则 |
| [`Zongsoft.CodeAnalysis.Analyzers.globalconfig`](analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) | C# 风格、命名、诊断严重级别，随包自动加载 |
| [`Zongsoft.CodeAnalysis.props`](analysis/Zongsoft.CodeAnalysis.props) / [`.targets`](analysis/Zongsoft.CodeAnalysis.targets) | NuGet 自动导入的 SDK 开关、全局规则注册与严格检查 |
| [`analysis`](analysis/README.zh-Hans.md) | 分析器源码、打包项目和真实包消费回归测试 |
| [`.gitattributes`](.gitattributes) | `.sh` 使用 LF、`.cmd` 使用 CRLF |

包按标准 `analyzers/dotnet/cs` 布局放置 DLL，以 `buildTransitive` 导入配置；不含业务运行时程序集或 Roslyn 运行时依赖。支持 SDK 风格的 C# 项目与 PackageReference；当前验证使用 .NET 10 SDK，覆盖 net8.0、net9.0、net10.0。SDK 内置规则随 SDK 变化，升级后仍需重新验证。

### 接入项目

普通项目添加：

```xml
<ItemGroup>
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="0.2.0" PrivateAssets="all" />
</ItemGroup>
```

使用中央包版本管理时，在 `Directory.Packages.props` 添加版本：

```xml
<PackageVersion Include="Zongsoft.CodeAnalysis" Version="0.2.0" />
```

在 C# 项目或公共 `Directory.Build.props` 中添加不带版本的引用：

```xml
<ItemGroup Condition="'$(MSBuildProjectExtension)' == '.csproj'">
	<PackageReference Include="Zongsoft.CodeAnalysis" PrivateAssets="all" />
</ItemGroup>
```

还原后，NuGet 自动加载分析器和共享规则，无需手工导入 props、复制源码或初始化子模块。`PrivateAssets="all"` 防止规则经业务库传递给下游；每个需要执行规范的项目应明确引用，仓库可以在公共构建文件统一添加。

保留根 EditorConfig 的编辑器设置。初次接入可参考本仓库文件或包中的 `.editorconfig` 模板，已有配置应按需合并，不覆盖项目例外。C# 规则来自包内 Global AnalyzerConfig；本地 EditorConfig 的同名设置优先。因此从旧版迁移时应移除复制的 C# 规则，只保留有意的项目覆盖，避免旧规则阻挡包升级。框架已完成此迁移，两边的根 EditorConfig 仍保持一致。

从 0.2.0 起，设置 `ZongsoftGuidelinesSynchronization` 为仓库根目录即可启用 [构建时自动同步](analysis/README.zh-Hans.md#editorconfig-同步)。guidelines 文件是唯一维护源，消费仓库提交同步副本，有意的项目例外放在子目录配置中。VS 跳过构建时可用“重新生成”触发同步。

### 打包、验证与升级

可在 `analysis` 目录通过 [Cake 脚本](analysis/build.cake) 完成编译、测试、打包和发布；参数及发布密钥配置见 [Cake 构建流程](analysis/README.zh-Hans.md#cake-构建流程)。

GitHub Actions 手动发布使用 [publish-nuget.yml](.github/workflows/publish-nuget.yml)，首次使用需要配置 `release` 环境、`NUGET_USER` 和 nuget.org 信任策略，详见 [GitHub Actions 发布](analysis/README.zh-Hans.md#github-actions-发布)。

直接使用 .NET CLI 时，在 guidelines 根目录执行：

```powershell
dotnet test ./analysis/Zongsoft.CodeAnalysis.slnx
dotnet pack ./analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj -c Release
```

Release 包默认生成在 `analysis`，可从该目录获取 `.nupkg` 文件后发布。

测试会先生成包，再以 PackageReference 构建独立临时消费项目，每个项目使用独立包缓存，避免旧版本缓存造成误判。包版本在 `analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.csproj` 维护；发布新版必须递增版本，不覆盖已发布版本。

首次发布前可在 framework 本地验证：

```powershell
dotnet restore ./Zongsoft.Core/src/Zongsoft.Core.csproj --source ../guidelines/analysis --source 'https://api.nuget.org/v3/index.json'
dotnet build ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore -p:GeneratePackageOnBuild=false
```

上述本地包源仅用于发布前验证，不写入消费仓库配置。正式接入前需要把包发布到业务项目可访问的 NuGet 源；尚未发布的版本无法在全新工作区或 CI 仅从公共源还原。打包不会发布，发布源与权限按维护者流程处理。

以后只在 guidelines 修改、验证并发布新版本，消费方更新 PackageReference 或中央版本后还原和构建。C# 检测规则随包版本升级；启用同步后，编辑器模板随实际构建更新。回退同样只需选择此前的包版本。

> 💡 **文件格式：** 配置为新文件提供 CRLF 默认值，但编辑器可能把该值应用到已有文件。既有 LF 文件需用就近 `.editorconfig` 覆盖以保留原换行符。配置刻意不设置 `charset`，避免统一改写已有 BOM；新文件按开发规范保存为 UTF-8 无 BOM。Markdown 保留用于硬换行的行尾双空格，YAML 使用空格缩进，生成文件由生成器维护。

### 自动修复

ZS0005（未使用引用）与 ZS2003（语句组空行）已提供随 NuGet 分发的代码修复器。在 VS2026 的警告位置按 `Ctrl+.`，可以预览单处修复，或修复文档、项目、解决方案内同一规则的所有位置。详见 [修复用法](analysis/README.zh-Hans.md#代码修复) 和 [完整实现规划](analysis/CODE_FIXES.zh-Hans.md)。本地化资源迁移等其他自定义修复仍属于后续规划。

### 检查命令

以下命令以 framework 仓库根目录为工作目录，检查 Core 项目；其他项目替换对应路径即可。首次执行前先完成项目依赖还原，后续可使用 `--no-restore`。

```powershell
dotnet restore ./Zongsoft.Core/src/Zongsoft.Core.csproj
dotnet build ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore -p:GeneratePackageOnBuild=false
```

普通构建报告明确的风格警告；`CS4014`（部分未等待任务）和 `CA2012`（部分 ValueTask 误用）保持为错误。`var`、表达式体、只读成员和现代语法等建议不因本配套而统一升级为错误。配置以追加方式保留原有 `WarningsAsErrors`；如果项目自身启用了更严格的策略，仍以该项目配置为准。

严格检查将指定风格诊断升级为错误，适用于已经整理完成的项目或 CI：

```powershell
dotnet build ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore -p:GeneratePackageOnBuild=false -p:ZongsoftCodeStyleStrict=true
```

严格模式的具体诊断见 [规则索引](RULES.zh-Hans.md#index)。它检查整个项目的编译输入及实际构建的项目引用，不仅检查 Git 差异；历史项目可能有大量存量诊断，普通构建成功不能写成严格检查通过。

`IDE0049`（使用 `int` 等类型关键字）不参与普通 `dotnet build`，即使启用构建风格分析也如此。补充以下只读检查，不能仅依赖严格构建覆盖这项规则：

```powershell
dotnet format style ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore --verify-no-changes --diagnostics IDE0049
```

需要查看标准格式器对本次修改文件的排版建议时，可使用：

```powershell
dotnet format whitespace ./Zongsoft.Core/src/Zongsoft.Core.csproj --no-restore --verify-no-changes --include ./Zongsoft.Core/src/Components/Handler.cs
```

**注意：** `dotnet format whitespace` 直接比较格式化结果，不执行诊断抑制器，仍可能对合法的条件编译缩进和紧凑块返回差异；本规范的最终诊断以加载配套分析器的构建为准，不将该格式器退出码直接作为这些例外的 CI 门禁。

`--include` 路径相对于当前工作目录，上例文件替换为实际修改文件，可以给出多个路径。`--verify-no-changes` 不改写源码，发现格式差异会返回非零退出码；CI 必须检查构建和 IDE0049 验证命令的退出码；标准格式器的已知例外差异单独审查。多目标项目的构建覆盖配置的目标框架；格式检查不替代各目标框架的编译与分析。

> ⚠️ **自动修正：** 不要对全仓直接运行无范围限制的 `dotnet format` 或编辑器“整理导入”。标准导入整理器使用字母顺序，无法实现本规范的组内长度排序。需要修正时只对本次修改文件使用 `dotnet format whitespace --include ...`，随后检查差异。不要用批量重命名或语法转换自动修复已有公共 API。

### 规则覆盖与边界

诊断 ID、默认及严格模式级别、触发条件、例外、示例和修复能力统一维护在 [📋 规则列表](RULES.zh-Hans.md#index)。自定义 ZS 诊断的 VS 帮助链接直达对应规则锚点；SDK 诊断保留官方链接。[人工审查清单](RULES.zh-Hans.md#review) 说明自动检查尚未覆盖的要求。

### 工具依据

参考 [规则列表中的官方资料](RULES.zh-Hans.md#references)，包括微软代码样式规则索引、诊断帮助链接、配置及格式化工具说明。
