# .NET 开发规范(Ver 0.0.1)

[TOC]

## 参考资料：
- [C#编码约定](https://docs.microsoft.com/zh-cn/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- [Framework Design Guidelines](https://docs.microsoft.com/zh-cn/dotnet/standard/design-guidelines/)


## 原则
- 在尊重 .NET/C# 普遍的开发习惯和传统的基础上，尽量采用更简洁的编码风格。


## 命名规范

### 大小写命名
- PascalCasing
	帕斯卡命名法：该命名法又被称为“*大驼峰命名法*”，每个单词的首字母均大写。
- camelCasing
	驼峰命名法：除第一个单词外，其他单词的首字母均大写。

1. 使用 PascalCasing 命名法为所有公共成员命名，以及所有类型名、命名空间、接口、枚举(含枚举项)、属性、方法、事件。
注意：接口必须使用大写字母 `I` 打头。

2. 使用 camelCasing 命名法为私有字段、变量、参数命名，对于私有字段以下划线(`_`)打头。

#### 示例：
```csharp
namespace Zongsoft.Data
{
	/// <summary>
	/// 提供数据服务的基类。
	/// </summary>
	public abstract class DataServiceBase<TEntity> : IDataService
	{
		#region 成员字段
		private Zongsoft.Services.IServiceProvider _serviceProvider;
		#endregion

		#region 构造函数
		protected DataServiceBase(string name, Zongsoft.Services.IServiceProvider serviceProvider)
		{
			if(string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException(nameof(name));

			this.Name = name;
			_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		}
		#endregion

		#region 公共属性
		public string Name { get; }
		#endregion

		#region 公共方法
		public TEntity Get<TKey>(TKey key)
		{
			throw new NotImplementedException();
		}
		#endregion
	}
}
```

### 常量命名
私有常量所有字符均为大写，单词或词组中间使用下划线连接，如果同一个类型中有不同种类的私有常量，则不同种类的私有常量应使用特定种类名作为前缀或后缀，前缀或后缀与常量之间亦使用下划线连接。

*注：共有常量遵循公共成员命名规则。*

#### 示例：
```csharp
internal static class MembershipHelper
{
	#region 常量定义
	internal const string DATA_CONTAINER_NAME           = "Security";

	internal const string DATA_ENTITY_USER              = DATA_CONTAINER_NAME + ".User";
	internal const string DATA_ENTITY_ROLE              = DATA_CONTAINER_NAME + ".Role";
	internal const string DATA_ENTITY_MEMBER            = DATA_CONTAINER_NAME + ".Member";
	internal const string DATA_ENTITY_PERMISSION        = DATA_CONTAINER_NAME + ".Permission";
	internal const string DATA_ENTITY_PERMISSION_FILTER = DATA_CONTAINER_NAME + ".PermissionFilter";
	#endregion
}
```

### 缩写
避免使用单词或词组的缩写，除非该缩写是众所周知的行业标准，譬如：`IO`、`Xml`、`Html`之类，多于两个字符的缩写采用 Pascal 命名，避免使用全大写，譬如不要使用 `XML`、`HTML`、`XAML` 等。

### 抽象类
- 所有抽象类的命名一律以`Base`作为名称的后缀，譬如：`DataAccessBase`、`NamedCollectionBase`。
- 抽象类必须显式申明其构造函数作用域为 `protected` 或 `internal protected`。

### 静态类
- 不要滥用静态类，以免破坏OOP面向对象设计范式。
- 工具类为静态类，工具类命名一般以 `Utility` 或 `Helper` 结尾。
- 扩展静态类命名一般以 `Extension` 结尾。

### 结构
- 尽量定义成只读结构。
- 务必实现 `IEquatable<T>` 接口并重写 `==` 和 `!=` 两个符号。

### 元素命名
- 接口、类、结构、属性、字段、变量命名为名词或形容词。
- 方法命名采用动词或动词+名称形式。
- 事件命名采用进行时和过去式形式，前置事件采用进行时，后置事件采用过去式。
- 用 `EventArgs` 后缀命名事件参数类。


### 其他规则
- 命名空间应尽量使用单词复数，譬如：`System.Collections`、`Zongsoft.Services`、`Zongsoft.Options`，对于某些例外应遵循.NET框架现有命名约定，譬如：`System.IO`、`System.Data`、`System.Net.Http`、`System.Configuration`。

- 命名空间的组织结构：`<Organization>.(<ProductFamily>|<Technology>).(<Product>|<Feature>|<Subnamespace>)`，譬如：`Zongsoft.Data.MySql`、`Automao.Common.Models`

- 特定类型的命名规范参考微软.NET框架设计规范中的约定，譬如异常类命名必须以`Exception`结尾；注解/特性(Attribute)类必须以`Attribute`结尾；委托类必须以`Delegate`结尾等。

## 布局约定
- 代码编辑器必须采用等宽字体，推荐：`Courier New`、`Consolas` 字体。
- 采用Tab制表符缩进，每个制表符占4个字符宽度。
- 每行只写一条语句。
- 每行只写一个声明。
- 采用 `#region` 和 `#endregion` 对代码块进行分段，段间采用一个空行进行分隔，`#region` 与内部首行代码之间不要加空行，`#endregion` 与内部最末代码行之间不要加空行。

### 命名空间
- 按顶级命名空间导入进行分段。
- 段内的命名空间按长度进行排列。
- 确保系统命名空间 `System` 段位于首段，而末段为代码所属命名的引用区。

#### 示例：
```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Zongsoft.Web;
using Zongsoft.Web.Http.Headers;
using Zongsoft.Data;
using Zongsoft.Services;
using Zongsoft.Security.Membership;

using Automao.Common.Models;
using Automao.Common.Services;

namespace Automao.Common.Web.Controllers
{
	[Authorize]
	[ApiController]
	[Route("Employees")]
	public class EmployeeController : ApiControllerBase<Employee, EmployeeService>
	{
	}
}
```

### 代码分段
- 对于接口、类、结构内的代码按类型和作用域进行分段顺序处理。

```csharp
public class Foo
{
	#region 常量定义
	#endregion

	#region 单例字段
	#endregion

	#region 事件声明
	#endregion

	#region 成员字段
	#endregion

	#region 公共属性
	#endregion

	#region 内部属性
	#endregion

	#region 保护属性
	#endregion

	#region 重写属性
	#endregion

	#region 公共方法
	#endregion

	#region 内部方法
	#endregion

	#region 保护方法
	#endregion

	#region 重写方法
	#endregion

	#region 静态方法
	#endregion

	#region 符号重载
	#endregion

	#region 显式实现
	#endregion
}
```


## 注释约定

- 将注释放在单独的行上，而非代码行的末尾。
- 以句号结束注释文本（中文注释为中文句号，英文注释则为英文句点符）。
- 在注释分隔符 `//` 与注释文本之间插入一个空格。
- 超过两行的注释应采用 `/*` 和 `*/` 多行注释，中间行以 `*` 作为对齐符，并且与注释文本之间插入一个空格。
- 对所有公共成员或非公共的类、接口、结构、枚举、委托、事件、方法、属性采用 `///` 注释。
- 对于属性的注释必须以 `获取或设置` 开头，如果是只读属性则以 `获取一个值，` 开头。
> 注意：不要定义只写属性，应将其定义成 `Set...(...)` 方法。

#### 示例
```csharp
/// <summary>
/// 表示机器设备的业务实体类。
/// </summary>
public abstract class Machine
{
	#region 普通属性
	/// <summary>获取或设置机器编号。</summary>
	public abstract uint MachineId { get; set; }

	/// <summary>获取或设置机器代号。</summary>
	public abstract string MachineNo { get; set; }

	/// <summary>获取或设置机器号码（内部号码）。</summary>
	public abstract string MachineCode { get; set; }

	/// <summary>获取或设置条码编号。</summary>
	public abstract string Barcode { get; set; }

	/// <summary>获取或设置是否可用。</summary>
	public abstract bool Enabled { get; set; }

	/// <summary>获取或设置是否可见。</summary>
	public abstract bool Visible { get; set; }
	#endregion

	#region 计算属性
	/// <summary>获取一个值，指示当前机器是否处于报警状态。</summary>
	public bool IsFaulted { get => this.Fault > 0 ? true : false; }

	/// <summary>获取一个值，指示当前机器是否处于有效期限。</summary>
	public bool IsValidity { get => this.Enabled && this.Visible && this.StartTime <= DateTime.Now && this.FinalTime >= DateTime.Now; }
	#endregion
}
```

## 语言准则

- 使用字符串内插来连接短字符串，如下面的代码所示。
```csharp
var employees = this.DataAccess.Select<Employee>(
	Condition.Equal(nameof(Employee.UserId), userId),
	$"{nameof(Employee.UserId)}," +
	$"{nameof(Employee.EmployeeNo)}," +
	$"{nameof(Employee.FullName)},"
)
```

- 当变量类型明显来自赋值的右侧时，请对本地变量进行隐式类型化。
- 对于非`int`和`double`类型的数值类型的变量初始化，可通过数值常量说明符进行显式申明。
```csharp
var timestamp = DateTime.Now;
var name = "Popeye Zhong";
var userId = 100U;
var index = 0;
var amount = 0m;
var input = Console.ReadLine();
```

- 使用对象实例化的简洁形式。
```csharp
Employee employee = new();
```

- 使用对象初始值设定项简化对象创建。
```csharp
var array1 = new[] { 1, 2, 3 };
var array2 = new[] { "Hello", "World" };

var employee = new Employee
{
	Name = "Popeye Zhong",
	Gender = Gender.Male,
	Location = "Redmond",
};

var dictionary = new Dictionary<string, string>()
{
	{ "Key1", "Value#1" },
	{ "Key2", "Value#2" },
}
```
