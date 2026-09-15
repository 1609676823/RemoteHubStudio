@echo off
chcp 65001 >nul
setlocal EnableExtensions DisableDelayedExpansion

set "PAUSE_AT_END=1"
if /I "%~1"=="--no-pause" set "PAUSE_AT_END=0"
echo [Encoding] UTF-8 code page 65001 is active.

rem Prevent inherited Git repository-location variables from changing init behavior.
set "GIT_DIR="
set "GIT_WORK_TREE="
set "GIT_COMMON_DIR="
set "GIT_INDEX_FILE="
set "GIT_OBJECT_DIRECTORY="
set "GIT_ALTERNATE_OBJECT_DIRECTORIES="

for %%I in ("%~dp0.") do set "REPO_ROOT=%%~fI"
for %%I in ("%REPO_ROOT%") do set "DRIVE_ROOT=%%~dI\"
set "TARGET_GIT_DIR=%REPO_ROOT%\.git"

where git >nul 2>&1
if errorlevel 1 (
    echo ERROR: Git was not found in PATH.
    call :pause_before_exit
    endlocal & exit /b 1
)

if /I "%REPO_ROOT%"=="%DRIVE_ROOT%" (
    echo ERROR: Refusing to reset a drive root.
    call :pause_before_exit
    endlocal & exit /b 1
)

if not exist "%REPO_ROOT%\RemoteHubStudio.slnx" goto :wrong_project
if not exist "%REPO_ROOT%\RemoteHubStudio\RemoteHubStudio.csproj" goto :wrong_project

git config --global --get user.name >nul 2>&1
if errorlevel 1 goto :missing_identity
git config --global --get user.email >nul 2>&1
if errorlevel 1 goto :missing_identity

rem A .git file indicates a linked worktree; deleting it would leave external metadata.
if exist "%TARGET_GIT_DIR%\." goto :validate_existing_git_directory
if exist "%TARGET_GIT_DIR%" goto :unsafe_git_dir
goto :check_for_outer_repository

:validate_existing_git_directory
set "CURRENT_GIT_DIR="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --absolute-git-dir 2^>nul') do set "CURRENT_GIT_DIR=%%I"
if not defined CURRENT_GIT_DIR goto :existing_git_directory_ready
for %%I in ("%CURRENT_GIT_DIR%") do set "CURRENT_GIT_DIR=%%~fI"
if /I not "%CURRENT_GIT_DIR%"=="%TARGET_GIT_DIR%" goto :unsafe_git_dir

set "CURRENT_IS_BARE="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --is-bare-repository 2^>nul') do set "CURRENT_IS_BARE=%%I"
if /I "%CURRENT_IS_BARE%"=="true" goto :existing_git_directory_ready

set "CURRENT_TOP="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --show-toplevel 2^>nul') do set "CURRENT_TOP=%%I"
if not defined CURRENT_TOP goto :unsafe_git_dir
for %%I in ("%CURRENT_TOP%") do set "CURRENT_TOP=%%~fI"
if /I not "%CURRENT_TOP%"=="%REPO_ROOT%" goto :unsafe_git_dir
goto :existing_git_directory_ready

:check_for_outer_repository
set "OUTER_TOP="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --show-toplevel 2^>nul') do set "OUTER_TOP=%%I"
if defined OUTER_TOP goto :outer_repository
goto :repository_preflight_complete

:existing_git_directory_ready
echo Existing Git metadata will be removed from:
echo   "%TARGET_GIT_DIR%"

:repository_preflight_complete
echo.
echo This operation rebuilds the local repository without an interactive confirmation.
echo All existing local Git history, branches, tags, reflogs, stashes, hooks, and
echo repository-local configuration will be deleted. Working-tree files remain.
echo Effective commit identity:
git config --global --get user.name
git config --global --get user.email
echo.

if not exist "%TARGET_GIT_DIR%\." goto :initialize_repository
echo [1/5] Removing existing Git metadata...
attrib -r -h -s "%TARGET_GIT_DIR%" /s /d >nul 2>&1
rmdir /s /q "%TARGET_GIT_DIR%"
if exist "%TARGET_GIT_DIR%" (
    echo ERROR: Failed to remove "%TARGET_GIT_DIR%".
    call :pause_before_exit
    endlocal & exit /b 1
)

:initialize_repository
echo [2/5] Initializing a non-bare repository on master...
git -C "%REPO_ROOT%" init --no-bare --initial-branch=master
if errorlevel 1 goto :reset_failed

set "NEW_INSIDE_WORK_TREE="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --is-inside-work-tree 2^>nul') do set "NEW_INSIDE_WORK_TREE=%%I"
if /I not "%NEW_INSIDE_WORK_TREE%"=="true" goto :reset_failed

set "NEW_IS_BARE="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --is-bare-repository 2^>nul') do set "NEW_IS_BARE=%%I"
if /I not "%NEW_IS_BARE%"=="false" goto :reset_failed

set "NEW_GIT_DIR="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --absolute-git-dir 2^>nul') do set "NEW_GIT_DIR=%%I"
if not defined NEW_GIT_DIR goto :reset_failed
for %%I in ("%NEW_GIT_DIR%") do set "NEW_GIT_DIR=%%~fI"
if /I not "%NEW_GIT_DIR%"=="%TARGET_GIT_DIR%" goto :reset_failed

set "NEW_TOP="
for /f "delims=" %%I in ('git -C "%REPO_ROOT%" rev-parse --show-toplevel 2^>nul') do set "NEW_TOP=%%I"
if not defined NEW_TOP goto :reset_failed
for %%I in ("%NEW_TOP%") do set "NEW_TOP=%%~fI"
if /I not "%NEW_TOP%"=="%REPO_ROOT%" goto :reset_failed

echo [3/5] Configuring Gitee and GitHub remotes...
call "%REPO_ROOT%\setup-git-remotes.bat" --no-pause
if errorlevel 1 goto :reset_failed

echo [4/5] Staging all non-ignored working-tree files...
git -C "%REPO_ROOT%" add --all
if errorlevel 1 goto :reset_failed

echo [5/5] Creating the Initial commit...
git -C "%REPO_ROOT%" commit -m "Initial commit"
if errorlevel 1 goto :reset_failed

echo.
echo Repository reset completed successfully.
git -C "%REPO_ROOT%" log -1 --oneline
git -C "%REPO_ROOT%" status --short --branch
git -C "%REPO_ROOT%" remote -v
call :pause_before_exit
endlocal & exit /b 0

:wrong_project
echo ERROR: Required RemoteHubStudio project files were not found beside this script.
call :pause_before_exit
endlocal & exit /b 1

:unsafe_git_dir
echo ERROR: Refusing to delete an unexpected or linked Git directory.
echo Expected: "%TARGET_GIT_DIR%"
if defined CURRENT_GIT_DIR echo Actual:   "%CURRENT_GIT_DIR%"
call :pause_before_exit
endlocal & exit /b 1

:outer_repository
echo ERROR: The project directory is inside another Git work tree while its own .git is missing.
echo Refusing to create a nested repository.
call :pause_before_exit
endlocal & exit /b 1

:missing_identity
echo ERROR: Git user.name and user.email must be configured before resetting.
echo Configure them globally before rerunning this script.
call :pause_before_exit
endlocal & exit /b 1

:reset_failed
set "GIT_EXIT=%ERRORLEVEL%"
if "%GIT_EXIT%"=="0" set "GIT_EXIT=1"
echo ERROR: Repository recreation did not complete.
echo The working-tree files remain intact. Git exit code: %GIT_EXIT%
call :pause_before_exit
endlocal & exit /b %GIT_EXIT%

:pause_before_exit
if "%PAUSE_AT_END%"=="0" exit /b 0
echo.
echo Execution finished. Review the details above. / 执行完毕，请查看以上明细。
pause
exit /b 0
