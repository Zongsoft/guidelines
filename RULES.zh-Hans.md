# Zongsoft .NET/C# 规则列表

[English](RULES.md) | [简体中文](RULES.zh-Hans.md)

本页集中说明 `Zongsoft.CodeAnalysis` 的诊断级别、触发条件、例外和修复方式。[开发规范](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md) 保留编码原则与设计要求；[接入和检查命令](https://github.com/Zongsoft/Guidelines/blob/main/README.zh-Hans.md#code-analysis) 见 README。

[规则索引](#index) · [自定义规则](#custom-rules) · [SDK 规则](#sdk-rules) · [诊断例外](#suppressions) · [配置检查](#configuration) · [人工审查](#review) · [参考资料](#references)

> 💡 **从 VS 查看规则：** 自定义诊断 `ZS0005`、`ZS1304`、`ZS2003` 的帮助链接指向本页的固定锚点。点击诊断提示中的帮助链接或错误列表的规则代码即可查看；也可选中错误列表条目后按 `F1`。SDK 的 IDE/CA 及编译器诊断保留官方帮助链接。打开帮助与 `Ctrl+.` 自动修复是两个独立操作。

本页描述当前主分支，实际行为以项目安装的包及 SDK 版本为准。新文档需合并到 GitHub 的 `main` 分支，新分析器包需发布并在消费项目中升级，在线链接和新版行为才会同时可用。诊断链接默认打开中文页，可在页首切换英文；两版保持相同的规则锚点。

<a id="index"></a>

## 规则索引

以下级别来自包内 [Global AnalyzerConfig](https://github.com/Zongsoft/Guidelines/blob/main/analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) 和 [构建配置](https://github.com/Zongsoft/Guidelines/blob/main/analysis/Zongsoft.CodeAnalysis.targets)。项目自身的配置仍可能覆盖级别。严格模式指 `ZongsoftCodeStyleStrict=true`；“SDK”表示使用宿主提供的修复能力，具体可用操作由诊断和 SDK 版本决定。

| ID | 规则 | 默认 / 严格构建 | 修复 |
| --- | --- | --- | --- |
| [ZS0005](#zs0005) | 移除未使用的引用，允许普通 `using System;` | 警告 / 错误 | ✅ 移除未使用的引用 |
| [ZS1304](#zs1304) | 通过生成属性访问固定资源项 | 警告 / 错误 | 手工迁移资源访问 |
| [ZS2003](#zs2003) | 在指定语句组边界留空行 | 警告 / 错误 | ✅ 插入空行 |
| [IDE0009](#ide0009) | 实例成员访问使用 `this.` | 警告 / 错误 | SDK |
| [IDE0049](#ide0049) | 使用 C# 类型关键字 | 警告 / 不参与构建 | SDK |
| [IDE0055](#ide0055) | 空格、缩进与换行 | 警告 / 错误 | SDK，需检查例外 |
| [IDE0065](#ide0065) | `using` 放在命名空间之外 | 警告 / 错误 | SDK |
| [IDE0161](#ide0161) | 使用文件范围命名空间 | 警告 / 错误 | SDK |
| [IDE1006](#ide1006) | 符号命名 | 警告 / 错误 | SDK，公开契约需审查 |
| [IDE2001](#ide2001) | 嵌入语句另起一行 | 警告 / 错误 | SDK，需检查例外 |
| [CA1303](#ca1303) | 本地化用户可见文本 | 警告 / 错误 | 本包未提供资源迁移修复 |
| [CA2012](#ca2012) | 正确消费 `ValueTask` | 错误 / 错误 | 按异步契约修正 |
| [CS4014](#cs4014) | 观察异步任务的完成 | 错误 / 错误 | 按任务所有权修正 |

[有意关闭的诊断](#disabled-rules)、[SDK 风格建议](#suggestions)、[ZSS 例外](#suppressions) 和 [ZSCFG 配置错误](#configuration) 单独列出，避免与实际源码警告混淆。没有列为自定义修复的规则，不表示可以一键安全修改。

<a id="custom-rules"></a>

## 自定义规则

<a id="zs0005"></a>

### ZS0005 · 移除未使用的引用

- **触发条件：** 编译器 CS8019 判定某条引用未使用，分析器逐条报告。
- **例外：** 仅允许保留解析为全局 System 命名空间的普通 `using System;`；不包括 `System.*`、别名、`using static` 或 `global using`。
- **构建要求：** 启用 `GenerateDocumentationFile=true`；本包不强制覆盖项目关闭 XML 文档的选择。
- **修复：** `Ctrl+.` → “移除未使用的引用”，支持文档、项目、解决方案范围的全部修复，保留注释与条件编译指令。
- **配置：** `dotnet_diagnostic.ZS0005.severity`。原 [IDE0005](#ide0005) 已关闭，避免其合并连续引用导致 System 例外失效。

以下为文件头片段，假定正文没有使用 `System.Text` 的类型：

```csharp
using System;      //可以保留
using System.Text; //ZS0005：移除此行
```

此规则只检查引用是否使用，不负责禁止别名、约束全局引用、依赖分组或长度排序；这些要求见开发规范 3.2 节。

<a id="zs1304"></a>

### ZS1304 · 通过生成属性访问资源

- **触发条件：** 直接调用 `ResourceManager.GetString`、`GetObject` 或 `GetStream`，且第一个参数是可在编译期确定的非空字符串键；包括资源管理器派生类型。
- **例外：** 动态键及生成代码不报告。同名但不属于 ResourceManager 的方法不报告。
- **修复：** 在默认 `.resx` 中维护资源，通过 `ResXFileCodeGenerator` 生成的属性读取；目前没有自动修复，不能只把字符串机械替换成同名属性。
- **配置：** `dotnet_diagnostic.ZS1304.severity`；需要翻译的硬编码文本由 [CA1303](#ca1303) 辅助检查。

以下为方法体片段，假定 `manager` 为 ResourceManager，默认资源中已有 `InvalidValue`：

```csharp
//不符合：固定键绕过强类型属性
var message = manager.GetString("InvalidValue");
```

```csharp
//符合：属性由自定义工具生成
var message = Properties.Resources.InvalidValue;
```

以上为替换前后的独立片段。参数名、协议字段、路径及机器文本不属于翻译内容；自定义查询包装器、资源同步与文本用途仍需人工审查。

<a id="resource-generation"></a>

#### 资源生成配置

默认资源生成并提交 `*.Designer.cs`；区域性资源仅保存翻译。以下项目片段适用于 SDK 风格 C# 项目：

```xml
<ItemGroup>
	<EmbeddedResource Update="Properties/Resources.resx">
		<Generator>ResXFileCodeGenerator</Generator>
		<LastGenOutput>Resources.Designer.cs</LastGenOutput>
	</EmbeddedResource>
	<Compile Update="Properties/Resources.Designer.cs">
		<DesignTime>true</DesignTime>
		<AutoGen>true</AutoGen>
		<DependentUpon>Resources.resx</DependentUpon>
	</Compile>
</ItemGroup>
```

Visual Studio 自定义工具不由普通 `dotnet build` 执行；修改资源后重新生成并提交 Designer 文件，CI 编译已提交的文件。分析器自身以 `LocalizableResourceString` 配合 `nameof(生成属性)` 延迟选择语言，英文默认资源和 `zh-Hans` 卫星程序集随包分发。

<a id="zs2003"></a>

### ZS2003 · 语句组之间留空行

**仅在以下两类边界要求空行：**

1. 连续超过两条赋值或初始化语句，后面紧接 `if`、`switch`、`for`、`foreach`、`while` 或 `do`。
2. 同一层级相邻的独立控制结构或显式语句块。控制结构包括上述条件和循环，以及 `try`、`using`、`lock`、`checked`／`unchecked` 和 `unsafe` 块。

**计数与例外：** 带初始化的局部变量、常量声明，普通、复合和解构赋值均按语句计数；一条多变量声明或解构赋值只计一次。空行或其他种类的语句结束当前计数；注释行不代替空行。只有一条或两条赋值，或后面接调用、返回等语句，不因此要求空行。`if/else`、`try/catch/finally`、`do/while` 的关联子句和嵌套语句体不拆分。覆盖语句块、switch 分支和顶层语句；生成代码不报告。

以下为方法体片段，假定 `items` 为整数数组，已有 `Process(int)` 方法。三条初始化后接条件判断，应分隔：

```csharp
var index = 0;
var count = items.Length;
var enabled = count > 0;

if(enabled)
	Process(items[index]);
```

以下片段完全合规。假定 `GetCreator(type)` 返回创建委托，`map` 为可选映射委托，`state` 为映射参数：

```csharp
var entity = GetCreator(type)();
map?.Invoke(entity, state);
return entity;
```

相邻独立控制结构也应分隔，以下片段沿用上述 `items`、`enabled` 和 `Process(int)`：

```csharp
for(var index = 0; index < items.Length; index++)
	items[index]++;

if(enabled)
	Array.Reverse(items);

foreach(var item in items)
	Process(item);
```

**修复：** `Ctrl+.` → “插入空行”，支持文档、项目、解决方案范围的全部修复。只修改诊断边界，保留缩进、CRLF/LF、注释及条件编译，不格式化其他代码。配置项为 `dotnet_diagnostic.ZS2003.severity`。

<a id="sdk-rules"></a>

## SDK 与编译器规则

这里说明 Zongsoft 选项和边界；语言语义及 SDK 实现细节以对应官方文档为准。

<a id="ide0009"></a>

### IDE0009 · 实例成员限定

实例属性、方法和事件使用 `this.`，对应 `dotnet_style_qualification_for_property/method/event = true`。私有字段直接使用 `_camelCase`。字段选项无法区分可见性，公共字段的 `this.` 要求仍需审查。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0003-ide0009)

<a id="ide0049"></a>

### IDE0049 · C# 类型关键字

声明及静态成员访问使用 `int`、`string` 等关键字，配置 `dotnet_style_predefined_type_for_locals_parameters_members` 和 `dotnet_style_predefined_type_for_member_access`。该规则不在构建时运行；需要编辑器或 `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes` 补充检查。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0049)

<a id="ide0055"></a>

### IDE0055 · 格式

Tab 缩进，多行块使用 Allman 花括号；控制关键字与括号间不加空格，二元运算符两侧加空格。文件换行与文件类型例外由根 `.editorconfig` 提供。条件编译指令与紧凑异常处理的允许写法见 [诊断例外](#suppressions)。

`dotnet format whitespace` 直接比较格式化文本，不运行诊断抑制器，仍可能对合法例外返回差异；这些例外以加载分析器的构建诊断为准。自动格式化仅限修改文件并审查差异。[官方格式规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0055)

<a id="ide0065"></a>

### IDE0065 · using 位置

`csharp_using_directive_placement = outside_namespace`，将 using 放在命名空间之前。此规则不负责导入排序、分组或别名限制。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0065)

<a id="ide0161"></a>

### IDE0161 · 文件范围命名空间

`csharp_style_namespace_declarations = file_scoped`，使用 `namespace Example;`。不要为形式统一改变程序集、RootNamespace 或领域边界。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0160-ide0161)

<a id="ide1006"></a>

### IDE1006 · 命名

接口使用 `I` 前缀，类型和成员 PascalCase，参数与普通局部变量 camelCase，私有字段 `_camelCase`，泛型参数使用 `T` 前缀。私有、内部及局部常量使用 UPPER_SNAKE_CASE，公共或受保护常量使用 PascalCase；方法内 `const int SIZE = sizeof(short);` 合规。

命名配置按符号种类、可见性和 `const` 区分，更具体的常量规则优先。除命名规则自身 severity 外，显式设置 `dotnet_diagnostic.IDE1006.severity = warning` 供构建使用。名称的领域含义、类型后缀和外部契约仍需审查；自动重命名公共 API 前检查兼容性。[官方命名规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/naming-rules)

<a id="ide2001"></a>

### IDE2001 · 嵌入语句另起一行

简单分支可省略花括号，语句另起一行。`csharp_style_allow_embedded_statements_on_same_line_experimental = false`；该选项为实验性，SDK 升级后需要复核。紧凑 try/catch/finally 由 [ZSS2001](#zss2001) 豁免。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide2001)

<a id="ca1303"></a>

### CA1303 · 本地化可见文本

报告 Console 提示、传给 `Localizable(true)` 参数或属性的硬编码字符串；包启用 `dotnet_code_quality.CA1303.use_naming_heuristic = true`，增加 Text/Message/Caption 命名启发式。按 [资源生成配置](#resource-generation) 迁移文本，再通过 Designer 属性访问。

启发式不能识别所有文本用途。机器文本、协议键、路径和自定义 UI／日志 API 需要审查；明确无需翻译的参数或属性可使用 `Localizable(false)`。本包尚未提供自动资源迁移修复。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/quality-rules/ca1303)

<a id="ca2012"></a>

### CA2012 · ValueTask 消费

默认仅消费一次 ValueTask，不重复等待、重复调用 AsTask 或混用两者。确需共享时只转换一次并保存 Task。规则只能发现部分误用，仍需审查生命周期；普通及严格构建均作为错误。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/quality-rules/ca2012)

<a id="cs4014"></a>

### CS4014 · 未等待的任务

明确等待、返回或由生命周期所有者管理异步任务。写 `_ = ...` 不能代替异常观察、取消与退出时等待。该编译器诊断只能发现部分未等待调用，普通及严格构建均作为错误。[官方诊断](https://learn.microsoft.com/zh-cn/dotnet/csharp/language-reference/compiler-messages/cs4014)

<a id="disabled-rules"></a>

### 有意关闭的诊断

以下规则在包配置中为 `none`；项目可以有意覆盖，但不应恢复与规范冲突的批量转换。

| ID | 原因 |
| --- | --- |
| <a id="ide0005"></a>IDE0005 | 以 [ZS0005](#zs0005) 替代，保留普通 System 导入 |
| <a id="ide0001"></a>IDE0001 | 保留解决名称冲突的 `global::` 完整限定名 |
| <a id="ide0002"></a>IDE0002 | 不自动简化上述限定访问 |
| <a id="ide0003"></a>IDE0003 | 不统一简化 `this.`；字段无法按可见性分别配置 |
| <a id="ide0130"></a>IDE0130 | 命名空间按 RootNamespace 和领域组织，不机械匹配目录 |
| <a id="ide0210"></a>IDE0210 | 不推动批量转换为顶层语句 |
| <a id="ide0211"></a>IDE0211 | 不推动批量转换为显式 Main |
| <a id="ide0290"></a>IDE0290 | 不推动批量转换为主构造函数 |

<a id="suggestions"></a>

### SDK 风格建议

`var`、表达式体、只读字段、目标类型 `new`、空值与模式匹配等按场景选择的写法保持建议或静默级别，不由本包严格模式统一提升为错误。集合表达式仅建议保持目标类型完全相同的转换；`using(...)` 块为默认偏好，资源确需延续到作用域末尾时才使用 `using var`。

完整选项以 [全局配置](https://github.com/Zongsoft/Guidelines/blob/main/analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) 为准，具体 SDK 诊断见 [微软规则索引](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/)。SDK 升级后重新验证，不能假定每项编辑器建议都在构建中运行。

<a id="suppressions"></a>

## 诊断例外

这些 ZSS 编号是抑制器的标识，不是新的源码警告，也没有独立的 Roslyn 诊断帮助链接；未被豁免的 IDE 警告仍使用官方链接。

<a id="zss0055"></a>

### ZSS0055 · 条件编译指令缩进

仅豁免 `#if`、`#elif`、`#else`、`#endif` 行前空白上的 IDE0055。指令可顶格，也可与所在代码保持同级缩进，不因嵌套条件额外缩进；不会豁免其他表达式格式问题。

<a id="zss0056"></a>
<a id="zss2001"></a>

### ZSS0056 / ZSS2001 · 紧凑异常处理

当 try/catch/finally 的每个子句各占一行、仅含一条简单语句时，分别豁免块边界 IDE0055 与块内 IDE2001。多条语句、嵌套控制流、跨行表达式及语句内部格式错误不属于例外。

以下为属性片段，假定 `_lock` 是对象持有的 ReaderWriterLockSlim，`_list` 是受保护的集合：

```csharp
public int Count
{
	get
	{
		_lock.EnterReadLock();
		try { return _list.Count; }
		finally { _lock.ExitReadLock(); }
	}
}
```

<a id="configuration"></a>

## 🛠️ EditorConfig 同步配置

设置 `ZongsoftGuidelinesSynchronization` 为目标目录后，实际构建自动同步；未设置或为空则禁用。保留 VS 快速最新检查，需要强制执行时重新生成，或清理后再生成。单独清理、还原及设计时构建不触发同步，详见 [EditorConfig 同步](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.zh-Hans.md#editorconfig-同步)。

<a id="zscfg001"></a>
<a id="zscfg002"></a>
<a id="zscfg003"></a>

**ZSCFG001：** 配置的同步目录不存在或不是目录。将属性设置为已有的仓库根目录；相对路径以消费项目目录为基准。文件缺失时自动创建，大小或修改时间不同时替换，写入失败会使构建失败。这是构建配置错误，不是 Roslyn 源码诊断。

<a id="review"></a>

## 仍需人工审查

分析器通过不代表符合全部开发规范。以下要求继续结合设计、契约和测试核对：

- using 别名限制、生产项目 global using 的适用范围、测试 Global.cs、依赖分组与长度排序。
- 中文职责分段、成员顺序、重载相邻、领域命名、外部接口名称与公共 API 兼容性。
- 本地化文本的真实用途、固定键查询包装器、生成器配置及 ResX／Designer 同步。
- 构造参数、比较器、集合枚举时机、资源所有权、取消传播、并发与后台任务生命周期。
- 文档、插件装配、异常契约及测试质量；不通过全仓关闭警告或批量改写公共 API 代替审查。

<a id="references"></a>

## 参考资料

- [微软代码样式规则索引](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/)：本列表按 ID、规则与选项组织的参考。
- [DiagnosticDescriptor 的 helpLinkUri](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnosticdescriptor.-ctor)：自定义诊断帮助链接。
- [Visual Studio 错误列表](https://learn.microsoft.com/en-us/visualstudio/ide/error-list-window)：定位源码与打开在线帮助。
- [分析器配置文件](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/configuration-files)与 [dotnet format](https://learn.microsoft.com/zh-cn/dotnet/core/tools/dotnet-format)：配置优先级、只读检查和修复范围。
- [DiagnosticSuppressor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticsuppressor)：按具体诊断位置实施例外。

> 🔗 **维护约定：** 规则锚点使用小写诊断 ID，如 `#zs2003`，不得因标题改写而改变。新增诊断时同步两版文档、帮助链接、级别、修复状态和测试；保留旧锚点，避免已发布包的链接失效。
