# .NET 开发规范(Ver 0.0.1)

[TOC]

## 参考资料：
- [C#编码约定](https://docs.microsoft.com/zh-cn/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- [Framework Design Guidelines](https://docs.microsoft.com/zh-cn/dotnet/standard/design-guidelines/)

## 命名规范

### 大小写命名
- PascalCasing
	帕斯卡命名法：该命名法又被称为“*大驼峰命名法*”，每个单词的首字母均大写。
- camelCasing
	驼峰命名法：除第一个单词外，其他单词的首字母均大写。

1. 使用 PascalCasing 命名法为所有公共成员命名，以及所有类型名、命名空间、接口、枚举(含枚举项)、属性、方法、事件。
注意：接口必须使用大写字母'I'打头。

2. 使用 camelCasing 命名法为私有字段、变量、参数命名，对于私有字段以下划线打头。

示例：
```csharp
namespace Zongsoft.Data
{
	/// <summary>
	/// 提供数据服务的类。
	/// </summary>
	public class DataService<TEntity> : IDataService
	{
		#region 成员字段
		private string _name;
		private Zongsoft.Services.IServiceProvider _serviceProvider;
		#endregion

		#region 构造函数
		protected DataService(string name, Zongsoft.Services.IServiceProvider serviceProvider)
		{
			if(string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException(nameof(name));
			if(serviceProvider == null)
				throw new ArgumentNullException(nameof(serviceProvider));

			_name = name.Trim();
			_serviceProvider = serviceProvider;
		}
		#endregion

		#region 公共属性
		public string Name
		{
			get;
		}
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

### 私有常量命名
私有常量所有字符均为大写，单词或词组中间使用下划线连接，如果同一个类型中有不同种类的私有常量，则不同种类的私有常量应使用特定种类名作为前缀或后缀，前缀或后缀与常量之间亦使用下划线连接。

示例：
```csharp
internal static class MembershipHelper
{
	internal const string DATA_CONTAINER_NAME = "Security";

	internal const string DATA_COMMAND_GETROLES = DATA_CONTAINER_NAME + ".GetRoles";
	internal const string DATA_COMMAND_GETMEMBERS = DATA_CONTAINER_NAME + ".GetMembers";

	internal const string DATA_ENTITY_USER = DATA_CONTAINER_NAME + ".User";
	internal const string DATA_ENTITY_ROLE = DATA_CONTAINER_NAME + ".Role";
	internal const string DATA_ENTITY_MEMBER = DATA_CONTAINER_NAME + ".Member";
	internal const string DATA_ENTITY_PERMISSION = DATA_CONTAINER_NAME + ".Permission";
	internal const string DATA_ENTITY_PERMISSION_FILTER = DATA_CONTAINER_NAME + ".PermissionFilter";
}
```

### 缩写
避免使用单词或词组的缩写，除非该缩写是众所周知的行业标准（`IO`、`Xml`、`Html`之类）。

### 抽象类
所有抽象类的命名一律以`Base`作为名称的后缀，譬如：`DataAccessBase`、`NamedCollectionBase`。

### 其他规则
- 命名空间应尽量使用单词复数，譬如：`System.Collections`、`Zongsoft.Services`、`Zongsoft.Options`，对于某些例外应遵循.NET框架现有命名约定，譬如：`System.IO`、`System.Data`、`System.Net.Http`。

- 命名空间的组织结构：`<Organization>.(<ProductFamily>|<Technology>).(<Product>|<Feature>|<Subnamespace>)`，譬如：`Zongsoft.Data.MySql`、`Automao.Common.Models`

- 特定类型的命名规范参考微软.NET框架设计规范中的约定，譬如异常类命名必须以`Exception`结尾；特性(Attribute)类必须以`Attribute`结尾；委托类必须以`Delegate`结尾等。
