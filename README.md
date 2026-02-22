# GMO.Family

ASP.NET Core MVC app skeleton. Solution and project live under `src/`. Uses central package management ([Directory.Packages.props](Directory.Packages.props)), Serilog, and GMO OpenTelemetry packages from the org NuGet feed.

## Build

```bash
dotnet build src/GMO.Family.sln
```

Restore requires authentication to the GMO GitHub Packages feed when using `GMO.*` packages. Set `GITHUB_USERNAME` and `GITHUB_PAT` (or use [nuget.config](src/nuget.config) with `packageSourceCredentials`) so NuGet can read from `https://nuget.pkg.github.com/gideonogegaorg/index.json`.

## Run

```bash
dotnet run --project src/GMO.Family.Web
```

Then open https://localhost:7295 (HTTPS) or http://localhost:5229 (HTTP) from [launchSettings](src/GMO.Family.Web/Properties/launchSettings.json), or the URL shown in the console.

## Deploy

Pushes to `main` and `dev` trigger GitHub Actions to build and deploy to EC2. You can also run the workflow manually (Actions → Build and Deploy → Run workflow) and choose the branch.

- **main** → `https://family.<DEPLOY_DOMAIN>`
- **dev** → `https://family-dev.<DEPLOY_DOMAIN>`

The deploy job uses GitHub **Environments** (`main` and `dev`). For each environment, configure:

- **SERVICE_NAME** (variable): systemd service to restart (e.g. `family` for main, `family-dev` for dev).
- **PORT** (variable): local port for the app (e.g. `5002`, `5003`).
- **DEPLOY_DOMAIN** (secret): used for deploy paths and Let's Encrypt cert path (e.g. `example.com` → `/var/www/family.example.com` and `/etc/letsencrypt/live/example.com/`). Set in both **main** and **dev** (or at repository level).

**Repository secrets** (required for CI on private repos; org secrets are not available to private repos on some plans):

- **GH_USER**: GitHub username or org name used for NuGet auth (mapped to `GITHUB_USERNAME` in CI).
- **GH_CLASSIC_PAT**: Classic PAT with `read:packages` so the build can restore GMO.* packages from GitHub Packages. Also used for deploy git clone (or use a separate **GH_PAT** for deploy).
- **DEPLOY_DOMAIN**, **GOOGLE_CLIENT_ID**, **GOOGLE_CLIENT_SECRET**, and deploy secrets (AWS, SSH, etc.) as needed.

Optional for OpenTelemetry (pipeline writes these to `.env` when set): **OPENTELEMETRY_ENABLED**, **OPENTELEMETRY_OTLPEXPORTENDPOINT**, **OPENTELEMETRY_HEADERS**, **OPENTELEMETRY_METRICSENDPOINT**, **OPENTELEMETRY_LOGGINGENDPOINT**. `Telemetry__EnvironmentName` is set automatically to `main` or `dev`.

Optional for Google sign-in: **GOOGLE_CLIENT_ID** and **GOOGLE_CLIENT_SECRET**. When both are set, the pipeline writes them to `$DEPLOY_PATH/.env` and the site requires sign-in except on the home and error pages.

**Settings → Secrets and variables → Actions** (repository) and **Settings → Environments → [main | dev]** (environment variables / secrets).

See [.github/workflows/build.yml](.github/workflows/build.yml) for the pipeline.

### Validating version on Linux

On the server, the deployed DLL’s **InformationalVersion** is set by the pipeline (e.g. `202602123456789` for main, `DEV-202602123456789` for dev). To check it:

- **From the DLL** (SSH into the server):
  ```bash
  DEPLOY_PATH=/var/www/family.example.com   # or family-dev.example.com for dev
  strings "$DEPLOY_PATH/publish/GMO.Family.Web.dll" | grep -E '^(DEV-)?[0-9]{12,}$'
  ```

- **From the live site**:
  ```bash
  curl -s https://family.example.com/ | grep -o 'Family Tree [^ -]*'
  ```

### Adding a new subdomain on the server

Use the provisioning script (run on the EC2 instance, e.g. from the deploy directory):

```bash
sudo ./scripts/configure-service.sh <subdomain> <port> <service_name> [cert_domain]
# Example (dev): sudo ./scripts/configure-service.sh family-dev.example.com 5002 family-dev example.com
# Then: sudo systemctl restart family-dev
```

This creates the web directory, systemd service, and Nginx config. The pipeline runs this script **only when** `scripts/configure-service.sh` has changed or the systemd service file is missing; otherwise it just updates code, writes `.env`, publishes to `./publish`, and restarts the service. You can run the script manually from the deployed directory (e.g. `sudo /var/www/family.example.com/scripts/configure-service.sh ...`). Full prerequisites and manual steps: [docs/configure-service.md](docs/configure-service.md).
