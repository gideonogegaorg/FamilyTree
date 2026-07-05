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

This creates the web directory, systemd unit, and Nginx config (HTTP→HTTPS and proxy to the app). The unit runs the app from `$WEB_ROOT/site`. Optional `$WEB_ROOT/.env` can supply extra env-var overrides via `EnvironmentFile`.

**Pipeline behaviour:** The GitHub Actions deploy job generates `appsettings.json` on the runner from the template and GitHub Environment secrets, runs `dotnet publish` into `./site`, then SCPs `site/*`, `scripts/configure-service.sh`, and `scripts/install-aspnetcore-runtime.sh` to the server. Generated config is **not** in the public git repo — it lands only on EC2. It always runs **install-aspnetcore-runtime.sh** (ensures ASP.NET Core 10), then runs **configure-service.sh** only when that script has changed or the systemd service file is missing; otherwise it copies the site output into `$DEPLOY_PATH/site`, ensures `$DEPLOY_PATH/logs` and `$DEPLOY_PATH/uploads` exist, and restarts the service. Legacy profile photos under `$DEPLOY_PATH/uploads` may still be served for accounts that have not re-uploaded; new profile and member photos are stored in a **private S3 bucket** (see below) and served only through authenticated `/photos/...` endpoints. The **service_name** and **port** must match the `SERVICE_NAME` and `PORT` variables for that environment. For manual deploy, generate `appsettings.json` locally (never commit it), publish into `./site`, then:

```bash
sudo systemctl restart family-dev
```

**Google authentication:** Set environment secrets `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`. The pipeline bakes them into the deployed `appsettings.json` on EC2. Configure the Google OAuth client with redirect URIs:

- `https://family.<DEPLOY_DOMAIN>/signin-google`
- `https://family-dev.<DEPLOY_DOMAIN>/signin-google`

---

## Private photo storage (S3)

Profile and member photos are stored in a **private** S3 bucket (no public ACL or bucket policy). The EC2 instance reads and writes objects via its **instance profile**; browsers receive images only through authenticated app routes (`GET /photos/profiles/me`, `GET /photos/members/{id}`).

### Cost-effective layout (recommended)

Use **one shared private bucket** for the organization and isolate apps/environments with a **path prefix**:

| Deploy | `StoragePrefix` | Example object key |
|--------|-----------------|-------------------|
| Family prod | `family/prod/` | `family/prod/members/12/34.jpg` |
| Family dev | `family/dev/` | `family/dev/profiles/user-id.jpg` |
| Another app | `other-app/prod/` | `other-app/prod/...` |

- Empty buckets cost nothing; you pay for storage and requests. One bucket vs several is negligible in cost but simpler to operate.
- Set the same `S3_PHOTOS_BUCKET` secret for all environments; differentiate with `PHOTOS_APP_NAME` + `PHOTOS_ENVIRONMENT` (the deploy pipeline sets `family/prod` or `family/dev` automatically).
- Override with an explicit `PHOTOS_STORAGE_PREFIX` env var when needed.
- The database stores **logical** keys only (`members/{treeId}/{memberId}.jpg`); the prefix is applied at read/write time.

Local dev/CI uses `Photos:Provider` = `Local` by default (see [launchSettings.json](../src/GMO.FamilyTree.Web/Properties/launchSettings.json)). For **localhost S3 parity**, run `docker compose up -d` and set `Photos__Provider=S3` (MinIO at `http://localhost:9000`, bucket `gideonogega-internal`, prefix `family/local/`).

### Alternative: separate buckets per environment

You can still point `S3_PHOTOS_BUCKET` at different buckets per GitHub environment (e.g. `family-photos-prod` vs `family-photos-dev`) if you prefer hard isolation. Leave `StoragePrefix` empty or use a short app prefix.

### 1. Create the bucket

Create one org-wide private bucket: **`gideonogega-internal`** (gideonogegaorg). Block all public access. Provision with:

```bash
./scripts/setup-s3-photos-bucket.sh
```

### 2. IAM policy for the EC2 instance role

**Shared bucket with prefix** (recommended — scope prod EC2 to prod prefix only):

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": "arn:aws:s3:::gideonogega-internal/family/prod/*"
    },
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": "arn:aws:s3:::gideonogega-internal",
      "Condition": {
        "StringLike": { "s3:prefix": ["family/prod/*"] }
      }
    }
  ]
}
```

Use `family/dev/*` on the dev instance.

### 3. App configuration

Set repository secret `S3_PHOTOS_BUCKET` to `gideonogega-internal` (or rely on the default in `generate-appsettings.sh`). The deploy pipeline generates:

```json
"Photos": {
  "Provider": "S3",
  "S3Bucket": "gideonogega-internal",
  "StoragePrefix": "family/prod",
  "LocalBasePath": "uploads/photos"
}
```

Logical object keys (stored in DB): `profiles/{userId}{ext}`, `members/{treeId}/{memberId}{ext}`.
Stored S3 keys: `{StoragePrefix}/profiles/...`, `{StoragePrefix}/members/...`.

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
