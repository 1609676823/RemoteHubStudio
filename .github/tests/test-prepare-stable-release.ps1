$ErrorActionPreference = 'Stop'
$taskScript = Join-Path (Get-Location) '.github/scripts/prepare-stable-release.ps1'
$taskFixtureRoot = Join-Path (Get-Location) ('artifacts/local-validation/prepare-' + [guid]::NewGuid().ToString('N'))
$taskHead = git rev-parse HEAD
function global:gh {
    $global:LASTEXITCODE = 0
    $taskCommand = $args -join ' '
    $taskCommand >> $env:MOCK_LOG
    if ($taskCommand -like 'api --paginate *') {
        switch ($env:MOCK_CASE) {
            'published' { return "false`tfalse`tfalse" }
            'draft' { return "true`tfalse`tfalse" }
            'wrong-channel' { return "false`ttrue`tfalse" }
            'force-published' { return "false`tfalse`tfalse" }
            'force-draft' { return "true`tfalse`tfalse" }
            'force-immutable' { return "false`tfalse`ttrue" }
            'force-wrong-channel' { return "false`ttrue`tfalse" }
            'force-api-failure' { $global:LASTEXITCODE = 22; return }
            'api-failure' { $global:LASTEXITCODE = 22; return }
        }
    } elseif ($taskCommand -like 'api */git/matching-refs/*') {
        if ($env:MOCK_CASE -eq 'tag-api-failure') { $global:LASTEXITCODE = 22; return }
        if ($env:MOCK_CASE -in @('draft', 'existing-tag', 'commit-api-failure')) { return 'refs/tags/v0.1.0' }
    } elseif ($taskCommand -like 'api */commits/*') {
        if ($env:MOCK_CASE -eq 'commit-api-failure') { $global:LASTEXITCODE = 22; return }
        return '1111111111111111111111111111111111111111'
    } else {
        throw "Unexpected gh call: $taskCommand"
    }
}

# gh is mocked and preparation is read-only, including all force-publication scenarios.
foreach ($taskCase in @('new', 'published', 'draft', 'existing-tag', 'beta', 'metadata', 'tag-match', 'tag-mismatch', 'invalid-version', 'api-failure', 'tag-api-failure', 'commit-api-failure', 'wrong-channel', 'upgrade', 'force-published', 'force-draft', 'force-new', 'force-immutable', 'force-wrong-channel', 'force-api-failure', 'force-beta', 'force-schedule', 'force-push', 'invalid-force')) {
    $taskFixture = Join-Path $taskFixtureRoot $taskCase
    New-Item -ItemType Directory -Path $taskFixture -Force | Out-Null
    $env:MOCK_CASE = $taskCase
    $env:MOCK_LOG = Join-Path $taskFixture 'gh.log'
    $env:GITHUB_OUTPUT = Join-Path $taskFixture 'output.txt'
    $env:GITHUB_STEP_SUMMARY = Join-Path $taskFixture 'summary.txt'
    $env:GITHUB_REF = 'refs/heads/master'
    $env:GH_REPO = 'example/repository'
    $env:FORCE_RELEASE = if ($taskCase.StartsWith('force-')) { 'true' } else { 'false' }
    $env:GITHUB_EVENT_NAME = 'workflow_dispatch'
    $taskVersion = '0.1.0'
    switch ($taskCase) {
        'beta' { $taskVersion = '0.2.0-beta.1' }
        'metadata' { $taskVersion = '0.2.0+build.1' }
        'tag-match' { $env:GITHUB_REF = 'refs/tags/v0.1.0' }
        'tag-mismatch' { $env:GITHUB_REF = 'refs/tags/v0.2.0' }
        'invalid-version' { $taskVersion = 'invalid' }
        'upgrade' { $taskVersion = '0.2.0' }
        'force-beta' { $taskVersion = '0.2.0-beta.1' }
        'force-schedule' { $env:GITHUB_EVENT_NAME = 'schedule' }
        'force-push' { $env:GITHUB_EVENT_NAME = 'push' }
        'invalid-force' { $env:FORCE_RELEASE = 'yes' }
    }
    Set-Content -LiteralPath (Join-Path $taskFixture 'Directory.Build.props') -Value "<Project><PropertyGroup><Version>$taskVersion</Version></PropertyGroup></Project>"
    $taskFailure = $null
    Push-Location $taskFixture
    try { & $taskScript } catch { $taskFailure = $_ } finally { Pop-Location }
    $taskExpectedFailure = $taskCase -in @('tag-mismatch', 'invalid-version', 'api-failure', 'tag-api-failure', 'commit-api-failure', 'wrong-channel', 'force-immutable', 'force-wrong-channel', 'force-api-failure', 'force-beta', 'force-schedule', 'force-push', 'invalid-force')
    if ([bool]$taskFailure -ne $taskExpectedFailure) { throw "Unexpected result for ${taskCase}: $taskFailure" }
    if (!$taskExpectedFailure) {
        $taskOutput = Get-Content -LiteralPath $env:GITHUB_OUTPUT
        $taskSkip = $taskCase -in @('published', 'beta', 'metadata')
        if ($taskSkip) {
            if ($taskOutput -notcontains 'should-build=false') { throw "Expected skip for $taskCase" }
        } else {
            $taskExpectedCommit = if ($taskCase -in @('draft', 'existing-tag')) { '1111111111111111111111111111111111111111' } else { $taskHead }
            if ($taskOutput -notcontains 'should-build=true' -or $taskOutput -notcontains "commit=$taskExpectedCommit" -or $taskOutput -notcontains "tag=v$taskVersion") { throw "Wrong build metadata for $taskCase" }
            if ($taskCase.StartsWith('force-') -and ($taskOutput -match '^published-tag=')) { throw 'Force build must not enter the skip/update-title branch.' }
        }
    }
    if (Test-Path -LiteralPath $env:MOCK_LOG) {
        if (Select-String -LiteralPath $env:MOCK_LOG -Pattern '--method|^release ') { throw 'Preflight must be read-only' }
        if ($taskCase.StartsWith('force-') -and (Select-String -LiteralPath $env:MOCK_LOG -Pattern '/commits/|/git/matching-refs/')) { throw 'Force build must use current source, not the existing tag.' }
    }
    Write-Output "PASS prepare-$taskCase"
}
