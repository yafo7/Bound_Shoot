import {Store} from './database.js';
import {check,id,now} from './security.js';
import {randomInt} from 'node:crypto';
export const QUICK_ROOM_ID='bound-shoot-quick-2v2';
export class Rooms {
 constructor(readonly store:Store,readonly publish:(roomId:string,event?:string)=>void,readonly maps:string[]=['Arena_01']){}
 member(uid:string){return this.store.get('SELECT * FROM room_members WHERE user_id=?',uid);}
 view(roomId:string) {
  const room=this.store.get('SELECT id,code,host_id as hostId,size,map_id as mapId,status,match_id as matchId FROM rooms WHERE id=?',roomId);
  check(room,404,'ROOM_MISSING','房间不存在');
  room.members=this.store.all('SELECT m.user_id as userId,u.display_name as username,c.skin,m.team,m.seat,m.ready,0 as isBot FROM room_members m JOIN users u ON u.id=m.user_id JOIN user_cosmetics c ON c.user_id=m.user_id WHERE m.room_id=? ORDER BY m.team,m.seat',roomId);
  if(roomId===QUICK_ROOM_ID){
   for(const m of room.members)m.ready=1;
   for(let team=0;team<2;team++)for(let seat=0;seat<2;seat++)if(!room.members.some((m:any)=>m.team===team&&m.seat===seat)){
    const bot=this.store.get('SELECT q.user_id as userId,u.display_name as username,c.skin FROM quick_identities q JOIN users u ON u.id=q.user_id JOIN user_cosmetics c ON c.user_id=q.user_id WHERE q.is_bot=1 AND q.name=?','AI_'+(team===0?'A':'B')+(seat+1));
    room.members.push({...bot,team,seat,ready:1,isBot:1});
   }
   room.members.sort((a:any,b:any)=>a.team-b.team||a.seat-b.seat);
  }
  return room;
 }
 quickJoin(uid:string){
  let room=this.store.get('SELECT * FROM rooms WHERE id=?',QUICK_ROOM_ID);
  if(!room){
   this.store.run('INSERT INTO rooms(id,code,host_id,size,map_id) VALUES(?,?,?,?,?)',QUICK_ROOM_ID,'000001',uid,2,this.maps[0]);
   room=this.store.get('SELECT * FROM rooms WHERE id=?',QUICK_ROOM_ID);
  }
  const current=this.member(uid);
  if(current?.room_id===QUICK_ROOM_ID){this.store.run('UPDATE room_members SET last_seen=? WHERE user_id=?',now(),uid);return this.view(QUICK_ROOM_ID);}
  check(!current,409,'ALREADY_IN_ROOM','请先退出当前房间');
  check(room.status==='Waiting',409,'ROOM_BUSY','房间正在对战，请稍后进入');
  // Remove timed-out humans before deciding whether a seat is available.
  this.store.run('DELETE FROM room_members WHERE room_id=? AND last_seen<?',QUICK_ROOM_ID,now()-20);
  const members=this.store.all('SELECT team,seat FROM room_members WHERE room_id=?',QUICK_ROOM_ID);
  const free=[{team:0,seat:0},{team:1,seat:0},{team:0,seat:1},{team:1,seat:1}].find(p=>!members.some(m=>m.team===p.team&&m.seat===p.seat));
  check(free,409,'ROOM_FULL','房间已满，请稍后进入');
  this.store.run('INSERT INTO room_members(user_id,room_id,team,seat,ready,last_seen) VALUES(?,?,?,?,1,?)',uid,QUICK_ROOM_ID,free.team,free.seat,now());
  if(!this.store.get('SELECT 1 FROM room_members WHERE user_id=? AND room_id=?',room.host_id,QUICK_ROOM_ID))this.store.run('UPDATE rooms SET host_id=? WHERE id=?',uid,QUICK_ROOM_ID);
  return this.view(QUICK_ROOM_ID);
 }
 require(uid:string,roomId:string) {
  const m=this.member(uid);check(m?.room_id===roomId,403,'NOT_MEMBER','你不在此房间'); return this.view(roomId);
 }
 waiting(uid:string,roomId:string,host=false) {
  const r=this.require(uid,roomId);check(r.status==='Waiting',409,'ROOM_BUSY','房间正在启动或战斗中');
  if(host) check(r.hostId===uid,403,'HOST_ONLY','仅房主可以操作'); return r;
 }
 create(uid:string) {
  check(!this.member(uid),409,'ALREADY_IN_ROOM','请先离开当前房间');
  return this.store.transaction(()=>{
   const rid=id();let code:string;do{code=String(randomInt(100000,1000000));}while(this.store.get('SELECT id FROM rooms WHERE code=?',code));
   this.store.run('INSERT INTO rooms(id,code,host_id) VALUES(?,?,?)',rid,code,uid);
   this.store.run('INSERT INTO room_members(user_id,room_id,team,seat,last_seen) VALUES(?,?,?,?,?)',uid,rid,0,0,now());
   return this.view(rid);
  });
 }
 join(uid:string,code:string) {
  const r=this.store.get('SELECT * FROM rooms WHERE code=?',code);
  check(r,404,'ROOM_MISSING','房间号不存在');
  const current=this.member(uid);if(current?.room_id===r.id)return this.view(r.id);
  check(!current,409,'ALREADY_IN_ROOM','请先离开当前房间');
  check(r.status==='Waiting',409,'ROOM_BUSY','房间正在战斗');
  const view=this.view(r.id);
  let free:any;for(let t=0;t<2;t++)for(let s=0;s<r.size;s++)if(!free&&!view.members.some((m:any)=>m.team===t&&m.seat===s))free={t,s};
  check(free,409,'ROOM_FULL','房间已满');
  this.store.run('INSERT INTO room_members(user_id,room_id,team,seat,last_seen) VALUES(?,?,?,?,?)',uid,r.id,free.t,free.s,now());
  this.publish(r.id,'member.joined');return this.view(r.id);
 }
 leave(uid:string,roomId:string) {
  this.waiting(uid,roomId);this.remove(uid,roomId);
 }
 remove(uid:string,roomId:string) {
  this.store.transaction(()=>{
   this.store.run('DELETE FROM room_members WHERE user_id=? AND room_id=?',uid,roomId);
   const remaining=this.store.get('SELECT user_id FROM room_members WHERE room_id=? ORDER BY rowid LIMIT 1',roomId);
   if(!remaining&&roomId!==QUICK_ROOM_ID)this.store.run('DELETE FROM rooms WHERE id=?',roomId);
   else if(remaining)this.store.run('UPDATE rooms SET host_id=CASE WHEN host_id=? THEN ? ELSE host_id END WHERE id=?',uid,remaining.user_id,roomId);
  });this.publish(roomId,'member.left');
 }
 seat(uid:string,roomId:string,team:number,seat:number) {
  const r=this.waiting(uid,roomId);
  check(seat<r.size,400,'BAD_SEAT','无效座位');
  check(!r.members.some((m:any)=>!m.isBot&&m.team===team&&m.seat===seat&&m.userId!==uid),409,'SEAT_TAKEN','此位置已被占用');
  this.store.run('UPDATE room_members SET team=?,seat=?,ready=0 WHERE user_id=?',team,seat,uid);
  this.publish(roomId,'seat.changed');return this.view(roomId);
 }
 ready(uid:string,roomId:string,ready:boolean) {
  this.waiting(uid,roomId);this.store.run('UPDATE room_members SET ready=? WHERE user_id=?',ready?1:0,uid);
  this.publish(roomId,'ready.changed');return this.view(roomId);
 }
 configure(uid:string,roomId:string,size:number,mapId:string) {
  const r=this.waiting(uid,roomId,true);
  check(roomId!==QUICK_ROOM_ID||size===2,400,'FIXED_MODE','当前房间固定为2v2');
  check(this.maps.includes(mapId),400,'MAP_UNAVAILABLE','地图尚未开放');
  check(r.members.every((m:any)=>m.seat<size),409,'SEATS_OUTSIDE_MODE','请先腾出超出新模式的座位');
  this.store.transaction(()=>{
   this.store.run('UPDATE rooms SET size=?,map_id=? WHERE id=?',size,mapId,roomId);
   this.store.run('UPDATE room_members SET ready=0 WHERE room_id=?',roomId);
  });this.publish(roomId,'config.changed');return this.view(roomId);
 }
}
