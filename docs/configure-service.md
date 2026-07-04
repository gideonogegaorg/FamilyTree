# Configure Service Guide (.NET / Nginx / Ubuntu)

Infrastructure required to host a .NET subdomain on the EC2 instance. Hostnames are `family.<DEPLOY_DOMAIN>` and `family-dev.<DEPLOY_DOMAIN>` (the `DEPLOY_DOMAIN` secret; examples below use `example.com`).

## Prerequisites

1. **DNS**: An A record for the subdomain pointing to the EC2 Elastic IP (e.g. `family-dev.example.com` → your IP).
2. **SSL certificates**: Let's Encrypt certs in place for your domain (see [Let's Encrypt setup](#lets-encrypt-setup) below).
3. **Port**: An unused local port for the .NET app (`5002` for main/`family`, `5003` for `family-dev`).
4. **ASP.NET Core Runtime 10**: Publish is framework-dependent, so the host needs `Microsoft.AspNetCore.App` 10.x (`/usr/bin/dotnet`). The pipeline runs [scripts/install-aspnetcore-runtime.sh](../scripts/install-aspnetcore-runtime.sh) on every deploy (idempotent `apt` install of `aspnetcore-runtime-10.0`). Manual provisioning via `configure-service.sh` runs the same script. To install by hand:

```bash
sudo ./scripts/install-aspnetcore-runtime.sh
# or: sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-10.0
```

---

## Let's Encrypt setup

Do this once per server (or per base domain). The provisioning script expects certs at `/etc/letsencrypt/live/<cert_domain>/` (same value as the `DEPLOY_DOMAIN` secret).

### 1. Install Certbot (Ubuntu/Debian)

```bash
sudo apt update
sudo apt install -y certbot python3-certbot-dns-route53
# Optional: Nginx plugin (HTTP-01 only)
sudo apt install -y python3-certbot-nginx
```

The EC2 instance uses an IAM instance profile (e.g. `EC2-Certbot-Role`) with `route53:ChangeResourceRecordSets` on the hosted zone so Certbot can complete DNS-01 challenges without static credentials on the box.

### 2. Obtain a certificate

**Recommended – Wildcard (DNS-01 via Route 53)**  
One cert covers the apex and all subdomains; use `example.com` as `cert_domain` when running the provisioning script.

```bash
sudo certbot certonly --dns-route53 \
  -d example.com -d "*.example.com" \
  --non-interactive --agree-tos \
  --register-unsafely-without-email \
  --cert-name example.com
```

Certificates will be in `/etc/letsencrypt/live/example.com/`.

**Alternative – Single subdomain (HTTP-01)**  
Use when you only need one hostname and port 80 is available to Certbot:

```bash
sudo certbot certonly --nginx -d family.example.com
```

Certificates will be in `/etc/letsencrypt/live/family.example.com/` (use that full name as `cert_domain` if you go this route).

### 3. Auto-renewal

Certbot installs a timer. Test and enable:

```bash
sudo certbot renew --dry-run
sudo systemctl enable certbot.timer
```

### 4. Optional: Nginx SSL options

For stronger SSL settings, you can generate DH params and use Certbot's Nginx snippets (uncomment the matching lines in the generated Nginx config if present):

```bash
sudo openssl dhparam -out /etc/letsencrypt/ssl-dhparams.pem 2048
```

Then in your Nginx server block you can add:

```nginx
include /etc/letsencrypt/options-ssl-nginx.conf;
ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;
```

---

## Provisioning a new subdomain

### Using the script (recommended)

From the repo root on the server (or from the deployed directory, e.g. `$DEPLOY_PATH`):

```bash
sudo ./scripts/configure-service.sh <subdomain> <port> <service_name> [cert_domain] [is_production]
```

- **subdomain**: Full hostname (e.g. `family-dev.example.com`).
- **port**: Local port for the .NET app (`5002` main, `5003` dev).
- **service_name**: Systemd service name; use the same as the pipeline's `SERVICE_NAME` variable for that environment (e.g. `family` for main, `family-dev` for dev).
- **cert_domain**: Base domain used for the Let's Encrypt path (e.g. `example.com` → `/etc/letsencrypt/live/example.com/`). Defaults to `example.com` if omitted.
- **is_production**: Optional. `true` for production (no `X-Robots-Tag`); otherwise adds `noindex, nofollow`.

Example:

```bash
# Main site (matches pipeline SERVICE_NAME / PORT for main)
sudo ./scripts/configure-service.sh family.example.com 5002 family example.com true
# Dev site
sudo ./scripts/configure-service.sh family-dev.example.com 5003 family-dev example.com false
```

This creates the web directory, systemd unit, and Nginx config (HTTP→HTTPS and proxy to the app). The unit runs the app from `$WEB_ROOT/site` and loads env vars from `$WEB_ROOT/.env` if present (optional).

**Pipeline behaviour:** The GitHub Actions deploy job generates `appsettings.json` on the runner, runs `dotnet publish` into `./site`, then SCPs `site/*`, `scripts/configure-service.sh`, and `scripts/install-aspnetcore-runtime.sh` to the server. It always runs **install-aspnetcore-runtime.sh** (ensures ASP.NET Core 10), then runs **configure-service.sh** only when that script has changed or the systemd service file is missing; otherwise it copies the site output into `$DEPLOY_PATH/site`, ensures `$DEPLOY_PATH/logs` and `$DEPLOY_PATH/uploads` exist, and restarts the service. User uploads (e.g. profile photos) are stored under `$DEPLOY_PATH/uploads` and served at `/uploads`. The **service_name** and **port** must match the `SERVICE_NAME` and `PORT` variables for that environment. For manual deploy, generate `appsettings.json` (e.g. from the template), publish into `./site`, then:

```bash
sudo systemctl restart family-dev
```

**Google authentication:** To enable Google sign-in, set repository (or environment) secrets `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`. The pipeline bakes them into `appsettings.json` (generated from the template) so no `.env` is required. Configure the Google OAuth client with redirect URIs:

- `https://family.<DEPLOY_DOMAIN>/signin-google`
- `https://family-dev.<DEPLOY_DOMAIN>/signin-google`

### Manual steps (without the script)

#### 1. Directory and permissions

```bash
sudo mkdir -p /var/www/<subdomain>
sudo chown -R ubuntu:www-data /var/www/<subdomain>
sudo chmod -R 775 /var/www/<subdomain>
```

#### 2. Systemd service

Create `/etc/systemd/system/<service_name>.service` with `WorkingDirectory` set to `/var/www/<subdomain>/site`, `ExecStart` pointing at the DLL in that directory and your chosen port, and optionally `EnvironmentFile=-/var/www/<subdomain>/.env` so the app can load extra env vars (e.g. overrides).

#### 3. Nginx

Add a site in `/etc/nginx/sites-available/` with HTTP→HTTPS redirect and a 443 server block that proxies to `http://localhost:<port>`. Set `ssl_certificate` and `ssl_certificate_key` to your Let's Encrypt paths. Enable the site and reload Nginx.
