# Code quality setup

One-time configuration for CI quality gates added to this repository.

## SonarCloud

1. Sign in at [sonarcloud.io](https://sonarcloud.io) with GitHub and import the repository (e.g. **`gideonogegaorg/FamilyTree`**).
2. Confirm SonarCloud matches CI context from [`.github/workflows/build.yml`](../.github/workflows/build.yml):
   - **Organization:** `github.repository_owner` (e.g. `gideonogegaorg`)
   - **Project key:** `{owner}_{repo}` (e.g. `gideonogegaorg_FamilyTree`)
   - **Display name:** `github.event.repository.name` (e.g. `FamilyTree`)
3. Add **`SONAR_TOKEN`** for CI analysis upload and PR decoration:
   - **Recommended:** GitHub org secret `SONAR_TOKEN` on **`gideonogegaorg`** (shared by all repos that use SonarCloud org `gideonogegaorg`, e.g. OpenTelemetry and FamilyTree).
   - **Alternative:** repository secret on each repo.
   - Value: SonarCloud → My Account → Security → Generate Token (one token can analyze any project you can access in that SonarCloud org).
4. In SonarCloud → Project → Quality Gate, use (or create) a gate with:
   - Overall line coverage ≥ **80%**
   - Coverage on new code ≥ **80%**
   - No new blocker issues
   - Duplicated lines ≤ **3%**
5. Enable **Pull Request decoration** under GitHub integration.
6. Disable **Automatic Analysis** (Administration → Analysis Method) when using CI-based scanner.
7. Set **Administration → Branches → Detection of long-lived branches** regex to `prod|dev|(branch|release)-.*`.
8. Confirm **Administration → Branches** shows **`prod`** as the SonarCloud main branch (not `dev`). The build workflow calls `project_branches/set_main` on pushes to GitHub `prod`; if that API call fails, ensure `SONAR_TOKEN` can **Administer** the project or set the main branch manually in SonarCloud.

The build workflow runs `dotnet sonarscanner begin/end` around build and tests. Scanner settings (exclusions, OpenCover paths, quality gate wait) are passed on the `begin` command — **do not** add `sonar-project.properties`; the .NET scanner rejects that file. Merge blocking still uses the required **SonarCloud Code Analysis** GitHub check even when scanner-side quality gate wait is skipped.

## GitHub security features

- **CodeQL**: runs via [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml). Enable *Settings → Code security → Code scanning* if prompts appear.
- **Dependabot**: configured in [`.github/dependabot.yml`](../.github/dependabot.yml).
- **Trivy**: filesystem scan via [`.github/workflows/trivy.yml`](../.github/workflows/trivy.yml); results appear in the Security tab.

## NuGet vulnerability audit

The lint job runs:

```bash
dotnet list GMO.FamilyTree.sln package --vulnerable --include-transitive
```

This fails CI when known vulnerabilities exist. A current transitive issue is **`OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.0** pulled in via `GMO.OpenTelemetry` packages; resolve by updating those GMO packages when a fixed release is available.

## Local commands (required before PRs to `dev` / `prod`)

| Check | Command |
|-------|---------|
| Format | `dotnet format GMO.FamilyTree.sln --verify-no-changes` |
| JS lint | `npm ci && npm run lint:js` |
| Tests | `dotnet test GMO.FamilyTree.sln` |
| Coverage (local HTML) | `.\scripts\run-coverage.ps1` then open `coverage/combined/index.html` |
