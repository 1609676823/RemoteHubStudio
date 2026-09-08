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

if not exist "%REPO_ROOT%\RemoteHubStudio.slnx" goto :wrong_project
if not exist "%REPO_ROOT%\RemoteHubStudio\RemoteHubStudio.csproj" goto :wrong_project

set "CURRENT_TOP="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --show-toplevel 2^>nul') do set "CURRENT_TOP=%%I"
if not defined CURRENT_TOP (
    echo ERROR: The script directory is not a Git repository.
    call :pause_before_exit
    endlocal & exit /b 1
)
for %%I in ("%CURRENT_TOP%") do set "CURRENT_TOP=%%~fI"
if /I not "%CURRENT_TOP%"=="%REPO_ROOT%" (
    echo ERROR: The detected Git top-level directory does not match this project.
    call :pause_before_exit
    endlocal & exit /b 1
)

set "CURRENT_BRANCH="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" branch --show-current 2^>nul') do set "CURRENT_BRANCH=%%I"
if /I not "%CURRENT_BRANCH%"=="master" (
    echo ERROR: Force push is restricted to the local master branch. Current branch: "%CURRENT_BRANCH%"
    call :pause_before_exit
    endlocal & exit /b 1
)

git -C "%REPO_ROOT%" rev-parse --verify HEAD >nul 2>&1
if errorlevel 1 (
    echo ERROR: The repository has no commit to push.
    call :pause_before_exit
    endlocal & exit /b 1
)

echo.
echo DANGER: This uses --force to replace the remote master history on BOTH repositories:
echo   %GITEE_URL%
echo   %GITHUB_URL%
echo The two pushes are not atomic; one remote may update even if the other fails.
echo Uncommitted working-tree changes are not included.
echo.
git -C "%REPO_ROOT%" log -1 --oneline
git -C "%REPO_ROOT%" status --short --branch
echo.
echo Starting force push without an interactive confirmation...
call "%REPO_ROOT%\setup-git-remotes.bat" --no-pause
if errorlevel 1 (
    echo ERROR: Remote setup failed; nothing was pushed.
    call :pause_before_exit
    endlocal & exit /b 1
)

rem Push each destination explicitly so both are attempted and reported independently.
git -C "%REPO_ROOT%" push --force "%GITEE_URL%" "HEAD:refs/heads/master"
set "GITEE_EXIT=%ERRORLEVEL%"
git -C "%REPO_ROOT%" push --force "%GITHUB_URL%" "HEAD:refs/heads/master"
set "GITHUB_EXIT=%ERRORLEVEL%"

echo.
echo Gitee push exit code: %GITEE_EXIT%
echo GitHub push exit code: %GITHUB_EXIT%
if not "%GITEE_EXIT%"=="0" goto :push_failed
if not "%GITHUB_EXIT%"=="0" goto :push_failed

echo Force push to Gitee and GitHub completed successfully.
call :pause_before_exit
endlocal & exit /b 0

:push_failed
echo ERROR: Force push failed or completed only partially. Review both remotes before retrying.
call :pause_before_exit
endlocal & exit /b 1

:wrong_project
echo ERROR: Required RemoteHubStudio project files were not found beside this script.
call :pause_before_exit
endlocal & exit /b 1

:pause_before_exit
if "%PAUSE_AT_END%"=="0" exit /b 0
echo.
echo Execution finished. Review the details above. / 执行完毕，请查看以上明细。
pause
exit /b 0
