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

- **SERVICE_NAME** (variable): systemd service to restart (e.g. `family-web` for main, `family-web-dev` for dev).
- **DEPLOY_DOMAIN** (secret): used for deploy paths (e.g. `gideonogega.com`). Set **DEPLOY_DOMAIN** in both the **main** and **dev** environments (or once at the repository level). If the main environment blocks repository secrets, add it to the main environment as well.

**Settings → Environments → [main | dev] → Environment variables / Secrets.**

See [.github/workflows/build.yml](.github/workflows/build.yml) for the pipeline.

### Validating version on Linux

On the server, the deployed DLL’s **InformationalVersion** is set by the pipeline (e.g. `202602123456789` for main, `DEV-202602123456789` for dev). To check it:

- **From the DLL** (SSH into the server):
  ```bash
  DEPLOY_PATH=/var/www/family.gideonogega.com   # or family-dev.gideonogega.com for dev
  strings "$DEPLOY_PATH/Family.Web.dll" | grep -E '^(DEV-)?[0-9]{12,}$'
  ```
  Or run the helper script (from the deploy directory): `./scripts/check-version.sh` (set `DEPLOY_PATH` if needed).

- **From the live site**:
  ```bash
  curl -s https://family.gideonogega.com/ | grep -o 'Family Tree [^ -]*'
  ```

### Adding a new subdomain on the server

Use the provisioning script (run on the EC2 instance):

```bash
sudo ./scripts/setup-subdomain.sh <subdomain> <port> <service_name> [cert_domain]
# Example (dev): sudo ./scripts/setup-subdomain.sh family-dev.example.com 5002 family-web-dev example.com
# Then: sudo systemctl restart family-web-dev
```

This creates the web directory, systemd service, and Nginx config. The GitHub Actions pipeline publishes the app into that directory, copies the repo’s **scripts** into a **scripts** subfolder there, removes the clone, then restarts the service. You can run scripts from the deployed site (e.g. `sudo /var/www/family.example.com/scripts/setup-subdomain.sh ...`). Full prerequisites and manual steps: [docs/subdomain-provisioning.md](docs/subdomain-provisioning.md).
