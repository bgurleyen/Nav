#!/bin/bash
# Unity Build Automation post-build: upload IPA to App Store Connect (TestFlight).
# Dashboard → iOS target → Advanced → Post-build script: Tools/upload-ios-testflight.sh
#
# Required environment variables (Build Automation → Configuration → Environment variables, all Secret):
#   APPLE_API_KEY_ID          e.g. V957JKYQ32
#   APPLE_API_ISSUER_ID       from App Store Connect → Integrations → App Store Connect API
#   APPLE_API_KEY_CONTENT_BASE64  base64 of AuthKey_<KEY_ID>.p8 (single line, no spaces)

set -euo pipefail

echo "=== TestFlight upload (App Store Connect API) ==="

if [ -z "${APPLE_API_KEY_ID:-}" ] || [ -z "${APPLE_API_ISSUER_ID:-}" ] || [ -z "${APPLE_API_KEY_CONTENT_BASE64:-}" ]; then
  echo "Missing APPLE_API_KEY_ID, APPLE_API_ISSUER_ID, or APPLE_API_KEY_CONTENT_BASE64"
  exit 1
fi

PATH_TO_IPA="${UNITY_PLAYER_PATH:-}"
if [ -z "$PATH_TO_IPA" ] || [ ! -f "$PATH_TO_IPA" ]; then
  PATH_TO_IPA=$(find "${WORKSPACE:-.}" -name "*.ipa" 2>/dev/null | head -n 1 || true)
fi

if [ -z "$PATH_TO_IPA" ] || [ ! -f "$PATH_TO_IPA" ]; then
  echo "IPA not found (UNITY_PLAYER_PATH unset and search failed)"
  exit 1
fi

echo "IPA: $PATH_TO_IPA"

PRIVATE_KEYS_DIR="${HOME}/.private_keys"
mkdir -p "$PRIVATE_KEYS_DIR"
PRIVATE_KEY_FILE="${PRIVATE_KEYS_DIR}/AuthKey_${APPLE_API_KEY_ID}.p8"

echo "$APPLE_API_KEY_CONTENT_BASE64" | base64 -D > "$PRIVATE_KEY_FILE"
if [ ! -s "$PRIVATE_KEY_FILE" ]; then
  echo "Failed to decode APPLE_API_KEY_CONTENT_BASE64"
  exit 1
fi

chmod 600 "$PRIVATE_KEY_FILE"

if xcrun altool --upload-app -t ios -f "$PATH_TO_IPA" \
  --apiKey "$APPLE_API_KEY_ID" \
  --apiIssuer "$APPLE_API_ISSUER_ID"; then
  echo "Upload to App Store Connect succeeded."
  rm -f "$PRIVATE_KEY_FILE"
  exit 0
fi

echo "Upload failed."
rm -f "$PRIVATE_KEY_FILE"
exit 1
