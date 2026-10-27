#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "$0")/.." && pwd -P)"
cd "$project_dir/backend"
echo "Machine:"
uname -m
node --version
echo "API:"
curl --fail --silent --show-error http://127.0.0.1:3000/health
printf '\n'
echo "Configuration:"
node --input-type=module <<'NODE'
import fs from 'node:fs';
import dotenv from 'dotenv';
const c=dotenv.parse(fs.readFileSync('.env'));
const playable=Boolean(c.GAME_SERVER_PATH&&fs.existsSync(c.GAME_SERVER_PATH));
console.log(JSON.stringify({
  apiListenHost:c.HOST,
  gameHost:c.PUBLIC_GAME_HOST,
  udpPorts:[Number(c.GAME_PORT_START),Number(c.GAME_PORT_START)+Number(c.MAX_MATCHES)-1],
  contentPresent:Boolean(c.GAME_CONFIG_PATH&&fs.existsSync(c.GAME_CONFIG_PATH)),
  gameServerUploaded:playable
},null,2));
if(!playable)console.log('API/rooms can be tested. Matches require the Linux Unity server first.');
NODE
echo "Service:"
systemctl --user --no-pager status bound-shoot.service
