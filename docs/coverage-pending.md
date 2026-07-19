# Coverage report – what’s still pending (all code)

Snapshot guidance after local `.\scripts\run-coverage.ps1`. **SonarCloud is the authoritative coverage gate in CI** (≥80% line and new-code coverage).

Open **coverage/combined/index.html** for the local HTML report.

---

## Coverage exclusions (coverlet + SonarCloud)

| Path | Reason |
|------|--------|
| `**/Migrations/**` | EF migration `Down()` methods are not exercised in tests |
| `**/AppDbContextFactory.cs` | Design-time EF tooling only |
| `**/wwwroot/lib/**` | Vendored third-party assets (SonarCloud only) |

CI enforces coverage via the **SonarCloud quality gate**; see [`code-quality-setup.md`](code-quality-setup.md).

Sonar exclusions in `build.yml` still omit Razor views and S3 photo storage (not used in CI). Revisit `FamilyMemberController` exclusions as integration coverage grows.

---

## Remaining gaps (high level)

### Controllers / auth

| Area | What’s thin |
|------|-------------|
| **AccountController** | Google OAuth challenge / **ExternalLoginCallback** (needs mocked external auth) |
| **HomeController.Error** | Error page / **ErrorViewModel** still lightly hit |

**Already covered (do not treat as zero):** `HomeController` Index / Landing / AddFirstMember paths via unit + integration + UI tests; `/Home/Privacy`; `/health` (`HealthEndpointTests`); Share flows (`ShareControllerTests` / share service tests); flexible dates / DOD validation; member details UI tests.

### Data / tooling

| File | Note |
|------|------|
| **AppDbContextFactory.cs** | Design-time only |
| **Migrations `Down()`** | Not run in tests |

---

## Low or partial coverage

- **AccountController** – SignIn GET / ExternalLoginCallback branches
- **ConfigurationExtensions** / **AuthenticationExtensions** – some env-specific branches
- **UserMenuViewComponent** – null/empty branches
- **Login/Register views** – Google button visibility branches

---

## Summary: useful next tests

1. Mocked Google external-login callback
2. Explicit Error page / ErrorViewModel assertion
3. Broader FamilyMemberController integration coverage if Sonar exclusions are tightened

Everything else is well or partially covered; prefer Sonar new-code coverage over chasing this snapshot.
