# Zongsoft .NET/C# 规则列表

[English](RULES.md) | [简体中文](RULES.zh-Hans.md)

本页集中说明 `Zongsoft.CodeAnalysis` 的诊断级别、触发条件、例外和修复方式。[开发规范](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md) 保留编码原则与设计要求；[接入和检查命令](https://github.com/Zongsoft/Guidelines/blob/main/README.zh-Hans.md#code-analysis) 见 README。

[规则索引](#index) · [自定义规则](#custom-rules) · [SDK 规则](#sdk-rules) · [诊断例外](#suppressions) · [配置检查](#configuration) · [人工审查](#review) · [参考资料](#references)

> 💡 **从 VS 查看规则：** 自定义 `ZS` 诊断的帮助链接指向本页的固定锚点。点击诊断提示中的帮助链接或错误列表的规则代码即可查看；也可选中错误列表条目后按 `F1`。SDK 的 IDE/CA 及编译器诊断保留官方帮助链接。打开帮助与 `Ctrl+.` 自动修复是两个独立操作。

本页描述当前主分支，实际行为以项目安装的包及 SDK 版本为准。新文档需合并到 GitHub 的 `main` 分支，新分析器包需发布并在消费项目中升级，在线链接和新版行为才会同时可用。诊断链接默认打开中文页，可在页首切换英文；两版保持相同的规则锚点。

<a id="index"></a>

## 规则索引

以下级别来自包内 [Global AnalyzerConfig](https://github.com/Zongsoft/Guidelines/blob/main/analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) 和 [构建配置](https://github.com/Zongsoft/Guidelines/blob/main/analysis/Zongsoft.CodeAnalysis.targets)。项目自身的配置仍可能覆盖级别。严格模式指 `ZongsoftCodeStyleStrict=true`；“SDK”表示使用宿主提供的修复能力，具体可用操作由诊断和 SDK 版本决定。

| ID | 规则 | 默认 / 严格构建 | 修复 |
| --- | --- | --- | --- |
| [ZS0005](#zs0005) | 移除未使用的别名、静态及全局引用 | 警告 / 错误 | ✅ 移除未使用的引用 |
| [ZS1301](#zs1301) | 本地化异常消息 | 警告 / 错误 | 手工迁移资源 |
| [ZS1302](#zs1302) | 本地化非 ASCII 文字 | 警告 / 错误 | 手工迁移资源 |
| [ZS1304](#zs1304) | 通过生成属性访问固定资源项 | 警告 / 错误 | 手工迁移资源访问 |
| [ZS2003](#zs2003) | 在指定语句组边界留空行 | 警告 / 错误 | ✅ 插入空行 |
| [ZS2004](#zs2004) | 非测试项目中超过 9 个方法与属性的文件必须分段 | 警告 / 错误 | 手工按职责分段 |
| [ZS2005](#zs2005) | 嵌套控制语句体使用花括号 | 警告 / 错误 | ✅ 添加花括号 |
| [ZS2006](#zs2006) | 局部函数位于末尾并按规定分隔 | 警告 / 错误 | ✅ 整理局部函数 |
| [ZS3001](#zs3001) | 补齐方法参数 XML 文档 | 警告 / 错误 | 手工补充契约 |
| [ZS3002](#zs3002) | 补齐非 void 方法返回值 XML 文档 | 警告 / 错误 | 手工补充契约 |
| [ZS3003](#zs3003) | 单行 XML 内容及标签写在同一行 | 警告 / 错误 | ✅ 合并 XML 文档行 |
| [IDE0009](#ide0009) | 实例成员访问使用 `this.` | 警告 / 错误 | SDK |
| [IDE0049](#ide0049) | 使用 C# 类型关键字 | 警告 / 不参与构建 | SDK |
| [IDE0055](#ide0055) | 空格、缩进与换行 | 警告 / 错误 | SDK，需检查例外 |
| [IDE0065](#ide0065) | `using` 放在命名空间之外 | 警告 / 错误 | SDK |
| [IDE0161](#ide0161) | 使用文件范围命名空间 | 警告 / 错误 | SDK |
| [IDE1006](#ide1006) | 符号命名 | 警告 / 错误 | SDK，公开契约需审查 |
| [IDE2001](#ide2001) | 嵌入语句另起一行 | 警告 / 错误 | SDK，需检查例外 |
| [CA2012](#ca2012) | 正确消费 `ValueTask` | 错误 / 错误 | 按异步契约修正 |
| [CS4014](#cs4014) | 观察异步任务的完成 | 错误 / 错误 | 按任务所有权修正 |

[有意关闭的诊断](#disabled-rules)、[SDK 风格建议](#suggestions)、[ZSS 例外](#suppressions) 和 [ZSCFG 配置错误](#configuration) 单独列出，避免与实际源码警告混淆。没有列为自定义修复的规则，不表示可以一键安全修改。

<a id="custom-rules"></a>

## 自定义规则

<a id="zs0005"></a>

### ZS0005 · 移除未使用的引用

- **触发条件：** 编译器 CS8019 判定某条引用未使用，分析器逐条报告。
- **例外：** 允许保留任意命名空间的普通引用；别名、`using static` 和 `global using` 不属于例外。
- **构建要求：** 启用 `GenerateDocumentationFile=true`；本包不强制覆盖项目关闭 XML 文档的选择。
- **修复：** `Ctrl+.` → “移除未使用的引用”，支持文档、项目、解决方案范围的全部修复，保留注释与条件编译指令。
- **配置：** `dotnet_diagnostic.ZS0005.severity`。[IDE0005](#ide0005) 默认关闭，避免其合并连续引用导致普通命名空间引用的例外失效。

此规则只检查引用是否使用，不负责禁止别名、约束全局引用、依赖分组或长度排序；这些要求见开发规范 3.2 节。

<a id="zs1301"></a>

### ZS1301 · 本地化异常消息

- **触发条件：** 构造 `System.Exception` 或派生类时，实参对应声明为 `string message` 的形参（名称完整匹配，忽略大小写），且实参中直接写有非空白的固定文本。位置参数、命名参数、普通 `new`、`throw new` 和目标类型推断的 `new(...)` 一致。
- **检查范围：** 字符串字面量、括号／内置转换中的文本、内置字符串拼接的固定片段、插值字符串的固定文本和直接字符串插值项；`System.String.Format` 只检查模板，不把格式化数据参数当模板。每个固定片段报告一次。
- **刻意不追踪：** 变量、const 标识符、字段、属性、任意方法返回值、异常工厂、构造链、自定义运算符／转换和条件表达式。不猜测 `reason`、`text`、`errorMessage`、`paramName`；构造器的可选默认值不在调用处重复检查。
- **例外：** null、空串、纯空白不报告；数字、标点、Emoji、ANSI、图案作为直接异常消息仍报告。`LocalizableAttribute` 不改变结果。
- **修复：** 使用资源属性或资源格式模板。资源键以 `.` 分隔，异常消息资源键以 `.Message` 结尾；相同含义优先复用资源项。本包不提供自动资源迁移。
- **配置：** `dotnet_diagnostic.ZS1301.severity`；默认警告，严格模式错误。

```csharp
throw new Exception("Failed.");                          //ZS1301
throw new Exception(message: $"Invalid value: {value}"); //ZS1301
throw new Exception(Properties.Resources.Failure_Message); //资源访问不告警
throw new Exception(message);                            //不追踪变量
```

示例中的资源属性和变量由调用方提供。不告警不等于已经本地化；这是确定性的现场检查，不做文本来源证明。

<a id="zs1302"></a>

### ZS1302 · 本地化非 ASCII 文字

- **触发条件：** 手写 C# 字符串包含非 ASCII 文字，包括中日韩泰、俄文、希腊文、阿拉伯文、重音拉丁文字及附着于文字的组合附加符号。
- **所有位置：** 普通、逐字、原始、UTF-8 字符串；插值固定文本及格式文本；变量、字段、属性初始化、特性和默认参数；路径、JSON 字段名、正则及技术键不设白名单。按解码字符判断，`\u`／`\U` 与直接字符一致，不追踪跨表达式拼接后的结果。
- **字符边界：** 使用固定 Unicode 17.0 的 Letter／Mark 分类和 Emoji 属性。ASCII 字母、Emoji、图标、标点、普通数学符号、控制字符、私用区图标及独立附加符号本身不触发；`é` 与 `e\u0301` 均触发。变体选择符不算文字；Emoji 与中文混合仍触发。
- **不扫描：** 注释、普通标识符、字符字面量、生成代码和 `.resx`。不推断运行时值，也不凭字符猜测具体语言。`LocalizableAttribute` 不提供豁免。
- **去重：** 同一片段属于正在检查的异常消息时优先报告 ZS1301；关闭 ZS1301 后仍可报告 ZS1302。
- **修复：** 将文本放入资源。特性和默认参数等常量位置需要调整设计，不能机械替换成属性调用；本包不提供自动修复。
- **配置：** `dotnet_diagnostic.ZS1302.severity`；默认警告，严格模式错误。

```csharp
var title = "你好";       //ZS1302
var title = "café";      //ZS1302
var icon = "ℹ️ 👩🏽‍💻";  //允许
var title = "ℹ️ 提示";  //ZS1302
```

**共同边界：** ZS1301、ZS1302、ZS1304 在 `IsTestProject=true` 时均不注册检查，IDE 和构建一致；缺省或 false 正常检查。不按项目名、目录或测试框架推断。生成代码不检查。测试项目豁免不传播到被测生产项目。

Unicode 数据和许可：[分类](https://www.unicode.org/Public/17.0.0/ucd/extracted/DerivedGeneralCategory.txt)、[Emoji](https://www.unicode.org/Public/17.0.0/ucd/emoji/emoji-data.txt)、[再生成步骤](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.zh-Hans.md#unicode)。表格在维护时生成，构建和分析时不联网。

<a id="zs1304"></a>

### ZS1304 · 通过生成属性访问资源

- **触发条件：** 直接调用 `ResourceManager.GetString`、`GetObject` 或 `GetStream`，且第一个参数是可在编译期确定的非空字符串键；包括资源管理器派生类型。
- **例外：** 测试项目、动态键及生成代码不报告。同名但不属于 ResourceManager 的方法不报告。
- **修复：** 在默认 `.resx` 中维护资源，通过 `ResXFileCodeGenerator` 生成的属性读取；目前没有自动修复，不能只把字符串机械替换成同名属性。
- **配置：** `dotnet_diagnostic.ZS1304.severity`；需要翻译的硬编码文本由 [ZS1301](#zs1301) 和 [ZS1302](#zs1302) 检查。

以下为方法体片段，假定 `manager` 为 ResourceManager，默认资源中已有 `InvalidValue`：

```csharp
//不符合：固定键绕过强类型属性
var message = manager.GetString("InvalidValue");
```

```csharp
//符合：属性由自定义工具生成
var message = Properties.Resources.InvalidValue;
```

以上为替换前后的独立片段。ZS1304 检查固定资源访问；字符串内容由 ZS1301 和 ZS1302 分别检查。自定义查询封装和资源同步需人工审查。

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

**在以下四类边界要求至少一个空行，下面列出的例外允许省略：**

1. 连续普通语句后紧接 `if`、`switch`、`for`、`foreach`、`while` 或 `do`：带块或复杂结构前至少两条、简单无花括号结构前至少三条即须分隔。低于相应门槛时，可紧接，也可保留空行。
2. 同一层级相邻的独立控制结构或显式语句块。控制结构包括上述条件和循环，以及 `try`、`using`、`lock`、`checked`／`unchecked`、`unsafe` 和 `fixed`。
3. `try`、`using(...)`、`lock`、`fixed`、`checked`／`unchecked`、`unsafe` 或显式语句块开始前，存在上一条同层级独立语句。即使前面只有一条声明、赋值或调用，也须分隔。资源组的前置边界见下表。
4. 含有执行语句块的完整复合语句或显式语句块结束后，紧接同一层级的任意独立语句。后续的调用、断言、事件通知、赋值、局部声明、资源释放、`return`、`throw`、`yield` 和跳转语句均不因用途而豁免。

这里的“同一层级”指同一语句块、同一 switch 分支或顶层语句列表的直接子语句。“含有执行语句块”包括控制结构本身的块，以及沿其嵌套语句体包含的块；例如外层 `if` 无花括号、内层 `while` 有块，完整 `if` 后仍需空行。`await using`、`await foreach` 和附有标签的语句块同样适用。

**条件与循环的前置门槛：** 以下只判定第 1 项；第 2、3、4 项及各自例外独立适用，不因计数放宽而取消。

| 后续条件或循环 | 连续 1 条普通语句 | 连续 2 条普通语句 | 连续 3 条及以上普通语句 |
| --- | --- | --- | --- |
| 带块或复杂结构 | 可紧接 | 必须空行 | 必须空行 |
| 简单无花括号结构 | 可紧接 | 可紧接 | 必须空行 |

**简单与复杂的定义：** 简单无花括号结构仅限 `if`、`for`、`foreach`、`while`，语句体为下表定义的单条简单语句，`if` 无 `else`。其他条件和循环均按复杂结构判定，包括 `switch`、`do`、带 `else`／`else if` 的分支、空块、单语句块和嵌套控制语句体；完整结构只有一行也不改变门槛。带标签时按标签所附的完整语句判定。断言、日志、取消检查或资源获取等调用按普通语句计数，不根据调用名称、用途或与后续结构的数据依赖决定门槛。

**完整结构边界：** 不拆开 `if/else if/else`、`try/catch/finally` 和 `do/while` 的关联子句，也不拆开控制语句与嵌套语句体。第 1、3 项的空行位于完整结构开始前；没有上一条同层级语句时，不要求额外前置空行。第 4 项的空行位于最终分支或子句之后；`do` 位于最终 `while(...);` 之后。即使最后一个 `else` 无花括号，只要任一分支含有执行语句块，也按完整结构检查后置分隔。相邻的外层结束花括号、不同 switch 分支的标签不是本规则要求分隔的独立语句。

**允许省略的布局：** 以下例外不删除已有空行，也不豁免结构内部的其他语句边界。

| 布局 | 精确条件 |
| --- | --- |
| 连续同类型的简单控制语句 | 仅限 `if`、`for`、`foreach`、`while`；各语句体均无花括号，且为单条表达式语句（如调用、赋值或自增／自减）、`return`、`throw`、空语句、`break`、`continue` 或 `yield`；`if` 不含 `else`。不同类型、带块或嵌套控制结构仍须分隔。 |
| 连续紧凑异常处理语句 | 每个完整 `try/catch/finally` 各占一个不同的物理行，所有关联子句均在该行；各子句的直接语句不超过两条，且完整结构内部无预处理指令。只有各子句单行而整个结构跨行的写法不属于此例外。组前存在独立语句或组后接普通语句时，仍须空行。 |
| 资源声明与嵌套 `using` 链 | 同一语句列表中，无分隔空行的连续 `using var`、`using Type` 或对应 `await using` 资源声明，可与其后紧接的一个 `using(...)`／`await using(...)` 完整结构组成资源组。组内声明及直接嵌套的 `using` 链不要求空行；第 3 项的前置分隔在首条资源声明前检查。普通语句或已有分隔空行打断该组。只有资源声明时，不因第 3 项要求组前空行；普通变量声明不属于资源组，两个已经结束的独立 `using` 块也不能合并为组。 |
| switch 分支末尾的 `break` | `break` 必须是该 switch 分支列表的最后一条直接语句，用于退出该 switch。嵌套块或循环中的 `break`、分支内非末尾的 `break`，以及 `return`、`throw`、`continue`、`goto case/default` 均不属于此例外。 |

**计数与空行：** 普通语句按语法包括局部变量或常量声明（有无初始化均计入，包含资源声明）和表达式语句（调用、`await` 调用、普通／复合／解构赋值、自增、自减等）；混合出现也累计。一条多变量声明、解构赋值或跨多行的声明／表达式语句只计一次。已有空行或其他种类的语句结束连续计数；资源组的组首分隔仍按第 3 项判断。普通语句之间，以及普通语句后接返回等非条件、非循环语句，不因第 1 项逐条要求空行；职责和阶段分组仍须人工审查。空行须仅含空白字符，并位于注释之外；注释行、预处理指令行、块注释内部或未激活条件编译文本中的空白行不能代替分隔空行。低于计数门槛时不强制删除已有空行。

**检查范围：** 生产代码和测试代码使用同一规则，生成代码不报告；AI 编写且由人工维护的源码不因来源而豁免。方法、类型、属性和访问器声明之间的排版由其他规范约束；局部函数声明本身不是第 3 项的复杂控制结构，其结束块也不触发第 4 项。对象／集合初始化器、属性模式、switch 表达式、lambda 和匿名方法的花括号属于表达式，不据此要求外围语句之间插入空行；这些函数体内部仍检查语句分隔。

以下均为独立的方法体片段，假定 `items` 为整数数组，`enabled`、`disabled` 为布尔值，已有 `Process(int)`、`Complete()`、`NotifyChanged()` 方法，并已导入 `System` 命名空间；`Assert.True(bool)` 来自项目使用的测试框架。标有 `//ZS2003` 的语句应报告，其余片段符合本规则。

单条普通语句后接带块条件，紧凑写法合规。

```csharp
var count = items.Length;
if(count > 0)
{
	Process(count);
}
```

同一处主动保留空行也合规，修复器不删除它。

```csharp
var count = items.Length;

if(count > 0)
{
	Process(count);
}
```

两条声明后接带块条件，缺少空行时报告。

```csharp
var count = items.Length;
var index = 0;
if(count > 0) //ZS2003
{
	Process(items[index]);
}
```

对应的合规分隔如下。

```csharp
var count = items.Length;
var index = 0;

if(count > 0)
{
	Process(items[index]);
}
```

声明与调用混合累计，不因语句种类改变而重新计数。

```csharp
var count = items.Length;
Complete();
if(count > 0) //ZS2003
{
	Process(count);
}
```

两条普通语句后接简单无花括号条件，可以紧接。

```csharp
var count = items.Length;
var index = 0;
if(count > 0)
	Process(items[index]);
```

三条普通语句后接简单无花括号条件，必须分隔；调用也计入。

```csharp
var count = items.Length;
var index = 0;
Complete();
if(count > 0) //ZS2003
	Process(items[index]);
```

跨多行的初始化仍只是一条普通语句，可以紧接带块条件。

```csharp
var values = new int[]
{
	1, 2, 3,
};
if(enabled)
{
	Process(values[0]);
}
```

一条初始化后接 `try`：即使只有一条普通语句，仍须前置分隔。

```csharp
var x = 100;
try //ZS2003
{
	Process(x);
}
catch { }
```

单条断言后接带块循环：未达到两条普通语句的门槛，可以紧接；测试与生产代码使用同一计数规则。

```csharp
Assert.True(items.Length > 0);
foreach(var item in items)
{
	Process(item);
}
```

无花括号多分支仍是复杂结构，但单条普通语句后可以紧接；`else` 与 `if` 之间不拆开。

```csharp
Complete();
if(enabled)
	Process(items[0]);
else
	NotifyChanged();
```

下面只有一条初始化，后接没有 `else` 的简单无花括号 `if`，不要求前置空行。

```csharp
var count = items.Length;
if(count == 0)
	return;
```

资源组：准备调用与第一条资源声明分隔，组内资源声明及嵌套 `using` 紧接。

```csharp
Complete();

using var buffer = new global::System.IO.MemoryStream();
using(var stream = new global::System.IO.MemoryStream())
using(var writer = new global::System.IO.StreamWriter(stream))
{
	writer.Write(items.Length);
}
```

资源组前缺少空行时，在首条资源声明处报告，修复不拆开该组。

```csharp
Complete();
using var buffer = new global::System.IO.MemoryStream(); //ZS2003
using(var stream = new global::System.IO.MemoryStream())
{
	GC.KeepAlive(stream);
}
```

普通调用打断资源组，后续 `using(...)` 前仍须分隔。

```csharp
using var buffer = new global::System.IO.MemoryStream();
GC.KeepAlive(buffer);
using(var stream = new global::System.IO.MemoryStream()) //ZS2003
{
	GC.KeepAlive(stream);
}
```

两个已经结束的独立 `using` 块，不能套用嵌套资源链的例外。

```csharp
using(var first = new global::System.IO.MemoryStream())
{
	GC.KeepAlive(first);
}
using(var second = new global::System.IO.MemoryStream()) //ZS2003
{
	GC.KeepAlive(second);
}
```

块后接返回或断言：返回不豁免；测试中的断言应与前一个循环分隔。

```csharp
foreach(var item in items)
{
	Process(item);
}
return; //ZS2003
```

以下断言与循环之间已有空行，循环前后均符合规则。

```csharp
Assert.True(items.Length > 0);

foreach(var item in items)
{
	Process(item);
}

Assert.True(items.Length > 0);
```

完整分支后接通知：else 紧接关联的 if，通知前留空行；最后一个分支无花括号也不改变此边界。

```csharp
if(enabled)
{
	Process(items[0]);
}
else
	Complete();

NotifyChanged();
```

do/while：关联的 while 前不插入分隔空行，后续语句在分号之后分隔。

```csharp
do
{
	Complete();
}
while(enabled);

NotifyChanged();
```

紧凑异常处理组：两个完整单行 try 可紧接，组后的调用必须分隔。

```csharp
Complete();

try { Process(items[0]); } catch { }
try { Complete(); } catch { }

NotifyChanged();
```

下面虽然每个子句都是单行，但第一个完整 try 跨行，第二个 try 前仍须空行。

```csharp
try { Process(items[0]); }
catch { }
try { Complete(); } catch { } //ZS2003
```

switch 分支末尾：紧接块的分支末尾 break 可以不留空行，switch 之后的调用仍须分隔。

```csharp
switch(items.Length)
{
	case 0:
		if(enabled)
		{
			Complete();
		}
		break;
}

NotifyChanged();
```

循环中的 break 没有上述例外。

```csharp
while(enabled)
{
	if(items.Length == 0)
	{
		Complete();
	}
	break; //ZS2003
}
```

switch 分支中的 return 也没有上述例外。

```csharp
switch(items.Length)
{
	case 0:
		if(enabled)
		{
			Complete();
		}
		return; //ZS2003
}
```

下条语句的注释应与前一个结构一起隔开。

```csharp
if(enabled)
{
	Complete();
}

//Notify observers.
NotifyChanged();
```

初始化器的结束花括号不会触发外围语句分隔。

```csharp
var values = new int[]
{
	1, 2, 3,
};
GC.KeepAlive(values);
```

lambda 的结束花括号也不会触发外围语句分隔。

```csharp
Action action = () =>
{
	Complete();
};
action();
```

普通语句组：三条初始化后接简单无花括号条件判断时必须分隔。此片段不使用前述 enabled 变量，而是自行声明。

```csharp
var index = 0;
var count = items.Length;
var enabled = count > 0;

if(enabled)
	Process(items[index]);
```

下面仅有一条初始化，后接调用和返回，符合本规则。此片段所在方法返回对象，GetCreator(type) 返回创建委托，map 为可选映射委托，state 为映射参数。

```csharp
var entity = GetCreator(type)();
map?.Invoke(entity, state);
return entity;
```

简单无花括号控制组：同类型组内可不留空行，不同类型组间必须留空行。

```csharp
if(enabled)
	;
if(disabled)
	;

for(var i = 0; i < items.Length; i++)
	;
for(var j = 0; j < items.Length; j++)
	;

foreach(var item in items)
	;
foreach(var entry in items)
	;

while(enabled)
	;
while(disabled)
	;
```

**修复：** `Ctrl+.` → “插入空行”，支持文档、项目、解决方案范围的全部修复。在后一个语句或资源组首条资源声明的首个标记处报告；修复重新核对边界及例外，只插入缺少的分隔，保留缩进、CRLF/LF、注释及条件编译，不格式化其他代码。配置项为 `dotnet_diagnostic.ZS2003.severity`，默认警告、严格模式错误。

<a id="zs2004"></a>

### ZS2004 · 方法与属性分段

- **测试项目例外：** `IsTestProject=true` 时不注册检查，IDE 和构建一致，严格模式也不报告；属性缺省、为 false 或不是有效布尔值时正常检查。不按项目名称、目录或测试框架推断，例外不传播到被测试的生产项目。
- **触发条件：** 当前代码文件内的方法与属性合计超过 9 个时，必须将这些成员放入成对的 `#region`／`#endregion` 中；在第一个未分段成员名称处报告，每个文件一次。
- **计数：** 包含当前文件中所有类型和嵌套类型的方法、属性及索引器，每个声明计一次；接口、抽象及显式实现成员同样计入。不计构造函数、运算符、访问器、局部函数、字段和事件；不跨文件合并 `partial` 成员，不统计未启用的条件编译代码。
- **边界：** 9 个或更少不强制分段；空分段、未配对分段或方法体内部的分段不能覆盖成员。生成代码不报告。中文分段名称、职责划分、成员顺序及空分段本身继续人工审查。
- **配置与修复：** `dotnet_diagnostic.ZS2004.severity`；默认警告，严格模式错误。手工按职责组织，工具不猜测分段名称。

<a id="zs2005"></a>

### ZS2005 · 嵌套控制语句使用花括号

- **触发条件：** `if` 的 then 分支、`else` 分支、`for`、`foreach`、`while`、`do`、`using`、`lock` 或 `fixed` 的直接语句体没有花括号，且语句体本身又是控制结构时，要求给外层语句体加块。内层已有自己的块也仍须报告。`foreach` 包括解构和 `await foreach`，`using` 包括 `await using`。
- **内层结构：** 条件、循环、`using`、`lock`、`fixed`、`switch`、`try`、`checked`／`unchecked` 和 `unsafe` 语句均计作控制结构；表达式中的同名关键字不计。外层语句体已经是显式块时符合要求。
- **逐层检查：** 只看直接语句体，不按缩进或物理行数计数。两个直接嵌套的控制结构已经触发；方法体和最终普通语句不计层数。更深的嵌套逐条直接关系检查。
- **报告位置：** 在需要加块的外层头部首个标记处报告，`else` 分支报告在 `else`，`await using`／`await foreach` 报告在 `await`。
- **关联分支：** 标准 `else if` 链不要求在 `else` 与其 `if` 之间加块；`if` 的 then 分支又是 `if`，以及 `else` 分支直接包含循环等其他结构，仍须加块。
- **纯链例外：** 直接连续的 `using`／`await using` 链、纯 `fixed` 链、纯 `lock` 链可以共享最终语句体。最终只有一条简单普通语句时可无块，多条语句使用共同块。混合类型不属于例外；最终又是其他控制结构时，最后一个链头必须加块。普通语句的表达式复杂度继续按开发规范审查。
- **范围与配置：** 生产代码和测试代码一致，生成代码不报告。配置项为 `dotnet_diagnostic.ZS2005.severity`，默认警告、严格模式错误。本规则不要求所有简单单语句控制体一律加块，不负责逻辑空行或嵌入语句另起一行。

以下为独立的方法体片段，假定 `items` 为整数数组，`enabled` 为布尔变量，`Process(int)` 与 `Complete()` 已定义；资源片段使用 BCL 的完整类型名。注释 `//ZS2005` 标记应加块的外层头；片段只展示本规则，控制结构前后的空行仍遵循 [ZS2003](#zs2003)。

不符合：两个控制结构已触发，最终调用不计第三层。

```csharp
foreach(var item in items) //ZS2005
	if(item > 0)
		Process(item);
```

修复后：外层使用块，内层简单单语句仍可无块。

```csharp
foreach(var item in items)
{
	if(item > 0)
		Process(item);
}
```

内层已有块仍不豁免外层；此片段中的 `if` 应加块。

```csharp
if(enabled) //ZS2005
	foreach(var item in items)
	{
		Process(item);
	}
```

更深的嵌套分别检查：`for` 与它的直接 `if` 都需要块。

```csharp
for(var index = 0; index < items.Length; index++) //ZS2005
	if(enabled) //ZS2005
		while(items[index] > 0)
			items[index]--;
```

纯资源链允许最终单条调用无块。

```csharp
using(var stream = new global::System.IO.MemoryStream())
using(var writer = new global::System.IO.StreamWriter(stream))
	writer.Flush();
```

纯锁链也允许最终单条调用无块，假定两个同步对象已定义。

```csharp
lock(firstSync)
lock(secondSync)
	Complete();
```

纯固定链采用相同例外，以下片段位于启用不安全代码的 `unsafe` 方法中。

```csharp
fixed(int* first = items)
fixed(int* second = items)
	global::System.GC.KeepAlive(items);
```

混合链必须在类型变化的外层语句体加块；这里保留块内纯资源链，假定 `sync` 已定义。

```csharp
lock(sync)
{
	using(var first = new global::System.IO.MemoryStream())
	using(var second = new global::System.IO.MemoryStream())
		global::System.GC.KeepAlive(second);
}
```

反向混合也不能套用纯链例外；下面最后一个 `using` 必须加块。

```csharp
using(var first = new global::System.IO.MemoryStream())
using(var second = new global::System.IO.MemoryStream()) //ZS2005
	lock(sync)
	{
		global::System.GC.KeepAlive(second);
	}
```

标准 `else if` 属于关联分支，符合本规则。

```csharp
if(enabled)
	Complete();
else if(items.Length > 0)
	Process(items[0]);
else
	Complete();
```

**修复：** `Ctrl+.` → “添加花括号”，支持文档、项目、解决方案范围的全部修复。重新核对嵌套关系和例外后，给对应语句体加块并调整其缩进，保留注释、CRLF/LF 和原有 `else` 归属，不格式化无关代码。目标控制结构存在语法错误，或从外层头部末标记至直接语句体末尾含有预处理指令、未激活源码或损坏语法时，不提供自动修复；结构之前的正常指令不影响修复。跨行尾注释结束后与语句体同处一行、无法安全插块时，也需人工处理。逻辑分组与 `return` 前空行的人工判定见 [人工审查](#review)。

<a id="zs2006"></a>

### ZS2006 · 局部函数位于末尾并按规定分隔

- **位置：** 局部函数必须集中在所在语句块、switch 分支或顶级语句列表的末尾，后面不得再有同列表的普通语句。保留原作用域，不提升到外层方法。所有允许局部函数的宿主均适用，包括构造函数、访问器、运算符、lambda／匿名方法及其他局部函数。
- **主体分隔：** 主体代码与第一个局部函数之间必须恰好一个真实空行。函数的说明注释与函数组成整体，空行位于说明注释之前；注释与声明之间不留分隔空行。没有前置主体代码时，不额外要求开花括号后的空行。
- **单行例外：** 所在列表的全部主体代码仅占一个物理行、组首局部函数使用表达式体（`=>`、无 `{}` 函数体），且前置区域没有独立注释行或指令时，允许零个或一个空行。主体语句的行尾注释不影响例外；一条跨行语句不属于单行主体。
- **相邻函数：** 连续两个表达式体局部函数之间必须零个空行；表达式体后接块体函数允许零个或一个空行；块体函数之后必须恰好一个空行。无表达式体的 `extern` 局部函数按非表达式体处理。函数间的说明注释仍与后一个函数相邻。
- **空行判定：** 只计仅含空白字符、位于注释和未激活源码之外的真实空行；注释行不代替空行，不删除块注释内部的空白行。局部函数前的分隔统一由本规则检查，避免与 [ZS2003](#zs2003) 重复报告；函数体内部的普通语句仍遵循 ZS2003。
- **报告与配置：** 在违规局部函数的首个标记处报告；位置违规时不再重复报告同一函数的空行问题。生产代码与测试代码一致，生成代码不报告。配置为 `dotnet_diagnostic.ZS2006.severity`，默认警告、严格模式错误。
- **修复：** `Ctrl+.` → “整理局部函数”，支持文档、项目、解决方案范围的全部修复。在原列表内保持普通语句及函数各自的顺序，说明和行尾注释按原归属保留，正文按原文本片段搬运，保留 CRLF/LF，不格式化其他代码。
- **移动边界：** 移位后核对项目编译。项目原本无法编译、目标列表有语法错误或指令／未激活源码、需要跨越 `using`／`await using` 声明、注释归属不清，或存在隐式 `CallerLineNumber` 实参时，不提供自动移动。空行修复也不跨越指令或未激活源码。

以下均为相互独立的方法声明，放在已导入 `System` 的类型中；`//ZS2006` 标记违规声明，其余片段合规。

主体代码在前，局部函数集中在末尾：

```csharp
static void Foo()
{
	Console.Write("before");
	Console.Write(GetMessage());

	static string GetMessage() => "This is a message.";
}
```

不符合：局部函数插在主体代码中间。

```csharp
static void Foo()
{
	Console.Write("before");

	static string GetMessage() => "This is a message."; //ZS2006

	Console.Write(GetMessage());
}
```

单行主体可省略分隔空行；连续表达式体函数必须紧凑：

```csharp
static void Foo()
{
	Console.Write(GetMessage());
	static string GetMessage() => Format("message");
	static string Format(string value) => value;
}
```

不符合：两个表达式体函数之间存在空行。

```csharp
static void Foo()
{
	Console.Write(GetMessage());

	static string GetMessage() => Format("message");

	static string Format(string value) => value; //ZS2006
}
```

独立说明注释取消紧凑例外，分隔放在注释之前：

```csharp
static void Foo()
{
	Console.Write(GetMessage());

	// Function documentation.
	static string GetMessage() => "This is a message.";
}
```

块体函数之后必须一个空行：

```csharp
static void Foo()
{
	Console.Write(GetMessage());

	static string GetMessage()
	{
		return Format("message");
	}

	static string Format(string value) => value;
}
```

内部块末尾的局部函数保留作用域，外层方法仍可继续执行：

```csharp
static void Foo(bool enabled)
{
	if(enabled)
	{
		var message = "This is a message.";
		Console.Write(GetMessage());

		string GetMessage() => message;
	}

	Console.Write("after");
}
```


<a id="zs3001"></a>

### ZS3001 · 方法参数 XML 文档

已有 XML 注释的方法必须为每个参数提供顶层 `<param name="参数名">...</param>`；名称区分大小写，以源码标识符的实际名称匹配，`@` 转义不属于名称。每个缺失参数在参数名处报告一次；只写 `<summary>`、文档中引用 `<paramref>` 或写错名称均不算完整。

适用于任意可见性的方法，包括接口、抽象及显式实现；不检查构造函数、局部函数、委托和运算符。无 XML 注释的方法不由本规则要求新增注释。`<inheritdoc />` 和 `<include />` 不替代显式 `<param>`；自闭合 `<param name="..." />` 计作已有元素，其内容质量仍需人工审查。

配置为 `dotnet_diagnostic.ZS3001.severity`，默认警告、严格模式错误。手工填写参数契约。

<a id="zs3002"></a>

### ZS3002 · 方法返回值 XML 文档

已有 XML 注释且返回类型不是 `void` 的方法必须有顶层 `<returns>`，缺失时在返回类型处报告。`Task`、`ValueTask`、泛型和 `ref` 返回值均需说明；`void`／`async void` 不要求此元素。方法范围及继承文档边界与 [ZS3001](#zs3001) 相同，自闭合 `<returns />` 计作已有元素。

配置为 `dotnet_diagnostic.ZS3002.severity`，默认警告、严格模式错误。手工填写返回值契约，不自动生成空洞描述。

<a id="zs3003"></a>

### ZS3003 · 单行 XML 元素

XML 成对元素内容只有一行时，开始标签、内容和结束标签必须在同一行；忽略文档前缀和空白行，允许行内 `<see />`、`<paramref />` 等嵌套元素。覆盖所有声明的 `///` 和 `/** */` XML 文档，不限于方法，也不限于 `<summary>`。真正跨行的文本或 XML 结构保留原布局；空内容和语法不完整的 XML 不报告。

以下为方法声明片段的修改前后对照：

```csharp
/// <summary>
/// 一行内容。
/// </summary>
void Foo() { }
```

```csharp
/// <summary>一行内容。</summary>
void Foo() { }
```

在开始标签处报告，配置为 `dotnet_diagnostic.ZS3003.severity`，默认警告、严格模式错误。

**修复：** `Ctrl+.` → “合并 XML 文档行”，支持文档、项目、解决方案范围的全部修复。仅合并元素范围内的行，移除文档前缀和内容首尾的排版空白；保留原始文本、XML 实体、行内嵌套元素、周围注释与代码，以及其余 CRLF／LF 换行。支持 `///` 和 `/** */`。标签自身跨行、XML 语法不完整或当前元素及其祖先显式设置 `xml:space="preserve"` 时不提供修复；真正的多行内容、空内容、已合并元素及过期诊断也不改写。

ZS3001／ZS3002／ZS3003 均跳过生成代码；需要 Roslyn 解析 XML 文档（`DocumentationMode.Parse` 或 `Diagnose`），不要求生成 XML 输出文件。`DocumentationMode.None` 不检查。标签是否存在不证明契约准确、描述完整，也不替代编译器对错误参数名称等 XML 问题的检查。

<a id="sdk-rules"></a>

## SDK 与编译器规则

这里说明 Zongsoft 选项和边界；语言语义及 SDK 实现细节以对应官方文档为准。

<a id="ide0009"></a>

### IDE0009 · 实例成员限定

实例属性、方法和事件使用 `this.`，对应 `dotnet_style_qualification_for_property/method/event = true`。私有字段直接按名称访问。字段选项无法区分可见性，公共字段的 `this.` 要求仍需审查。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0003-ide0009)

<a id="ide0049"></a>

### IDE0049 · C# 类型关键字

声明及静态成员访问使用 `int`、`string` 等关键字，配置 `dotnet_style_predefined_type_for_locals_parameters_members` 和 `dotnet_style_predefined_type_for_member_access`。该规则不在构建时运行；需要编辑器或 `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes` 补充检查。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0049)

<a id="ide0055"></a>

### IDE0055 · 格式

Tab 缩进，多行块使用 Allman 花括号；控制关键字与括号间不加空格，二元运算符两侧加空格。文件换行与文件类型例外由根 `.editorconfig` 提供。指令缩进、局部对齐与紧凑布局的允许写法见 [诊断例外](#suppressions)。

表达式体方法后紧接另一个方法时，可以不留空行；`=>` 后的表达式位于同一行或下一行均适用。后一个方法可以使用块体；[ZS2003](#zs2003) 的语句组检查不要求在这两个方法之间插入空行。

无成员的结构、类和记录声明之间也可以不留空行，包括带主构造函数的空类型和 `record struct`。以下为合规的类型声明片段：

```csharp
struct MyStruct { }
class MyClass { }
record MyRecord(int Value) { }
```

`dotnet format whitespace` 直接比较格式化文本，不运行诊断抑制器，仍可能对合法例外返回差异；这些例外以加载分析器的构建诊断为准。自动格式化仅限修改文件并审查差异。[官方格式规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0055)

<a id="ide0065"></a>

### IDE0065 · using 位置

`csharp_using_directive_placement = outside_namespace`，将 using 放在命名空间之前。此规则不负责导入排序、分组或别名限制。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0065)

<a id="ide0161"></a>

### IDE0161 · 文件范围命名空间

`csharp_style_namespace_declarations = file_scoped`，使用 `namespace Example;`。不要为形式统一改变程序集、RootNamespace 或领域边界。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide0160-ide0161)

<a id="ide1006"></a>

### IDE1006 · 命名

接口使用 `I` 前缀，类型和成员 PascalCase，参数与普通局部变量 camelCase，私有字段默认 `_camelCase`，泛型参数使用 `T` 前缀。私有、内部及局部常量使用 UPPER_SNAKE_CASE，公共或受保护常量使用 PascalCase；方法内 `const int SIZE = sizeof(short);` 合规。

私有字段还允许[全大写名称或以下划线包围的名称](#zss1006)。

命名配置按符号种类、可见性和 `const` 区分，更具体的常量规则优先。除命名规则自身 severity 外，显式设置 `dotnet_diagnostic.IDE1006.severity = warning` 供构建使用。名称的领域含义、类型后缀和外部契约仍需审查；自动重命名公共 API 前检查兼容性。[官方命名规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/naming-rules)

<a id="ide2001"></a>

### IDE2001 · 嵌入语句另起一行

简单分支可省略花括号，语句另起一行。`csharp_style_allow_embedded_statements_on_same_line_experimental = false`；该选项为实验性，SDK 升级后需要复核。紧凑 try/catch/finally 由 [ZSS2001](#zss2001) 豁免，空 `while(...);` 由 [ZSS2002](#zss2002) 豁免；最多两条简单语句的单行方法体或匿名函数体由 [ZSS2003](#zss2003) 豁免。[官方规则](https://learn.microsoft.com/zh-cn/dotnet/fundamentals/code-analysis/style-rules/ide2001)

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
| <a id="ca1303"></a>CA1303 | 本地化范围由 [ZS1301](#zs1301)／[ZS1302](#zs1302) 定义；消费项目可显式启用 SDK 原生检查 |
| <a id="ide0005"></a>IDE0005 | 由 [ZS0005](#zs0005) 检查未使用引用，允许保留普通命名空间引用 |
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

### ZSS0055 · 指令缩进

豁免所有 `#` 指令行前空白上的 IDE0055，包括 `#pragma`、`#region`、`#nullable`、`#line` 及条件编译指令。指令可顶格，也可与所在代码保持同级缩进，不因嵌套条件额外缩进；不会豁免其他表达式格式问题。

<a id="zss0056"></a>
<a id="zss2001"></a>

### ZSS0056 / ZSS2001 · 紧凑异常处理

按 `try`、`catch`、`finally` 子句分别判断：子句自身占一行，且花括号内的直接语句不超过两条时，豁免其块边界 IDE0055 与块内 IDE2001。语句类型不限，包括 `break`、`continue`、`yield break` 和控制流语句；嵌套语句不重复计入外层子句，局部函数和匿名函数的函数体仍独立检查。相邻紧凑子句可以同处一行，也允许单行与多行子句混用。超过两条直接语句、跨行布局及表达式内部格式错误不属于例外。

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

<a id="zss0057"></a>

### ZSS0057 · 局部对齐与紧凑边界

仅豁免以下位置的 IDE0055，不忽略语句内部的其他格式问题：

- 变量声明中类型与变量名之间、变量名与初始化 `=` 之间的对齐空白。
- 二元／条件表达式续行对齐表达式开头，包括 lambda、throw、括号及取反表达式中的操作数；条件赋值的续行也可对齐 `=`。
- 方法调用的后续实参对齐首个实参；构造调用还可对齐 `new` 后的类型名。保留起始行的 Tab 缩进层级，再用空白对齐，按 `tab_width` 计算显示列；不豁免普通语句块的错误缩进或表达式内部的运算符空格。
- 参数特性列表之间、最后一个特性列表与参数类型或修饰符之间省略的空格。
- 元组元素名与 `:` 之间的空格；三元表达式的 `:` 在行尾时省略的前导空格。
- 空 `while` 循环的右括号与同一行分号之间的布局。
- 构造函数初始化器后的一个空格及同一行的空 `{ }`，包括跨行的初始化器；Tab 分隔不属于此例外。
- 匿名方法的 `delegate` 与 `(` 之间省略空格。

<a id="zss0058"></a>
<a id="zss2003"></a>

### ZSS0058 / ZSS2003 · 简短方法与匿名函数

方法、局部函数、lambda 或匿名方法可以使用单行块体，最多包含两条简单语句：调用／赋值、局部声明、return 或 throw。此例外仅豁免块边界的 IDE0055 和块内的 IDE2001，不豁免表达式内部格式。三条以上语句、嵌套控制流、多行块体和 yield 语句不属于此例外。try/catch/finally 按每个子句最多两条直接语句、语句类型不限的独立规则检查。

```csharp
public int Next(int value) { value++; return value; }
```

<a id="zss2002"></a>

### ZSS2002 · 空 while 循环

允许 `while(queue.TryDequeue(out _));`，不报告 IDE2001；这里假定 `queue` 是并发队列。非空循环体仍另起一行；本例外不判断空循环是否符合业务意图。

<a id="zss1006"></a>

### ZSS1006 · 私有字段命名例外

在默认 `_camelCase` 之外，以下私有字段名称不报告 IDE1006：

- 全大写名称：至少包含一个大写字母，其余字符只能是大写字母、数字或下划线，如 `SIZE`、`DEFAULT_CAPACITY`、`HTTP2_BUFFER`。
- 以 `_` 开头并以 `_` 结尾的合法 C# 标识符：内部大小写及下划线的数量与组合不限，如 `_GaugeMethod_`、`_gauge_method_`、`__getHandlers__`。

例外仅依据字段的私有可见性，实例／静态、可变／只读字段及私有常量均适用。不会放宽其他可见性字段、属性、参数或局部变量的命名；例如 `_GaugeMethod` 不满足两端下划线条件，仍按私有字段命名规则检查。

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

- [逻辑分组](zongsoft.csharp.guidelines.md#statement-groups)：按职责、执行阶段和验证对象划分，组间留空行；没有连续物理行数的硬上限。相同语句种类、接收对象或数据依赖不自动构成紧凑组。同质映射、注册、初始化和断言表等例外必须满足规范条件。测试中的简单输入构造可与紧接着的执行同组；准备阶段的前置断言可紧接其直接对应的环境或数据获取操作；共同验证同一次操作结果的状态、内容等断言，以及辅助取值与对应断言，也可以同组；独立准备阶段与执行、执行与验证、不同验证对象及每个字段的多步验证仍须分组。
- [返回布局](zongsoft.csharp.guidelines.md#return-spacing)：紧凑的获取／包装／状态结果等单元可以紧接返回；独立回退、多步报文填充和创建／获取／发布阶段结束后的返回须分隔。条件表达式跨行本身不强制空行，最终配置回调与返回可以成组。不要统一给所有 return 加空行。
- ZS2003 只检查已列出的确定语法边界，不根据调用名称、对象身份或业务阶段猜测上述空行，也不自动修复这些人工判定项；人工紧凑组不得豁免已有 ZS2003 边界。

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

> 🔗 **维护约定：** 规则锚点使用小写诊断 ID，如 `#zs2003`，不得因标题改写而改变。新增诊断时同步两版文档、帮助链接、级别、修复状态和测试；删除规则时同时清理对应锚点与引用。
