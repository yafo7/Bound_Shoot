import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,readFileSync,rmSync} from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createService} from '../src/service.js';
import {QUICK_ROOM_ID} from '../src/rooms.js';
import {DatabaseSync} from 'node:sqlite';
import {randomUUID} from 'node:crypto';
import bcrypt from 'bcryptjs';

test('name only → one 2v2 room → AI fill → authoritative result → reenter same ID',async()=>{
 const dir=mkdtempSync(path.join(os.tmpdir(),'bound-quick-'));
 const service=createService({dbPath:path.join(dir,'test.sqlite'),port:0,runtimeDir:dir});
 await new Promise<void>(r=>service.server.listen(0,'127.0.0.1',r));
 const base='http://127.0.0.1:'+(service.server.address() as any).port;
 const call=async(method:string,url:string,body:any={},token='')=>{
  const response=await fetch(base+url,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(method==='GET'?{}:{body:JSON.stringify(body)})});
  return {status:response.status,...await response.json() as any};
 };
 try{
  assert.equal((await call('POST','/api/auth/quick',{name:''})).status,400);
  assert.equal((await call('POST','/api/auth/quick',{name:'AI_A1'})).status,400);
  assert.equal((await call('POST','/api/auth/register',{username:'old',password:'valid-password'})).status,410);
  const login=await call('POST','/api/auth/quick',{name:'yafo777'});
  assert.equal(login.ok,true);const user=login.data,room=user.room,token=user.accessToken;
  assert.equal(user.user.username,'yafo777');assert.equal(room.id,QUICK_ROOM_ID);assert.equal(room.size,2);
  assert.equal(room.members.length,4);assert.equal(room.members.filter((m:any)=>m.isBot===1).length,3);
  assert.equal((await call('POST','/api/auth/quick',{name:'YAFO777'})).status,409);
  assert.equal((await call('POST','/api/rooms',{},token)).status,410);
  for(const size of [3,4,2]){
   const changed=await call('PUT','/api/rooms/'+room.id+'/config',{size,mapId:'Forest_01'},token);
   assert.equal(changed.status,200);assert.equal(changed.data.members.length,size*2);
   assert.equal(new Set(changed.data.members.map((m:any)=>m.team+':'+m.seat)).size,size*2);
  }
  assert.equal((await call('PATCH','/api/users/me/cosmetic',{skin:7},token)).status,200);
  assert.equal((await call('PATCH','/api/users/me/cosmetic',{skin:8},token)).status,400);
  // A human can select a bot's seat; the old seat becomes a bot.
  const seated=await call('PUT','/api/rooms/'+room.id+'/seat',{team:1,seat:1},token);
  assert.equal(seated.data.members.filter((m:any)=>m.isBot===1).length,3);
  const mid=(await call('POST','/api/rooms/'+room.id+'/start',{},token)).data.id;
  assert.ok(mid);assert.equal((await call('POST','/api/rooms/'+room.id+'/start',{},token)).data.id,mid);
  assert.equal((await call('POST','/api/auth/quick',{name:'late'})).status,409);
  assert.equal(service.store.get("SELECT count(*) n FROM quick_identities WHERE name='late'").n,0);
  const manifest=JSON.parse(readFileSync(path.join(dir,mid+'.json'),'utf8'));
  service.matches.internal(mid,manifest.secret);service.matches.ready(mid);
  const roster=service.matches.roster(mid);assert.equal(roster.filter(p=>p.isBot).length,3);
  assert.throws(()=>service.matches.ticket(mid,roster.find(p=>p.isBot)!.userId),/AI/);
  const ticket=(await call('POST','/api/matches/'+mid+'/ticket',{},token)).data.ticket;
  assert.equal(service.matches.consume(mid,ticket).isBot,0);
  const result={winner:0,duration:12,players:roster.map(p=>({userId:p.userId,kills:1,deaths:1,damage:2}))};
  service.matches.finish(mid,result);assert.equal(service.matches.finish(mid,result).duplicate,true);
  assert.equal((await call('GET','/api/users/me',{},token)).data.games,1);
  await call('POST','/api/rooms/'+room.id+'/leave',{},token);
  const again=(await call('POST','/api/auth/quick',{name:'yafo777'})).data;
  assert.equal(again.user.id,user.user.id);assert.equal(again.user.games,1);
  assert.equal((await call('GET','/api/users/me',{},token)).status,401);
  // Additional people all enter the same room and replace AI, never create a new room.
  const other=await Promise.all(['小王','小李','小张'].map(name=>call('POST','/api/auth/quick',{name})));
  assert.ok(other.every(r=>r.ok&&r.data.room.id===room.id));
  const full=service.rooms.view(room.id);assert.equal(full.members.filter((m:any)=>m.isBot).length,0);
  assert.equal(new Set(full.members.map((m:any)=>m.team+':'+m.seat)).size,4);
  assert.equal((await call('POST','/api/auth/quick',{name:'fifth'})).status,409);
  assert.equal(service.store.get('SELECT count(*) n FROM rooms').n,1);
  // A non-host can start without a separate ready step.
  service.store.run('UPDATE matches SET ended_at=0 WHERE id=?',mid);
  const next=(await call('POST','/api/rooms/'+room.id+'/start',{},other[0].data.accessToken)).data;
  assert.notEqual(next.id,mid);assert.equal(next.players.length,4);assert.equal(next.players.filter((m:any)=>m.isBot).length,0);
 }finally{await service.close();rmSync(dir,{recursive:true,force:true});}
});

test('schema upgrade keeps previous password accounts separate from name-only IDs',async()=>{
 const dir=mkdtempSync(path.join(os.tmpdir(),'bound-quick-migrate-')),db=path.join(dir,'state.sqlite');
 const old=new DatabaseSync(db),legacyId=randomUUID();
 old.exec(readFileSync(new URL('../migrations/001_initial.sql',import.meta.url),'utf8'));
 old.prepare('INSERT INTO users(id,username,display_name,password_hash,created_at) VALUES(?,?,?,?,?)').run(legacyId,'yafo777','yafo777',await bcrypt.hash('legacy-password',12),0);
 old.prepare('INSERT INTO user_cosmetics(user_id) VALUES(?)').run(legacyId);old.close();
 const service=createService({dbPath:db,runtimeDir:dir});
 try{
  assert.equal(service.auth.profile(legacyId).username,'yafo777');
  assert.equal(service.store.get('SELECT count(*) n FROM schema_migrations').n,3);
  const {QuickPlay}=await import('../src/quickplay.js');const quick=new QuickPlay(service.store,service.rooms,service.auth);
  const fresh=quick.login('yafo777');assert.notEqual(fresh.user.id,legacyId);
  assert.equal(fresh.user.username,'yafo777');assert.equal((await service.auth.login('yafo777','legacy-password')).user.id,legacyId);
  service.store.run('UPDATE room_members SET last_seen=0 WHERE user_id=?',fresh.user.id);
  const resumed=quick.login('YAFO777');assert.equal(resumed.user.id,fresh.user.id);
 }finally{await service.close();rmSync(dir,{recursive:true,force:true});}
});
