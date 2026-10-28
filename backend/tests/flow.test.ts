import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,readFileSync,rmSync} from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {WebSocket} from 'ws';
import {createService} from '../src/service.js';

test('real HTTP account → room → single-use tickets → authoritative result → rematch',async()=>{
 const runtime=mkdtempSync(path.join(os.tmpdir(),'bound-shoot-'));
 const s=createService({dbPath:':memory:',port:0,runtimeDir:runtime,maxMatches:1,quickPlay:false});
 await new Promise<void>(r=>s.server.listen(0,'127.0.0.1',r));
 const port=(s.server.address() as any).port,base='http://127.0.0.1:'+port;
 const call=async(method:string,url:string,body:any={},token='',secret='')=>{
  const response=await fetch(base+url,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{}),...(secret?{'X-Match-Secret':secret}:{})},...(method==='GET'?{}:{body:JSON.stringify(body)})});
  return {status:response.status,...await response.json() as any};
 };
 try{
  const users:any[]=[];
  for(let i=0;i<4;i++){
   const r=await call('POST','/api/auth/register',{username:'player_'+i,password:'valid-password'});
   assert.equal(r.ok,true);users.push(r.data);
  }
  assert.equal((await call('POST','/api/auth/register',{username:'PLAYER_0',password:'valid-password'})).status,409);
  assert.equal((await call('POST','/api/auth/login',{username:'player_0',password:'wrong-password'})).status,401);
  assert.equal((await call('GET','/api/users/me',{},'forged')).status,401);
  const login=await call('POST','/api/auth/login',{username:'player_0',password:'valid-password'});
  assert.equal(login.data.user.id,users[0].user.id);
  const refresh=await call('POST','/api/auth/refresh',{refreshToken:login.data.refreshToken});
  assert.equal(refresh.ok,true);
  assert.equal((await call('GET','/api/users/me',{},login.data.accessToken)).status,401);
  assert.equal((await call('POST','/api/auth/refresh',{refreshToken:login.data.refreshToken})).status,401);
  await call('POST','/api/auth/logout',{},refresh.data.accessToken);
  assert.equal((await call('GET','/api/users/me',{},refresh.data.accessToken)).status,401);
  const api=(i:number,method:string,url:string,body:any={})=>call(method,url,body,users[i].accessToken);
  await api(0,'PATCH','/api/users/me/cosmetic',{skin:3});
  assert.equal((await api(0,'GET','/api/users/me')).data.skin,3);
  let room=(await api(0,'POST','/api/rooms')).data;
  const roomUrl='/api/rooms/'+room.id;
  for(let i=1;i<4;i++)await api(i,'POST','/api/rooms/join',{code:room.code});
  assert.equal((await api(0,'GET',roomUrl)).data.members.length,4);
  assert.equal((await api(0,'PUT',roomUrl+'/seat',{team:1,seat:0})).status,409);
  assert.equal((await api(1,'PUT',roomUrl+'/config',{size:2,mapId:'Arena_01'})).status,403);
  assert.equal((await api(0,'PUT',roomUrl+'/config',{size:2,mapId:'Arena_02'})).status,400);
  await api(0,'PUT',roomUrl+'/config',{size:3,mapId:'Arena_01'});
  const race=await Promise.all([api(0,'PUT',roomUrl+'/seat',{team:0,seat:2}),api(2,'PUT',roomUrl+'/seat',{team:0,seat:2})]);
  assert.deepEqual(race.map(r=>r.status).sort(),[200,409]);
  if(race[0].status===200)await api(0,'PUT',roomUrl+'/seat',{team:0,seat:0});
  else await api(2,'PUT',roomUrl+'/seat',{team:1,seat:0});
  await api(0,'PUT',roomUrl+'/config',{size:2,mapId:'Arena_01'});
  assert.equal((await api(0,'POST',roomUrl+'/start')).status,409);
  for(let i=0;i<4;i++)assert.equal((await api(i,'PUT',roomUrl+'/ready',{ready:true})).ok,true);
  const starts=await Promise.all([api(0,'POST',roomUrl+'/start'),api(0,'POST',roomUrl+'/start')]);
  const mid=starts[0].data.id;assert.equal(starts[1].data.id,mid);
  assert.equal(s.store.get('SELECT count(*) n FROM matches').n,1);
  const launch=JSON.parse(readFileSync(path.join(runtime,mid+'.json'),'utf8'));
  const internal=(suffix:string,body:any={})=>call('POST','/internal/matches/'+mid+'/'+suffix,body,'',launch.secret);
  assert.equal((await call('POST','/internal/matches/'+mid+'/ready',{},users[0].accessToken)).status,403);
  assert.equal((await internal('register')).data.players.length,4);
  assert.equal((await api(0,'POST','/api/matches/'+mid+'/ticket')).status,409);
  assert.equal((await internal('ready')).ok,true);
  const expired=(await api(0,'POST','/api/matches/'+mid+'/ticket')).data.ticket;
  s.store.run('UPDATE match_tickets SET expires_at=0 WHERE match_id=?',mid);
  assert.equal((await internal('consume-ticket',{ticket:expired})).status,403);
  for(let i=0;i<4;i++){
   const ticket=(await api(i,'POST','/api/matches/'+mid+'/ticket')).data.ticket;
   const verified=await internal('consume-ticket',{ticket});assert.equal(verified.data.userId,users[i].user.id);
   assert.equal((await internal('consume-ticket',{ticket})).status,403);
  }
  assert.equal((await internal('heartbeat',{playing:true})).ok,true);
  const roster=s.matches.roster(mid);
  const result={winner:0,duration:35,players:roster.map((p:any)=>({userId:p.userId,kills:1,deaths:1,damage:3}))};
  assert.equal((await call('POST','/internal/matches/'+mid+'/result',result,users[0].accessToken)).status,403);
  assert.equal((await internal('result',{...result,players:[result.players[0],result.players[0],result.players[2],result.players[3]]})).status,400);
  assert.equal((await internal('result',result)).data.duplicate,false);
  assert.equal((await internal('result',result)).data.duplicate,true);
  const profile=(await api(0,'GET','/api/users/me')).data;
  assert.equal(profile.games,1);assert.equal(profile.wins,1);assert.equal(profile.kills,1);
  room=(await api(0,'GET',roomUrl)).data;assert.equal(room.status,'Waiting');assert.ok(room.members.every((m:any)=>!m.ready));
  assert.equal((await api(0,'GET','/api/users/me/matches')).data.matches.length,1);
  const ws=new WebSocket('ws://127.0.0.1:'+port+'/ws');
  await new Promise<void>(r=>ws.on('open',()=>r()));
  const event=new Promise<any>(r=>ws.once('message',raw=>r(JSON.parse(raw.toString()))));
  ws.send(JSON.stringify({type:'auth',accessToken:users[0].accessToken}));
  assert.equal((await event).event,'authenticated');ws.close();
  for(let i=0;i<4;i++)await api(i,'PUT',roomUrl+'/ready',{ready:true});
  s.cfg.serverMode='managed';s.cfg.serverPath=path.join(runtime,'missing-game-server');
  const unavailable=await api(0,'POST',roomUrl+'/start');
  assert.equal(unavailable.status,503);assert.equal(unavailable.error.code,'SERVER_UNCONFIGURED');
  assert.equal(s.store.get('SELECT count(*) n FROM matches').n,1);
  s.cfg.serverMode='manual';
  assert.equal((await api(0,'POST',roomUrl+'/start')).status,503); // manual process release cooldown
  s.store.run('UPDATE matches SET ended_at=0 WHERE id=?',mid);
  const next=(await api(0,'POST',roomUrl+'/start')).data;assert.notEqual(next.id,mid);
  s.matches.abort(next.id,'test interruption');
  assert.equal((await api(0,'GET','/api/users/me')).data.games,1);
  await api(0,'POST',roomUrl+'/leave');
  assert.equal((await api(1,'GET',roomUrl)).data.hostId,users[1].user.id);
 }finally{await s.close();rmSync(runtime,{recursive:true,force:true});}
});

test('restart safely aborts matches without crediting scores; port exhaustion is explicit',async()=>{
 const temp=mkdtempSync(path.join(os.tmpdir(),'bound-shoot-restart-'));
 const db=path.join(temp,'test.sqlite');let s=createService({dbPath:db,runtimeDir:temp,maxMatches:1,quickPlay:false});
 try{
  const users=[];for(let i=0;i<8;i++)users.push((await s.auth.register('user'+i,'valid-password')).user);
  for(let g=0;g<2;g++){
   const r=s.rooms.create(users[g*4].id);
   for(let i=1;i<4;i++)s.rooms.join(users[g*4+i].id,r.code);
   for(let i=0;i<4;i++)s.rooms.ready(users[g*4+i].id,r.id,true);
   if(g===0)s.matches.start(users[0].id,r.id);
   else assert.throws(()=>s.matches.start(users[4].id,r.id),/服务器繁忙/);
  }
  await s.close();s=createService({dbPath:db,runtimeDir:temp});
  assert.equal(s.store.get("SELECT count(*) n FROM matches WHERE status='Aborted'").n,1);
  assert.equal(s.store.get('SELECT sum(games) n FROM users').n,0);
  assert.ok(s.store.all('SELECT status FROM rooms').every(r=>r.status==='Waiting'));
 }finally{await s.close();rmSync(temp,{recursive:true,force:true});}
});
