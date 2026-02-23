# Coverage report: topic/NewUser vs dev (combined unit + integration)

This report is generated from **combined** unit and integration test coverage (merged Cobertura XML). To regenerate:

1. From repo root, run: `.\scripts\run-coverage.ps1`  
   - Runs `dotnet test GMO.Family.sln --collect:"XPlat Code Coverage" --results-directory ./coverage`
   - Merges all `coverage/**/coverage.cobertura.xml` into `coverage/combined/` using ReportGenerator
   - Produces `coverage/combined/index.html` (view in browser) and `coverage/combined/Cobertura.xml`

2. Open `coverage/combined/index.html` for the full report.

## Prerequisites

- From `src/`: `dotnet tool restore` (installs ReportGenerator if needed).

---

## Summary (from last combined run)

Combined coverage aggregates unit tests (DefaultFamilyTreeService, CurrentFamilyTreeService, LoggingEmailSender) and integration tests (FamilyTreeController, AccountController, TestAuthController, UserMenu, Program).

| Area | Line rate | Branch rate | Notes |
|------|-----------|-------------|--------|
| **Program** | 1 | 0.86 | Startup covered by integration. |
| **FamilyTreeController** | 1 | 0.65 | All actions covered; some branches (e.g. validation) partial. |
| **TestAuthController** | 0.95 | 0.75 | SignIn covered; one branch (e.g. env check) partial. |
| **CurrentFamilyTreeService** | 0.98 | 0.84 | Unit + integration; most paths covered. |
| **DefaultFamilyTreeService** | 1 | 1 | Full coverage. |
| **LoggingEmailSender** | 1 | 1 | Full coverage. |
| **AccountController** | ~0.31–0.52* | ~0.20–0.25* | *Rises after new tests (SignOut, Forgot, Reset, Upload, Switch, AccessDenied, Login existing-tree). |
| **UserMenuViewComponent** | ~0.95 | ~0.50 | Unauthenticated path covered by integration (Sign in link). |
| **AuthenticationExtensions** | ~0.97 | ~0.67 | One branch (non-Testing fallback policy) not hit in tests. |

*AccountController numbers improve once the new Account tests are run (see below).

---

## Tests added for items 1–3

1. **AccountController (item 1)**  
   - SignOut POST, ForgotPassword GET/POST (existing + unknown email), ForgotPasswordConfirmation GET  
   - ResetPassword GET (with/without token), ResetPassword POST (invalid token), ResetPasswordConfirmation GET  
   - AccessDenied GET, UploadPhoto GET, UploadPhoto POST (no file + valid PNG), SwitchFamilyTree POST  

2. **Login POST when user already has trees (item 2)**  
   - Register then Login again; asserts redirect (covers path where `EnsureDefaultFamilyTreeAsync` returns null).  

3. **Unauthenticated UserMenu (item 3)**  
   - `Unauthenticated_request_shows_sign_in_link_in_menu`: GET /Account/Login without signing in; asserts "Sign in" in response (exercises UserMenu unauthenticated branch).  

After building and running `.\scripts\run-coverage.ps1`, re-open `coverage/combined/index.html` to see updated AccountController and overall coverage.

---

## Remaining gaps (vs dev diff) after new tests

- **AccountController**: SignIn GET (Google Challenge) and **ExternalLoginCallback** (full flow) – not exercised by tests; would require mocked external auth.
- **AuthenticationExtensions**: Branch where environment is not Testing (fallback auth policy) – tests run in Testing.
- **AppDbContextFactory**: 0% – design-time only; typically excluded from coverage.
- **HomeController**: Not part of NewUser diff in the same way; any missing coverage is pre-existing.

---

## File reference

- **Script**: `scripts/run-coverage.ps1`  
- **Merged output**: `coverage/combined/index.html`, `coverage/combined/Cobertura.xml`  
- **Tool**: ReportGenerator (see `src/dotnet-tools.json`).
