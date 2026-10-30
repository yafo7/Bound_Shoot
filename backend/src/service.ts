import express,{Request,Response,NextFunction} from 'express';
import helmet from 'helmet';
import {rateLimit} from 'express-rate-limit';
import {createServer} from 'node:http';
import {WebSocketServer,WebSocket} from 'ws';
import {z,ZodError} from 'zod';
import {readFileSync} from 'node:fs';
import {config,Config} from './config.js';
import {Store} from './database.js';
import {Auth,Fault,check,now} from './security.js';
import {Rooms} from './rooms.js';
import {Matches} from './matches.js';
import {QuickPlay} from './quickplay.js';

const credentials=z.object({username:z.string().trim().min(3).max(24).regex(/^[a-zA-Z0-9_\u4e00-\u9fff]+$/),password:z.string().min(8).max(72).refine(s=>Buffer.byteLength(s,'utf8')<=72,'密码最多 72 字节')});
const resultSchema=z.object({winner:z.number().int().min(-1).max(1),duration:z.number().min(0).max(3600),
 players:z.array(z.object({userId:z.string().uuid(),kills:z.number().int().min(0).max(10000),deaths:z.number().int().min(0).max(10000),damage:z.number().int().min(0).max(1000000)})).min(4).max(8)});
export function createService(overrides:Partial<Config>={}) {
 const cfg=config(overrides),store=new Store(cfg.dbPath),auth=new Auth(store,cfg), app=express(),server=createServer(app);
 const wss=new WebSocketServer({server,path:'/ws',maxPayload:4096});
 const peers=new Map<WebSocket,{uid:string,sid:string,alive:boolean}>();
 function send(ws:WebSocket,data:any){if(ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify(data));}
 function publish(rid:string,event='room.changed') {
  const exists=store.get('SELECT id FROM rooms WHERE id=?',rid),room=exists?rooms.view(rid):null;
  for(const [ws,peer] of peers)if(rooms.member(peer.uid)?.room_id===rid)send(ws,{event,room});
 }
 const catalog=JSON.parse(readFileSync(cfg.gameConfigPath,'utf8'));
 const availableMaps=catalog.maps.filter((m:any)=>m.available).map((m:any)=>({id:m.id,name:m.name,available:true}));
 const rooms=new Rooms(store,publish,availableMaps.map((m:any)=>m.id)),matches=new Matches(store,cfg,rooms,publish);
 const quick=new QuickPlay(store,rooms,auth);
 app.disable('x-powered-by');app.use(helmet());app.use(express.json({limit:'16kb'}));
 app.use('/api/auth',rateLimit({windowMs:60000,limit:30,standardHeaders:'draft-8',legacyHeaders:false,
 handler:(_req,res)=>res.status(429).json({ok:false,error:{code:'RATE_LIMIT',message:'尝试过于频繁，请稍后重试'}})}));
 const ok=(res:Response,data:any)=>res.json({ok:true,data});
 const route=(fn:(req:Request,res:Response)=>any)=>(req:Request,res:Response,next:NextFunction)=>Promise.resolve().then(()=>fn(req,res)).catch(next);
 app.get('/health',(_req,res)=>ok(res,{service:'Bound Shoot',version:'0.2.0',entry:cfg.quickPlay?'name-only':'account',mode:cfg.quickPlay?'2v2':'rooms'}));
 app.post('/api/auth/quick',route((req,res)=>{
  check(cfg.quickPlay,404,'NOT_FOUND','入口未开放');
  const {name}=z.object({name:z.string().trim().min(1).max(24).regex(/^[a-zA-Z0-9_\u4e00-\u9fff]+$/)}).parse(req.body);
  ok(res,quick.login(name));
 }));
 app.post('/api/auth/register',route(async(req,res)=>{
  check(!cfg.quickPlay,410,'QUICK_PLAY_ONLY','请输入名字进入');
  const c=credentials.parse(req.body);ok(res,await auth.register(c.username,c.password));
 }));
 app.post('/api/auth/login',route(async(req,res)=>{
  check(!cfg.quickPlay,410,'QUICK_PLAY_ONLY','请输入名字进入');
  const c=credentials.parse(req.body);ok(res,await auth.login(c.username,c.password));
 }));
 app.post('/api/auth/refresh',route((req,res)=>ok(res,auth.refresh(z.object({refreshToken:z.string().min(16).max(256)}).parse(req.body).refreshToken))));
 app.use('/api',(req,res,next)=>{try{
  const p=auth.verify(req.headers.authorization?.replace(/^Bearer /,'')||'');
  res.locals.identity=p;
  store.run('UPDATE room_members SET last_seen=? WHERE user_id=?',now(),p.uid);next();
 }catch(e){next(e);}});
 const uid=(res:Response)=>res.locals.identity.uid as string;
 const rid=(req:Request)=>String(req.params.id);
 app.post('/api/auth/logout',route((_req,res)=>{
  store.run('UPDATE auth_sessions SET revoked=1 WHERE id=?',res.locals.identity.sid);ok(res,{});
 }));
 app.get('/api/users/me',route((_req,res)=>{
  const user=auth.profile(uid(res)), member=rooms.member(uid(res));
  ok(res,{...user,roomId:member?.room_id||''});
 }));
 app.patch('/api/users/me/cosmetic',route((req,res)=>{
  const {skin}=z.object({skin:z.number().int().min(0).max(7)}).parse(req.body);
  const member=rooms.member(uid(res));if(member)rooms.waiting(uid(res),member.room_id);
  store.run('UPDATE user_cosmetics SET skin=? WHERE user_id=?',skin,uid(res));
  if(member){store.run('UPDATE room_members SET ready=0 WHERE user_id=?',uid(res));publish(member.room_id,'cosmetic.changed');}
  ok(res,auth.profile(uid(res)));
 }));
 app.get('/api/catalog',(_req,res)=>ok(res,{maps:availableMaps,modes:cfg.quickPlay?[2]:[2,3,4]}));
 app.post('/api/rooms',route((_req,res)=>{check(!cfg.quickPlay,410,'QUICK_PLAY_ONLY','当前只有一个公共房间');ok(res,rooms.create(uid(res)));}));
 app.post('/api/rooms/join',route((req,res)=>{check(!cfg.quickPlay,410,'QUICK_PLAY_ONLY','请输入名字进入公共房间');ok(res,rooms.join(uid(res),z.object({code:z.string().regex(/^\d{6}$/)}).parse(req.body).code));}));
 app.get('/api/rooms/:id',route((req,res)=>ok(res,rooms.require(uid(res),rid(req)))));
 app.post('/api/rooms/:id/leave',route((req,res)=>{rooms.leave(uid(res),rid(req));ok(res,{});}));
 app.put('/api/rooms/:id/seat',route((req,res)=>{
  const s=z.object({team:z.number().int().min(0).max(1),seat:z.number().int().min(0).max(3)}).parse(req.body);
  ok(res,rooms.seat(uid(res),rid(req),s.team,s.seat));
 }));
 app.put('/api/rooms/:id/ready',route((req,res)=>ok(res,rooms.ready(uid(res),rid(req),z.object({ready:z.boolean()}).parse(req.body).ready))));
 app.put('/api/rooms/:id/config',route((req,res)=>{
  const s=z.object({size:z.union([z.literal(2),z.literal(3),z.literal(4)]),mapId:z.string()}).parse(req.body);
  ok(res,rooms.configure(uid(res),rid(req),s.size,s.mapId));
 }));
 app.post('/api/rooms/:id/start',route((req,res)=>ok(res,matches.start(uid(res),rid(req)))));
 app.get('/api/matches/:id',route((req,res)=>ok(res,matches.view(rid(req),uid(res)))));
 app.post('/api/matches/:id/ticket',route((req,res)=>ok(res,matches.ticket(rid(req),uid(res)))));
 app.get('/api/matches/:id/result',route((req,res)=>{
  matches.view(rid(req),uid(res));const m=store.get('SELECT status,result,map_id as mapId FROM matches WHERE id=?',rid(req));
  ok(res,{status:m.status,mapId:m.mapId,result:m.result?JSON.parse(m.result):null});
 }));
 app.get('/api/users/me/matches',route((_req,res)=>ok(res,{matches:store.all('SELECT m.id,m.map_id as mapId,m.status,m.winner,m.duration,p.team,p.kills,p.deaths,p.damage FROM matches m JOIN match_players p ON p.match_id=m.id WHERE p.user_id=? ORDER BY m.created_at DESC LIMIT 20',uid(res))})));
 app.use('/internal/matches/:id',(req,res,next)=>{try{res.locals.match=matches.internal(rid(req),req.headers['x-match-secret'] as string);next();}catch(e){next(e);}});
 app.post('/internal/matches/:id/register',route((req,res)=>{
  const m=res.locals.match;check(m.status==='Starting',409,'NOT_STARTING','对战并非启动状态');
  ok(res,{matchId:m.id,mapId:m.map_id,port:m.port,players:matches.roster(m.id),reconnectSeconds:cfg.reconnectSeconds});
 }));
 app.post('/internal/matches/:id/ready',route((req,res)=>ok(res,matches.ready(rid(req)))));
 app.post('/internal/matches/:id/heartbeat',route((req,res)=>{
  const m=res.locals.match;check(['Ready','Playing'].includes(m.status),409,'MATCH_ENDED','比赛已结束');
  store.run("UPDATE matches SET heartbeat=?,status=CASE WHEN status='Ready' AND ?=1 THEN 'Playing' ELSE status END WHERE id=?",now(),req.body?.playing===true?1:0,rid(req));ok(res,{});
 }));
 app.post('/internal/matches/:id/consume-ticket',route((req,res)=>ok(res,matches.consume(rid(req),z.object({ticket:z.string().min(16).max(256)}).parse(req.body).ticket))));
 app.post('/internal/matches/:id/result',route((req,res)=>ok(res,matches.finish(rid(req),resultSchema.parse(req.body)))));
 app.post('/internal/matches/:id/abort',route((req,res)=>{matches.abort(rid(req),'战斗服务器中止了比赛');ok(res,{});}));
 app.use((_req,res)=>res.status(404).json({ok:false,error:{code:'NOT_FOUND',message:'接口不存在'}}));
 app.use((e:any,_req:Request,res:Response,_next:NextFunction)=>{
  const status=e instanceof Fault?e.status:e instanceof ZodError||e.type==='entity.parse.failed'?400:500;
  if(status===500)console.error('Request failed:',e instanceof Error?e.name:'unknown');
  res.status(status).json({ok:false,error:{code:e instanceof Fault?e.code:status===400?'INVALID_INPUT':'INTERNAL_ERROR',message:e instanceof Fault?e.message:status===400?'输入格式不正确':'服务器内部错误'}});
 });
 wss.on('connection',ws=>{
  const deadline=setTimeout(()=>{if(!peers.has(ws))ws.close(4001,'Authentication required');},5000);deadline.unref();
  ws.on('message',raw=>{try{
   const data=JSON.parse(raw.toString());
   if(!peers.has(ws)){
    check(data.type==='auth',401,'AUTH_REQUIRED','请登录');const claims=auth.verify(data.accessToken);
    peers.set(ws,{...claims,alive:true});clearTimeout(deadline);
    const m=rooms.member(claims.uid);send(ws,{event:'authenticated',room:m?rooms.view(m.room_id):null});
   }else if(data.type==='ping'){const p=peers.get(ws)!;p.alive=true;store.run('UPDATE room_members SET last_seen=? WHERE user_id=?',now(),p.uid);send(ws,{event:'pong'});}
  }catch{ws.close(4001,'Invalid authentication or message');}});
  ws.on('pong',()=>{const p=peers.get(ws);if(p)p.alive=true;});
  ws.on('close',()=>{clearTimeout(deadline);peers.delete(ws);});
 });
 const sweep=setInterval(()=>{
  matches.sweep();
  for(const [ws,p] of peers){
   const session=store.get('SELECT id FROM auth_sessions WHERE id=? AND revoked=0 AND expires_at>?',p.sid,now());
   if(!session||!p.alive){ws.terminate();continue;}p.alive=false;ws.ping();
   store.run('UPDATE room_members SET last_seen=? WHERE user_id=?',now(),p.uid);
  }
  for(const m of store.all("SELECT m.* FROM room_members m JOIN rooms r ON r.id=m.room_id WHERE r.status='Waiting' AND m.last_seen<?",now()-cfg.reconnectSeconds))rooms.remove(m.user_id,m.room_id);
 },5000);sweep.unref();
 return {app,server,cfg,store,auth,rooms,matches,async close(){
  clearInterval(sweep);matches.stopping=true;for(const child of matches.processes.values())child.kill();
  for(const ws of wss.clients)ws.terminate();wss.close();
  await new Promise<void>(resolve=>server.listening?server.close(()=>resolve()):resolve());
  store.db.close();
 }};
}
