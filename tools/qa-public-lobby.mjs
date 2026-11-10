// Explicit public acceptance. Uses normal guest accounts and creates two real matches.
// Does not start a local backend, change server configuration, or read credentials.
import {spawn} from 'node:child_process';
import {randomBytes} from 'node:crypto';
import {mkdirSync,writeFileSync,readFileSync,existsSync,unlinkSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const endpoint=process.argv.find(a=>a.startsWith('--base='))?.slice(7);
if(!endpoint)throw new Error('Pass --base=http://123.207.238.56:3000 explicitly. This test creates real public matches.');
const url=new URL(endpoint);
if(!['http:','https:'].includes(url.protocol)||url.username||url.password||url.search||url.hash||url.pathname!=='/')throw new Error('Use an HTTP(S) origin without credentials.');
const base=url.origin;
if(process.platform!=='win32')throw new Error('Run on Windows with the existing Windows QA build.');
const exe=path.join(root,'builds/QA/BoundShootQA.exe');
if(!existsSync(exe))throw new Error('Missing QA build.');
const response=await fetch(base+'/health',{signal:AbortSignal.timeout(10000)});
const health=await response.json();
if(!response.ok||!health.ok||health.data?.version!=='0.3.0'||health.data?.mode!=='lobby')throw new Error('The public server is not running lobby version 0.3.0.');

const humans=2,size=2,children=[],plans=[];
const work=path.join(root,'logs','public-lobby-'+Date.now());
mkdirSync(work,{recursive:true});
const delay=ms=>new Promise(r=>setTimeout(r,ms));
const reports=()=>Array.from({length:humans},(_,i)=>{try{return JSON.parse(readFileSync(path.join(work,'client-'+i+'.report.json'),'utf8'));}catch{return null;}});
let launchError=null;
try{
 const suffix=randomBytes(4).toString('hex');
 for(let i=0;i<humans;i++){
  const plan=path.join(work,'plan-'+i+'.json');plans.push(plan);
  writeFileSync(plan,JSON.stringify({username:'qa_'+suffix+'_'+i,work,index:i,humans,size,mapId:'Depot_01'}));
  const child=spawn(exe,['-qaPlan',plan,'-backend',base,'-screen-width','960','-screen-height','600','-logFile',path.join(work,'client-'+i+'.log')],{
   stdio:'ignore',windowsHide:true,env:{...process.env,UNITY_PROXYSERVER:'',UNITY_NOPROXY:'*',NO_PROXY:url.hostname,no_proxy:url.hostname}});
  children.push(child);child.once('error',e=>{launchError=e;});
 }
 console.log('Public acceptance: 2 Windows clients + 2 server bots, 2 rounds. Reports: '+work);
 let previous='';
 const deadline=Date.now()+500000;
 while(Date.now()<deadline){
  if(launchError)throw launchError;
  const rows=reports(),status=rows.map((r,i)=>i+':'+(r?.stage??'starting')).join(' | ');
  if(status!==previous){console.log(status);previous=status;}
  if(rows.some(r=>r?.error))throw new Error(JSON.stringify(rows));
  if(children.some((c,i)=>c.exitCode!==null&&!rows[i]?.passed))throw new Error('Client exited before acceptance passed.');
  if(rows.every(r=>r?.passed)){
   writeFileSync(path.join(work,'summary.json'),JSON.stringify({passed:true,base,humans,size,bots:2,scope:'Two Windows clients on one computer over the public network to the deployed Linux dedicated server; separate client networks not exercised',health,reports:rows,work},null,2));
   console.log('PASS: public login, create/join, map sync, NGO/UDP battle, results, rematch, games=2, return to lobby/menu.');
   break;
  }
  await delay(1000);
 }
 if(!existsSync(path.join(work,'summary.json')))throw new Error('Public acceptance deadline exceeded: '+work);
}finally{
 for(const child of children)if(child.exitCode===null)child.kill();
 for(const plan of plans)try{unlinkSync(plan);}catch{}
}
