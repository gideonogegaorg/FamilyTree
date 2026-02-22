# Coverage report – what’s still pending (all code)

Generated after running `.\scripts\run-coverage.ps1`. Combined line coverage: **90.6%** (1442/1591 lines). Branch coverage: **70.4%**.

Open **coverage/combined/index.html** for the full report.

---

## Zero coverage (not exercised by tests)

### Controllers

| File | What’s not covered |
|------|--------------------|
| **HomeController.cs** | Entire controller: **Index()**, **Privacy()**, **Error()**, constructor. No integration test hits `/`, `/Home`, `/Home/Privacy`, or the error page. |
| **AccountController.cs** | **SignIn (GET)** – Google OAuth challenge (no test calls it). **ExternalLoginCallback** – full external-login flow (would need mocked external auth). |

### Views (Razor)

| View | Note |
|------|------|
| **Views/Home/Index.cshtml** | Rendered only when Home/Index is called. |
| **Views/Home/Privacy.cshtml** | Rendered only when Home/Privacy is called. |
| **Views/Shared/Error.cshtml** | Rendered only when the error page is shown (e.g. developer exception or Error action). |

### Data / tooling

| File | Note |
|------|------|
| **AppDbContextFactory.cs** | **CreateDbContext** – design-time only (e.g. `dotnet ef`). Not run in app or tests. |
| **ErrorViewModel.cs** | **RequestId**, **ShowRequestId** – only used when the Error view is rendered. |
| **Migrations** | Various **Down()** methods – never run in tests (only **Up()** when applying migrations). |

---

## Low or partial coverage

- **Migrations** – Some migration classes show &lt; 100% line coverage (e.g. **AddCurrentFamilyTreeIdToUserProfile**, **MakeOwnerIdRequiredAndAddFk**); only **Up()** is run at runtime.
- **AccountController** – Remaining uncovered: SignIn GET, ExternalLoginCallback (and some branches in other actions).
- **ConfigurationExtensions** – ~82% line, 50% branch (some config paths not hit in Testing).
- **AuthenticationExtensions** – ~97% line, ~67% branch (e.g. non-Testing fallback policy not hit).
- **UserMenuViewComponent** – ~96% line, ~56% branch (some branches for null/empty cases).
- **Login/Register views** – Some branches (e.g. Google button visibility) not fully covered.

---

## Summary: what to add if you want higher coverage

1. **HomeController** – Integration tests: GET `/` (or `/Home`), GET `/Home/Privacy`, and optionally GET error page (or trigger error path).
2. **Error view / ErrorViewModel** – Covered indirectly by (1) if you add an error-page test.
3. **AccountController** – **SignIn (GET)** and **ExternalLoginCallback** – only if you introduce mocked external auth or a test-only provider.
4. **AppDbContextFactory** – Usually excluded from coverage (design-time only).
5. **Migration Down()** – Typically not tested (rollback is a manual/ops concern).

Everything else in the codebase is either well covered or only partially covered in branches (error paths, env-specific branches, or view branches).
