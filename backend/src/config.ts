import 'dotenv/config';
import { randomBytes } from 'node:crypto';
import path from 'node:path';
export interface Config {
 host:string; port:number; dbPath:string; accessSecret:string; gameHost:string; portStart:number;
 maxMatches:number; serverMode:string; serverPath:string; runtimeDir:string;
 accessSeconds:number; refreshSeconds:number; reconnectSeconds:number; startupSeconds:number;gameConfigPath:string;
}
export function config(overrides:Partial<Config> = {}):Config {
 const secret=process.env.ACCESS_SECRET || randomBytes(48).toString('hex');
 if(secret.length<32) throw new Error('ACCESS_SECRET requires at least 32 characters');
 if(!process.env.ACCESS_SECRET && process.env.NODE_ENV==='production') throw new Error('ACCESS_SECRET is required in production');
 return {host:process.env.HOST || '127.0.0.1',port:Number(process.env.PORT || 3000),
 dbPath:process.env.DATABASE_PATH || 'data/bound-shoot.sqlite',accessSecret:secret,
 gameHost:process.env.PUBLIC_GAME_HOST || '127.0.0.1',portStart:Number(process.env.GAME_PORT_START || 7777),
 maxMatches:Number(process.env.MAX_MATCHES || 2),serverMode:process.env.SERVER_MODE || 'manual',
 serverPath:process.env.GAME_SERVER_PATH || '',runtimeDir:path.resolve(process.env.RUNTIME_DIR || 'runtime'),
 accessSeconds:900,refreshSeconds:30*86400,reconnectSeconds:20,startupSeconds:90,
 gameConfigPath:path.resolve(process.env.GAME_CONFIG_PATH || '../unity-client/Assets/BoundShoot/Resources/GameConfig.json'),...overrides};
}
