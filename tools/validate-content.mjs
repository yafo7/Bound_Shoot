import {readFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const c=JSON.parse(readFileSync(path.join(root,'unity-client/Assets/BoundShoot/Resources/GameConfig.json'),'utf8'));
const positive=(v,key)=>assert.ok(Number.isFinite(v)&&v>0,key+' must be positive');
for(const key of ['baseHealth','roundSeconds','physicsHz','snapshotHz','moveSpeed','ropeLength','ropeSpring','ropeDamping','ropeMaxForce','reviveSeconds','bombFuse','bombRadius'])positive(c[key],key);
assert.ok(c.snapshotHz<=c.physicsHz,'snapshot rate must not exceed physics rate');
assert.equal(new Set(c.weapons.map(w=>w.kind)).size,4,'provide four unique weapon kinds');
for(const w of c.weapons){
 assert.ok([1,2,3,4].includes(w.kind),'invalid weapon kind');
 assert.ok(Number.isInteger(w.maxHealth)&&w.maxHealth>0,'integer weapon health required');
 assert.ok(Number.isInteger(w.damage)&&w.damage>0,'integer damage required');
 positive(w.range,'weapon range');positive(w.cooldown,'cooldown');
 if(w.kind!==4)positive(w.bulletSpeed,'bullet speed');
}
assert.equal(new Set(c.items.map(i=>i.kind)).size,c.items.length,'duplicate item kind');
assert.ok(c.items.some(i=>i.enabled&&i.weight>0),'empty mystery pool');
for(const i of c.items){assert.ok([1,2,3,4,5,6,7].includes(i.kind),'invalid item kind');assert.ok(i.weight>=0&&i.duration>=0,'negative item weight/duration');}
assert.equal(new Set(c.maps.map(m=>m.id)).size,c.maps.length,'duplicate map id');
assert.ok(c.maps.some(m=>m.id==='Arena_01'&&m.available),'default map must remain available');
for(const m of c.maps.filter(m=>m.available)){
 positive(m.halfWidth,'map width');positive(m.halfHeight,'map height');
 assert.ok(m.blueSpawns.length===4&&m.redSpawns.length===4,'four spawn seats per team required');
 assert.ok(m.supplies.length>=8&&m.revivals.length>=2,'at least eight supply positions and two revival zones required');
 for(const b of m.blocks){positive(b.size.x,'block x');positive(b.size.y,'block y');}
 for(const p of [...m.blueSpawns,...m.redSpawns,...m.supplies,...m.revivals]){
  assert.ok(Number.isFinite(p.x)&&Number.isFinite(p.y)&&Math.abs(p.x)<m.halfWidth&&Math.abs(p.y)<m.halfHeight,'point outside '+m.id);
  assert.ok(!m.blocks.some(b=>Math.abs(p.x-b.center.x)<b.size.x/2+.45&&Math.abs(p.y-b.center.y)<b.size.y/2+.45),'point overlaps cover in '+m.id);
 }
}
console.log('PASS: shared weapon/item/map configuration and collision clearances');
