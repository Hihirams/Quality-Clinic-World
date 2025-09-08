@echo off
setlocal enabledelayedexpansion

:: ======= EDITA ESTAS DOS VARIABLES SI HACE FALTA =======
set "PROJECT_DIR=D:\Unity\QCU"
set "REPO_URL=https://github.com/Hihirams/Quality-Clinic.git"
:: =======================================================

echo.
echo === Publicar Unity a GitHub (solo lo necesario) ===
echo Proyecto: %PROJECT_DIR%
echo Remoto:   %REPO_URL%
echo.

if not exist "%PROJECT_DIR%" (
  echo [ERROR] No existe la carpeta del proyecto: %PROJECT_DIR%
  exit /b 1
)

cd /d "%PROJECT_DIR%"

:: Comprobación de git
git --version >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Git no esta instalado o no esta en PATH.
  exit /b 1
)

:: Normaliza finales de linea en Windows (evita warnings de LF/CRLF)
git config core.autocrlf true

:: Si quieres empezar absolutamente limpio, descomenta este bloque:
:: if exist ".git" (
::   echo - Quitando .git anterior...
::   rmdir /s /q .git
:: )

:: Inicializa y asegura rama main
if not exist ".git" (
  echo - Inicializando repo...
  git init
)
git branch -M main

:: (Opcional) Configurar identidad (descomenta y edita si hace falta)
:: git config user.name "Hihirams"
:: git config user.email "hiramstoker@hotmail.com"

echo - Escribiendo/actualizando .gitignore de Unity...
(
echo ## Unity - ignora artefactos regenerables
echo [Ll]ibrary/
echo [Tt]emp/
echo [Oo]bj/
echo [Bb]uild/
echo [Bb]uilds/
echo [Ll]ogs/
echo [Mm]emoryCaptures/
echo [Gg]radle/
echo [Uu]ser[Ss]ettings/
echo .vs/
echo .idea/
echo
echo ## Archivos de solucion y cache
echo *.csproj
echo *.sln
echo *.user
echo *.userprefs
echo *.pidb
echo *.svd
echo *.pdb
echo *.mdb
echo *.opendb
echo *.VC.db
echo *.tmp
echo *.DS_Store
echo
echo ## Mantener en repo: Assets/ Packages/ ProjectSettings/
) > ".gitignore"

echo - Escribiendo/actualizando .gitattributes...
echo * text^=auto> ".gitattributes"

:: (Opcional) Git LFS para binarios pesados comunes (texturas, audio, modelos)
:: Descomenta este bloque si usas archivos grandes y tienes Git LFS instalado:
:: git lfs install
:: git lfs track "*.png"
:: git lfs track "*.jpg"
:: git lfs track "*.jpeg"
:: git lfs track "*.psd"
:: git lfs track "*.wav"
:: git lfs track "*.fbx"
:: git add .gitattributes

echo - Añadiendo cambios (respetando .gitignore)...
git add .

:: Si no hay cambios que commitear, saltar commit
git diff --cached --quiet
if errorlevel 1 (
  echo - Creando commit...
  git commit -m "Publicacion automatizada: Unity (solo necesario)."
) else (
  echo - No hay cambios para commitear.
)

echo - Configurando remoto origin...
git remote remove origin 1>nul 2>nul
git remote add origin "%REPO_URL%"

echo - Empujando a main (FORCE)...
git push -u --force origin main

echo.
echo === Listo. El remoto ahora coincide con el contenido local (Unity con .gitignore). ===
endlocal
