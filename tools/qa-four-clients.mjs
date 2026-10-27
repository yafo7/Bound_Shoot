// Local process acceptance: four UI-driven clients + an independent authoritative server.
// Uses isolated test accounts/database. Never contacts the cloud server.
import {spawn,spawnSync} from 'node:child_process';
import {randomBytes} from 'node:crypto';
import {mkdirSync,writeFileSync,readFileSync,existsSync,unlinkSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {networkInterfaces} from 'node:os';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
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
const port=3129,base='http://127.0.0.1:'+port,clientBase='http://'+gameHost+':'+port,children=[],plans=[];
let backendOutput='';
const backend=spawn(process.execPath,['dist/index.js'],{cwd:path.join(root,'backend'),windowsHide:true,stdio:'pipe',env:{...process.env,
 HOST:linux?'0.0.0.0':'127.0.0.1',PORT:String(port),NODE_ENV:'test',ACCESS_SECRET:randomBytes(48).toString('hex'),DATABASE_PATH:path.join(work,'test.sqlite'),
 RUNTIME_DIR:runtime,SERVER_MODE:'managed',GAME_SERVER_PATH:gameServer,PUBLIC_GAME_HOST:gameHost,GAME_PORT_START:'17877',MAX_MATCHES:'2',
 GAME_CONFIG_PATH:process.env.QA_GAME_CONFIG_PATH||path.join(root,'unity-client/Assets/BoundShoot/Resources/GameConfig.json')}});
children.push(backend);backend.stdout.on('data',v=>backendOutput+=v);backend.stderr.on('data',v=>backendOutput+=v);
const delay=ms=>new Promise(r=>setTimeout(r,ms));
const reports=()=>Array.from({length:4},(_,i)=>{try{return JSON.parse(readFileSync(path.join(work,'client-'+i+'.report.json'),'utf8'));}catch{return null;}});
try{
 let ready=false;
 for(let i=0;i<30;i++){
  if(backend.exitCode!==null)throw new Error('Local backend exited: '+backendOutput);
  try{const r=await fetch(base+'/health');if(r.ok){ready=true;break;}}catch{}
  await delay(500);
 }
 if(!ready)throw new Error('Local backend did not start.');
 const suffix=randomBytes(4).toString('hex');
 for(let i=0;i<4;i++){
  const plan=path.join(work,'plan-'+i+'.json');plans.push(plan);
  writeFileSync(plan,JSON.stringify({username:'qa_'+suffix+'_'+i,password:'LocalQa_'+randomBytes(12).toString('hex'),work:windowsPath(work),index:i}));
  const child=spawn(exe,['-qaPlan',windowsPath(plan),'-backend',clientBase,'-screen-width','960','-screen-height','600','-logFile',windowsPath(path.join(work,'client-'+i+'.log'))],{stdio:'ignore',windowsHide:true});
  children.push(child);child.once('error',e=>{throw e;});
 }
 console.log('Four independent clients launched. Reports: '+work);
 let previous='';
 const deadline=Date.now()+380000;
 while(Date.now()<deadline){
  const rows=reports(),status=rows.map((r,i)=>i+':'+(r?.stage??'starting')).join(' | ');
  if(status!==previous){console.log(status);previous=status;}
  if(rows.some(r=>r?.error))throw new Error(JSON.stringify(rows));
  if(children.slice(1).some((c,i)=>c.exitCode!==null&&!rows[i]?.passed))throw new Error('Client exited before acceptance passed');
  if(rows.every(r=>r?.passed)){
   const summary={passed:true,scope:linux?'Local WSL/Linux dedicated server + four Windows NGO/UDP clients; cloud not exercised':'Local Windows NGO/UDP + managed Windows authoritative server; cloud/Linux not exercised',reports:rows,work};
   writeFileSync(path.join(work,'summary.json'),JSON.stringify(summary,null,2));console.log('PASS: registration/login, four owners, combat results, rematch, full 180-second round, persisted games=2');
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
