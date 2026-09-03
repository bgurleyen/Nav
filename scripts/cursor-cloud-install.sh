#!/usr/bin/env bash
# Cloud Agent install for the NavigationShare (Nav) Unity project.
#
# This is the canonical, reviewable copy of the Cloud Agent development
# environment bootstrap. The dashboard-managed environment runs a functionally
# identical inline `install` command; keep the two in sync when either changes.
#
# The script is idempotent: it detects already-installed toolchains and skips
# them, so it is safe to re-run on every boot / dependency refresh.
#
# What it provides:
#   - .NET SDK 8  : primary toolchain for the Unity C# scripts (analysis,
#                   formatting, scratch compilation).
#   - Firebase CLI: works with the committed Firestore backend config under
#                   ./firebase (rules, indexes) and the Firestore emulator.
#
# Unity itself is intentionally not installed: the editor is license-gated and
# too large for a Cloud Agent, so the environment targets code + backend config
# work rather than full player builds.
set -euo pipefail

log() { printf '\n=== %s ===\n' "$*"; }

# --- .NET SDK 8 (primary toolchain for the Unity C# codebase) ---
if command -v dotnet >/dev/null 2>&1; then
  log ".NET SDK already present: $(dotnet --version)"
else
  log "Installing .NET SDK 8"
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends dotnet-sdk-8.0
fi

# --- Firebase CLI (for the Firestore config under ./firebase) ---
# Use a user-writable npm global prefix so installs never try to write to "/".
NPM_GLOBAL="$HOME/.npm-global"
mkdir -p "$NPM_GLOBAL"
npm config set prefix "$NPM_GLOBAL"
export PATH="$NPM_GLOBAL/bin:$PATH"

# Persist the npm-global bin on PATH for interactive agent shells.
BASHRC="$HOME/.bashrc"
if ! grep -q 'NAV_CLOUD_NPM_GLOBAL' "$BASHRC" 2>/dev/null; then
  {
    echo ''
    echo '# NAV_CLOUD_NPM_GLOBAL: user npm global bin on PATH (Cloud Agent)'
    echo 'export PATH="$HOME/.npm-global/bin:$PATH"'
  } >> "$BASHRC"
fi

if command -v firebase >/dev/null 2>&1; then
  log "Firebase CLI already present: $(firebase --version)"
else
  log "Installing firebase-tools"
  npm install -g firebase-tools@13
fi

# Pre-cache the Firestore emulator so it is available without a runtime download.
log "Caching Firestore emulator"
firebase setup:emulators:firestore

log "Toolchain versions"
dotnet --version
firebase --version
