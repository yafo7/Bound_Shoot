import { DatabaseSync } from 'node:sqlite';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
export class Store {
 readonly db:DatabaseSync;
 constructor(file:string) {
  if(file!==':memory:') mkdirSync(path.dirname(path.resolve(file)),{recursive:true});
  this.db=new DatabaseSync(file);
  this.db.exec('PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;');
  const migration=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../migrations/001_initial.sql');
  this.db.exec(readFileSync(migration,'utf8'));
  if(!this.get('SELECT version FROM schema_migrations WHERE version=2'))
   this.transaction(()=>this.db.exec(readFileSync(path.join(path.dirname(migration),'002_quick_play.sql'),'utf8')));
 }
 get(sql:string,...args:any[]):any {return this.db.prepare(sql).get(...args);}
 all(sql:string,...args:any[]):any[] {return this.db.prepare(sql).all(...args);}
 run(sql:string,...args:any[]) {return this.db.prepare(sql).run(...args);}
 transaction<T>(fn:()=>T):T {
  this.db.exec('BEGIN IMMEDIATE');
  try {const result=fn(); this.db.exec('COMMIT'); return result;}
  catch(e) {this.db.exec('ROLLBACK'); throw e;}
 }
}
