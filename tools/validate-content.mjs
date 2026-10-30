import {readFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const c=JSON.parse(readFileSync(process.argv[2]||path.join(root,'unity-client/Assets/BoundShoot/Resources/GameConfig.json'),'utf8'));
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
 const typed=m.layoutVersion>=1;
 const pickups=typed?m.pickups:m.supplies.map((position,i)=>({position,weapon:i<5?i<4?i+1:0:i<10?i-5<4?i-4:0:0}));
 const zones=typed?m.revivalZones:m.revivals.map(center=>({center,size:{x:4,y:3}}));
 assert.ok(Array.isArray(pickups)&&pickups.some(p=>p.weapon>0),'provide at least one weapon pickup');
 assert.ok(Array.isArray(zones),'invalid legacy revival array');
 if(!typed)assert.ok(m.supplies.length>=8,'legacy supply array is incomplete');
 const inside=(p,h={x:0,y:0})=>Number.isFinite(p.x)&&Number.isFinite(p.y)&&Math.abs(p.x)+h.x<=m.halfWidth&&Math.abs(p.y)+h.y<=m.halfHeight;
 for(const rect of [...m.blocks,...(m.floors||[]),...zones]){
  positive(rect.size.x,'rectangle width');positive(rect.size.y,'rectangle height');assert.ok(inside(rect.center,{x:rect.size.x/2,y:rect.size.y/2}),'rectangle outside '+m.id);
 }
 if(typed)for(const p of pickups){
  assert.ok([0,1,2,3,4].includes(p.weapon)&&[0,1,2,3,4,5,6,7,8].includes(p.item),'invalid pickup enum');
  assert.ok((p.weapon>0)!==(p.item>0),'pickup must contain one weapon or item');
  assert.ok(Number.isFinite(p.respawnSeconds)&&p.respawnSeconds>=0,'invalid pickup respawn');
  if(p.item>0&&p.item!==8)assert.ok(c.items.some(i=>i.kind===p.item&&i.enabled),'disabled pickup item');
 }
 for(const p of [...m.blueSpawns,...m.redSpawns,...pickups.map(p=>p.position),...zones.map(z=>z.center)]){
  assert.ok(inside(p,{x:.45,y:.45}),'point outside '+m.id);
  assert.ok(!m.blocks.some(b=>Math.abs(p.x-b.center.x)<b.size.x/2+.45&&Math.abs(p.y-b.center.y)<b.size.y/2+.45),'point overlaps cover in '+m.id);
 }
}
console.log('PASS: shared weapon/item/map configuration and collision clearances');
