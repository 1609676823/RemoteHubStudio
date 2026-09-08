#!/usr/bin/env bash
set -euo pipefail
release_script="$PWD/.github/scripts/publish-release.sh"
validation_root="$PWD/artifacts/local-validation"
mkdir -p "$validation_root"
fixture_root=$(mktemp -d "$validation_root/publication.XXXXXX")

# This exported function replaces gh in the child process; no GitHub calls are made.
gh() {
  printf '%s\n' "$*" >> "$MOCK_LOG"
  case "$*" in
    'api --paginate '*)
      case "$MOCK_CASE" in
        nightly_existing|upload_failure|ref_failure) printf '1\tfalse\ttrue\tfalse\n' ;;
        nightly_immutable) printf '1\tfalse\ttrue\ttrue\n' ;;
        nightly_wrong_channel) printf '1\tfalse\tfalse\tfalse\n' ;;
        stable_published) printf '1\tfalse\tfalse\ttrue\n' ;;
        stable_draft) printf '1\ttrue\tfalse\tfalse\n' ;;
        stable_wrong_channel) printf '1\tfalse\ttrue\tfalse\n' ;;
        force_stable_new|force_stable_without_tag) ;;
        force_stable_draft) printf '1\ttrue\tfalse\tfalse\n' ;;
        force_stable_immutable) printf '1\tfalse\tfalse\ttrue\n' ;;
        force_stable_wrong_channel) printf '1\tfalse\ttrue\tfalse\n' ;;
        force_stable_*) printf '1\tfalse\tfalse\tfalse\n' ;;
        api_failure) return 22 ;;
      esac ;;
    api*'/commits/'*)
      if [[ "$MOCK_CASE" == 'stable_moved_tag' ]]; then
        printf '%040d\n' 2
      else
        printf '%s\n' "$RELEASE_COMMIT"
      fi ;;
    api*'/git/matching-refs/'*)
      if [[ "$MOCK_CASE" != 'nightly_new' && "$MOCK_CASE" != 'stable_without_tag' && "$MOCK_CASE" != 'force_stable_without_tag' ]]; then
        printf 'refs/tags/%s\n' "$RELEASE_TAG"
      fi ;;
    'api --method PATCH '*|'api --method POST '*)
      if [[ "$MOCK_CASE" == 'ref_failure' || "$MOCK_CASE" == 'force_stable_ref_failure' ]]; then return 23; fi ;;
    'release upload '*)
      if [[ "$MOCK_CASE" == 'upload_failure' || "$MOCK_CASE" == 'force_stable_upload_failure' ]]; then return 24; fi ;;
    'release view '*)
      if [[ "$MOCK_CASE" == 'force_stable_assets_failure' ]]; then return 25; fi
      printf 'RemoteHubStudio-%s-win-x64-self-contained.zip\n' "$RELEASE_TAG"
      printf 'RemoteHubStudio-%s-win-x64.zip\n' "$RELEASE_TAG"
      printf 'unrelated-notes.pdf\n' ;;
    'release delete-asset '*)
      if [[ "$MOCK_CASE" == 'force_stable_delete_failure' ]]; then return 26; fi
      [[ "$*" == "release delete-asset $RELEASE_TAG RemoteHubStudio-$RELEASE_TAG-win-x64.zip --yes" ]] || return 98 ;;
    'release create '*|'release edit '*)
      if [[ "$MOCK_CASE" == 'force_stable_publish_failure' && "$*" == *'--draft=false'* ]]; then return 27; fi ;;
    *) echo "Unexpected gh call: $*" >&2; return 99 ;;
  esac
  return 0
}
export -f gh

# All gh calls are mocked. These tests never upload, delete, or move real GitHub assets/tags.
for scenario in nightly_new nightly_existing nightly_immutable nightly_wrong_channel upload_failure ref_failure api_failure stable_new stable_without_tag stable_published stable_draft stable_wrong_channel stable_moved_tag invalid_tag checksum_failure force_stable_existing force_stable_draft force_stable_new force_stable_without_tag force_stable_immutable force_stable_wrong_channel force_stable_upload_failure force_stable_ref_failure force_stable_assets_failure force_stable_delete_failure force_stable_publish_failure force_stable_schedule force_stable_push invalid_force; do
  export MOCK_CASE="$scenario" GH_REPO='example/repository'
  export RELEASE_COMMIT='1111111111111111111111111111111111111111'
  export RELEASE_CHANNEL='nightly' RELEASE_TAG='v0.1.0-nightly'
  export FORCE_RELEASE='false' GITHUB_EVENT_NAME='workflow_dispatch'
  if [[ "$scenario" == stable_* || "$scenario" == force_stable_* ]]; then
    export RELEASE_CHANNEL='stable' RELEASE_TAG='v0.1.0'
  fi
  if [[ "$scenario" == force_stable_* ]]; then export FORCE_RELEASE='true'; fi
  if [[ "$scenario" == 'force_stable_schedule' ]]; then export GITHUB_EVENT_NAME='schedule'; fi
  if [[ "$scenario" == 'force_stable_push' ]]; then export GITHUB_EVENT_NAME='push'; fi
  if [[ "$scenario" == 'invalid_force' ]]; then export FORCE_RELEASE='yes'; fi
  if [[ "$scenario" == 'invalid_tag' ]]; then RELEASE_TAG='v0.1.0'; fi
  case_dir="$fixture_root/$scenario"
  mkdir -p "$case_dir/artifacts"
  export MOCK_LOG="$case_dir/gh.log" GITHUB_STEP_SUMMARY="$case_dir/summary.txt"
  export GITHUB_SERVER_URL='https://github.com'
  : > "$MOCK_LOG"
  printf 'test package\n' > "$case_dir/artifacts/RemoteHubStudio-$RELEASE_TAG-win-x64-self-contained.zip"
  printf 'portable package\n' > "$case_dir/artifacts/RemoteHubStudio-$RELEASE_TAG-win-portable-framework-dependent.zip"
  printf 'test notes\n' > "$case_dir/artifacts/release-notes.md"
  (cd "$case_dir/artifacts"; sha256sum --text "RemoteHubStudio-$RELEASE_TAG-win-x64-self-contained.zip" "RemoteHubStudio-$RELEASE_TAG-win-portable-framework-dependent.zip" > SHA256SUMS.txt)
  if [[ "$scenario" == 'checksum_failure' ]]; then
    printf 'corruption\n' >> "$case_dir/artifacts/RemoteHubStudio-$RELEASE_TAG-win-x64-self-contained.zip"
  fi
  status=0
  (cd "$case_dir"; bash "$release_script") > "$case_dir/output.txt" 2>&1 || status=$?

  case "$scenario" in
    nightly_new|nightly_existing|stable_new|stable_without_tag|stable_published|stable_draft|force_stable_existing|force_stable_draft|force_stable_new|force_stable_without_tag) [[ "$status" == 0 ]] ;;
    *) [[ "$status" != 0 ]] ;;
  esac
  case "$scenario" in
    nightly_new)
      grep -q '^release create .*--draft --prerelease --latest=false' "$MOCK_LOG"
      grep -q '^api --method POST .* -f ref=refs/tags/v0.1.0-nightly -f sha=111' "$MOCK_LOG"
      grep -q '^release edit .*--draft=false --prerelease --latest=false' "$MOCK_LOG" ;;
    nightly_existing)
      grep -q '^release edit .*--draft=true$' "$MOCK_LOG"
      grep -q '^api --method PATCH .* -f sha=111.* -F force=true$' "$MOCK_LOG"
      grep -q '^release upload .*--clobber$' "$MOCK_LOG"
      ! grep -q '^release create ' "$MOCK_LOG" ;;
    upload_failure)
      ! grep -q '^api --method ' "$MOCK_LOG"
      ! grep -q '^release edit .*--draft=false' "$MOCK_LOG" ;;
    ref_failure)
      ! grep -q '^release edit .*--draft=false' "$MOCK_LOG" ;;
    stable_without_tag)
      grep -q '^api --method POST .* -f ref=refs/tags/v0.1.0 -f sha=111' "$MOCK_LOG"
      ! grep -q '^api --method PATCH ' "$MOCK_LOG"
      grep -q '^release create .*--verify-tag' "$MOCK_LOG"
      grep -q '^release edit .*--draft=false --prerelease=false' "$MOCK_LOG" ;;
    stable_new|stable_draft)
      grep -q '^release upload ' "$MOCK_LOG"
      grep -q '^release edit .*--draft=false --prerelease=false' "$MOCK_LOG"
      ! grep -q '^api --method ' "$MOCK_LOG"
      if [[ "$scenario" == 'stable_new' ]]; then grep -q '^release create .*--verify-tag' "$MOCK_LOG"; fi ;;
    force_stable_existing|force_stable_draft|force_stable_new|force_stable_without_tag)
      grep -q '^release upload .*--clobber$' "$MOCK_LOG"
      grep -q '^release delete-asset v0.1.0 RemoteHubStudio-v0.1.0-win-x64.zip --yes$' "$MOCK_LOG"
      grep -q '^release edit .*--notes-file release-notes.md --draft=false --prerelease=false$' "$MOCK_LOG"
      ! grep -q -- '--prerelease --latest=false' "$MOCK_LOG"
      ! grep -q '^release delete ' "$MOCK_LOG"
      ! grep -q '/commits/' "$MOCK_LOG"
      if [[ "$scenario" == 'force_stable_existing' || "$scenario" == 'force_stable_draft' ]]; then
        grep -q '^release edit .*--draft=true$' "$MOCK_LOG"
        ! grep -q '^release create ' "$MOCK_LOG"
      else
        grep -q '^release create .*--target 111.*--draft --prerelease=false' "$MOCK_LOG"
      fi
      if [[ "$scenario" == 'force_stable_without_tag' ]]; then
        grep -q '^api --method POST .* -f ref=refs/tags/v0.1.0 -f sha=111' "$MOCK_LOG"
      else
        grep -q '^api --method PATCH .*git/refs/tags/v0.1.0 -f sha=111.* -F force=true$' "$MOCK_LOG"
      fi
      upload_line=$(grep -n '^release upload ' "$MOCK_LOG" | cut -d: -f1)
      tag_line=$(grep -n '^api --method ' "$MOCK_LOG" | cut -d: -f1)
      publish_line=$(grep -n '^release edit .*--draft=false' "$MOCK_LOG" | cut -d: -f1)
      (( upload_line < tag_line && tag_line < publish_line )) ;;
    force_stable_upload_failure|force_stable_assets_failure|force_stable_delete_failure)
      grep -q '^release edit .*--draft=true$' "$MOCK_LOG"
      ! grep -q '^api --method ' "$MOCK_LOG"
      ! grep -q '^release edit .*--draft=false' "$MOCK_LOG" ;;
    force_stable_ref_failure)
      ! grep -q '^release edit .*--draft=false' "$MOCK_LOG" ;;
    force_stable_publish_failure)
      grep -q '^release upload ' "$MOCK_LOG"
      [[ ! -s "$GITHUB_STEP_SUMMARY" ]] ;;
    *)
      ! grep -q '^release \|^api --method ' "$MOCK_LOG" ;;
  esac
  if [[ "$status" == 0 && "$scenario" != 'stable_published' ]]; then
    grep -q '^release upload .*win-x64-self-contained.zip .*win-portable-framework-dependent.zip SHA256SUMS.txt --clobber$' "$MOCK_LOG"
  fi
  printf 'PASS %s\n' "$scenario"
done
