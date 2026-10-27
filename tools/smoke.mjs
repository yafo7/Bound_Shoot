// Real UDP/NGO process test. It does not replace a two-computer network acceptance test.
import {spawn} from 'node:child_process';
import {randomBytes} from 'node:crypto';
import {mkdirSync,writeFileSync,readFileSync,existsSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const exe=path.join(root,'builds/Windows/BoundShoot.exe');
if(!existsSync(exe))throw new Error('Build Windows first');
const stamp=Date.now().toString(36),work=path.join(root,'logs','smoke-'+stamp),runtime=path.join(work,'runtime');
mkdirSync(runtime,{recursive:true});
const base='http://127.0.0.1:3127',children=[];
const env={...process.env,PORT:'3127',HOST:'127.0.0.1',ACCESS_SECRET:randomBytes(48).toString('hex'),DATABASE_PATH:path.join(work,'test.sqlite'),
 RUNTIME_DIR:runtime,SERVER_MODE:'managed',GAME_SERVER_PATH:exe,PUBLIC_GAME_HOST:'127.0.0.1',GAME_PORT_START:'17877',
 GAME_CONFIG_PATH:path.join(root,'unity-client/Assets/BoundShoot/Resources/GameConfig.json')};
const backend=spawn(process.execPath,['dist/index.js'],{cwd:path.join(root,'backend'),env,windowsHide:true,stdio:'pipe'});
children.push(backend);let backendOutput='';backend.stdout.on('data',x=>backendOutput+=x);backend.stderr.on('data',x=>backendOutput+=x);
const delay=ms=>new Promise(r=>setTimeout(r,ms));
async function call(method,url,body={},token=''){
 const r=await fetch(base+url,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(method==='GET'?{}:{body:JSON.stringify(body)})});
 const j=await r.json();if(!j.ok)throw new Error(url+': '+j.error.message);return j.data;
}
const logs=[],reports=[],rounds=[];
async function waitSync(matchId){
 for(let step=0;step<100;step++){
  await delay(500);
  const synced=reports.every(file=>{if(!existsSync(file))return false;const r=JSON.parse(readFileSync(file,'utf8'));return r.matchId===matchId&&r.players===4&&r.owned===1;});
  if(synced)return true;
 }
 throw new Error('Not all four clients synchronized for '+matchId+'. Inspect '+work);
}
async function waitResult(matchId,token){
 for(let i=0;i<100;i++){
  await delay(500);const result=await call('GET','/api/matches/'+matchId+'/result',{},token);
  if(result.status==='Finished')return result;
  if(result.status==='Aborted')throw new Error('Match aborted: '+matchId);
 }
 throw new Error('Match failed to finish within smoke test deadline');
}
try{
 let up=false;for(let i=0;i<60;i++){try{await call('GET','/health');up=true;break;}catch{}await delay(500);}if(!up)throw new Error('Backend failed: '+backendOutput);
 const users=[];for(let i=0;i<4;i++)users.push(await call('POST','/api/auth/register',{username:'smoke_'+i,password:'SmokeOnly_'+stamp}));
 let room=await call('POST','/api/rooms',{},users[0].accessToken);
 for(let i=1;i<4;i++)await call('POST','/api/rooms/join',{code:room.code},users[i].accessToken);
 for(let i=0;i<4;i++)await call('PUT','/api/rooms/'+room.id+'/ready',{ready:true},users[i].accessToken);
 const match=await call('POST','/api/rooms/'+room.id+'/start',{},users[0].accessToken);
 console.log('Started match '+match.id+'; launching four independent clients');
 for(let i=0;i<4;i++){
  const plan=path.join(work,'client-'+i+'.json'),log=path.join(work,'client-'+i+'.log'),reportFile=path.join(work,'client-'+i+'-sync.json');logs.push(log);reports.push(reportFile);
  writeFileSync(plan,JSON.stringify({backendUrl:base,username:'smoke_'+i,password:'SmokeOnly_'+stamp,
   screenshot:i===0?path.join(work,'battle.png'):'',reportFile,runSeconds:25,quitAfter:false,combat:true,autoReturn:true}));
  const child=spawn(exe,['-testPlan',plan,'-screen-width','1000','-screen-height','700','-logFile',log],{windowsHide:i!==0,stdio:'ignore'});children.push(child);
 }
 await waitSync(match.id);
 console.log('PASS: four independent clients each observe 4 players and exactly 1 owned player');
 const result=await waitResult(match.id,users[0].accessToken);rounds.push({matchId:match.id,...result});
 const serverLog=path.join(runtime,match.id+'.log');
 if(result.status==='Finished'){
  room=await call('GET','/api/rooms/'+room.id,{},users[0].accessToken);
  if(room.status!=='Waiting'||room.members.some(m=>m.ready))throw new Error('Room did not reset');
  console.log('PASS: authoritative result persisted and original room reset for next match');
 }
 await delay(1500); // Let all four clients display results and return through the normal UI flow.
 for(let i=0;i<4;i++)await call('PUT','/api/rooms/'+room.id+'/ready',{ready:true},users[i].accessToken);
 const next=await call('POST','/api/rooms/'+room.id+'/start',{},users[0].accessToken);
 if(next.id===match.id)throw new Error('Rematch reused match identity');
 await waitSync(next.id);
 const second=await waitResult(next.id,users[0].accessToken);rounds.push({matchId:next.id,...second});
 for(const user of users){const profile=await call('GET','/api/users/me',{},user.accessToken);if(profile.games!==2)throw new Error('Incorrect cumulative game count');}
 console.log('PASS: same four clients reconnected to a new dedicated server and completed a second match');
 await delay(1500);
 const output={fourClientsSynchronized:true,twoCompleteRounds:true,rounds,work,serverLog,clientLogs:logs};
 writeFileSync(path.join(work,'report.json'),JSON.stringify(output,null,2));console.log(JSON.stringify(output,null,2));
}finally{
 for(const child of children.reverse())if(child.exitCode===null)child.kill();
 writeFileSync(path.join(work,'backend.log'),backendOutput);
}
