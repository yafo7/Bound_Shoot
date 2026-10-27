import {createHmac,createHash,randomBytes,timingSafeEqual,randomUUID} from 'node:crypto';
import bcrypt from 'bcryptjs';
import type {Config} from './config.js';
import {Store} from './database.js';
export const id=()=>randomUUID();
export const opaque=()=>randomBytes(32).toString('base64url');
export const digest=(s:string)=>createHash('sha256').update(s).digest('hex');
export const now=()=>Math.floor(Date.now()/1000);
export class Fault extends Error {constructor(public status:number,public code:string,message:string){super(message);}}
export function check(value:any,status:number,code:string,message:string):asserts value {
 if(!value) throw new Fault(status,code,message);
}
export class Auth {
 constructor(readonly store:Store, readonly cfg:Config){}
 async register(username:string,password:string) {
  check(!this.store.get('SELECT id FROM users WHERE username=?',username),409,'USERNAME_EXISTS','用户名已存在');
  const hash=await bcrypt.hash(password,12);
  return this.store.transaction(()=>{
   check(!this.store.get('SELECT id FROM users WHERE username=?',username),409,'USERNAME_EXISTS','用户名已存在');
   const uid=id(); this.store.run('INSERT INTO users(id,username,display_name,password_hash,created_at) VALUES(?,?,?,?,?)',uid,username,username,hash,now());
   this.store.run('INSERT INTO user_cosmetics(user_id) VALUES(?)',uid);
   return this.issue(uid);
  });
 }
 async login(username:string,password:string) {
  const user=this.store.get('SELECT * FROM users WHERE username=?',username);
  const valid=await bcrypt.compare(password,user?.password_hash || '$2b$12$YbFzL2O4PKfOtLtEx44PMuVZJhK1YhLsvF0H1QdGKNhhYp7LTBLm2');
  check(user && valid,401,'LOGIN_FAILED','用户名或密码错误');
  return this.issue(user.id);
 }
 access(uid:string,sid:string) {
  const payload=Buffer.from(JSON.stringify({uid,sid,exp:now()+this.cfg.accessSeconds})).toString('base64url');
  const signature=createHmac('sha256',this.cfg.accessSecret).update(payload).digest('base64url');
  return payload+'.'+signature;
 }
 issue(uid:string) {
  const sid=id(), refresh=opaque();
  this.store.run('INSERT INTO auth_sessions(id,user_id,refresh_hash,expires_at) VALUES(?,?,?,?)',sid,uid,digest(refresh),now()+this.cfg.refreshSeconds);
  return {accessToken:this.access(uid,sid),refreshToken:refresh,user:this.profile(uid)};
 }
 verify(token:string) {
  check(typeof token==='string' && token.length<2048,401,'AUTH_REQUIRED','请重新登录');
  const parts=token.split('.');
  check(parts.length===2,401,'INVALID_TOKEN','登录凭证无效');
  const expected=createHmac('sha256',this.cfg.accessSecret).update(parts[0]).digest();
  const actual=Buffer.from(parts[1],'base64url');
  check(actual.length===expected.length && timingSafeEqual(actual,expected),401,'INVALID_TOKEN','登录凭证无效');
  let claims:any;try{claims=JSON.parse(Buffer.from(parts[0],'base64url').toString());}catch{throw new Fault(401,'INVALID_TOKEN','登录凭证无效');}
  check(claims.exp>now(),401,'TOKEN_EXPIRED','登录凭证已过期');
  const session=this.store.get('SELECT * FROM auth_sessions WHERE id=? AND user_id=? AND revoked=0 AND expires_at>?',claims.sid,claims.uid,now());
  check(session,401,'SESSION_REVOKED','登录已失效');
  return {uid:claims.uid as string,sid:claims.sid as string};
 }
 refresh(token:string) {
  return this.store.transaction(()=>{
   const session=this.store.get('SELECT * FROM auth_sessions WHERE refresh_hash=? AND revoked=0 AND expires_at>?',digest(token),now());
   check(session,401,'REFRESH_FAILED','登录已失效');
   this.store.run('UPDATE auth_sessions SET revoked=1 WHERE id=?',session.id);
   return this.issue(session.user_id);
  });
 }
 profile(uid:string) {
  const u=this.store.get('SELECT u.id,u.username,u.display_name as displayName,c.skin,u.games,u.wins,u.kills,u.deaths FROM users u JOIN user_cosmetics c ON c.user_id=u.id WHERE u.id=?',uid);
  check(u,404,'USER_MISSING','账号不存在'); return u;
 }
}
