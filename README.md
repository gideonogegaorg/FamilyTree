# Family

ASP.NET Core MVC app skeleton. Solution and project live under `src/`.

## Build

```bash
dotnet build src/Family.sln
```

## Run

```bash
dotnet run --project src/Family.Web
```

Then open https://localhost:5001 (or the URL shown in the console).

## Deploy

Pushes to `main` and `dev` trigger GitHub Actions to build and deploy to EC2:

- **main** → `https://family.<DEPLOY_DOMAIN>`
- **dev** → `https://family-dev.<DEPLOY_DOMAIN>`

The deploy job uses GitHub **Environments** (`main` and `dev`). For each environment, configure:

- **SERVICE_NAME** (variable): systemd service to restart (e.g. `family` for main, `family-dev` for dev).
- **PORT** (variable): local port for the app (e.g. `5002`, `5003`).
- **DEPLOY_DOMAIN** (secret): used for deploy paths (e.g. `gideonogega.com`). Set **DEPLOY_DOMAIN** in both the **main** and **dev** environments (or once at the repository level). If the main environment blocks repository secrets, add it to the main environment as well.

Optional for Google sign-in: **GOOGLE_CLIENT_ID** and **GOOGLE_CLIENT_SECRET** (repository or environment secrets). The pipeline writes them to `$DEPLOY_PATH/.env`; when both are set, the site requires sign-in except on the home and error pages.

**Settings → Environments → [main | dev] → Environment variables / Secrets.**

See [.github/workflows/build.yml](.github/workflows/build.yml) for the pipeline.

### Validating version on Linux

On the server, the deployed DLL’s **InformationalVersion** is set by the pipeline (e.g. `202602123456789` for main, `DEV-202602123456789` for dev). To check it:

- **From the DLL** (SSH into the server):
  ```bash
  DEPLOY_PATH=/var/www/family.gideonogega.com   # or family-dev.gideonogega.com for dev
  strings "$DEPLOY_PATH/publish/Family.Web.dll" | grep -E '^(DEV-)?[0-9]{12,}$'
  ```

- **From the live site**:
  ```bash
  curl -s https://family.gideonogega.com/ | grep -o 'Family Tree [^ -]*'
  ```

### Adding a new subdomain on the server

Use the provisioning script (run on the EC2 instance, e.g. from the deploy directory):

```bash
sudo ./scripts/configure-service.sh <subdomain> <port> <service_name> [cert_domain]
# Example (dev): sudo ./scripts/configure-service.sh family-dev.example.com 5002 family-dev example.com
# Then: sudo systemctl restart family-dev
```

This creates the web directory, systemd service, and Nginx config. The pipeline runs this script **only when** `scripts/configure-service.sh` has changed or the systemd service file is missing; otherwise it just updates code, writes `.env`, publishes to `./publish`, and restarts the service. You can run the script manually from the deployed directory (e.g. `sudo /var/www/family.example.com/scripts/configure-service.sh ...`). Full prerequisites and manual steps: [docs/configure-service.md](docs/configure-service.md).
