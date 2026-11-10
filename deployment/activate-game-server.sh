#!/usr/bin/env bash
# Run after copying the complete LinuxServer output into this project's server/.
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "$0")/.." && pwd -P)"
if [ -x "$project_dir/tools/node/bin/node" ]; then export PATH="$project_dir/tools/node/bin:$PATH"; fi
server="$project_dir/server/BoundShootServer.x86_64"
test -f "$project_dir/backend/.env" || { echo 'Install the backend first.' >&2; exit 1; }
for file in "$server" "$project_dir/server/UnityPlayer.so" "$project_dir/server/BoundShootServer_Data/boot.config" "$project_dir/content/GameConfig.json"; do
  test -f "$file" || { echo "Missing: $file. Copy the complete LinuxServer folder contents into server/." >&2; exit 1; }
done
for file in "$project_dir/backend/src/quickplay.ts" "$project_dir/backend/migrations/004_lobby_presence.sql" "$project_dir/server/release.json"; do
  test -f "$file" || { echo "Missing updated backend file: $file. Copy the complete UploadToServer contents first." >&2; exit 1; }
done
node --input-type=module - "$project_dir" <<'NODE'
import fs from 'node:fs';
import {createHash} from 'node:crypto';
const root=process.argv[2],release=JSON.parse(fs.readFileSync(root+'/server/release.json','utf8'));
const hash=createHash('sha256').update(fs.readFileSync(root+'/content/GameConfig.json')).digest('hex');
if(release.version!=='0.3.0'||release.gameConfigSha256!==hash)throw new Error('Upload the matching lobby server build and GameConfig before activation.');
NODE
if [ "$(systemctl --user show bound-shoot.service --property=WorkingDirectory --value)" != "$project_dir/backend" ]; then
  echo 'The service does not belong to this project. Nothing was changed.' >&2; exit 1
fi
chmod u+x "$server"
if command -v ldd >/dev/null; then
  dependencies="$(ldd "$server" "$project_dir/server/UnityPlayer.so" 2>&1)" || { printf '%s\n' "$dependencies" >&2; exit 1; }
  if [[ "$dependencies" == *'not found'* ]]; then
    printf '%s\n' "$dependencies" >&2
    echo 'Install the listed missing Linux libraries before activating.' >&2; exit 1
  fi
fi
cd "$project_dir/backend"
npm ci --ignore-scripts
npm run build
node --input-type=module - "$project_dir" <<'NODE'
import fs from 'node:fs';
const root=process.argv[2],file=root+'/backend/.env';
let source=fs.readFileSync(file,'utf8');
for(const [key,value] of Object.entries({SERVER_MODE:'managed',GAME_SERVER_PATH:root+'/server/BoundShootServer.x86_64',
 GAME_CONFIG_PATH:root+'/content/GameConfig.json',PUBLIC_GAME_HOST:'123.207.238.56',LOBBY_MODE:'true'})){
 const pattern=new RegExp('^'+key+'=.*$','m');
 source=pattern.test(source)?source.replace(pattern,()=>key+'='+value):source.trimEnd()+'\n'+key+'='+value+'\n';
}
fs.writeFileSync(file,source,{mode:0o600});
NODE
systemctl --user restart bound-shoot.service
curl --fail --silent --show-error --retry 10 --retry-connrefused --retry-delay 1 http://127.0.0.1:3000/health
printf '\nReady. Enter the public lobby, create/join a room, select a map and start. Empty seats use server AI.\n'
