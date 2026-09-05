$ErrorActionPreference = 'Stop'

$props = [xml](Get-Content -LiteralPath Directory.Build.props -Raw)
$version = [string]$props.Project.PropertyGroup.Version
$versionPattern = '(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
if ($version -notmatch "^$versionPattern(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$") {
    throw 'Directory.Build.props must contain a three-part semantic version.'
}

$tag = "v$version"
if ($env:GITHUB_REF.StartsWith('refs/tags/') -and $env:GITHUB_REF -ne "refs/tags/$tag") {
    throw 'The requested tag must match Version in Directory.Build.props exactly.'
}
if ($version -notmatch "^$versionPattern$") {
    'should-build=false' >> $env:GITHUB_OUTPUT
    "Skipping stable publication: $version is a prerelease or contains build metadata." >> $env:GITHUB_STEP_SUMMARY
    exit 0
}
"tag=$tag" >> $env:GITHUB_OUTPUT

# A failed API call is not evidence that a release or tag is absent.
$query = ".[] | select(.tag_name == `"$tag`") | [.draft, .prerelease] | @tsv"
$releaseInfo = [string](& gh api --paginate "repos/$env:GH_REPO/releases?per_page=100" --jq $query)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect existing releases.' }
if ($releaseInfo) {
    $state = $releaseInfo -split "`t"
    if ($state[1] -ne 'false') { throw 'A prerelease already uses the stable version tag.' }
    if ($state[0] -eq 'false') {
        'should-build=false' >> $env:GITHUB_OUTPUT
        "published-tag=$tag" >> $env:GITHUB_OUTPUT
        "Stable release $tag is already published. Skipping its build and keeping its assets intact." >> $env:GITHUB_STEP_SUMMARY
        exit 0
    }
}

$query = ".[] | select(.ref == `"refs/tags/$tag`") | .ref"
$tagRef = [string](& gh api "repos/$env:GH_REPO/git/matching-refs/tags/$tag" --jq $query)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the stable tag.' }
if ($tagRef) {
    # An existing tag, including an annotated tag, selects the original release source.
    $commit = [string](& gh api "repos/$env:GH_REPO/commits/$tag" --jq '.sha')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the stable tag commit.' }
} else {
    $commit = [string](& git rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the requested source commit.' }
}
if ($commit -notmatch '^[0-9a-f]{40}$') { throw 'Invalid source commit returned.' }
"commit=$commit" >> $env:GITHUB_OUTPUT
'should-build=true' >> $env:GITHUB_OUTPUT
"Stable release $tag will be built from $commit. Its tag will only be created after successful tests." >> $env:GITHUB_STEP_SUMMARY
