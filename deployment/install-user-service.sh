#!/usr/bin/env bash
set -euo pipefail
umask 077
project_dir="$(cd -- "$(dirname -- "$0")/.." && pwd -P)"
if [ "$(id -u)" -eq 0 ]; then
  echo "Run this as ubuntu, without sudo." >&2
  exit 1
fi
command -v node >/dev/null || { echo "Install Node.js 24 or newer first." >&2; exit 1; }
command -v npm >/dev/null || { echo "npm is required." >&2; exit 1; }
node -e 'if(Number(process.versions.node.split(".")[0])<24)process.exit(1)' || { echo "Node.js 24 or newer is required." >&2; exit 1; }
test -f "$project_dir/backend/package-lock.json" || { echo "Upload backend/ first." >&2; exit 1; }
test -f "$project_dir/content/GameConfig.json" || { echo "Upload GameConfig.json into content/ first." >&2; exit 1; }
if command -v ss >/dev/null && [ -n "$(ss -ltnH 'sport = :3000')" ] && ! systemctl --user is-active --quiet bound-shoot.service; then
  echo "TCP 3000 is occupied by another service. No existing service was stopped." >&2
  exit 1
fi
mkdir -p "$project_dir/state" "$project_dir/runtime" "$project_dir/server" "$HOME/.config/systemd/user"
if [ ! -f "$project_dir/backend/.env" ]; then
  secret="$(node -e 'process.stdout.write(require("node:crypto").randomBytes(48).toString("hex"))')"
  cat > "$project_dir/backend/.env" <<EOF
HOST=0.0.0.0
PORT=3000
NODE_ENV=production
ACCESS_SECRET=$secret
DATABASE_PATH=$project_dir/state/game.sqlite
RUNTIME_DIR=$project_dir/runtime
SERVER_MODE=managed
GAME_SERVER_PATH=$project_dir/server/BoundShootServer.x86_64
GAME_CONFIG_PATH=$project_dir/content/GameConfig.json
PUBLIC_GAME_HOST=134.175.153.218
GAME_PORT_START=7777
MAX_MATCHES=2
EOF
fi
chmod 600 "$project_dir/backend/.env"
if [ -f "$project_dir/server/BoundShootServer.x86_64" ]; then
  chmod u+x "$project_dir/server/BoundShootServer.x86_64"
fi
cd "$project_dir/backend"
npm ci
npm run build
node_path="$(command -v node)"
unit="$HOME/.config/systemd/user/bound-shoot.service"
if [ -f "$unit" ] && ! grep -Fq "$project_dir/backend" "$unit"; then
  echo "A different bound-shoot.service already exists. No service was replaced." >&2
  exit 1
fi
node --input-type=module - "$project_dir" "$node_path" "$unit" <<'NODE'
import fs from 'node:fs';
const [root,node,target]=process.argv.slice(2);
if(/[\s%"\\]/u.test(root)||/[\s%"\\]/u.test(node))throw new Error('Use a project/node path without spaces, percent signs, quotes or backslashes for this systemd template.');
const source=fs.readFileSync(root+'/deployment/bound-shoot.service','utf8');
fs.writeFileSync(target,source.replaceAll('@PROJECT_ROOT@',root).replaceAll('@NODE@',node));
NODE
if command -v systemd-analyze >/dev/null; then
  systemd-analyze --user verify "$unit"
fi
systemctl --user daemon-reload
systemctl --user enable bound-shoot.service
systemctl --user restart bound-shoot.service
echo "API installed. Check: bash $project_dir/deployment/check.sh"
echo "For persistence after logout, run once: sudo loginctl enable-linger $(id -un)"
