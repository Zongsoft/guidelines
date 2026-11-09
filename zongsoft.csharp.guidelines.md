# Zongsoft .NET/C# 开发规范（Ver 2.0.0）

本规范面向 Zongsoft 框架及其应用项目的开发者，统一 .NET/C# 的代码风格、类型设计、公共契约和工程质量要求。

📋 自动检测的诊断编号、级别、例外与修复示例统一见 [规则列表](RULES.zh-Hans.md#index)。本文保留编码和设计要求，工具的检测边界以规则列表说明为准。

> **AI 编码入口：** [AI 开发协作规范（AGENTS.md）](AGENTS.md) 规定编码前阅读、修改边界、验证和交付流程，并引用本文作为编码与设计依据。

**阅读导航**

1. [基本原则](#principles)
2. [命名规范](#naming)
3. [文件与布局](#layout)：[嵌套控制体](#nested-control-bodies)、[逻辑分组](#statement-groups)、[返回布局](#return-spacing)、[局部函数](#local-functions)
4. [注释与 API 文档](#documentation)
5. [C# 语言与类型使用](#language)
6. [设计范式与公共契约](#design)
7. [异步、取消、异常与资源](#asynchrony)
8. [项目配置与质量](#quality)
9. [依据与参考](#references)

<a id="principles"></a>

## 1. 基本原则

- **必须／不得**表示强制要求；**默认／优先**表示没有明确理由时遵循的写法；**可以**表示满足所述条件时可选。
- 本文未规定的细节遵循项目约定、同职责代码和 `.editorconfig`，保持项目内部一致性。
- 语言正确性、业务语义、兼容性和资源安全优先于形式一致。历史代码、第三方代码和生成代码中的个别写法不自动构成规范。
- 优先复用已有类型、基类、扩展方法和装配机制。新增抽象应有明确需求和清晰职责，避免为假想需求预建接口、工厂、配置开关或继承层次。
- 在尊重 .NET/C# 普遍的开发习惯和传统的基础上，尽量采用更简洁的编码风格。
- 尽量采用新的 .NET/C# 版本，充分利用新式语法提升生产力、代码简洁性和可读性。

### 1.1 风格速查

| 项目 | 约定 |
| --- | --- |
| 缩进换行 | Tab 缩进，显示宽度 4；新建文本文件使用 CRLF，平台脚本例外见第 3 节 |
| 命名空间 | `using` 在前，文件范围命名空间在后 |
| 成员访问 | 实例属性、方法和事件使用 `this.`；私有字段直接按名称访问，命名例外见第 2 节 |
| 大花括号 | 多行块采用 Allman 风格；简单单语句分支可省略花括号；直接嵌套控制体须用块包围，限定例外见第 3.5 节 |
| 控制语句 | 关键字与左括号之间不加空格；同层级的独立控制结构之间及含执行块的完整结构之后留空行；条件和循环的前置空行按普通语句数量判定；限定例外见第 3.4 节及 [ZS2003](RULES.zh-Hans.md#zs2003) |
| 逻辑分组 | 按职责、对象和执行阶段分组，组间留空行；同构表格式段可紧凑，不设置物理行数硬上限；返回布局见第 3.7 节 |
| 本地化文本 | 使用 `.resx` 和 `ResXFileCodeGenerator` 生成的 Designer 属性访问可翻译文本 |
| 成员布局 | 非测试项目的文件内方法与属性合计超过 9 个时必须按职责使用中文 `#region`，不创建空分段并尽量让分段名保持 4 个汉字 |
| 公共入口 | 校验和流程集中，重载转发；通过已有的窄扩展点参与流程 |
| 异步方法 | `Async` 后缀，取消令牌沿调用链传递，保持已有返回类型和取消语义 |
| 框架消费 | 面向公共契约，由宿主、模块、插件和提供者完成具体实现装配 |

<a id="naming"></a>

## 2. 命名规范

### 2.1 大小写与语义

- **P**ascal **C**asing
	帕斯卡命名法：每个单词的首字母均大写 _(也被称为 “大驼峰命名法”)_。
- **c**amel **C**asing
	小驼峰命名法：表示首个单词首字母小写、其余单词首字母大写。

| 元素 | 规则 | 示例 |
| --- | --- | --- |
| 命名空间、类型、方法、属性、事件 | PascalCasing，非公共成员也遵循此规则 | `Zongsoft.Services`、`GetAccessor`、`IsDisposed` |
| 接口 | 大写 `I` 前缀加 PascalCasing | `IDataAccess`、`IMatchable` |
| 私有实例字段、私有静态字段 | 默认 `_camelCase`；允许全大写命名及以下划线包围的名称 | `_name`、`DEFAULT_CAPACITY`、`_GaugeMethod_` |
| 参数、普通局部变量、主构造函数参数 | camelCasing | `name`、`result`、`cancellation` |
| 公共或受保护字段，包括只读字段 | PascalCasing；新增时先评估是否应为属性 | `Instance`、`Empty` |
| 私有、内部及局部常量 | 大写单词以下划线连接，无起始下划线 | `DISPOSED`、`DEFAULT_CAPACITY`、`KEY_TIMEOUT_OPTION` |
| 公共、受保护常量 | PascalCasing | `DefaultCapacity` |
| 泛型参数 | 单一且含义明显时用 `T`，否则使用 `T` 加职责名 | `TModel`、`TService`、`TArgument`、`TResult` |

- 类型、属性、字段和变量使用名词或名词短语；能力接口可使用形容词，如 `IMatchable`。方法使用动词或动宾短语。
- 布尔成员使用能直接读出判断含义的名称，如 `Enabled`、`Visible`、`IsEmpty`、`HasValue`、`CanDelete`。避免双重否定及含义不清的 `flag`、`b1`。
- 避免生造缩写；标准缩写遵循现有 .NET/项目拼写，如 `IO`、`Xml`、`Html`、`Http`、`Json`。
	> 既有协议名、供应商名和公共 API 不因大小写偏好而改名。
- 私有字段允许采用常量风格的全大写名称，如 `SIZE`、`DEFAULT_CAPACITY`、`HTTP2_BUFFER`，可包含数字和下划线；这不改变字段是否可变的语义。
- 私有字段的名称若以 `_` 开头并以 `_` 结尾，则不再限制内部的大小写、下划线数量或组合方式，例如 `_GaugeMethod_`、`_gauge_method_`、`__GetHandlersMethodTemplate__`。名称仍须是合法的 C# 标识符。
- 以上例外适用于私有实例字段和私有静态字段，不要求 `readonly`，也包括私有常量字段；不扩展到其他可见性的字段、属性、参数或局部变量。自动检测边界见 [ZSS1006](RULES.zh-Hans.md#zss1006)。
- 局部循环索引可使用 `i`、`j`。
- 命名空间按领域与功能组织，通常使用复数，如 `Services`、`Collections`、`Components`；`Data`、`IO`、`Configuration` 等沿用惯例。
- 命名空间通常采用 `<Organization>.<ProductOrTechnology>.<Feature>`。以项目 `RootNamespace` 和领域归属为准，不把 `src`、部署目录或程序集名称机械拼入命名空间。

### 2.2 类型后缀

- 提供可继承实现骨架的基类默认以 `Base` 结尾，如 `DataAccessBase`、`HandlerBase<TArgument>`。以抽象类表达的数据模型保留领域名称，不机械添加 `Base`。
- 工具静态类通常以 `Utility` 或 `Helper` 结尾；扩展方法容器使用单数 `Extension`，如 `ServiceProviderExtension`。
- 配置类型使用 `Options`、`Settings` 或 `ConnectionSettings`，操作上下文使用 `Context`，元数据使用 `Descriptor`，集合使用 `Collection`，具体名称应符合其实际职责。
- 异常、特性和事件参数类型分别以 `Exception`、`Attribute`、`EventArgs` 结尾。
- 优先使用 `Action`、`Func`、`Predicate<T>` 和 `EventHandler<TEventArgs>`。确需自定义委托时按职责命名，事件委托使用 `EventHandler` 后缀，普通回调可使用 `Callback` 后缀；不得统一添加 `Delegate` 后缀。
- 普通枚举使用单数名称，位标志枚举通常使用复数名称；不添加 `Enum` 后缀。位标志使用 `[Flags]`，独立位显式指定为 2 的幂，零值按语义命名为 `None` 等；为保持兼容性避免重排已发布枚举的数值。
- 方法返回 `Task`、`Task<T>`、`ValueTask`、`ValueTask<T>`，或表示异步迭代操作时，默认以 `Async` 结尾。实现外部接口、重写基类或维护已有协议时服从既有名称。

<a id="layout"></a>

## 3. 文件与布局

### 3.1 文件规则

- 保持已有文件的编码、BOM 状态和换行符；新建文本文件默认使用 UTF-8、无 BOM、CRLF。有明确项目约束时遵循项目约束。
- `.cmd` 文件必须使用 CRLF；`.sh` 等仅用于 Linux/Unix 的脚本遵循 `.gitattributes`，使用 LF。
- C#、XML 和项目文件使用 Tab 缩进，Tab 显示宽度为 4。续行也以 Tab 表达层级，仅在局部对齐时补充空格。
- 文件末尾保留一个换行；移除行尾多余空白；成员和逻辑段之间通常只留一个空行。
- 项目文件中，同层级的 `PropertyGroup`、`ItemGroup` 等独立段落之间保留一个空行。
- 默认一个文件承载一个主要类型，文件名对应类型名。泛型与非泛型同名类型、紧密关联的嵌套类型可以按项目惯例组织；泛型文件名及 `partial` 文件拆分沿用现有约定。
- 仅因一个文件较长不必拆成大量碎片；需要拆分时按稳定职责划分，如同步/异步实现、序列化适配或嵌套实现。
- 保留已有版权和许可头；新建源码按所属项目模板填写，确保库名、作者信息和版权年份准确。

### 3.2 using 与命名空间

- 默认使用文件范围命名空间：`namespace Zongsoft.Services;`。`using` 指令放在命名空间之前。
- 按顶级命名空间分组：`System` 在首组，第三方库随后，框架引用与业务引用按依赖层次排列，所属项目的引用组靠后；组间一个空行。
- 组内默认按命名空间长度由短到长排列，同长度保持局部一致。
- 允许保留未使用的普通命名空间引用，包括 `System`、第三方库和项目自身的命名空间。这一例外不包括别名、`using static` 或 `global using`。正式项目慎用 `global using`，优先在各文件中显式导入所需命名空间，保持依赖清晰；隐式引用遵循项目统一配置。单元测试项目在项目文件的独立 `ItemGroup` 中集中导入所需的测试框架命名空间。
- 不得使用 `using` 别名指令为命名空间或类型定义别名，如 `using Aaaa = Xxxxx.Yyyyy.Zzzzz;`。发生类型名冲突时，应在使用位置采用带 `global::` 前缀的完整限定名，如 `global::Zongsoft.IO.Path`，从全局命名空间明确指定目标类型。

#### 示例一：按依赖分组的文件头部

以下是业务控制器文件的头部片段，正文省略。项目引用 ASP.NET Core 和 Zongsoft.Core，`Automao.Samples.Models` 与 `Automao.Samples.Services` 表示示例业务项目自己的命名空间；实际文件只保留正文使用的引用。

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Zongsoft.IO;
using Zongsoft.Data;
using Zongsoft.Services;
using Zongsoft.Configuration;

using Automao.Samples.Models;
using Automao.Samples.Services;

namespace Automao.Samples.Web.Controllers;
```

- `System` 位于首组，随后是 Microsoft 组件、Zongsoft 框架和业务项目自身的引用，各组之间留一个空行。
- 每组分别按命名空间长度排序，不跨组混排。例如 `System.Collections.Generic` 位于 `System.Threading.Tasks` 之后，`Zongsoft.IO` 仍保留在 Zongsoft 组内。
- 全部 `using` 位于文件范围命名空间之前；类型声明紧接在 `namespace ...;` 之后，不再增加命名空间花括号。

#### 示例二：同名类型使用完整限定名

上例同时导入了 `System.IO` 和 `Zongsoft.IO`，两者都定义了 `Path`。需要分别使用它们时，在使用位置明确写出完整限定名；以下为引用 Zongsoft.Core 的方法体片段：

```csharp
var localPath = global::System.IO.Path.Combine("reports", "daily.csv");
var virtualPath = global::Zongsoft.IO.Path.Parse("local:/reports/daily.csv");
```

这里不定义 `using LocalPath = System.IO.Path;` 或 `using VirtualPath = Zongsoft.IO.Path;` 一类别名。`global::` 明确从全局命名空间解析，即使当前作用域存在同名命名空间或类型，也不会改变目标类型。没有冲突的类型继续使用普通名称，无需将文件中的所有类型都改成完整限定名。

#### 示例三：单元测试项目的全局引用

正式项目应慎用 `global using`，避免仅为减少几行导入而扩大命名空间的可见范围。单元测试项目中的测试框架引用较为统一，可以在 `.csproj` 文件的独立 `ItemGroup` 中集中声明：

```xml
<ItemGroup>
	<Using Include="Xunit" />
</ItemGroup>
```

上述示例适用于已引用相应 xUnit 包的单元测试项目。需要测试框架子命名空间时，在同一段中按需添加对应的 `Using`。各测试文件无需重复导入这些命名空间，文件自身需要的其他命名空间仍使用普通 `using` 按需导入。

> 💡 **适用范围：** 全局引用仅在所属项目中生效；测试项目采用这一写法，不代表业务类库和应用项目也应采用。项目级 `Using` 生成 `global using` 导入命名空间；`global::` 从全局命名空间限定类型，两者用途不同。

### 3.3 成员分段

- 具有多组职责的类型使用中文 `#region`。典型顺序为：常量定义、静态字段／单例字段、事件声明、成员字段、构造函数、属性、方法、符号重载、显式实现、释放资源、嵌套类型。
- 非测试项目中，一个代码文件内的方法与属性合计超过 9 个（即至少 10 个）时，必须使用成对的 `#region`／`#endregion` 按职责组织这些成员。测试项目（`IsTestProject=true`）不受此强制分段规则约束。按当前文件统计，包含嵌套类型及同文件的其他类型；索引器计作属性，构造函数、访问器、局部函数及运算符不计入，`partial` 的其他文件不合并计数。检测边界见 [ZS2004](RULES.zh-Hans.md#zs2004)。
- 属性和方法可进一步划分为公共、内部、保护、私有、抽象、虚拟、重写和静态成员；同一流程也可采用“解析方法”“服务匹配”“事件触发”等职责分段。
- 分段顺序服务于阅读，优先保持所在类型的组织方式；不要求每个类型照抄完整清单。未达到上述文件阈值的简单接口、枚举、小类型可以不分段。
- `#region` 与首个成员之间、最后成员与 `#endregion` 之间不留空行；分段之间留一个空行。不得保留空分段。
- 同名重载相邻排列，简单重载在前并转发到完整重载；避免把一组重载拆散到多个不相邻区域。

> 💡 **布局示例：** 以下类型可独立编译，省略所属项目的版权头。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zongsoft.Samples;

/// <summary>表示处理文本的基类。</summary>
public abstract class TextProcessorBase
{
	#region 构造函数
	/// <summary>初始化文本处理器。</summary>
	/// <param name="name">非空白的处理器名称。</param>
	/// <exception cref="ArgumentException"><paramref name="name"/> 为空或空白。</exception>
	protected TextProcessorBase(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		this.Name = name;
	}
	#endregion

	#region 公共属性
	/// <summary>获取处理器名称。</summary>
	public string Name { get; }
	#endregion

	#region 公共方法
	/// <summary>异步处理指定文本。</summary>
	/// <param name="text">待处理的文本，允许空字符串。</param>
	/// <param name="cancellation">取消操作的令牌。</param>
	/// <returns>表示处理操作的任务。</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> 为空。</exception>
	/// <exception cref="OperationCanceledException">操作已被取消。</exception>
	public Task ProcessAsync(string text, CancellationToken cancellation = default)
	{
		ArgumentNullException.ThrowIfNull(text);
		cancellation.ThrowIfCancellationRequested();
		return this.OnProcessAsync(text, cancellation);
	}
	#endregion

	#region 抽象方法
	/// <summary>执行具体的文本处理。</summary>
	/// <param name="text">已验证非空的文本。</param>
	/// <param name="cancellation">取消操作的令牌。</param>
	/// <returns>表示处理操作的任务。</returns>
	protected abstract Task OnProcessAsync(string text, CancellationToken cancellation);
	#endregion
}
```

### 3.4 空格、换行与表达式体

- 类型和多行语句块的左右花括号各占一行，采用 Allman 风格。自动属性、短表达式成员和空构造函数可以单行书写；构造函数初始化器跨行时，空的 `{ }` 也可在一个空格后紧接 `this(...)`／`base(...)`。方法、局部函数及 lambda／匿名方法的单行块体最多包含两条简单语句，不包含嵌套控制流或跨行表达式。`try`、`catch`、`finally` 子句各自包含不超过两条直接语句时，可以单行书写，语句类型不限；嵌套语句不重复计入外层子句。相邻紧凑子句可放在同一行，也允许单行与多行子句混用。超过两条直接语句或内容跨行时，使用常规多行块。紧凑布局不改变异常处理的语义要求。
- 所有以 `#` 开头的指令（包括 `#pragma`、`#region` 和条件编译指令）可以顶格，也可以与所在代码保持相同缩进层次；不因嵌套条件额外增加缩进。
- 控制关键字与括号之间不加空格，如 `if(value == null)`、`foreach(var item in items)`、`using(var stream = ...)`；声明和调用的括号内侧不加空格；匿名方法可以写成 `delegate(...)`。
- 二元运算符两侧、逗号之后使用一个空格；一元运算符和成员访问运算符不加空格。变量名之前、声明初始化的 `=` 之前可以增加空白以纵向对齐；二元或条件表达式续行可以对齐表达式开头，包括 lambda、throw、括号及取反表达式中的操作数；条件赋值的续行也可对齐赋值号 `=`。方法实参续行可对齐首个实参，构造实参也可对齐 `new` 后的类型名。以上布局保留 Tab 缩进层级，再补充空格进行纵向对齐。
- 参数特性列表之间，以及最后一个特性列表与参数类型或修饰符之间，可以省略空格。元组元素名的 `:` 前可加空格；三元表达式的 `:` 位于行尾时，其前空格可省略，同一行中的三元运算符仍按常规留空格。
- 一行只写一条普通语句或一个普通声明；`for` 头部、解构声明、自动属性访问器及上述合规的单行块体不受此限制。
- 空 `while` 循环体可以把 `;` 紧接条件写在同一行，如 `while(queue.TryDequeue(out _));`；确认空循环体符合真实意图。
- 单语句且清晰的分支可以省略花括号，语句仍另起一行。多语句、多行复杂表达式或易产生悬空 `else` 的嵌套分支必须加花括号；直接嵌套的控制体另按第 3.5 节检查。
- 按职责组织逻辑段。连续普通语句后紧接条件或循环时，带块或复杂结构前至少两条、简单无花括号结构前至少三条即须留空行。普通语句包括局部声明、赋值、调用（含 `await`）、自增和自减，混合出现也累计；按语句而非物理行计数。未达到门槛时可紧接，也可主动保留空行；声明、调用、返回之间不机械分隔。具体分类和示例见 [ZS2003](RULES.zh-Hans.md#zs2003)。
- 同一层级的相邻独立控制语句或显式语句块之间留一个空行；含有执行语句块的完整复合语句或显式语句块之后，紧接任何独立语句时也须留空行。调用、断言、事件通知、赋值、声明、资源释放、返回及跳转不因用途而豁免；测试代码与生产代码使用相同标准。
- `try`、`using(...)`、`lock`、`fixed`、`checked`／`unchecked`、`unsafe` 和显式语句块之前存在同层级独立语句时，须留一个空行，不因前面只有一条普通语句而豁免。条件和循环采用上一项的计数门槛；`switch`、`do`、带 `else`／`else if` 的分支、带块或嵌套控制语句体属于复杂结构。资源组和其他限定例外见 [ZS2003](RULES.zh-Hans.md#zs2003)。
- `if`／`else if`／`else`、`try`／`catch`／`finally` 和 `do`／`while` 属于同一结构，其关联子句之间不因此插入空行；控制语句与其嵌套语句体也不拆开。前置分隔位于完整结构之前，后置分隔位于完整结构之后；`do` 的后置分隔在最终 `while(...);` 之后；最后一个分支无花括号也不改变带块结构的分隔要求。
- 连续同类型的简单无花括号 `if`、`for`、`foreach`、`while`，连续完整单行的紧凑异常处理语句，以及紧接块的 switch 分支末尾 `break`，在限定条件下可以省略空行。具体检测条件和正反例集中见 [ZS2003](RULES.zh-Hans.md#zs2003)；其他返回或跳转不能套用 switch 的 `break` 例外。
- 连续资源声明与其后直接嵌套的 `using(...)`／`await using(...)` 链可以作为紧凑资源组，前置分隔放在第一条资源声明之前，不拆开组内资源语句。普通语句打断资源组，两个已经结束的独立 `using` 块仍须分隔；完整条件和示例见 [ZS2003](RULES.zh-Hans.md#zs2003)。
- 注释行和预处理指令行不能代替空行；块注释内部的空白行也不计作语句分隔。初始化器、模式、switch 表达式、lambda 和匿名方法的花括号属于表达式，不据此要求外围语句之间插入空行。
- 优先使用提前返回减少嵌套。不要机械移除所有 `else`，成对分支在语义更清楚时保留。
- 简单计算属性、转发方法、操作符和短构造函数优先使用表达式体；仅存储值的属性优先使用自动属性。包含多步操作、循环、异常处理或复杂分支时使用块体。
- 表达式体方法与紧随其后的方法之间可以不留空行；表达式体写在同一行或在 `=>` 后换行均适用，也不要求后一个方法使用表达式体。
- 无成员的结构、类和记录声明之间可以不留空行，包括带主构造函数的空类型及 `record struct`；空的 `{ }` 可以与声明写在同一行。
- 长参数列表、链式调用和条件表达式按语义换行；避免多层三元表达式和为了压缩行数合并副作用。

> 💡 **规则示例：** [语句组分隔与紧凑逻辑段](RULES.zh-Hans.md#zs2003)、[指令缩进](RULES.zh-Hans.md#zss0055)、[单行异常处理](RULES.zh-Hans.md#zss0056)、[简短方法与匿名函数](RULES.zh-Hans.md#zss2003)、[局部布局例外](RULES.zh-Hans.md#zss0057)。

<a id="nested-control-bodies"></a>

### 3.5 嵌套控制体

- 控制语句的语句体如果又是一条控制语句，必须用 `{ ... }` 包围该语句体。直接嵌套两条控制语句即适用，例如 `for → if`；不是等到第三条控制语句才要求花括号。
- 内层控制语句自身已有块，也不能替代外层语句体的块；更深的嵌套逐层检查。此要求用于控制语句与语句体的关系，不把方法体、条件表达式或参数列表算作控制语句层数。
- 标准 `else if` 是关联分支，可以直接连接；`if` 的真分支或普通 `else` 分支内嵌另一控制结构时，仍须块。控制语句前后的空行继续遵守第 3.4 节，不在父语句与嵌套语句体之间机械插入空行。
- 纯 `using(...)`／`await using(...)` 链、纯 `fixed` 链和纯 `lock` 链可以共享终端语句体；终端须为简单单语句或块。`using` 与 `await using` 属于同一资源链；`lock → using`、`using → lock` 等混合职责链不属于此例外。最后一个链头若直接接条件、循环或另一类控制语句，必须给这个语句体加块；前面的同类链头仍可保持紧接。
- 简单终端语句与块的具体语法范围、生成代码边界和修复行为见 [ZS2005](RULES.zh-Hans.md#zs2005)。lambda、匿名方法和局部函数的函数体内部独立遵守本节，不因它们出现在表达式中而豁免。

以下为独立的布局片段，假定 `items` 为 `int[]`，`enabled` 为 `bool`，`Process(int)` 和 `Complete()` 为已有的静态示例方法。标为“不符合”的片段用于展示问题。

不符合：循环直接以另一控制语句为体。

```csharp
foreach(var item in items)
	if(item > 0)
		Process(item);
```

符合：给外层循环的语句体加块，内层简单分支仍可省略花括号。

```csharp
foreach(var item in items)
{
	if(item > 0)
		Process(item);
}
```

不符合：内层 `while` 的块不能包围外层 `if` 的语句体。

```csharp
if(enabled)
	while(enabled)
	{
		enabled = false;
	}
```

符合：更深的控制嵌套逐层包围；最终的简单单语句体仍可紧凑。

```csharp
foreach(var item in items)
{
	if(item > 0)
	{
		while(enabled)
			enabled = false;
	}
}
```

符合：标准 `else if` 保留关联分支布局。

```csharp
if(items.Length == 0)
	Process(0);
else if(items.Length == 1)
	Process(items[0]);
else
	Process(items.Length);
```

符合：纯资源链共享终端单语句。这是返回字符串的方法体片段，已导入 `System.IO`，`text` 为字符串。

```csharp
using(var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)))
using(var reader = new StreamReader(stream))
	return reader.ReadToEnd();
```

对应异步资源链也可共享终端单语句。以下为异步方法体片段，已导入 `System.IO`，`sourceBytes` 为 `byte[]`，`cancellation` 为 `CancellationToken`。

```csharp
await using(var source = new MemoryStream(sourceBytes, false))
await using(var target = new MemoryStream())
	await source.CopyToAsync(target, cancellation);
```

纯 `fixed` 链和纯 `lock` 链也可共享终端体。下面两个片段分别位于 `unsafe` 方法和普通实例方法中；`sourceBuffer`、`targetBuffer` 是长度均大于零的字节数组，`_outerSync`、`_innerSync` 是私有同步对象，各调用方遵守相同的锁定顺序。

```csharp
fixed(byte* source = sourceBuffer)
fixed(byte* target = targetBuffer)
	*target = *source;
```

```csharp
lock(_outerSync)
lock(_innerSync)
	Complete();
```

混合资源与同步职责时必须加块。以下两个片段均已导入 `System.IO`，`_sync` 是私有同步对象；内存写入仅用于展示布局。

```csharp
lock(_sync)
{
	using(var stream = new MemoryStream())
		stream.WriteByte(1);
}
```

```csharp
using(var stream = new MemoryStream())
{
	lock(_sync)
		stream.WriteByte(1);
}
```

<a id="statement-groups"></a>

### 3.6 逻辑分组

- 独立语句按职责、操作对象和执行阶段组织逻辑段，组间必须留空行。独立的准备阶段与执行、不同对象的配置、执行与发布状态、不同验证目标都是应识别的边界，不按物理行数设置硬上限。
- 同一个接收者、同一种语句语法或前后存在数据依赖，不足以把整段代码归为一个逻辑组。普通声明、调用、赋值和返回之间也不逐项机械插入空行；判断的是职责是否切换。
- 同一对象的元数据映射、同类值的批量初始化或注册、完整的中间件流水线、同阶段简单守卫、简单条件返回表，以及生成一个完整表达式的连续 `Emit` 指令，可以保留表格式紧凑布局。切换注册类别、对象或执行阶段时仍须分组；不能把所有同名调用或所有赋值都作为表格式例外。
- 标题单元格设置与范围样式设置、报文准备与缓冲填充、连接配置与连接／Poller 注册、活动对象发布等阶段之间须留空行。一个阶段内可以混合声明、调用和赋值，不因语法类别变化再次切分。
- 简单输入构造（例如单条局部变量声明）与紧接着使用该输入的执行可以组成一个紧凑组，不必因名称上属于准备与执行就拆段。多步环境搭建或对象配置形成独立准备阶段时，仍须与执行分隔；不能仅凭相邻语句有数据依赖就合并多个阶段。
- 准备阶段中的前置断言（例如确认新建环境为空、待修复诊断存在或修复动作非空）可以紧接其直接对应的环境或数据获取操作。它们确认后续受测执行所需的条件，仍属于准备阶段；`await` 和 `Assert` 本身不构成阶段边界。受测执行后的结果验证仍须另起一组。
- 测试代码按实际职责组织准备、执行和验证；简单输入准备可与紧接着的执行同组，执行与验证、不同验证目标之间留空行。验证目标按所验证的行为判定：同一次操作的成功状态、预期结果和相关错误检查可以共同验证一个结果，不因读取不同属性或使用不同断言方法就拆段。同一验证目标的辅助取值声明与断言可以同组，辅助取值不自动成为新的受测执行阶段。对多个独立字段逐项做取值验证和值验证时，每个字段组成一个小组，字段组之间须分隔。只有严格同质、每项仅一条断言、仅字段或属性名称变化的表格式验证，才可保持紧凑；不能豁免所有 `Assert` 调用。
- 对象／集合初始化器、参数列表、流式调用链和 switch 表达式分支不是多条同级独立语句，不逐项强制空行。表达式内部的 lambda 等函数体仍独立检查。
- 本节的职责和阶段划分由人工审查，不由语法类别推测自动诊断。它不撤销 [ZS2003](RULES.zh-Hans.md#zs2003) 的明确结构边界、资源组边界或条件／循环之前按普通语句数量判定的分隔要求。

同一对象的元数据映射可以保持紧凑。以下为方法体片段，`source` 和 `target` 均为 `System.Data.DataColumn`。

```csharp
target.ColumnName = source.ColumnName;
target.Caption = source.Caption;
target.AllowDBNull = source.AllowDBNull;
target.MaxLength = source.MaxLength;
```

同类批量注册可以紧凑，不同类别之间分组。以下片段已导入 `System.Xml`。

```csharp
var names = new NameTable();

names.Add("schema");
names.Add("entity");
names.Add("property");

names.Add("name");
names.Add("type");
names.Add("length");
```

完整中间件流水线可以保持相邻调用。以下为引用 ASP.NET Core 的方法体片段，已导入 `Microsoft.AspNetCore.Builder`，`app` 为 `IApplicationBuilder`。

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCompression();
```

连续 IL 指令按所生成的表达式分组。以下片段已导入 `System.Reflection.Emit`，`generator` 为生成 `int → int` 静态方法的 `ILGenerator`，整个序列生成 `return argument + 1;`。

```csharp
generator.Emit(OpCodes.Ldarg_0);
generator.Emit(OpCodes.Ldc_I4_1);
generator.Emit(OpCodes.Add);
generator.Emit(OpCodes.Ret);
```

不同对象的配置分组。以下为方法体片段，`first`、`second` 为 `System.Text.StringBuilder`，`firstText`、`secondText` 为字符串。

```csharp
first.Capacity = firstText.Length;
first.Append(firstText);

second.Capacity = secondText.Length;
second.Append(secondText);
```

简单输入构造与紧接着的执行同组，验证另起一组。以下为 xUnit 方法体片段，已导入 `System.Linq` 和 `Xunit`；代码只展示布局，不是要求新增对 BCL 的测试。不推荐在 `values` 与 `result` 的声明之间再加空行，这会把一个紧密的操作拆得过细。

```csharp
var values = new[] { 1, 2, 3 };
var result = values.Sum();

Assert.Equal(6, result);
```

准备环境与确认其初始状态同组，继续配置环境时另起一组。以下为本仓库 [EditorConfigBuildTest](analysis/analyzers/test/EditorConfigBuildTest.cs) 的方法体片段，位于继承 `EditorConfigFixture` 的测试类中，已导入 `System.IO` 和 `Xunit`；`CreateAsync()` 为环境构造助手。

```csharp
var consumer = await CreateAsync();
Assert.False(File.Exists(consumer.Config));

var localConfig = Path.Combine(Path.GetDirectoryName(consumer.Project), ".editorconfig");
var localContent = "[*.cs]\r\nindent_size = 2\r\n";
File.WriteAllText(localConfig, localContent);
```

同一次操作的相关断言紧接。以下为 xUnit 方法体片段，已导入 `Xunit`；两条断言共同验证“解析成功且得到预期数值”，不应在它们之间再加空行。分析器测试中，同一次分析的成功状态、预期诊断和没有编译错误的检查也可以同组。

```csharp
var input = "42";
var success = int.TryParse(input, out var value);

Assert.True(success);
Assert.Equal(42, value);
```

同一字段的多步验证紧接，不同字段的小组分隔。以下为 xUnit 方法体片段，`values` 为 `Dictionary<string, string>`。

```csharp
Assert.True(values.TryGetValue("Name", out var name));
Assert.Equal("Alice", name);

Assert.True(values.TryGetValue("Code", out var code));
Assert.Equal("A001", code);
```

严格同质的单断言表可以保持紧凑。以下为 xUnit 方法体片段，`values` 为 `Dictionary<string, string>`；各项仅验证字段是否存在。

```csharp
Assert.False(values.ContainsKey("Property01"));
Assert.False(values.ContainsKey("Property02"));
Assert.False(values.ContainsKey("Property03"));
Assert.False(values.ContainsKey("Property04"));
```

下列测试片段不符合分组要求：执行与验证未分隔，两个验证目标也连续混排。这里将获取数量与求和作为两次独立受测执行，断言分别验证它们的结果；`input` 为 `int[]`，`expectedCount`、`expectedSum` 为整数，已导入 `System.Linq` 和 `Xunit`。

```csharp
var values = input.ToArray();
var count = values.Length;
Assert.Equal(expectedCount, count);
var sum = values.Sum();
Assert.Equal(expectedSum, sum);
```

对应分组如下：输入构造与第一次执行同组，两次独立执行与各自验证均分隔：

```csharp
var values = input.ToArray();
var count = values.Length;

Assert.Equal(expectedCount, count);

var sum = values.Sum();

Assert.Equal(expectedSum, sum);
```

<a id="return-spacing"></a>

### 3.7 返回布局

`return` 表达独立的返回阶段时，前面必须留空行；如果只是直接完成紧邻的读取、包装、最终配置或简单状态操作，可以按下表紧凑书写。职责与返回阶段的判断由人工审查，不能仅凭前一句是声明、调用还是赋值自动推断。

| 可以紧凑的末尾步骤 | 限定条件 |
| --- | --- |
| 简单获取后返回同一变量 | 同步或 `await` 获取结果后立即返回该结果，没有插入其他执行阶段。 |
| 获取后包装返回 | 返回值只是对刚获取结果的直接包装，不包含独立处理流程。 |
| 读取元数据后选择回退表达式 | `return` 内部的条件或合并表达式完成当前查询；条件表达式换成多行本身不要求前置空行。 |
| 相关对象创建后返回元组 | 这些对象共同组成同一结果；仅为数量增加不机械切分。 |
| 相关准备声明后 `return new ...` | 声明只准备该返回包装对象的参数，没有独立执行或发布阶段。 |
| 最终配置回调后返回现有对象 | 回调完成当前对象的最终配置，随后直接返回它；此前的其他阶段仍按职责分组。 |
| 单条相关操作或 `out` 设置后返回布尔结果 | 布尔值直接表达该步骤的约定结果，不夹带无关操作。 |
| 资源声明后返回读取结果 | `return` 直接消费该资源，读取在资源释放前完成；异步读取必须在该作用域内等待。 |
| 单条状态修改后 `return this` | 简单修改当前对象后返回自身，不据此豁免多阶段构建。 |

以下情况必须分隔：简单条件返回表之后的独立兜底 `return`；报文填充等执行阶段之后的结果返回；完成锁对象创建、获取与发布到字段等多步操作后的返回。并非所有多步对象构建都一概要求在最终配置回调与返回之间分隔，但也不能把“返回同一对象”作为整个构建流程的广泛豁免。

完整执行块结束后紧接 `return`，继续由 [ZS2003](RULES.zh-Hans.md#zs2003) 要求空行；不能用本节的紧凑例外跨过这个结构边界。其他返回形式及资源语义也不因布局例外改变。

获取后立即返回可以紧凑。以下为返回字符串的异步方法体片段，已导入 `System.IO`，`path` 为字符串，`cancellation` 为 `CancellationToken`。

```csharp
var content = await File.ReadAllTextAsync(path, cancellation);
return content;
```

读取元数据后的条件回退表达式可以紧凑，表达式跨行也不改变这一点。以下为返回字符串的方法体片段，已导入 `System.Reflection` 和 `System.ComponentModel`，`type` 为 `System.Type`。

```csharp
var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description;
return string.IsNullOrWhiteSpace(description) ?
	type.Name :
	description;
```

相关对象创建后返回元组可以紧凑。以下方法返回 `(byte[] Header, byte[] Payload)`，`length` 为非负整数。

```csharp
var header = new byte[4];
var payload = new byte[length];
return (header, payload);
```

相关声明只准备返回包装对象的参数时，可以紧凑。以下方法返回 `Tuple<string, int>`，`name` 为字符串，`count` 为整数。

```csharp
var normalizedName = name.Trim();
var capacity = Math.Max(0, count);
return new Tuple<string, int>(normalizedName, capacity);
```

最终配置回调后返回现有对象可以紧凑。以下方法返回 `System.Text.StringBuilder`，`capacity` 为非负整数，`configure` 为可选 `Action<System.Text.StringBuilder>`。

```csharp
var result = new System.Text.StringBuilder(capacity);
configure?.Invoke(result);
return result;
```

单条 `out` 设置和直接布尔返回可以紧凑。以下为具有 `out int value` 参数的失败路径片段。

```csharp
value = default;
return false;
```

资源声明后直接返回读取结果可以紧凑。以下方法返回字符串，已导入 `System.IO`，`text` 为字符串。

```csharp
using var reader = new StringReader(text);
return reader.ReadToEnd();
```

简单状态修改后返回自身可以紧凑。以下为实例方法体片段，方法返回当前类型，`_enabled` 为私有布尔字段。

```csharp
_enabled = true;
return this;
```

简单条件返回表之后的独立兜底结果必须分隔。以下方法返回非负且小于 `1000` 的整数 `value` 的十进制位数。

```csharp
if(value < 10)
	return 1;
if(value < 100)
	return 2;

return 3;
```

报文准备、填充和结果返回分别分组。以下方法返回 `byte[]`，已导入 `System.Text`，`text` 为字符串；首字节是示例协议标记。

```csharp
var payload = Encoding.UTF8.GetBytes(text);
var packet = new byte[payload.Length + 1];

packet[0] = 1;
payload.CopyTo(packet, 1);

return packet;
```

完成锁对象的创建、获取与发布后，返回结果另成一组。以下方法返回示例类型 `Locker`，它提供 `void Acquire()`；`_locker` 为对象持有的私有字段。此类型仅表示示例职责，不是框架 API。

```csharp
var locker = new Locker();
locker.Acquire();
_locker = locker;

return locker;
```

不符合：执行块后直接返回，不能套用“返回现有对象”的紧凑布局。下面两个片段的方法均返回 `int[]`，`items` 为 `int[]`。

```csharp
foreach(var item in items)
{
	Process(item);
}
return items;
```

符合：结构结束与返回之间留空行。

```csharp
foreach(var item in items)
{
	Process(item);
}

return items;
```

<a id="local-functions"></a>

### 3.8 局部函数布局

- 局部函数必须集中放在所在语句块或语句列表的末尾，后面不得再有同层级的普通语句。内部块保留原作用域，外层方法可以继续执行；所有允许声明局部函数的宿主均适用。
- 主体代码与第一个局部函数之间必须恰好一个空行。局部函数的说明注释与函数组成整体，分隔空行放在说明注释之前；没有前置主体代码时，不额外要求空行。
- 所在列表的全部主体代码仅占一个物理行、组首函数使用表达式体，且没有独立注释行或指令时，可以省略分隔空行；行尾注释不影响此例外。一条语句跨多行不属于单行主体。
- 连续两个表达式体局部函数之间不得有空行；表达式体后接块体函数时，可以紧邻或留一个空行；块体函数之后必须留一个空行。函数说明注释仍与所属函数相邻，不用注释代替分隔空行。
- 无表达式体的 `extern` 局部函数按非表达式体处理。检测、注释边界及安全修复范围见 [ZS2006](RULES.zh-Hans.md#zs2006)。

以下为相互独立的方法声明，放在已导入 `System` 的类型中。

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


<a id="documentation"></a>

## 4. 注释与 API 文档

- 默认使用中文解释领域含义、边界条件、所有权和设计原因；保留项目已有的文档语言。标识符与协议名称保持原文。
- 新增或修改的公共、受保护 API 必须有准确的 XML 文档；接口契约、扩展点和不明显的内部行为也必须说明。私有直观实现不要求机械补齐 `///`。
- 为方法编写 XML 注释时，每个参数必须有名称对应的 `<param>`；返回类型不是 `void` 时必须有 `<returns>`，包括返回 `Task` 或 `ValueTask` 的方法。此要求适用于所有可见性的方法；没有编写 XML 注释的方法是否需要文档，仍按上一条判断。
- 成对 XML 元素 `<xxx>...</xxx>` 的内容只有一行时，开始标签、内容和结束标签必须写在同一行，例如 `/// <summary>一行内容。</summary>`；不得仅为标签拆成三行。此要求适用于 `<summary>`、`<param>`、`<returns>`、`<remarks>` 等元素，真正的多行内容保留多行布局。完整性及排版检测见 [ZS3001](RULES.zh-Hans.md#zs3001)、[ZS3002](RULES.zh-Hans.md#zs3002)、[ZS3003](RULES.zh-Hans.md#zs3003)。
- 引用类型、成员和参数使用 `<see cref="..." />`、`<paramref name="..." />`、`<typeparamref name="..." />`；实现完全继承契约时可用 `<inheritdoc />`，补充实际差异，如果无差异则不需要添加 `<inheritdoc />`。一旦为方法添加 XML 注释，`<inheritdoc />` 或 `<include />` 不替代上述显式参数和返回值元素。
- 可读写属性通常以“获取或设置……”开头；只读属性使用“获取……”；布尔只读属性使用“获取一个值，指示……”。不得为所有只读属性机械添加“一个值”。
- 文档要明确 `null`、空集合、默认值、失败返回、取消、枚举时机、线程安全和资源释放责任中与调用方有关的内容；不得承诺实现未提供的回滚、隔离或自动重试。
- 普通注释通常独占一行。框架现有中文注释多采用 `//说明`，修改时保持局部形式；独立新文件默认采用该形式。完整句子使用相应语言的句末标点，简短标签可省略。
- 连续数行说明可以使用多行 `//`，成段算法解释或许可头可使用 `/* ... */`，不按“两行”机械切换。块注释中间行使用 `*` 对齐。
- 不写复述语句的注释，不保留废弃代码块。注释必须随行为更新；示例必须注明是完整示例还是依赖上下文的片段，并使用真实存在的 API。

<a id="language"></a>

## 5. C# 语言与类型使用

### 5.1 成员访问与变量

- 访问当前实例的属性、方法、事件及公共实例字段时使用 `this.`，如 `this.Name`、`this.OnChanged(args)`；私有字段直接写 `_name`。调用基类实现使用 `base.`。
- 静态成员不使用实例限定；同类型静态辅助方法可直接调用，跨类型调用明确类型名。
- 使用 C# 类型关键字，如 `int`、`long`、`decimal`、`string`、`object`；静态调用也使用 `int.TryParse(...)`、`string.Equals(...)`。
- 局部变量优先使用 `var`，尤其在右侧类型明确时；数值精度、接口视图、空值初始状态或阅读理解需要明确类型时显式声明。
- 使用准确的数值后缀，如 `100U`、`0L`、`0m`、`1f`。不得因类型推断无意改变整数范围、数值精度或运算结果。
- 字段、属性、参数和返回值必须表达其契约类型，不把可明确建模的数据统一装进 `object`、`dynamic` 或字典。
- 初始化后不再赋值的字段默认使用 `readonly`。只读引用不代表被引用集合或对象不可变，也不代表线程安全。

### 5.2 创建对象与现代语法

> 💡 **提示：** 适时升级 .NET 开发环境，优先采用新版的 C# 语法，提升生产力美学。

- 类型可从目标确定时优先使用 `new(...)`；局部变量使用 `var` 时写出构造类型，不重复两侧类型。
- 对象初始化器用于设置可选状态；必需依赖和对象不变量通常由构造函数建立。不得把对象处于无效状态的初始化过程暴露给调用方。
- 集合表达式必须具有目标类型；不得使用 `var values = [1, 2, 3];`。不要将原本惰性枚举的序列改成集合表达式而无意提前枚举和分配。
- 需要比较器、容量或特定构造语义时明确调用构造函数，不为简写丢失这些参数。
- 简单封装可以使用主构造函数。需要明确构造可见性、复杂验证、资源获取或避免参数重复存储时，使用普通构造函数；不得机械转换已有构造函数。
- 仅存储值的属性优先使用自动属性；只在构造时赋值的属性使用只读自动属性。访问器需要验证、通知等额外行为且仍需存储值时，在项目语言版本支持的前提下，优先通过 `field` 使用编译器生成的后备字段；仅在需要独立字段或上述写法无法满足需求时显式声明后备字段。
- 使用模式匹配、`switch` 表达式、`??`、`??=`、`out var`、解构和局部函数简化清晰的逻辑。只服务于当前方法的辅助逻辑可以使用局部函数，无需捕获时优先声明为 `static`。
- `record` 适用于明确需要值相等语义的数据类型；不得仅为减少代码把实体、服务或既有类批量改成 `record`。

```csharp
var index = 0;
var userId = 100U;
var amount = 0m;
int[] values = [1, 2, 3];
IList<string> names = ["Hello", "World"];

Dictionary<string, object> parameters = new(StringComparer.OrdinalIgnoreCase)
{
	["UserId"] = userId,
	["Amount"] = amount,
};
```

### 5.3 字符串、空值与集合

- 短文本拼接优先使用字符串内插；大量循环拼接根据实际规模使用 `StringBuilder` 等适合的实现。日志模板沿用日志 API 的结构化参数机制。
- 代码成员名使用 `nameof`，不手写可由编译器维护的名称；稳定的配置键、协议字段和外部格式不因 CLR 成员改名自动改变。
- 标识符、名称和协议文本按契约明确使用 `StringComparison.Ordinal` 或 `OrdinalIgnoreCase`；集合键使用相应的 `StringComparer`。自然语言排序另按文化规则处理。
- 不通过统一 `ToLower()`／`ToUpper()` 代替比较器；不强制所有键都不区分大小写。文件系统路径尤其需要遵循平台与既有路径契约。
- 序列化、持久化和协议中的时间、数字格式必须明确文化、时区和单位，不依赖进程区域设置。时间类型或含义的变化应纳入兼容性评估。
- 遵循项目 `Nullable` 配置；已启用时准确标注 `?` 并消除实际空值问题，不用 `!` 或关闭警告掩盖问题。未启用时仍需明确空值契约；启用可空引用类型应作为有计划的兼容性迁移。
- `null`、空字符串、空集合、未配置和“使用默认值”并非总是等价；严格遵循当前 API 的区别。新集合 API 默认以空集合表达“没有元素”，但必须保留已有 `null` 语义。
- 集合类型按需要选择：只枚举用 `IEnumerable<T>`，异步流用 `IAsyncEnumerable<T>`，需要计数或索引时用相应只读接口，需要修改时才暴露可写接口。明确返回的是快照还是实时视图。
- 避免无意重复枚举和为单次查询反复 `ToList()`／`ToArray()`；确需固定快照、跨生命周期保存或避免源变化时显式物化。

### 5.4 结构与相等性

- 结构用于小型、具有值语义的数据，默认考虑 `readonly struct`；不能仅为“减少堆分配”把可变服务或大对象改成结构。
- 作为公开值对象参与比较或集合键时，必须使 `IEquatable<T>`、`Equals(object)`、`GetHashCode()`、`==` 和 `!=` 语义一致；可由 `readonly record struct` 正确生成的部分无需重复手写。
- 相等判断和哈希使用相同的字段与比较规则。必须考虑 `default(T)` 状态，不能假设所有结构都经过自定义构造函数。
- 内部游标、临时上下文和 `ref struct` 不要求为形式完整而增加没有业务意义的相等接口和操作符。

<a id="design"></a>

## 6. 设计范式与公共契约

### 6.1 职责与依赖方向

- 行为放在拥有该契约的最窄项目中。Core 承载跨实现的公共语义，Data 承载数据引擎，驱动和第三方 SDK 差异留在相应适配器中。
- 业务消费者面向公共接口；通用契约不得无必要地暴露供应商 SDK 类型、数据库方言或宿主专属状态。
- 插件加载和配置装配归装配层；Web 控制器负责 HTTP 到服务的转换，领域校验和业务操作归服务层。不要在每个控制器或适配器中重写框架已有流程。
- 接口描述稳定能力，基类复用流程和共同状态；可替换策略使用已有 Provider、Factory、Builder、Binder、Visitor、Parser 或 Filter 等扩展点，具体选择由职责决定。
- 不为单一实现需求扩充所有实现都必须遵循的公共接口，也不通过大量空实现、恒定返回或 `NotSupportedException` 伪装共同能力。
- 仅局部使用的辅助类型保持 `private`／`internal`，与宿主类型紧密相关时可嵌套。
- 不得为测试便利性，扩大被测试方法、类型等的作用域。

### 6.2 基类、重载与扩展点

- 公共入口集中处理参数校验、状态检查、上下文创建和流程组织；派生类型实现必要步骤。保持现有 `OnXxx`、`CreateXxx`、`Resolve` 等扩展点名称和调用时序。
- 简单重载转发到完整重载，默认值和行为只能有一份权威实现。泛型／非泛型接口桥接优先使用显式接口实现，避免重复维护流程。
- `abstract` 表示必须由派生类提供的步骤；`virtual` 表示具有默认实现的扩展点。只将有明确派生需求的成员开放为扩展点。
- 可使用 `sealed override` 固定必须保留的流程，再提供更窄的虚方法。不得把所有成员默认改成 `virtual` 或把现有可继承公共类型随意改成 `sealed`。
- 普通抽象基类的显式构造函数使用 `protected`；需要更窄边界时使用 `private protected` 或 `internal`，确有程序集内额外创建需求时才使用 `protected internal`。不要误将后两种组合理解为相同可见性。
- 主构造函数和框架动态实现的数据模型按实际需求处理，不机械要求每个抽象类型都补一个无参构造函数。
- 构造函数负责建立不变量，避免调用可重写成员、启动后台任务或执行隐藏的外部 I/O。重型初始化应有明确阶段与失败清理路径。
- 属性默认表示状态或低成本查询。明显耗时、会失败的外部 I/O 或命令式操作用方法表达；不得新增只写属性，改用有意义的 `SetXxx(...)` 方法。

### 6.3 选项、上下文、事件与集合

- 一组可扩展的可选参数适合使用 `XxxOptions`；一次操作的相关状态适合使用 `XxxContext`。字段很少且没有独立语义时不强行包装。
- 上下文只保存该操作需要的状态，不为一个回调公开整个解析器、连接或可变内部集合。可写成员必须有明确的修改权与有效阶段。
- 配置需要在一次操作期间保持稳定时，按契约创建快照；浅复制、只读属性和只读接口均不保证深层不可变，特别要考虑集合和委托捕获状态。
- 事件描述发生的事实。前置／后置事件默认采用进行时／过去式，如 `Changing`／`Changed`，事件参数以 `EventArgs` 结尾；通过 `OnXxx` 方法集中触发。
- 前置事件是否可取消、回调是否异步、异常是否传播、失败是否回滚、是否允许重入，都必须由契约明确。不得假设事件名称自带这些保证。
- 单次操作的局部通知可以使用选项中的 `Action<TContext>`／`Func<...>` 回调，不必升级成全局事件或公共管线。
- 具名集合优先复用项目现有集合或 `KeyedCollection<TKey, TItem>`；比较器、重复键、默认项、枚举顺序和可变性均属于行为契约。

### 6.4 插件、服务与配置装配

本节适用于使用 Zongsoft 装配机制的项目；普通 .NET 应用不必为遵守本规范引入插件运行时。

- 宿主和装配层负责选择具体实现，业务消费者依赖公共契约。按组件职责选择构造注入、属性注入、模块容器或应用容器解析；纯领域算法保持依赖明确。
- 服务注册、模块归属、服务别名、具名实例和连接名称是不同概念。名称解析、匹配和默认回退应具有明确且稳定的语义。
- 服务生命周期与所属容器一致；模块容器和请求作用域应分别设计。共享实例的消费者不负责释放实例，共享状态应满足相应线程安全要求。
- 插件、配置、映射和部署清单共同组成运行契约，应与公开类型、资源及使用文档保持一致。节点名称、大小写、顺序和依赖关系均可能影响装配行为。
- 编译依赖、部署产物、插件加载和实际运行能力分别承担不同职责；程序集可引用或插件可发现，不代表依赖、配置和服务调用均可用。

### 6.5 兼容性

- 公共类型、成员签名、泛型约束、参数名、可选参数默认值、枚举数值和异常类型都属于兼容契约。
- 配置键、序列化字段、文本语法、路由、分页、事件顺序、取消、资源释放、集合顺序和名称回退也属于兼容契约。
- 公共契约变化应兼顾直接实现、派生类、调用方、反射使用和相关产物。新增接口成员或重载要评估实现负担及重载解析变化。
- 公共 `const` 值可能嵌入调用方程序集；会随版本变化的默认值应评估使用 `static readonly` 或属性，替换已发布成员时必须评估二进制和源码兼容性。
- 不扩大容错范围掩盖配置错误，也不借重构收紧已承诺的合法输入。破坏性变更应有明确的版本安排、影响说明和迁移方案。

<a id="asynchrony"></a>

## 7. 异步、取消、异常与资源

### 7.1 异步与取消

- 保持既有 `Task`／`ValueTask` 契约。新 API 依同族接口保持一致；没有既有约束时默认用 `Task`，有同步完成、池化等明确收益且消费方式合适时才选用 `ValueTask`。
- `ValueTask` 默认只消费一次；不得重复等待、重复调用 `AsTask()`，或混用两种消费方式。确需共享或组合时只转换一次，并保存转换后的 `Task`。
- 异步方法默认以 `CancellationToken cancellation = default` 作为最后的普通参数；实现既有接口、重写方法或存在 `params` 等语法限制时遵循实际签名。
- 将取消令牌传递给支持取消的下游 I/O、等待和异步枚举。长时间 CPU 循环应在合理位置响应取消；不在每条简单语句前重复检查。
- 异步路径不得使用 `.Result`、`.Wait()` 或 `.GetAwaiter().GetResult()` 同步阻塞。已有同步兼容入口应集中维护，不复制到新流程；`ConfigureAwait(false)` 不能消除同步阻塞本身的风险。
- 不使用 `Task.Run` 包装同步 I/O 来伪造异步接口。除必须匹配签名的事件处理器外，不新增 `async void`。
- 不随意丢弃任务。后台操作必须有生命周期所有者，负责观察异常、取消和停止时等待；写 `_ = ...` 本身不能完成这些责任。
- 纯转发可以直接返回任务；需要 `try/catch/finally`、资源作用域或完成后操作时，必须在相应作用域内 `await`。不得返回尚未完成的任务后提前释放其依赖资源。
- `ConfigureAwait(false)` 适用于不依赖调用上下文的库代码，是否使用由当前组件的上下文契约决定。

### 7.2 参数与异常

- 在公共边界验证必需参数和对象状态；内部流程可依赖已建立的不变量，不层层重复相同校验。
- 使用合适的标准异常或已有领域异常。新代码可用 `ArgumentNullException.ThrowIfNull` 等简洁校验；必须保持正确参数名及项目支持范围。
- `TryXxx` 用 `false` 表达约定范围内的预期失败，并明确输出值；不能通过 `catch(Exception)` 把取消、I/O 故障和编程错误全部变成“未找到”。
- 只在能够恢复、转换边界异常或承担顶层处理职责时捕获。重新抛出使用 `throw;`，包装异常保留内部异常；不吞掉取消和后台异常。
- 错误日志放在能够负责处理的边界，避免每层重复记录同一异常；用户可见文本和异常消息遵循 7.4 节的本地化约定。
- 日志、注释、示例和测试快照不得包含真实密钥、令牌、私钥、完整连接字符串或敏感业务负载。

### 7.3 所有权与并发

> ⚠️ **并发边界：** 只读引用、线程安全的容器与原子赋值各有适用范围，不能替代对象完整的生命周期设计。

- 创建或取得流、连接、客户端、订阅、计时器、锁租约和缓存条目时，必须明确谁拥有、何时移交、何时释放。不能仅凭“由我获取”判断归我释放。
- 自己拥有的资源使用 `using`／`await using` 或 `try/finally` 可靠释放。沿用项目偏好的 `using(...)` 块；`using var` 仅在资源确应存活到当前作用域末尾时使用。
- `Dispose`／`DisposeAsync` 的重复调用、操作与释放竞争及部分初始化失败必须有明确处理；不为普通托管对象添加终结器，不把同步与异步释放链重复执行。
- 订阅必须有取消订阅路径；池化对象和缓冲区用完归还。不得在资源归还、流关闭或所有者释放后继续引用其内存或惰性枚举。
- 共享状态必须考虑并发和重入。`volatile` 不保证复合操作原子性，`ConcurrentDictionary` 不保证其值对象线程安全，`??=` 不保证只创建一次实例。
- `GetOrAdd` 工厂在竞争时可能多次执行；创建连接、注册回调等有副作用的工厂必须考虑重复创建和失败清理，不能把容器线程安全等同于生命周期安全。
- 锁定私有同步对象，不锁 `this`、字符串或公开对象。异步等待使用适合的异步同步机制，不在持锁区执行不可控回调或长时间 I/O。
- 使用 `Interlocked`、信号量或状态机时，先说明被保护的不变量与状态转换；不因一次串行运行成功就宣称线程安全。
- 热路径优化先确认复杂度、分配和锁竞争，再以测量支持结论。`Span<T>`、池化、反射缓存和 `unsafe` 必须服从实际需求及生命周期约束，不作为默认模板。

### 7.4 文本本地化与强类型资源

- 测试项目（`IsTestProject=true`）不启用 ZS1301、ZS1302、ZS1304；此例外不适用于被测试的生产项目。
- 非测试项目的异常消息要求本地化；所有手写字符串中出现的非 ASCII 文字也要求本地化，不按界面、日志、路径或协议等用途豁免。普通英文提示不因 Console 或 Localizable 特性而强制检查。将需要本地化的文本集中放入 `.resx`。
- 资源键以 `.` 分隔，不使用 `_` 或 `-`；异常消息资源键以 `.Message` 结尾。内容含义相同的消息优先合并复用，Designer 生成的属性名由工具转换。
- 默认资源文件提供完整的回退文本，按需添加 `Resources.zh-Hans.resx` 等区域性资源文件。资源键保持稳定，各语言版本使用相同的键和格式占位符；不要拼接多个翻译片段组成完整句子。
- 默认 `.resx` 的自定义工具必须设置为 `ResXFileCodeGenerator`，由工具生成对应的 `*.Designer.cs` 强类型访问器。修改资源后重新运行自定义工具，并将资源文件和生成文件一并提交；不要手工维护生成属性。区域性 `.resx` 只保存翻译，不重复生成访问器。
- 在业务代码中通过生成属性访问资源，如 `Properties.Resources.InvalidValue`。不得使用字符串资源键直接调用 `ResourceManager.GetString`、`GetObject`、`GetStream`，也不要封装一层固定键查询来绕过强类型访问。
- 需要插入参数时，在资源中保存完整格式模板，再通过 `string.Format(CultureInfo.CurrentCulture, Properties.Resources.InvalidValue, value)` 等方式格式化。资源语言选择遵循 UI 区域性，数字、日期等呈现遵循所需的格式区域性。
- 非 ASCII 文字检查排除 Emoji／Icon、标点、普通数学符号和控制字符本身；混合文字仍检查。异常消息中的数字、标点、Emoji、ANSI 和图案均不额外豁免，只有 null、空串和纯空白消息不报告。
- 检测异常类型及 `string message` 形参，只检查直接文本、拼接／插值片段和 string.Format 模板，不追踪变量、工厂或构造链，不猜测 reason 等参数。未告警不等于已本地化。
- 通用资源基础设施确需按动态键解析时，应集中封装。Roslyn 的 `LocalizableResourceString` 等要求延迟选择语言的 API，可以使用 `nameof(生成的资源属性)` 和生成类的 `ResourceManager`，保留资源键的编译期关联；普通消息仍优先直接读取生成属性。

> 🛠️ **检测与配置：** [ZS1301](RULES.zh-Hans.md#zs1301) 检查异常消息，[ZS1302](RULES.zh-Hans.md#zs1302) 检查非 ASCII 文字，[ZS1304](RULES.zh-Hans.md#zs1304) 检查固定资源键的访问；项目 XML 配置和 Designer 生成步骤见 [资源生成配置](RULES.zh-Hans.md#resource-generation)。

以下为参数校验片段，假定默认资源已定义 `InvalidValue`，并由工具生成对应属性：

```csharp
if(value < 0)
	throw new ArgumentOutOfRangeException(nameof(value), Properties.Resources.InvalidValue);
```

<a id="quality"></a>

## 8. 项目配置与质量

### 8.1 构建与依赖

- SDK、目标框架、语言版本及可空配置由项目和共享构建配置统一定义；多目标项目的代码与依赖必须兼容各目标框架。
- 包版本优先服从集中管理，`VersionOverride` 用于有明确理由的项目级差异。依赖升级应评估传递依赖和兼容性，避免通过增加直接引用或抑制警告掩盖冲突。
- Debug 与 Release 应明确各自的引用来源和构建行为；本地程序集、项目引用和 NuGet 包不能视为天然等价。

### 8.2 测试与文档

- 测试验证可观察的契约，覆盖关键成功、失败和边界情形；缺陷修复应有能复现原始问题的回归用例。
- 测试不镜像私有实现，不为测试便利增加生产开关或扩大公共 API。涉及并发、取消和释放时，应覆盖相应的生命周期行为。
- 集成测试与单元测试明确分离；环境、外部依赖和数据清理范围应可配置、可隔离，不依赖生产服务或共享业务数据。
- 性能结论以可复现的测量为依据，说明负载、运行环境和比较基线。
- 文档、代码示例和配置示例属于交付质量的一部分；示例应准确、可验证，链接有效，编码和格式符合约定。

### 8.3 自动化规范检查

- 使用配套 `.editorconfig` 和 `Zongsoft.CodeAnalysis` NuGet 包，项目只保留有意的本地覆盖。接入、自动同步及代码只读检查方式见 [🛠️ 代码规范检查](README.zh-Hans.md#code-analysis)。
- 诊断级别、检测边界与自动修复能力见 [📋 规则列表](RULES.zh-Hans.md#index)；VS 中的自定义诊断帮助链接指向对应规则。自动修复后检查差异，设计、兼容性及资源所有权继续人工审查。
- 尚未自动覆盖的要求见 [人工审查清单](RULES.zh-Hans.md#review)。

<a id="references"></a>

## 9. 依据与参考

### 9.1 设计范式的实践参考

第 6 节的职责划分、扩展点和服务装配，可以结合框架中的实际实现进一步理解。以下选取三个典型场景，说明阅读源码时值得关注的设计关系，便于按当前问题选择参考。

**公共入口与扩展点如何配合。** 第 6.2 节强调将公共流程集中维护，再将具体步骤交给派生类型。以 [泛型处理器基类](https://github.com/Zongsoft/framework/blob/main/Zongsoft.Core/src/Components/HandlerBase%601.cs) 为例，可以沿着 `HandleAsync` 到 `OnHandleAsync` 的调用关系，观察重载转发、泛型与非泛型接口桥接，以及参数转换与具体处理的职责划分。

**公共抽象与具体实现如何分工。** 第 6.1 节要求行为归属于拥有相应契约的层次。对照 Core 中的 [数据访问提供者基类](https://github.com/Zongsoft/framework/blob/main/Zongsoft.Core/src/Data/DataAccessProviderBase.cs) 与 Data 中的 [具体提供者](https://github.com/Zongsoft/framework/blob/main/Zongsoft.Data/src/DataAccessProvider.cs)，可以看到访问器获取、名称处理和缓存复用由基类组织，而具体访问器的创建与注入通过 `CreateAccessor` 扩展点完成。这种分工有助于判断哪些行为适合复用，哪些应留在具体实现中。

**服务契约如何连接到运行时实例。** 第 6.4 节将服务消费与装配职责分开。结合 [服务注册](https://github.com/Zongsoft/framework/blob/main/Zongsoft.Core/src/Services/ServiceCollectionExtension.cs) 和 [具名服务定位](https://github.com/Zongsoft/framework/blob/main/Zongsoft.Core/src/Services/ServiceLocator.cs) 的实现，可以理解服务契约、注册信息与提供者如何协作，也能看清服务别名、具名实例和提供者名称各自参与解析的位置。

> 💡 **阅读重点：** 关注职责、调用关系与扩展边界。源码链接指向框架主分支，具体行为以所用版本为准；历史实现中的个别写法不替代本文的编码要求。

### 9.2 .NET 官方参考

这些资料解释语言和 API 语义。其空格、缩进、`this.`、字段前缀等格式偏好不能覆盖本规范的 Zongsoft 约定。

- [Microsoft .NET/C# 代码约定](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)。
- [类型、接口、泛型参数及委托命名](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-classes-structs-and-interfaces)：用于核对第 2 节的通用命名规则。
- [成员与事件命名](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-type-members)：用于核对属性、方法与事件命名。
- [C# 集合表达式](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions)：用于核对目标类型和立即物化语义，使用时注意文档中标明的版本范围。
- [ValueTask 消费约束](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask)：用于核对第 7 节的一次消费规则。
- [dotnet/runtime C# Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)：作为外部风格参考，不直接套用其与 Zongsoft 不同的约定。
