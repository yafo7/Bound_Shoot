import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,readFileSync,rmSync} from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {WebSocket} from 'ws';
import {createService} from '../src/service.js';

test('public lobby: names → multiple rooms → host config → 4v4 AI match → results → host transfer',async()=>{
 const dir=mkdtempSync(path.join(os.tmpdir(),'bound-lobby-'));
 const s=createService({dbPath:':memory:',runtimeDir:dir,port:0,lobbyMode:true});
 await new Promise<void>(r=>s.server.listen(0,'127.0.0.1',r));
 const base='http://127.0.0.1:'+(s.server.address() as any).port;
 const call=async(method:string,url:string,body:any={},token='')=>{
  const response=await fetch(base+url,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(method==='GET'?{}:{body:JSON.stringify(body)})});
  return {status:response.status,...await response.json() as any};
 };
 let ws:WebSocket|undefined;
 try{
  assert.equal((await call('GET','/health')).data.mode,'lobby');
  assert.equal((await call('POST','/api/auth/quick',{name:'old'})).status,426);
  assert.equal((await call('GET','/api/rooms')).status,401);
  assert.equal((await call('POST','/api/auth/guest',{name:'AI_A1',skin:7})).status,400);
  const a=(await call('POST','/api/auth/guest',{name:'大厅甲',skin:7})).data;
  const b=(await call('POST','/api/auth/guest',{name:'大厅乙',skin:5})).data;
  const c=(await call('POST','/api/auth/guest',{name:'大厅丙',skin:2})).data;
  assert.equal(a.room,null);assert.equal(a.user.skin,7);
  assert.equal((await call('POST','/api/auth/guest',{name:'大厅甲'})).status,409);
  const api=(u:any,method:string,url:string,body:any={})=>call(method,url,body,u.accessToken);
  assert.deepEqual((await api(a,'GET','/api/catalog')).data.modes,[2,3,4]);
  const r=(await api(a,'POST','/api/rooms')).data,url='/api/rooms/'+r.id;
  assert.equal(r.members.length,4);assert.equal(r.members.filter((p:any)=>p.isBot).length,3);
  const r2=(await api(c,'POST','/api/rooms')).data;
  let list=(await api(b,'GET','/api/rooms')).data;
  assert.equal(list.total,2);assert.ok(list.rooms.every((x:any)=>x.humans===1));
  assert.equal((await api(b,'POST','/api/rooms/join',{code:r.code})).status,200);
  assert.equal((await api(b,'POST','/api/rooms/join',{code:r2.code})).status,409);
  assert.equal((await api(b,'PUT',url+'/config',{size:4,mapId:'Depot_01'})).status,403);
  assert.equal((await api(b,'POST',url+'/start')).status,403);
  ws=new WebSocket(base.replace('http','ws')+'/ws');const events:any[]=[];
  ws.on('message',raw=>events.push(JSON.parse(raw.toString())));
  await new Promise<void>(resolve=>ws!.once('open',()=>{ws!.send(JSON.stringify({type:'auth',accessToken:b.accessToken}));resolve();}));
  for(let i=0;i<30&&!events.length;i++)await new Promise(r=>setTimeout(r,10));
  const configured=await api(a,'PUT',url+'/config',{size:4,mapId:'Depot_01'});
  assert.equal(configured.data.members.length,8);assert.equal(configured.data.members.filter((p:any)=>p.isBot).length,6);
  for(let i=0;i<30&&!events.some(e=>e.event==='config.changed');i++)await new Promise(r=>setTimeout(r,10));
  assert.ok(events.some(e=>e.event==='config.changed'&&e.room.mapId==='Depot_01'&&e.room.size===4));
  assert.equal((await api(b,'PUT',url+'/seat',{team:1,seat:3})).status,200);
  assert.equal((await api(a,'PUT',url+'/config',{size:2,mapId:'Depot_01'})).status,409);
  assert.equal((await api(a,'POST',url+'/start')).status,409);
  assert.equal((await api(a,'PUT',url+'/ready',{ready:true})).data.status,'Waiting');
  assert.equal((await api(a,'PUT',url+'/ready',{ready:false})).data.status,'Waiting');
  assert.equal((await api(b,'PUT',url+'/ready',{ready:true})).data.status,'Waiting');
  const starting=(await api(a,'PUT',url+'/ready',{ready:true})).data;
  assert.equal(starting.status,'Starting');
  const mid=starting.matchId;
  assert.ok(mid);assert.equal((await api(a,'POST',url+'/start')).data.id,mid);
  const roster=s.matches.roster(mid);assert.equal(roster.length,8);assert.equal(new Set(roster.map(p=>p.userId)).size,8);
  assert.equal(roster.find(p=>p.userId===a.user.id).skin,7);
  await api(c,'POST','/api/rooms/'+r2.id+'/leave');
  assert.equal((await api(c,'POST','/api/rooms/join',{code:r.code})).status,409);
  const launch=JSON.parse(readFileSync(path.join(dir,mid+'.json'),'utf8'));
  const registration=await fetch(base+'/internal/matches/'+mid+'/register',{method:'POST',headers:{'x-match-secret':launch.secret}});
  assert.equal(registration.status,200);
  const {data}=await registration.json() as any;
  assert.equal(data.warmupHostId,a.user.id);
  assert.equal(data.mapId,'Depot_01');
  s.matches.ready(mid);
  const ticket=(await api(a,'POST','/api/matches/'+mid+'/ticket')).data.ticket;
  assert.equal(s.matches.consume(mid,ticket).userId,a.user.id);
  assert.throws(()=>s.matches.consume(mid,ticket));
  const submitted=await fetch(base+'/internal/matches/'+mid+'/result',{
   method:'POST',headers:{'x-match-secret':launch.secret,'content-type':'application/json'},
   body:JSON.stringify({winner:1,duration:80,players:roster.map(p=>({userId:p.userId,kills:0,deaths:1,assists:2,damage:10}))})
  });
  assert.equal(submitted.status,200);
  const result=(await api(b,'GET','/api/matches/'+mid+'/result')).data;
  assert.equal(result.status,'Finished');
  assert.equal(result.result.players.find((p:any)=>p.userId===b.user.id).assists,2);
  assert.equal((await api(a,'GET',url)).data.status,'Waiting');
  await api(a,'POST',url+'/leave');assert.equal((await api(b,'GET',url)).data.hostId,b.user.id);
  await api(b,'POST','/api/auth/logout');assert.equal(s.store.get('SELECT id FROM rooms WHERE id=?',r.id),undefined);
  assert.equal((await api(c,'GET','/api/rooms')).data.total,0);
  assert.equal((await api(b,'GET','/api/rooms')).status,401);
 }finally{ws?.terminate();await s.close();rmSync(dir,{recursive:true,force:true});}
});

test('lobby pagination, capacity, reconnect presence and startup recovery',async()=>{
 const dir=mkdtempSync(path.join(os.tmpdir(),'bound-lobby-recovery-')),db=path.join(dir,'state.sqlite');
 let s=createService({dbPath:db,runtimeDir:dir,lobbyMode:true});
 try{
  const {QuickPlay}=await import('../src/quickplay.js');let quick=new QuickPlay(s.store,s.rooms,s.auth);
  const users=Array.from({length:8},(_,i)=>quick.loginLobby('guest'+i,i));
  const first=s.rooms.create(users[0].user.id);
  for(let i=1;i<4;i++)s.rooms.join(users[i].user.id,first.code);
  assert.throws(()=>s.rooms.join(users[4].user.id,first.code),/已满/);
  assert.equal(s.rooms.view(first.id).members.filter((p:any)=>p.isBot).length,0);
  for(let i=1;i<4;i++)s.rooms.leave(users[i].user.id,first.id);
  for(let i=1;i<8;i++)s.rooms.create(users[i].user.id);
  assert.equal(s.rooms.list(0).rooms.length,6);assert.equal(s.rooms.list(1).rooms.length,2);
  assert.equal(s.rooms.list(1).total,8);
  s.store.run('UPDATE auth_sessions SET last_seen=0');s.store.run('UPDATE room_members SET last_seen=0');
  const resumed=quick.loginLobby('GUEST0',6);assert.equal(resumed.user.id,users[0].user.id);assert.equal(resumed.room.id,first.id);
  assert.throws(()=>s.auth.verify(users[0].accessToken));
  s.rooms.ready(resumed.user.id,first.id,true);
  const mid=s.matches.start(resumed.user.id,first.id).id;
  await s.close();s=createService({dbPath:db,runtimeDir:dir,lobbyMode:true});
  assert.equal(s.rooms.view(first.id).status,'Waiting');assert.equal(s.matches.view(mid,resumed.user.id).status,'Aborted');
  assert.equal(s.store.get('SELECT count(*) n FROM schema_migrations').n,4);
 }finally{await s.close();rmSync(dir,{recursive:true,force:true});}
});
