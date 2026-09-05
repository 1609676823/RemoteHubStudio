@echo off
chcp 65001 >nul
setlocal EnableExtensions DisableDelayedExpansion

set "PAUSE_AT_END=1"
if /I "%~1"=="--no-pause" set "PAUSE_AT_END=0"
echo [Encoding] UTF-8 code page 65001 is active.

set "GIT_DIR="
set "GIT_WORK_TREE="
set "GIT_COMMON_DIR="
set "GIT_INDEX_FILE="

for %%I in ("%~dp0.") do set "REPO_ROOT=%%~fI"
set "GITEE_URL=https://gitee.com/lnsyzjw/remote-hub-studio.git"
set "GITHUB_URL=https://github.com/1609676823/RemoteHubStudio.git"

where git >nul 2>&1
if errorlevel 1 (
    echo ERROR: Git was not found in PATH.
    call :pause_before_exit
    endlocal & exit /b 1
)

git -C "%REPO_ROOT%" rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo ERROR: The script directory is not a Git work tree: "%REPO_ROOT%"
    call :pause_before_exit
    endlocal & exit /b 1
)

set "CURRENT_TOP="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --show-toplevel 2^>nul') do set "CURRENT_TOP=%%I"
if not defined CURRENT_TOP (
    echo ERROR: Unable to resolve the Git top-level directory.
    call :pause_before_exit
    endlocal & exit /b 1
)
for %%I in ("%CURRENT_TOP%") do set "CURRENT_TOP=%%~fI"
if /I not "%CURRENT_TOP%"=="%REPO_ROOT%" (
    echo ERROR: The Git top-level directory must be the directory containing this script.
    echo Expected: "%REPO_ROOT%"
    echo Actual:   "%CURRENT_TOP%"
    call :pause_before_exit
    endlocal & exit /b 1
)

set "ORIGIN_EXISTS="
for /f "delims=" %%R in ('git -C "%REPO_ROOT%" remote 2^>nul') do if /I "%%R"=="origin" set "ORIGIN_EXISTS=1"

if defined ORIGIN_EXISTS goto :update_origin
git -C "%REPO_ROOT%" remote add origin "%GITEE_URL%"
if errorlevel 1 goto :git_error
goto :origin_ready

:update_origin
git -C "%REPO_ROOT%" config --local --replace-all remote.origin.url "%GITEE_URL%"
if errorlevel 1 goto :git_error

:origin_ready
rem Rebuild pushurl values so rerunning this script cannot accumulate duplicates.
git -C "%REPO_ROOT%" config --local --unset-all remote.origin.pushurl >nul 2>&1
set "UNSET_EXIT=%ERRORLEVEL%"
if "%UNSET_EXIT%"=="0" goto :push_urls_reset
if "%UNSET_EXIT%"=="5" goto :push_urls_reset
echo ERROR: Unable to reset origin push URLs. Git exit code: %UNSET_EXIT%
call :pause_before_exit
endlocal & exit /b %UNSET_EXIT%

:push_urls_reset
git -C "%REPO_ROOT%" config --local --add remote.origin.pushurl "%GITEE_URL%"
if errorlevel 1 goto :git_error
git -C "%REPO_ROOT%" config --local --add remote.origin.pushurl "%GITHUB_URL%"
if errorlevel 1 goto :git_error

set "FETCH_URL="
for /f "delims=" %%U in ('git -C "%REPO_ROOT%" config --local --get remote.origin.url 2^>nul') do set "FETCH_URL=%%U"

set "PUSH_URL_1="
set "PUSH_URL_2="
set "PUSH_URL_3="
for /f "tokens=1,* delims=:" %%A in ('git -C "%REPO_ROOT%" config --local --get-all remote.origin.pushurl 2^>nul ^| findstr /n "^"') do (
    if "%%A"=="1" set "PUSH_URL_1=%%B"
    if "%%A"=="2" set "PUSH_URL_2=%%B"
    if "%%A"=="3" set "PUSH_URL_3=%%B"
)

if /I not "%FETCH_URL%"=="%GITEE_URL%" goto :verification_error
if /I not "%PUSH_URL_1%"=="%GITEE_URL%" goto :verification_error
if /I not "%PUSH_URL_2%"=="%GITHUB_URL%" goto :verification_error
if defined PUSH_URL_3 goto :verification_error

echo Git remotes configured successfully.
git -C "%REPO_ROOT%" remote -v
call :pause_before_exit
endlocal & exit /b 0

:verification_error
echo ERROR: The resulting origin URL configuration was not the expected Gitee plus GitHub layout.
git -C "%REPO_ROOT%" remote -v
call :pause_before_exit
endlocal & exit /b 1

:git_error
set "GIT_EXIT=%ERRORLEVEL%"
if "%GIT_EXIT%"=="0" set "GIT_EXIT=1"
echo ERROR: Git remote configuration failed. Git exit code: %GIT_EXIT%
call :pause_before_exit
endlocal & exit /b %GIT_EXIT%

:pause_before_exit
if "%PAUSE_AT_END%"=="0" exit /b 0
echo.
echo Execution finished. Review the details above. / 执行完毕，请查看以上明细。
pause
exit /b 0
