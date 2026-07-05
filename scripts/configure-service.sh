#!/bin/bash

# ==============================================================================
# AUTOMATED SUBDOMAIN PROVISIONER
# Usage: sudo ./configure-service.sh <DEPLOY_DOMAIN> <port> <service_name> [cert_domain] [is_production]
#   DEPLOY_DOMAIN  Full hostname (e.g. family-dev.example.com); matches pipeline.
#   port           Local port for the .NET app (e.g. 5002)
#   service_name   Systemd service name; match pipeline SERVICE_NAME (prod=family, dev=family-dev)
#   cert_domain    Optional. Base domain for Let's Encrypt cert path
#                  (e.g. example.com → /etc/letsencrypt/live/example.com/).
#                  Default: example.com (replace with your cert domain).
#   is_production  Optional. "true" for production (no X-Robots-Tag), else adds noindex header.
# Example: sudo ./configure-service.sh family-dev.example.com 5002 family-dev example.com false
# See docs/configure-service.md for Let's Encrypt setup.
# ==============================================================================

set -e

# 1. VALIDATION
if [ "$#" -lt 3 ] || [ "$#" -gt 5 ]; then
    echo "Usage: sudo $0 <DEPLOY_DOMAIN> <port> <service_name> [cert_domain] [is_production]"
    echo "Example: sudo $0 family-dev.example.com 5002 family-dev example.com false"
    exit 1
fi

DEPLOY_DOMAIN=$1
PORT=$2
SERVICE_NAME=$3
CERT_DOMAIN=${4:-example.com}
IS_PRODUCTION=${5:-false}
WEB_ROOT="/var/www/$DEPLOY_DOMAIN"
DLL_NAME="GMO.FamilyTree.Web.dll" # Change this if your DLL name varies per project

# Ensure running as root
if [ "$EUID" -ne 0 ]; then
  echo "Error: Please run as root (sudo)"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$SCRIPT_DIR/install-aspnetcore-runtime.sh" ]; then
  bash "$SCRIPT_DIR/install-aspnetcore-runtime.sh"
else
  echo "Warning: install-aspnetcore-runtime.sh not found next to this script; skipping runtime install."
fi

echo "Starting provisioning for $DEPLOY_DOMAIN on port $PORT (cert domain: $CERT_DOMAIN, is_production: $IS_PRODUCTION)..."

# ==============================================================================
# 2. DIRECTORY & PERMISSIONS
# ==============================================================================
echo "Ensuring web directories..."
mkdir -p "$WEB_ROOT"

# Set ownership to ubuntu (deployment user) but group to www-data (web server)
chown -R ubuntu:www-data "$WEB_ROOT"
chmod -R 775 "$WEB_ROOT"
echo "Directories asserted at $WEB_ROOT"

# ==============================================================================
# 3. SYSTEMD SERVICE
# ==============================================================================
echo "Generating Systemd service..."
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"

cat <<EOF > "$SERVICE_FILE"
[Unit]
Description=$DEPLOY_DOMAIN
After=network.target

[Service]
WorkingDirectory=$WEB_ROOT/site
ExecStart=/usr/bin/dotnet $WEB_ROOT/site/$DLL_NAME --urls "http://localhost:$PORT"
EnvironmentFile=-$WEB_ROOT/.env
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$SERVICE_NAME
User=ubuntu
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOF

echo "Service file asserted at $SERVICE_FILE"

# ==============================================================================
# 4. NGINX CONFIGURATION
# ==============================================================================
echo "Generating Nginx config..."
NGINX_FILE="/etc/nginx/sites-available/$DEPLOY_DOMAIN"

# Build X-Robots-Tag directive for non-production environments
ROBOTS_HEADER=""
if [ "$IS_PRODUCTION" != "true" ] && [ "$IS_PRODUCTION" != "production" ]; then
    ROBOTS_HEADER='add_header X-Robots-Tag "noindex, nofollow" always;'
fi

cat <<EOF > "$NGINX_FILE"
# HTTP -> HTTPS Redirect
server {
    listen 80;
    server_name $DEPLOY_DOMAIN;
    return 301 https://\$host\$request_uri;
}

# HTTPS Server Block
server {
    listen 443 ssl;
    server_name $DEPLOY_DOMAIN;

    # Let's Encrypt (cert_domain = $CERT_DOMAIN)
    ssl_certificate /etc/letsencrypt/live/$CERT_DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$CERT_DOMAIN/privkey.pem;

    # Hardening (Commented out to prevent errors if missing)
    # include /etc/letsencrypt/options-ssl-nginx.conf;
    # ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    location / {
        proxy_pass         http://localhost:$PORT;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host \$host;
        proxy_cache_bypass \$http_upgrade;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        $ROBOTS_HEADER
    }
}
EOF

echo "Nginx config asserted at $NGINX_FILE"

# ==============================================================================
# 5. ACTIVATION
# ==============================================================================
echo "Activating configurations..."

# Enable Nginx Site (Symlink - force overwrite if exists)
ln -sf "$NGINX_FILE" "/etc/nginx/sites-enabled/"
echo "Nginx site linked."

# Reload Daemons
systemctl daemon-reload
echo "Systemd daemon reloaded."

# Enable Service (Idempotent)
systemctl enable "$SERVICE_NAME"
echo "Service enabled."

# Test and Reload Nginx
if nginx -t > /dev/null 2>&1; then
    systemctl reload nginx
    echo "Nginx reloaded successfully."
else
    echo "Error: Nginx configuration failed verification. Please check $NGINX_FILE"
    # Run test again without silent output so the user can see the error
    nginx -t
    exit 1
fi

echo "========================================================"
echo "Provisioning Complete!"
echo "1. Deploy your code to: $WEB_ROOT (app in $WEB_ROOT/site)"
echo "2. Put .env in $WEB_ROOT for optional env-var overrides (e.g. Google auth)"
echo "3. Start the app: sudo systemctl restart $SERVICE_NAME"
echo "========================================================"
