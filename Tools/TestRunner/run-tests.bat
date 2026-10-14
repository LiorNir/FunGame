@echo off
rem Builds the pure game code and its EditMode tests with the Unity editor's own compiler and runs them (see README.md).
setlocal enabledelayedexpansion

if "%UNITY_DATA%"=="" set "UNITY_DATA=C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data"
set "MONO=%UNITY_DATA%\MonoBleedingEdge\lib\mono\4.7.1-api"
set "DOTNET=%UNITY_DATA%\NetCoreRuntime\dotnet.exe"
if not exist "%DOTNET%" (
    echo Could not find Unity at "%UNITY_DATA%". Set UNITY_DATA to the editor's Data folder.
    exit /b 1
)

cd /d "%~dp0\..\.."
if not exist "Tools\TestRunner\bin" mkdir "Tools\TestRunner\bin"

set FILES=
for %%f in (Assets\Scripts\Board\*.cs Assets\Scripts\Board\Battle\*.cs Assets\Scripts\Board\Sim\*.cs Assets\Scripts\Board\Progress\*.cs Assets\Tests\EditMode\*.cs Tools\TestRunner\Runner.cs) do set FILES=!FILES! "%%f"

"%DOTNET%" "%UNITY_DATA%\DotNetSdkRoslyn\csc.dll" -nologo -target:exe -out:Tools\TestRunner\bin\tests.dll -langversion:9 -nowarn:CS1701,CS1702,CS8632 ^
  -r:"%MONO%\mscorlib.dll" -r:"%MONO%\System.dll" -r:"%MONO%\System.Core.dll" -r:"%MONO%\Facades\netstandard.dll" -r:"%MONO%\Facades\System.Runtime.dll" ^
  -r:"%MONO%\Facades\System.Collections.dll" -r:"%MONO%\Facades\System.Linq.dll" -r:"%MONO%\Facades\System.Reflection.dll" ^
  -r:"%MONO%\Facades\System.Threading.Tasks.Parallel.dll" -r:"%MONO%\Facades\System.Threading.Tasks.dll" -r:"%MONO%\Facades\System.Threading.dll" ^
  -r:"%MONO%\Facades\System.IO.dll" -r:"%MONO%\Facades\System.Text.Encoding.dll" -r:"%MONO%\Facades\System.Runtime.Extensions.dll" -r:"%MONO%\Facades\System.Globalization.dll" ^
  !FILES!
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

copy /y "Tools\BalanceLab\lab.runtimeconfig.json" "Tools\TestRunner\bin\tests.runtimeconfig.json" >nul
"%DOTNET%" Tools\TestRunner\bin\tests.dll
