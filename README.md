# GMO.FamilyTree

ASP.NET Core MVC app skeleton. Solution and project live under `src/`. Uses central package management ([Directory.Packages.props](Directory.Packages.props)), Serilog, and GMO OpenTelemetry packages from the org NuGet feed.

## Documentation

For features, testing, and setup see **[docs/](docs/)**: [docs/README.md](docs/README.md) (overview and new user guide), [tree-layout-orientation.md](docs/tree-layout-orientation.md) (visual ranks, orientations, lineage), [database-setup.md](docs/database-setup.md), [testing-environment.md](docs/testing-environment.md), [ui-testing-approach.md](docs/ui-testing-approach.md). Quick start: local setup below, then [docs/README.md](docs/README.md#new-user-guide) and [testing-environment.md](docs/testing-environment.md) for test data.

## Local setup

Start dependencies (PostgreSQL + MinIO S3):

```bash
docker compose up -d
```

MinIO serves the same bucket name as production (`gideonogega-internal`) at **http://localhost:9000** (console: **http://localhost:9001**, `minioadmin` / `minioadmin`). Object keys use prefix `family/local/` — matching prod layout with a local environment segment.

`appsettings.json` is **gitignored** and generated from [appsettings.json.template](src/GMO.FamilyTree.Web/appsettings.json.template). To set up for local dev:

```bash
# Option A: use the helper script (sets safe defaults for all placeholders)
bash scripts/generate-appsettings.sh

# Option B: copy and edit manually
cp src/GMO.FamilyTree.Web/appsettings.json.template src/GMO.FamilyTree.Web/appsettings.json
# Then replace ^^PLACEHOLDER^^ tokens with your values
```

You can override specific placeholders with env vars before running the script:

```bash
export SERILOG_LOG_PATH=../logs
export UPLOADS_PATH=../uploads
export OPENTELEMETRY_ENABLED=false
bash scripts/generate-appsettings.sh
```

## Build

```bash
dotnet build GMO.FamilyTree.sln
```

Restore requires authentication to the GMO GitHub Packages feed when using `GMO.*` packages. Set `GITHUB_USERNAME` and `GITHUB_PAT` (or use [nuget.config](src/nuget.config) with `packageSourceCredentials`) so NuGet can read from `https://nuget.pkg.github.com/gideonogegaorg/index.json`.

## Run

```bash
dotnet run --project src/GMO.FamilyTree.Web
```

With `docker compose up -d` running, launch profiles default to **Local** filesystem storage under `Paths:UploadsPath` (see [launchSettings.json](src/GMO.FamilyTree.Web/Properties/launchSettings.json)). To exercise S3 parity with MinIO, set `Photos__Provider=S3` (MinIO at `http://localhost:9000`, bucket `gideonogega-internal`, prefix `family/local/`).

Then open https://localhost:7295 (HTTPS) or http://localhost:5229 (HTTP) from [launchSettings](src/GMO.FamilyTree.Web/Properties/launchSettings.json), or the URL shown in the console.

## Deploy

Pushes to `main` and `dev` trigger GitHub Actions to build, publish, and deploy to EC2. You can also run the workflow manually (Actions > Build and Deploy > Run workflow) and choose the branch.

- **main** → `https://family.<DEPLOY_DOMAIN>`
- **dev** → `https://family-dev.<DEPLOY_DOMAIN>`

The pipeline generates `appsettings.json` from the template on the **GitHub Actions runner** (using Environment secrets), publishes it into the site output, and copies that to EC2 via SCP. **Nothing with real credentials is ever committed to git** — only `appsettings.json.template` (with `^^PLACEHOLDERS^^`) is in the public repo; generated `appsettings.json` is gitignored and exists only on the runner during CI and on the private EC2 host after deploy. Logs and user uploads live outside the app folder: the pipeline ensures `$DEPLOY_PATH/logs` and `$DEPLOY_PATH/uploads` exist and configures the app to use them.

The deploy job uses GitHub **Environments** (`main` and `dev`). For each environment, configure:

- **SERVICE_NAME** (variable): systemd service to restart (e.g. `family` for main, `family-dev` for dev).
- **PORT** (variable): local port for the app (`5002` for main, `5003` for dev).
- **DEPLOY_DOMAIN** (secret): used for deploy paths and Let's Encrypt cert path (e.g. `example.com` → `/var/www/family.example.com` and `/etc/letsencrypt/live/example.com/`). Set in both **main** and **dev** (or at repository level).

**Repository secrets** (required for CI on private repos; org secrets are not available to private repos on some plans):

- **GH_USER**: GitHub username or org name used for NuGet auth (mapped to `GITHUB_USERNAME` in CI).
- **GH_CLASSIC_PAT**: Classic PAT with `read:packages` so the build can restore GMO.* packages from GitHub Packages.
- **DEPLOY_DOMAIN** and deploy secrets (AWS, SSH, etc.) as needed.

**PostgreSQL** (per environment): **PG_USER** and **PG_PASS**. The deploy job builds the connection string as `Host=localhost;Port=5432;Database=<SERVICE_NAME>;Username=<PG_USER>;Password=<PG_PASS>` and bakes it into the published `appsettings.json` on EC2 (Postgres on the same EC2; database name matches **SERVICE_NAME**).

Optional for OpenTelemetry (same pattern — generated on the runner, deployed to EC2 only): **OPENTELEMETRY_ENABLED**, **OPENTELEMETRY_OTLPEXPORTENDPOINT**, **OPENTELEMETRY_HEADERS**, **OPENTELEMETRY_METRICSENDPOINT**, **OPENTELEMETRY_LOGGINGENDPOINT**. `Telemetry.EnvironmentName` is set automatically to `main` or `dev`.

Optional for Google sign-in: **GOOGLE_CLIENT_ID** and **GOOGLE_CLIENT_SECRET**. When both are set, the site requires sign-in except on the home and error pages.

Optional variable: **S3_PHOTOS_BUCKET** (environment variable — private bucket name for photo storage).

**Settings → Secrets and variables → Actions** (repository) and **Settings → Environments → [main | dev]** (environment variables / secrets).

See [.github/workflows/build.yml](.github/workflows/build.yml) for the pipeline.

### Validating version on Linux

On the server, the deployed DLL’s **InformationalVersion** is set by the pipeline (e.g. `202602123456789` for main, `DEV-202602123456789` for dev). To check it:

- **From the DLL** (SSH into the server):
  ```bash
  DEPLOY_PATH=/var/www/family.example.com   # or family-dev.example.com for dev
  strings "$DEPLOY_PATH/site/GMO.FamilyTree.Web.dll" | grep -E '^(DEV-)?[0-9]{12,}$'
  ```

- **From the live site**:
  ```bash
  curl -s https://family.example.com/ | grep -o 'Family Tree [^ -]*'
  ```

### Adding a new subdomain on the server

Use the provisioning script (run on the EC2 instance, e.g. from the deploy directory):

```bash
sudo ./scripts/configure-service.sh <subdomain> <port> <service_name> [cert_domain]
# Example (dev): sudo ./scripts/configure-service.sh family-dev.example.com 5003 family-dev example.com
# Then: sudo systemctl restart family-dev
```

This creates the web directory, systemd service, and Nginx config. The pipeline runs this script **only when** `scripts/configure-service.sh` has changed or the systemd service file is missing; otherwise it just copies the site output and restarts the service. Full prerequisites and manual steps: [docs/configure-service.md](docs/configure-service.md).
