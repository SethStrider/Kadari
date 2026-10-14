#!/bin/sh
# Regenerates deploy/nginx/snippets/cloudflare-real-ip.conf from Cloudflare's published ranges, then shows the diff.
# Run monthly (or when Cloudflare announces a change), review, commit, and reload the edge:
#   docker compose -f deploy/docker-compose.prod.yml exec edge nginx -s reload
set -eu

out="$(dirname "$0")/../nginx/snippets/cloudflare-real-ip.conf"
tmp="$(mktemp)"

{
    echo "# The real client address (GL-06). Cloudflare puts the visitor's IP in CF-Connecting-IP; nginx believes it ONLY"
    echo "# when the TCP peer is one of Cloudflare's published ranges, so a request that reaches the origin some other way"
    echo "# cannot pick its own IP. Rate limits and session audit IPs downstream then see the visitor, not Cloudflare."
    echo "#"
    echo "# Source: https://www.cloudflare.com/ips-v4 and https://www.cloudflare.com/ips-v6 (checked $(date -u +%Y-%m-%d))."
    echo "# Refresh with deploy/scripts/update-cloudflare-ips.sh and reload nginx; Cloudflare announces changes in advance."
    echo
    for range in $(curl -fsS https://www.cloudflare.com/ips-v4) $(curl -fsS https://www.cloudflare.com/ips-v6); do
        echo "set_real_ip_from ${range};"
    done
    echo
    echo "real_ip_header CF-Connecting-IP;"
} > "$tmp"

diff -u "$out" "$tmp" || true
mv "$tmp" "$out"
