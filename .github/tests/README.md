# Release workflow regression checks

Run from the repository root. These checks mock every `gh` call and do not access or modify GitHub releases, tags, or assets. Fixtures and command logs are written under the ignored `artifacts/local-validation` directory.

```powershell
pwsh -NoProfile -File .github/tests/test-prepare-stable-release.ps1
```

```bash
bash .github/tests/test-publish-release.sh
```

On Windows, use Git Bash for the second command. Run `actionlint` separately to validate all workflow definitions and reusable-workflow inputs.

The preparation suite covers normal skips, original-tag selection, forced current-source selection, version checks, API errors, immutable releases, and rejection of force flags on automatic events. The publication suite covers normal stable preservation, nightly updates, forced stable replacement/creation, obsolete-asset removal, checksum errors, and failures during upload, cleanup, tag updates, or final publication. It verifies that tags are updated only after successful uploads and publication follows tag updates.
