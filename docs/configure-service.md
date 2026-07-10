# Configure Service Guide (.NET / Nginx / Ubuntu)

Infrastructure required to host a .NET subdomain on the EC2 instance. Hostnames are `familytree.<DEPLOY_DOMAIN>` and `familytree-dev.<DEPLOY_DOMAIN>` (the `DEPLOY_DOMAIN` secret; examples below use `example.com`).

## Prerequisites

1. **DNS**: An A record for the subdomain pointing to the EC2 Elastic IP (e.g. `familytree-dev.example.com` → your IP).
2. **SSL certificates**: Let's Encrypt certs in place for your domain (see [Let's Encrypt setup](#lets-encrypt-setup) below).
3. **Port**: An unused local port for the .NET app (`5002` for prod/`familytree`, `5003` for `familytree-dev`).
Generic nginx/systemd/certbot steps live in [gideonogegaorg/DevOps](https://github.com/gideonogegaorg/DevOps) (`ec2/nginx/configure-subdomain.sh`, `ec2/certbot/setup-wildcard-route53.sh`). The deploy pipeline checks out DevOps and runs those scripts automatically when the workflow changes or the systemd unit is missing.

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
sudo certbot certonly --nginx -d familytree.example.com
```

Certificates will be in `/etc/letsencrypt/live/familytree.example.com/` (use that full name as `cert_domain` if you go this route).

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

Clone DevOps on the server and run `ec2/nginx/configure-subdomain.sh` (see [domain-migration.md](https://github.com/gideonogegaorg/DevOps/blob/main/ec2/domain-migration.md)):

```bash
sudo DLL_NAME=GMO.FamilyTree.Web.dll ./configure-subdomain.sh <subdomain> <port> <service_name> [cert_domain] [is_production]
```

- **subdomain**: Full hostname (e.g. `familytree-dev.example.com`).
- **port**: Local port for the .NET app (`5002` prod, `5003` dev).
- **service_name**: Systemd service name; use the same as the pipeline's `SERVICE_NAME` variable for that environment (e.g. `familytree` for prod, `familytree-dev` for dev).
- **cert_domain**: Base domain used for the Let's Encrypt path (e.g. `example.com` → `/etc/letsencrypt/live/example.com/`). Defaults to `example.com` if omitted.
- **is_production**: Optional. `true` for production (no `X-Robots-Tag`); otherwise adds `noindex, nofollow`.

Example:

```bash
# Production site (matches pipeline SERVICE_NAME / PORT for prod)
sudo DLL_NAME=GMO.FamilyTree.Web.dll ./configure-subdomain.sh familytree.example.com 5002 familytree example.com true
# Dev site
sudo DLL_NAME=GMO.FamilyTree.Web.dll ./configure-subdomain.sh familytree-dev.example.com 5003 familytree-dev example.com false
```

This creates the web directory, systemd unit, and Nginx config (HTTP→HTTPS and proxy to the app). The unit runs the app from `$WEB_ROOT/site`. **Configuration** (connection strings, secrets, S3, OAuth) comes from CI-generated `appsettings.json` in that folder — not from a host `.env` file.

**Pipeline behaviour:** The GitHub Actions deploy job checks out DevOps, generates `appsettings.json`, publishes into `./site`, SCPs the site and DevOps scripts to EC2, and runs `dotnet/ec2/deploy-to-ec2.sh`. Each deploy replaces `site/appsettings.json` and removes any legacy `$WEB_ROOT/.env` override.

```bash
sudo systemctl restart familytree-dev
```

**Google authentication:** Set environment secrets `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`. The pipeline bakes them into the deployed `appsettings.json` on EC2. Configure the Google OAuth client with redirect URIs:

- `https://familytree.<DEPLOY_DOMAIN>/signin-google`
- `https://familytree-dev.<DEPLOY_DOMAIN>/signin-google`

**Transactional email (AWS SES):** Password confirmation, password reset, and tree-share invites send via SES when `Email:Provider` is `Ses`.

| Config | Source | Notes |
|--------|--------|--------|
| `Email:Provider` | var `EMAIL_PROVIDER` (default `Ses` on deploy) | Use `Logging` locally |
| `Email:FromDisplayName` | var `EMAIL_FROM_DISPLAY_NAME` (default `GOOM Family Tree`) | Shown in the From header |
| `Email:FromAddress` | derived as `noreply@{FULL_HOSTNAME}` unless secret `EMAIL_FROM_ADDRESS` is set | e.g. `noreply@familytree-dev.goom.life` / `noreply@familytree.goom.life` |
| `Email:Region` | var `EMAIL_REGION` (default `us-east-1`) | Must match the SES region |

SES setup checklist (account `360673240635`, region `us-east-1`):

1. Verify sending domains in SES: `goom.life`, `familytree.goom.life`, `familytree-dev.goom.life` (Easy DKIM **Successful**).
2. Publish Easy DKIM CNAMEs in Route53 (`goom.life` zone) and SPF TXT (`v=spf1 include:amazonses.com ~all`); optional DMARC.
3. Request production access (leave the SES sandbox) so you can send to arbitrary recipients. Until approved, only verified recipient identities can receive mail.
   - Current status (2026-07-10): **DENIED** (case `178365954000258`). CLI resubmit returns `ConflictException`.
   - Next: AWS Console → Support Center → open/reply on that case (or SES → Account dashboard). Describe bounce/complaint handling (SES account-level suppression), recipient consent (self-registration / owner invite), DKIM+SPF on `goom.life`, and low transactional volume. Paid Support may be required to get a human review.
4. Grant the EC2 instance role (`EC2-Certbot-Role`) `ses:SendEmail` and `ses:SendRawEmail` (inline policy `FamilyTreeSesSendPolicy`).
5. Set GitHub environment variables on `dev` / `prod`: `EMAIL_PROVIDER=Ses`, `EMAIL_REGION=us-east-1`, `EMAIL_FROM_DISPLAY_NAME=GOOM Family Tree`; redeploy and smoke-test register confirmation, forgot-password, or a share invite.

---

## Private photo storage (S3)

Profile and member photos are stored in a **private** S3 bucket (no public ACL or bucket policy). The EC2 instance reads and writes objects via its **instance profile**; browsers receive images only through authenticated app routes (`GET /photos/profiles/me`, `GET /photos/members/{id}`).

### Cost-effective layout (recommended)

Use **one shared private bucket** for the organization and isolate apps/environments with a **path prefix**:

| Deploy | `StoragePrefix` | Example object key |
|--------|-----------------|-------------------|
| Family prod | `familytree/prod/` | `familytree/prod/members/12/34.jpg` |
| Family dev | `familytree/dev/` | `familytree/dev/profiles/user-id.jpg` |
| Another app | `other-app/prod/` | `other-app/prod/...` |

- Empty buckets cost nothing; you pay for storage and requests. One bucket vs several is negligible in cost but simpler to operate.
- Set the same `S3_PHOTOS_BUCKET` secret for all environments; differentiate with `PHOTOS_APP_NAME` + `PHOTOS_ENVIRONMENT` (the deploy pipeline sets `familytree/prod` or `familytree/dev` automatically).
- Override with an explicit `PHOTOS_STORAGE_PREFIX` env var when needed.
- The database stores **logical** keys only (`members/{treeId}/{memberId}.jpg`); the prefix is applied at read/write time.

Local dev/CI uses `Photos:Provider` = `Local` by default (see [launchSettings.json](../src/GMO.FamilyTree.Web/Properties/launchSettings.json)). For **localhost S3 parity**, run `docker compose up -d` and set `Photos__Provider=S3` (MinIO at `http://localhost:9000`, bucket `gideonogega-internal`, prefix `familytree/local/`).

### Alternative: separate buckets per environment

You can still point `S3_PHOTOS_BUCKET` at different buckets per GitHub environment (e.g. `familytree-photos-prod` vs `familytree-photos-dev`) if you prefer hard isolation. Leave `StoragePrefix` empty or use a short app prefix.

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
      "Resource": "arn:aws:s3:::gideonogega-internal/familytree/prod/*"
    },
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": "arn:aws:s3:::gideonogega-internal",
      "Condition": {
        "StringLike": { "s3:prefix": ["familytree/prod/*"] }
      }
    }
  ]
}
```

Use `familytree/dev/*` on the dev instance.

### 3. App configuration

Set repository secret `S3_PHOTOS_BUCKET` to `gideonogega-internal` (or rely on the default in `generate-appsettings.sh`). The deploy pipeline generates:

```json
"Photos": {
  "Provider": "S3",
  "S3Bucket": "gideonogega-internal",
  "StoragePrefix": "familytree/prod",
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

Create `/etc/systemd/system/<service_name>.service` with `WorkingDirectory` set to `/var/www/<subdomain>/site`, `ExecStart` pointing at the DLL in that directory and your chosen port. Put connection strings and secrets in `site/appsettings.json` (generated by CI); do not use a host `.env` override.

#### 3. Nginx

Add a site in `/etc/nginx/sites-available/` with HTTP→HTTPS redirect and a 443 server block that proxies to `http://localhost:<port>`. Set `ssl_certificate` and `ssl_certificate_key` to your Let's Encrypt paths. Enable the site and reload Nginx.
