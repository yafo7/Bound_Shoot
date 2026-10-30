import {Auth,check,id,now} from './security.js';
import {Rooms,QUICK_ROOM_ID} from './rooms.js';
import {Store} from './database.js';

// Name-only debug identities are separate from the previous password accounts.
export class QuickPlay {
 constructor(readonly store:Store,readonly rooms:Rooms,readonly auth:Auth){
  store.transaction(()=>{
   for(let team=0;team<2;team++)for(let seat=0;seat<4;seat++){
    const name='AI_'+(team===0?'A':'B')+(seat+1);
    const bot=store.get('SELECT user_id FROM quick_identities WHERE name=?',name);
    if(!bot)this.create(name,true,team*4+seat);
    else store.run('UPDATE user_cosmetics SET skin=? WHERE user_id=?',team*4+seat,bot.user_id);
   }
  });
 }
 create(name:string,bot=false,skin=0){
  const uid=id();
  this.store.run('INSERT INTO users(id,username,display_name,password_hash,created_at) VALUES(?,?,?,?,?)',uid,'quick_'+uid,name,'!name-only',now());
  this.store.run('INSERT INTO user_cosmetics(user_id,skin) VALUES(?,?)',uid,skin);
  this.store.run('INSERT INTO quick_identities(user_id,name,is_bot) VALUES(?,?,?)',uid,name,bot?1:0);
  return uid;
 }
 login(name:string){
  check(!name.toUpperCase().startsWith('AI_'),400,'RESERVED_NAME','请换一个名字');
  const result=this.store.transaction(()=>{
   const identity=this.store.get('SELECT * FROM quick_identities WHERE name=?',name);
   const uid=identity?.user_id??this.create(name);
   const previous=this.rooms.member(uid);
   check(!previous||previous.last_seen<=now()-20,409,'NAME_IN_USE','这个名字正在使用，请换一个名字');
   const room=this.rooms.quickJoin(uid);
   this.store.run('UPDATE auth_sessions SET revoked=1 WHERE user_id=?',uid);
   return {...this.auth.issue(uid),room};
  });
  this.rooms.publish(QUICK_ROOM_ID,'member.joined');
  return result;
 }
}
