@echo off
cd /d "%~dp0TravelerScraper"

echo Compilando el proyecto TravelerScraper...
"C:\dotnet6\dotnet.exe" build

echo Copiando el archivo generado...
copy "%~dp0TravelerScraper\bin\Debug\net6.0\TravelerScraper.dll" "%~dp0Mods\TravelerScraper.dll"

echo ¡Proceso terminado con exito!
pause