@echo off
REM %~dp0 obtiene la ruta de la carpeta actual (donde esta este script y el juego)
cd /d "%~dp0CustomTravelerEngine"

echo Compilando el proyecto CustomTravelerEngine...
"C:\dotnet6\dotnet.exe" build

echo Copiando el archivo generado...
copy "%~dp0CustomTravelerEngine\bin\Debug\net6.0\CustomTravelerEngine.dll" "%~dp0Mods\CustomTravelerEngine.dll"

echo ¡Proceso terminado con exito!
pause