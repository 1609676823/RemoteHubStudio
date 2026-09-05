#!/usr/bin/env bash
set -euo pipefail

: "${GH_REPO:?}" "${RELEASE_CHANNEL:?}" "${RELEASE_TAG:?}" "${RELEASE_COMMIT:?}"

# Validate before using tag names in API paths, queries, or mutations.
version_pattern='(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
case "$RELEASE_CHANNEL" in
  nightly) [[ "$RELEASE_TAG" =~ ^v${version_pattern}-nightly$ ]] ;;
  stable) [[ "$RELEASE_TAG" =~ ^v${version_pattern}$ ]] ;;
  *) echo "Unknown release channel: $RELEASE_CHANNEL" >&2; exit 1 ;;
esac
[[ "$RELEASE_COMMIT" =~ ^[0-9a-f]{40}$ ]]

cd artifacts
test -s release-notes.md
test -s SHA256SUMS.txt
# SHA256SUMS is also the exact package manifest; do not upload unrelated/stale ZIPs.
archives=()
while read -r checksum archive; do
  [[ "$checksum" =~ ^[0-9a-f]{64}$ ]]
  [[ "$archive" == "RemoteHubStudio-$RELEASE_TAG-"* ]]
  suffix=${archive#"RemoteHubStudio-$RELEASE_TAG-"}
  [[ "$suffix" =~ ^win-(x86|x64|arm64|portable)-(self-contained|framework-dependent)\.zip$ ]]
  [[ "$suffix" != 'win-portable-self-contained.zip' ]]
  test -s "$archive"
  for existing in "${archives[@]}"; do [[ "$existing" != "$archive" ]]; done
  archives+=("$archive")
done < SHA256SUMS.txt
(( ${#archives[@]} > 0 ))
sha256sum --check --strict SHA256SUMS.txt

# A failed API request must stop the job, not be mistaken for a missing release.
# The validated tag can contain only version digits, dots, and the nightly suffix.
release=$(gh api --paginate "repos/$GH_REPO/releases?per_page=100" \
  --jq ".[] | select(.tag_name == \"$RELEASE_TAG\") | [.id, .draft, .prerelease, (.immutable // false)] | @tsv")
release_id='' draft='' prerelease='' immutable=''
if [[ -n "$release" ]]; then
  IFS=$'\t' read -r release_id draft prerelease immutable <<< "$release"
fi

if [[ "$RELEASE_CHANNEL" == 'stable' ]]; then
  if [[ -n "$release_id" ]]; then
    if [[ "$prerelease" != 'false' ]]; then
      echo 'Refusing to replace a prerelease with a stable release.' >&2
      exit 1
    fi
    if [[ "$draft" == 'false' ]]; then
      echo "Stable release $RELEASE_TAG is already published; keeping its assets intact."
      exit 0
    fi
  fi

  # Branch/manual runs can create a missing stable tag after tests pass.
  # An existing tag is resolved (including annotated tags) and must never move.
  tag_ref=$(gh api "repos/$GH_REPO/git/matching-refs/tags/$RELEASE_TAG" \
    --jq ".[] | select(.ref == \"refs/tags/$RELEASE_TAG\") | .ref")
  if [[ -n "$tag_ref" ]]; then
    tag_commit=$(gh api "repos/$GH_REPO/commits/$RELEASE_TAG" --jq '.sha')
    if [[ "$tag_commit" != "$RELEASE_COMMIT" ]]; then
      echo 'The remote stable tag no longer matches the tested commit.' >&2
      exit 1
    fi
  else
    gh api --method POST "repos/$GH_REPO/git/refs" \
      -f ref="refs/tags/$RELEASE_TAG" -f sha="$RELEASE_COMMIT" > /dev/null
  fi
  if [[ -z "$release_id" ]]; then
    gh release create "$RELEASE_TAG" --verify-tag --draft \
      --title "$RELEASE_TAG" --notes-file release-notes.md
  fi
  gh release upload "$RELEASE_TAG" "${archives[@]}" SHA256SUMS.txt --clobber
  # Let GitHub determine Latest from the release versions, including maintenance releases.
  gh release edit "$RELEASE_TAG" --title "$RELEASE_TAG" --draft=false --prerelease=false
else
  if [[ -n "$release_id" ]]; then
    if [[ "$immutable" == 'true' || "$prerelease" != 'true' ]]; then
      echo 'Rolling nightlies require a mutable prerelease. Existing release left untouched.' >&2
      exit 1
    fi
    # Hide the release during replacement so a partial update is not offered publicly.
    gh release edit "$RELEASE_TAG" --draft=true
  else
    gh release create "$RELEASE_TAG" --target "$RELEASE_COMMIT" --draft --prerelease --latest=false \
      --title "$RELEASE_TAG" --notes-file release-notes.md
  fi

  gh release upload "$RELEASE_TAG" "${archives[@]}" SHA256SUMS.txt --clobber
  # A mode/target switch replaces the nightly package set, including legacy win-x64 ZIPs.
  # Delete only obsolete application ZIPs while the release is a draft; preserve other assets.
  release_assets=$(gh release view "$RELEASE_TAG" --json assets --jq '.assets[].name')
  while IFS= read -r asset; do
    [[ "$asset" == "RemoteHubStudio-$RELEASE_TAG-"*.zip ]] || continue
    keep=false
    for archive in "${archives[@]}"; do
      if [[ "$asset" == "$archive" ]]; then keep=true; break; fi
    done
    if [[ "$keep" == false ]]; then gh release delete-asset "$RELEASE_TAG" "$asset" --yes; fi
  done <<< "$release_assets"
  # A new draft may not have a tag yet. List matching refs to distinguish absence from API failure.
  tag_ref=$(gh api "repos/$GH_REPO/git/matching-refs/tags/$RELEASE_TAG" \
    --jq ".[] | select(.ref == \"refs/tags/$RELEASE_TAG\") | .ref")
  if [[ -n "$tag_ref" ]]; then
    gh api --method PATCH "repos/$GH_REPO/git/refs/tags/$RELEASE_TAG" \
      -f sha="$RELEASE_COMMIT" -F force=true > /dev/null
  else
    gh api --method POST "repos/$GH_REPO/git/refs" \
      -f ref="refs/tags/$RELEASE_TAG" -f sha="$RELEASE_COMMIT" > /dev/null
  fi
  gh release edit "$RELEASE_TAG" --target "$RELEASE_COMMIT" \
    --title "$RELEASE_TAG" --notes-file release-notes.md \
    --draft=false --prerelease --latest=false
fi

echo "Release: $GITHUB_SERVER_URL/$GH_REPO/releases/tag/$RELEASE_TAG" >> "$GITHUB_STEP_SUMMARY"
