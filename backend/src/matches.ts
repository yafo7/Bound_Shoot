import {mkdirSync,writeFileSync,unlinkSync,accessSync,constants} from 'node:fs';
import {spawn,ChildProcess} from 'node:child_process';
import path from 'node:path';
import type {Config} from './config.js';
import {Store} from './database.js';
import {Rooms} from './rooms.js';
import {check,digest,id,now,opaque} from './security.js';
export class Matches {
 processes=new Map<string,ChildProcess>();
 stopping=false;
 constructor(readonly store:Store,readonly cfg:Config,readonly rooms:Rooms,readonly publish:(room:string,event?:string)=>void) {
  // No orphaned game may accept credentials after a backend restart.
  this.store.transaction(()=>{
   this.store.run("UPDATE matches SET status='Aborted',ended_at=? WHERE status IN ('Starting','Ready','Playing')",now());
   this.store.run("UPDATE rooms SET status='Waiting',match_id=NULL");
   this.store.run('UPDATE room_members SET ready=0,last_seen=?',now());
   this.store.run('DELETE FROM match_tickets');
  });
 }
 start(uid:string,rid:string) {
  const room=this.rooms.require(uid,rid);
  check(room.hostId===uid,403,'HOST_ONLY','仅房主可以开始');
  if(room.status!=='Waiting')return this.view(room.matchId,uid);
  check(room.members.length===room.size*2&&room.members.every((m:any)=>m.ready),409,'NOT_READY','需要两队满员且全员准备');
  const count=this.store.get("SELECT count(*) n FROM matches WHERE status IN ('Starting','Ready','Playing')").n;
  check(count<this.cfg.maxMatches,503,'SERVER_BUSY','服务器繁忙');
  if(this.cfg.serverMode==='managed'){
   let executable=false;try{accessSync(this.cfg.serverPath,constants.X_OK);executable=true;}catch{}
   check(executable,503,'SERVER_UNCONFIGURED','对战服务尚未就绪');
  }
  let port=-1;
  for(let candidate=this.cfg.portStart;candidate<this.cfg.portStart+this.cfg.maxMatches;candidate++){
   if(this.store.get("SELECT id FROM matches WHERE port=? AND status IN ('Starting','Ready','Playing')",candidate))continue;
   // A finished match may still be flushing its result before its process releases UDP.
   const running=[...this.processes.entries()].some(([mid,child])=>child.exitCode===null&&this.store.get('SELECT port FROM matches WHERE id=?',mid)?.port===candidate);
   const manualCooldown=this.cfg.serverMode==='manual'&&this.store.get("SELECT id FROM matches WHERE port=? AND ended_at>?",candidate,now()-10);
   if(!running&&!manualCooldown){port=candidate;break;}
  }
  check(port>=0,503,'SERVER_BUSY','服务器繁忙，请稍后重试');
  const mid=id(),secret=opaque();
  this.store.transaction(()=>{
   this.store.run("INSERT INTO matches(id,room_id,map_id,port,secret_hash,status,created_at,heartbeat) VALUES(?,?,?,?,?,'Starting',?,?)",mid,rid,room.mapId,port,digest(secret),now(),now());
   for(const m of room.members)this.store.run('INSERT INTO match_players(match_id,user_id,username,skin,team,seat) VALUES(?,?,?,?,?,?)',mid,m.userId,m.username,m.skin,m.team,m.seat);
   this.store.run("UPDATE rooms SET status='Starting',match_id=? WHERE id=?",mid,rid);
  });
  try {
   mkdirSync(this.cfg.runtimeDir,{recursive:true});
   const launchFile=path.join(this.cfg.runtimeDir,mid+'.json');
   writeFileSync(launchFile,JSON.stringify({matchId:mid,secret,backendUrl:'http://127.0.0.1:'+this.cfg.port,port}),{mode:0o600});
   if(this.cfg.serverMode==='managed'){
    const child=spawn(this.cfg.serverPath,['-batchmode','-nographics','-matchFile',launchFile,'-logFile',path.join(this.cfg.runtimeDir,mid+'.log')],{stdio:'ignore',windowsHide:true});
    this.processes.set(mid,child);
    child.once('error',()=>this.abort(mid,'游戏服务器启动失败'));
    child.once('exit',()=>{this.processes.delete(mid);this.abort(mid,'游戏服务器意外退出');});
   }
  }catch{this.abort(mid,'游戏服务器启动失败');check(false,503,'SERVER_START_FAILED','游戏服务器启动失败');}
  this.publish(rid,'match.starting');return this.view(mid,uid);
 }
 internal(mid:string,secret:string) {
  const m=this.store.get('SELECT * FROM matches WHERE id=?',mid);
  check(m&&secret&&digest(secret)===m.secret_hash,403,'SERVER_AUTH_FAILED','服务间认证失败');
  return m;
 }
 roster(mid:string) {
  return this.store.all('SELECT user_id as userId,username,skin,team,seat FROM match_players WHERE match_id=? ORDER BY team,seat',mid);
 }
 view(mid:string,uid:string) {
  const m=this.store.get('SELECT * FROM matches WHERE id=?',mid);
  check(m&&this.store.get('SELECT 1 FROM match_players WHERE match_id=? AND user_id=?',mid,uid),404,'MATCH_MISSING','对战不存在');
  return {id:m.id,roomId:m.room_id,mapId:m.map_id,status:m.status,host:this.cfg.gameHost,port:m.port,players:this.roster(mid)};
 }
 ready(mid:string) {
  const m=this.store.get('SELECT * FROM matches WHERE id=?',mid);
  if(m.status==='Ready'||m.status==='Playing')return {accepted:true};
  check(m.status==='Starting',409,'MATCH_INACTIVE','对战已结束或中止');
  this.store.transaction(()=>{
   this.store.run("UPDATE matches SET status='Ready',heartbeat=? WHERE id=?",now(),mid);
   this.store.run("UPDATE rooms SET status='InGame' WHERE id=?",m.room_id);
  });this.publish(m.room_id,'match.ready');return {accepted:true};
 }
 ticket(mid:string,uid:string) {
  const m=this.view(mid,uid);check(['Ready','Playing'].includes(m.status),409,'SERVER_NOT_READY','服务器尚未就绪');
  const token=opaque();
  this.store.transaction(()=>{
   this.store.run('DELETE FROM match_tickets WHERE match_id=? AND user_id=? AND used=0',mid,uid);
   this.store.run('INSERT INTO match_tickets(hash,match_id,user_id,expires_at) VALUES(?,?,?,?)',digest(token),mid,uid,now()+30);
  });return {matchId:mid,ticket:token,host:m.host,port:m.port};
 }
 consume(mid:string,token:string) {
  return this.store.transaction(()=>{
   const m=this.store.get('SELECT status FROM matches WHERE id=?',mid);
   check(m&&['Ready','Playing'].includes(m.status),409,'MATCH_ENDED','比赛已结束');
   const t=this.store.get('SELECT * FROM match_tickets WHERE hash=? AND match_id=? AND used=0 AND expires_at>?',digest(token),mid,now());
   check(t,403,'TICKET_INVALID','连接票据无效或已使用');
   this.store.run('UPDATE match_tickets SET used=1 WHERE hash=?',digest(token));
   return this.store.get('SELECT user_id as userId,username,skin,team,seat FROM match_players WHERE match_id=? AND user_id=?',mid,t.user_id);
  });
 }
 finish(mid:string,result:any) {
  const m=this.store.get('SELECT * FROM matches WHERE id=?',mid);
  if(m.status==='Finished')return {accepted:true,duplicate:true};
  check(['Ready','Playing'].includes(m.status),409,'MATCH_INACTIVE','对战已终止');
  const roster=this.roster(mid), ids=new Set(result.players.map((p:any)=>p.userId));
  check(result.players.length===roster.length&&ids.size===roster.length&&roster.every(p=>ids.has(p.userId)),400,'BAD_RESULT','结算必须包含完整唯一玩家名单');
  this.store.transaction(()=>{
   this.store.run("UPDATE matches SET status='Finished',ended_at=?,winner=?,duration=?,result=? WHERE id=?",now(),result.winner,result.duration,JSON.stringify(result),mid);
   for(const p of result.players){
    const r=roster.find(r=>r.userId===p.userId)!;
    this.store.run('UPDATE match_players SET kills=?,deaths=?,damage=? WHERE match_id=? AND user_id=?',p.kills,p.deaths,p.damage,mid,p.userId);
    this.store.run('UPDATE users SET games=games+1,wins=wins+?,kills=kills+?,deaths=deaths+? WHERE id=?',result.winner===r.team?1:0,p.kills,p.deaths,p.userId);
   }
   this.resetRoom(m.room_id);
  });this.publish(m.room_id,'match.finished');this.cleanup(mid);return {accepted:true,duplicate:false};
 }
 resetRoom(rid:string) {
  this.store.run("UPDATE rooms SET status='Waiting' WHERE id=?",rid);
  this.store.run('UPDATE room_members SET ready=0,last_seen=? WHERE room_id=?',now(),rid);
 }
 abort(mid:string,message:string) {
  if(this.stopping)return;
  const m=this.store.get('SELECT * FROM matches WHERE id=?',mid);
  if(!m||!['Starting','Ready','Playing'].includes(m.status))return;
  this.store.transaction(()=>{
   this.store.run("UPDATE matches SET status='Aborted',ended_at=?,result=? WHERE id=?",now(),JSON.stringify({message}),mid);
   this.resetRoom(m.room_id);
  });this.publish(m.room_id,'match.aborted');this.cleanup(mid);
 }
 cleanup(mid:string) {
  try{unlinkSync(path.join(this.cfg.runtimeDir,mid+'.json'));}catch{}
  const child=this.processes.get(mid);
  if(child){setTimeout(()=>{if(child.exitCode===null)child.kill();},5000).unref();}
 }
 sweep(){
  for(const m of this.store.all("SELECT * FROM matches WHERE status IN ('Starting','Ready','Playing')")){
   if(now()-(m.status==='Starting'?m.created_at:m.heartbeat)>(m.status==='Starting'?this.cfg.startupSeconds:20))this.abort(m.id,'战斗服务器失去响应');
  }
 }
}
