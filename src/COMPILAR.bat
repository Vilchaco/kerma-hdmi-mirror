@echo off
title Compilando HdmiMirror
cd /d "%~dp0"

echo.
echo ==========================================
echo    COMPILANDO HdmiMirror
echo ==========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 goto SINDOTNET

echo Compilando... esto tarda 1-3 minutos la primera vez.
echo No cierres esta ventana.
echo.

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%~dp0Compilado"

if errorlevel 1 goto ERROR

echo.
echo ==========================================
echo    LISTO
echo ==========================================
echo.
echo El programa esta en la carpeta "Compilado"
echo que acaba de aparecer aqui al lado.
echo.
echo Abre esa carpeta y haz doble clic en:
echo    HdmiMirror.exe
echo.
pause
exit /b 0


:SINDOTNET
echo ==========================================
echo    FALTA INSTALAR .NET
echo ==========================================
echo.
echo Ve a esta pagina:
echo    https://dotnet.microsoft.com/es-es/download/dotnet/8.0
echo.
echo Busca la columna que pone "SDK" (la de la izquierda,
echo NO la que pone "Runtime") y descarga la version:
echo    Windows  -  x64  -  Installer
echo.
echo Instalalo, CIERRA esta ventana, y vuelve a hacer
echo doble clic en COMPILAR.bat
echo.
pause
exit /b 1


:ERROR
echo.
echo ==========================================
echo    HA DADO ERROR
echo ==========================================
echo.
echo Selecciona TODO el texto de esta ventana
echo (clic derecho en la barra de arriba -^> Editar -^> Seleccionar todo,
echo  y luego Enter para copiarlo)
echo y pegaselo a Claude para que lo arregle.
echo.
pause
exit /b 1
