# Zongsoft 开发规范

[English](README.md) | [简体中文](README.zh-Hans.md)

## 📚 开发规范

- [《Zongsoft C# 开发规范》](zongsoft.csharp.guidelines.md)
- [《Zongsoft REST API 规范》](zongsoft.rest-api.guidelines.md)
- [《AI 开发协作规范》](AGENTS.md)

<a id="code-analysis"></a>

## 🛠️ .NET/C# 代码规范检查

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
	<PackageReference Include="Zongsoft.CodeAnalysis" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

使用中央包版本管理时，在 `Directory.Packages.props` 添加版本：

```xml
<PackageVersion Include="Zongsoft.CodeAnalysis" Version="0.1.0" />
```

在 C# 项目或公共 `Directory.Build.props` 中添加不带版本的引用：

```xml
<ItemGroup Condition="'$(MSBuildProjectExtension)' == '.csproj'">
	<PackageReference Include="Zongsoft.CodeAnalysis" PrivateAssets="all" />
</ItemGroup>
```

还原后，NuGet 自动加载分析器和共享规则，无需手工导入 props、复制源码或初始化子模块。`PrivateAssets="all"` 防止规则经业务库传递给下游；每个需要执行规范的项目应明确引用，仓库可以在公共构建文件统一添加。

保留根 EditorConfig 的编辑器设置。初次接入可参考本仓库文件或包中的 `.editorconfig` 模板，已有配置应按需合并，不覆盖项目例外。C# 规则来自包内 Global AnalyzerConfig；本地 EditorConfig 的同名设置优先。因此从旧版迁移时应移除复制的 C# 规则，只保留有意的项目覆盖，避免旧规则阻挡包升级。框架已完成此迁移，两边的根 EditorConfig 仍保持一致。

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

以后只在 guidelines 修改、验证并发布新版本，消费方更新 PackageReference 或中央版本后还原和构建。C# 检测规则随包版本升级；仅编辑器通用设置发生变化时才需单独合并模板。回退同样只需选择此前的包版本。

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

严格模式覆盖 `ZS0005`、`ZS2003`、`ZS1304`、`CA1303`、`IDE0009`、`IDE0055`、`IDE0065`、`IDE0161`、`IDE1006`、`IDE2001`。它检查整个项目的编译输入及实际构建的项目引用，不仅检查 Git 差异；历史项目可能有大量存量诊断，普通构建成功不能写成严格检查通过。

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

| 规范 | 配套行为 | 边界 |
| --- | --- | --- |
| 2.1 大小写、接口 `I`、字段 `_camelCase`、常量、泛型参数 | `IDE1006`，按成员种类、可见性和 `const` 区分 | 不能判断名称的领域含义，不能验证完整命名空间设计；遵守外部接口的名称需按契约审查 |
| 3.1 / 3.4 缩进、换行、空格、Allman | EditorConfig 编辑器设置、包内全局规则与 `IDE0055`；配套抑制器豁免条件编译指令行前空白与简短异常处理块边界 | XML、Markdown 等不由 C# 格式检查器检查；多行块及逻辑分段仍需审查 |
| 3.2 文件范围命名空间、导入位置、未使用引用 | `IDE0161`、`IDE0065`、`ZS0005` | `ZS0005` 使用编译器的未使用引用诊断，构建时仍要求 `GenerateDocumentationFile=true`；普通 `using System;` 不报警；禁用原 `IDE0005`，避免其合并连续引用 |
| 3.2 禁止别名、正式项目慎用 `global using`、测试 `Global.cs`、依赖分组和组内长度排序 | 由代码审查检查 | SDK 内置规则不能完整表达；需要自动强制时另行开发专用 Roslyn 分析器，不能用正则或虚构配置键充数 |
| 3.3 中文 `#region`、职责顺序、重载相邻 | 由代码审查检查 | 标准格式器不能判断职责分段 |
| 3.4 简单分支可省略花括号，语句另起一行 | 花括号作为建议；`IDE2001` 检查同行嵌入语句 | `IDE2001` 使用实验性配置项，SDK 升级时需复核；配套抑制器允许每个子句仅含一条简单语句且各自单行的 try/catch/finally，悬空 `else` 和复杂表达式需审查 |
| 3.4 语句分组与独立控制结构之间的空行 | `ZS2003` | 检查声明组与表达式语句／控制结构的双向边界，以及相邻独立控制结构，覆盖语句块、switch 分支和顶层语句。连续声明（含完整解构）、简短的声明后返回及关联子句不拆分；其他逻辑分组仍需审查 |
| 7.4 文本本地化与强类型资源 | `CA1303`、`ZS1304` | CA1303 检查 Console、Localizable(true) 和 Text/Message/Caption 命名启发式；ZS1304 检查固定键直接读取 ResourceManager。动态键、生成代码不报告；文本用途、自定义查询包装器、生成器设置及 Designer 同步仍需审查 |
| 5.1 实例属性、方法、事件使用 `this.` | `IDE0009` | 内置字段选项不能按可见性区分；公共字段应使用 `this.`、私有字段不使用 `this.`，由审查检查 |
| 3.2 / 5.1 保留冲突消解的完整限定名 | 关闭 `IDE0001`、`IDE0002` 与 `IDE0003` 自动简化诊断 | 同时放弃部分无害简化提示，不代表允许在所有位置滥用完整限定名 |
| 5.1 类型关键字 | 编辑器及 `dotnet format style` 的 `IDE0049`，不参与构建 | `var` 仍是建议，允许显式类型表达精度、接口视图等含义 |
| 5.2 目标类型 `new`、集合表达式、表达式体 | SDK 风格建议，集合转换仅建议目标类型完全相同的情况 | 构造参数、比较器、枚举时机和生命周期由审查核对；不推动批量主构造函数或顶级语句转换 |
| 7.1 部分未等待任务与 ValueTask 误用 | `CS4014`、`CA2012` 为错误 | 不能完整检查 `Async` 后缀、`async void`、取消传播、同步阻塞或后台任务所有权 |
| 4 / 6 / 7 / 8 文档、契约、装配、异常、资源与测试质量 | 编译器和 SDK 提供部分诊断，其余由审查与测试检查 | 不新增全仓文档警告抑制，不改变已有 `NoWarn`；检查通过不能替代行为验证 |

命名规则中的 `severity` 与构建诊断级别不是同一层配置，因此同时显式设置了 `IDE1006` 的警告级别。包内全局配置中，可见性与修饰符更具体的常量规则优先于普通私有字段／局部变量规则，合法的 `DEFAULT_CAPACITY` 不会被要求改为 `_defaultCapacity`，方法内 `const int SIZE` 不会被要求改为 `size`。

### 工具依据

- [分析器配置文件](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)：编辑器与分析器的配置职责。
- [命名规则](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules)：符号匹配、优先级与构建严重级别。
- [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)：检查范围、子命令和退出码。
- [DiagnosticSuppressor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticsuppressor)：按语法位置豁免诊断；直接格式化操作并不等同于运行诊断抑制器。
- [IDE0049](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0049)：类型关键字检查不在构建时运行。
- [CA1303](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1303)：可本地化参数、Console 文本、命名启发式与适用边界。
