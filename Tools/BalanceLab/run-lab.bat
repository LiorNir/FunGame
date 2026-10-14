@echo off
rem Builds and runs the balance lab with the compiler and runtime that ship with the Unity editor (no .NET SDK needed).
rem   run-lab.bat report                      full report (see README.md)
rem   run-lab.bat report --runs 200           more battles per power step
rem   run-lab.bat report --builtin            use the built-in sample encounters
rem   run-lab.bat quick 50                    short console table
rem Set UNITY_DATA to the editor's Data folder if Unity is not installed at the default place.
setlocal enabledelayedexpansion

if "%UNITY_DATA%"=="" set "UNITY_DATA=C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data"
set "MONO=%UNITY_DATA%\MonoBleedingEdge\lib\mono\4.7.1-api"
set "DOTNET=%UNITY_DATA%\NetCoreRuntime\dotnet.exe"

if not exist "%DOTNET%" (
    echo Could not find Unity at "%UNITY_DATA%". Set UNITY_DATA to the editor's Data folder.
    exit /b 1
)

cd /d "%~dp0\..\.."
if not exist "Tools\BalanceLab\bin" mkdir "Tools\BalanceLab\bin"

set FILES=
for %%f in (Assets\Scripts\Board\*.cs Assets\Scripts\Board\Battle\*.cs Assets\Scripts\Board\Sim\*.cs Assets\Scripts\Board\Progress\*.cs Tools\BalanceLab\*.cs) do set FILES=!FILES! "%%f"

"%DOTNET%" "%UNITY_DATA%\DotNetSdkRoslyn\csc.dll" -nologo -optimize+ -target:exe -out:Tools\BalanceLab\bin\lab.dll -langversion:9 -nowarn:CS1701,CS1702,CS8632 ^
  -r:"%MONO%\mscorlib.dll" -r:"%MONO%\System.dll" -r:"%MONO%\System.Core.dll" -r:"%MONO%\Facades\netstandard.dll" -r:"%MONO%\Facades\System.Runtime.dll" ^
  -r:"%MONO%\Facades\System.Collections.dll" -r:"%MONO%\Facades\System.Linq.dll" -r:"%MONO%\Facades\System.Threading.Tasks.Parallel.dll" -r:"%MONO%\Facades\System.Threading.Tasks.dll" ^
  -r:"%MONO%\Facades\System.Reflection.dll" -r:"%MONO%\Facades\System.IO.dll" -r:"%MONO%\Facades\System.Diagnostics.Debug.dll" -r:"%MONO%\Facades\System.Text.Encoding.dll" ^
  -r:"%MONO%\Facades\System.Threading.dll" -r:"%MONO%\Facades\System.Runtime.Extensions.dll" -r:"%MONO%\Facades\System.Globalization.dll" ^
  !FILES!
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

copy /y "Tools\BalanceLab\lab.runtimeconfig.json" "Tools\BalanceLab\bin\lab.runtimeconfig.json" >nul
"%DOTNET%" Tools\BalanceLab\bin\lab.dll %*
