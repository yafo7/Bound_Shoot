// Local process acceptance: name-only UI clients + server bots + an authoritative server.
// Uses isolated test accounts/database. Never contacts the cloud server.
import {spawn,spawnSync} from 'node:child_process';
import {randomBytes} from 'node:crypto';
import {mkdirSync,writeFileSync,readFileSync,existsSync,unlinkSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {networkInterfaces} from 'node:os';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const humans=Number(process.argv.find(a=>a.startsWith('--humans='))?.split('=')[1]??1);
if(![1,2,4].includes(humans))throw new Error('Use --humans=1, 2 or 4');
const exe=path.join(root,'builds/QA/BoundShootQA.exe');
if(!existsSync(exe))throw new Error('Run tools/build-game.ps1 -Target QA first.');
const linux=process.platform==='linux';
const gameServer=linux?path.join(root,'builds/LinuxServer/BoundShootServer.x86_64'):exe;
if(!existsSync(gameServer))throw new Error('Build LinuxServer first.');
const gameHost=linux?networkInterfaces().eth0?.find(n=>n.family==='IPv4')?.address:'127.0.0.1';
if(!gameHost)throw new Error('Cannot determine local WSL address');
const windowsPath=p=>linux?p.replace(/^\/mnt\/([a-z])\//,(_m,drive)=>drive.toUpperCase()+':/').replaceAll('/','\\'):p;
const work=path.join(root,'logs','four-clients-'+Date.now()),runtime=path.join(work,'runtime');
mkdirSync(runtime,{recursive:true});
const port=Number(process.argv.find(a=>a.startsWith('--port='))?.split('=')[1]??3129);
if(!Number.isInteger(port)||port<1024||port>45000)throw new Error('Invalid isolated QA port');
const base='http://127.0.0.1:'+port,clientBase='http://'+gameHost+':'+port,children=[],plans=[];
let backendOutput='';
const backend=spawn(process.execPath,['dist/index.js'],{cwd:path.join(root,'backend'),windowsHide:true,stdio:'pipe',env:{...process.env,
 HOST:linux?'0.0.0.0':'127.0.0.1',PORT:String(port),NODE_ENV:'test',ACCESS_SECRET:randomBytes(48).toString('hex'),DATABASE_PATH:path.join(work,'test.sqlite'),
 RUNTIME_DIR:runtime,SERVER_MODE:'managed',GAME_SERVER_PATH:gameServer,PUBLIC_GAME_HOST:gameHost,GAME_PORT_START:String(17877+(port-3129)*2),MAX_MATCHES:'2',
 GAME_CONFIG_PATH:process.env.QA_GAME_CONFIG_PATH||path.join(root,'unity-client/Assets/BoundShoot/Resources/GameConfig.json')}});
children.push(backend);backend.stdout.on('data',v=>backendOutput+=v);backend.stderr.on('data',v=>backendOutput+=v);
const delay=ms=>new Promise(r=>setTimeout(r,ms));
const reports=()=>Array.from({length:humans},(_,i)=>{try{return JSON.parse(readFileSync(path.join(work,'client-'+i+'.report.json'),'utf8'));}catch{return null;}});
try{
 let ready=false;
 for(let i=0;i<30;i++){
  if(backend.exitCode!==null)throw new Error('Local backend exited: '+backendOutput);
  try{const r=await fetch(base+'/health');if(r.ok){ready=true;break;}}catch{}
  await delay(500);
 }
 if(!ready)throw new Error('Local backend did not start.');
 const suffix=randomBytes(4).toString('hex');
 for(let i=0;i<humans;i++){
  const plan=path.join(work,'plan-'+i+'.json');plans.push(plan);
  writeFileSync(plan,JSON.stringify({username:'qa_'+suffix+'_'+i,work:windowsPath(work),index:i,humans}));
  const child=spawn(exe,['-qaPlan',windowsPath(plan),'-backend',clientBase,'-screen-width','960','-screen-height','600','-logFile',windowsPath(path.join(work,'client-'+i+'.log'))],{stdio:'ignore',windowsHide:true});
  children.push(child);child.once('error',e=>{throw e;});
 }
 console.log(humans+' human client(s) + '+(4-humans)+' server bots. Reports: '+work);
 let previous='';
 const deadline=Date.now()+500000;
 while(Date.now()<deadline){
  const rows=reports(),status=rows.map((r,i)=>i+':'+(r?.stage??'starting')).join(' | ');
  if(status!==previous){console.log(status);previous=status;}
  if(rows.some(r=>r?.error))throw new Error(JSON.stringify(rows));
  if(children.slice(1).some((c,i)=>c.exitCode!==null&&!rows[i]?.passed))throw new Error('Client exited before acceptance passed');
  if(rows.every(r=>r?.passed)){
   const summary={passed:true,humans,bots:4-humans,scope:linux?'Local WSL/Linux dedicated server + Windows NGO/UDP clients; cloud not exercised':'Local Windows NGO/UDP + managed authoritative server; cloud not exercised',reports:rows,work};
   writeFileSync(path.join(work,'summary.json'),JSON.stringify(summary,null,2));console.log('PASS: name-only entry, shared room, AI fill, combat results, rematch, persisted games=2');
   break;
  }
  await delay(1000);
 }
 if(!existsSync(path.join(work,'summary.json')))throw new Error('Acceptance deadline exceeded: '+work);
}finally{
 for(const child of children.reverse())if(child.exitCode===null)child.kill();
 if(linux){
  // Killing a WSL interop shim does not always close its Windows player.
  const literal=s=>"'"+s.replaceAll("'","''")+"'";
  spawnSync('powershell.exe',['-NoProfile','-Command',
   `Get-CimInstance Win32_Process -Filter "Name='BoundShootQA.exe'" | Where-Object { $_.ExecutablePath -eq ${literal(windowsPath(exe))} -and $_.CommandLine.Contains(${literal(windowsPath(work))}) } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }`],{stdio:'ignore',windowsHide:true});
 }
 for(const plan of plans)try{unlinkSync(plan);}catch{}
 writeFileSync(path.join(work,'backend.log'),backendOutput);
}
