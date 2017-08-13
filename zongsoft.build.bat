@echo off

SET msbuild="C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
SET current=%cd%

setlocal EnableDelayedExpansion

SET proj_1="%current%\Zongsoft.CoreLibrary\src\Zongsoft.CoreLibrary.sln"
SET proj_2="%current%\Zongsoft.Net\src\Zongsoft.Net.sln"
SET proj_3="%current%\Zongsoft.Web\src\Zongsoft.Web.sln"
SET proj_4="%current%\Zongsoft.Commands\src\Zongsoft.Commands.sln"
SET proj_5="%current%\Zongsoft.Plugins\src\Zongsoft.Plugins.sln"
SET proj_6="%current%\Zongsoft.Plugins.Web\src\Zongsoft.Plugins.Web.sln"
SET proj_7="%current%\Zongsoft.Security\src\Zongsoft.Security.sln"
SET proj_8="%current%\Zongsoft.Security.Web\src\Zongsoft.Security.Web.sln"
SET proj_9="%current%\Zongsoft.Externals.Json\src\Zongsoft.Externals.Json.sln"
SET proj_10="%current%\Zongsoft.Externals.Redis\src\Zongsoft.Externals.Redis.sln"
SET proj_11="%current%\Zongsoft.Externals.Aliyun\src\Zongsoft.Externals.Aliyun.sln"
SET proj_12="%current%\Zongsoft.Externals.Juhe\src\Zongsoft.Externals.Juhe.sln"

for /L %%i in (1,1,12) do (
	if exist !proj_%%i! (
		@echo [%%i] !proj_%%i!
		%msbuild% !proj_%%i! /t:rebuild /clp:ErrorsOnly,PerformanceSummary,NoSummary /v:minimal

		REM if errorlevel 1 @echo The !proj_%%i! file compile ERROR!!!  & pause > null

		if errorlevel 1 (
			choice /T 30 /D y /C ny /M "!proj_%%i! 项目编译出错，是否要退出？"

			if errorlevel 2 goto EXIT
		)
	) else @echo The !proj_%%i! file is not exists.
)

:EXIT
