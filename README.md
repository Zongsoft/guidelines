
## 关于编译

建议在 `Zongsoft` 目录中创建一个“快捷方式”，其指向 `Guidelines` 子目录中的 `zongsoft.build.bat` 脚本文件，方便批量编译。

**注意：** 如果是首次编译，因为 MSBuild 没有自动更新项目文件中的 Nuget 引用导致编译失败，所以首次编译请使用 Visual Studio 2018+ 编译，之后就可以使用脚本进行编译了。
