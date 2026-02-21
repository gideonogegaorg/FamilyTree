#!/bin/bash

# ==============================================================================
# AUTOMATED SUBDOMAIN PROVISIONER
# Usage: sudo ./setup-subdomain.sh <subdomain> <port> <service_name> [cert_domain]
#   subdomain   Full hostname (e.g. family-dev.example.com)
#   port        Local port for the .NET app (e.g. 5002)
#   service_name  Systemd service name (e.g. family-dev)
#   cert_domain  Optional. Base domain for Let's Encrypt cert path
#                (e.g. example.com → /etc/letsencrypt/live/example.com/).
#                Default: example.com (replace with your cert domain).
# Example: sudo ./setup-subdomain.sh family-dev.example.com 5002 family-dev example.com
# See docs/subdomain-provisioning.md for Let's Encrypt setup.
# ==============================================================================

set -e

# 1. VALIDATION
if [ "$#" -lt 3 ] || [ "$#" -gt 4 ]; then
    echo "Usage: sudo $0 <subdomain> <port> <service_name> [cert_domain]"
    echo "Example: sudo $0 family-dev.example.com 5002 family-dev example.com"
    exit 1
fi

DOMAIN=$1
PORT=$2
SERVICE_NAME=$3
CERT_DOMAIN=${4:-example.com}
WEB_ROOT="/var/www/$DOMAIN"
DLL_NAME="Family.Web.dll" # Change this if your DLL name varies per project

# Ensure running as root
if [ "$EUID" -ne 0 ]; then
  echo "Error: Please run as root (sudo)"
  exit 1
fi

echo "Starting provisioning for $DOMAIN on port $PORT (cert domain: $CERT_DOMAIN)..."

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
Description=.NET Web App - $DOMAIN
After=network.target

[Service]
WorkingDirectory=$WEB_ROOT
ExecStart=/usr/bin/dotnet $WEB_ROOT/$DLL_NAME --urls "http://localhost:$PORT"
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
NGINX_FILE="/etc/nginx/sites-available/$DOMAIN"

cat <<EOF > "$NGINX_FILE"
# HTTP -> HTTPS Redirect
server {
    listen 80;
    server_name $DOMAIN;
    return 301 https://\$host\$request_uri;
}

# HTTPS Server Block
server {
    listen 443 ssl;
    server_name $DOMAIN;

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
echo "1. Deploy your code to: $WEB_ROOT"
echo "2. Start the app: sudo systemctl restart $SERVICE_NAME"
echo "========================================================"