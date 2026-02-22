# Configure Service Guide (.NET / Nginx / Ubuntu)

Infrastructure required to host a .NET subdomain on the EC2 instance. Replace `example.com` and `YOUR_DOMAIN` with your own domain.

## Prerequisites

1. **DNS**: An A record for the subdomain pointing to the EC2 Elastic IP (e.g. `family-dev.example.com` → your IP).
2. **SSL certificates**: Let's Encrypt certs in place for your domain (see [Let's Encrypt setup](#lets-encrypt-setup) below).
3. **Port**: An unused local port for the .NET app (e.g. 5000 main, 5001 family, 5002 family-dev).

---

## Let's Encrypt setup

Do this once per server (or per base domain). The provisioning script expects certs at `/etc/letsencrypt/live/<cert_domain>/`.

### 1. Install Certbot (Ubuntu/Debian)

```bash
sudo apt update
sudo apt install -y certbot
# Optional: certbot plugin for Nginx (if you use it for HTTP-01)
sudo apt install -y python3-certbot-nginx
```

### 2. Obtain a certificate

**Option A – Single subdomain (HTTP-01)**  
Certbot can stand up a temporary HTTP server. Stop Nginx if it's using port 80, or use Nginx plugin:

```bash
sudo certbot certonly --standalone -d family.example.com
# Or with Nginx running:
sudo certbot certonly --nginx -d family.example.com
```

Certificates will be in `/etc/letsencrypt/live/family.example.com/`.

**Option B – Wildcard (DNS-01)**  
For `*.example.com` you must use DNS challenge (Certbot can't get a wildcard via HTTP):

```bash
sudo certbot certonly --manual --preferred-challenges dns -d "*.example.com" -d example.com
```

Follow the prompts and create the requested TXT record in your DNS. Certs will be in `/etc/letsencrypt/live/example.com/` (use `example.com` as the `cert_domain` when running the provisioning script).

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
sudo ./scripts/configure-service.sh <subdomain> <port> <service_name> [cert_domain]
```

- **subdomain**: Full hostname (e.g. `family-dev.example.com`).
- **port**: Local port for the .NET app (e.g. `5002`).
- **service_name**: Systemd service name; use the same as the pipeline's `SERVICE_NAME` variable for that environment (e.g. `family` for main, `family-dev` for dev).
- **cert_domain**: Base domain used for the Let's Encrypt path (e.g. `example.com` → `/etc/letsencrypt/live/example.com/`). Defaults to `example.com` if omitted.

Example:

```bash
# Dev site (matches pipeline SERVICE_NAME for dev)
sudo ./scripts/configure-service.sh family-dev.example.com 5002 family-dev example.com
# Main site
sudo ./scripts/configure-service.sh family.example.com 5001 family example.com
```

This creates the web directory, systemd unit, and Nginx config (HTTP→HTTPS and proxy to the app). The unit runs the app from `$WEB_ROOT/publish` and loads env vars from `$WEB_ROOT/.env`.

**Pipeline behaviour:** The GitHub Actions deploy job uses a "git in place" flow: it clones (or updates) the repo into the deploy path, then runs **configure-service.sh** only when the script file has changed (detected by `dorny/paths-filter`) or when the systemd service file for that environment is missing. It then writes `.env` from repository secrets (e.g. `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`), runs `dotnet publish` into `./publish`, and restarts the service. The **service_name** and **port** must match the `SERVICE_NAME` and `PORT` variables for that environment. For manual deploy, ensure `.env` exists in the web root, publish into `./publish`, then:

```bash
sudo systemctl restart family-dev
```

**Google authentication:** To enable Google sign-in, set repository (or environment) secrets `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`. The pipeline writes them to `$DEPLOY_PATH/.env` as `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret`. Configure the Google OAuth client with redirect URI `https://<your-domain>/signin-google`.

### Manual steps (without the script)

#### 1. Directory and permissions

```bash
sudo mkdir -p /var/www/<subdomain>
sudo chown -R ubuntu:www-data /var/www/<subdomain>
sudo chmod -R 775 /var/www/<subdomain>
```

#### 2. Systemd service

Create `/etc/systemd/system/<service_name>.service` with `WorkingDirectory` set to `/var/www/<subdomain>/publish`, `ExecStart` pointing at the DLL in that directory and your chosen port, and `EnvironmentFile=/var/www/<subdomain>/.env` so the app can load secrets (e.g. Google auth).

#### 3. Nginx

Add a site in `/etc/nginx/sites-available/` with HTTP→HTTPS redirect and a 443 server block that proxies to `http://localhost:<port>`. Set `ssl_certificate` and `ssl_certificate_key` to your Let's Encrypt paths. Enable the site and reload Nginx.
