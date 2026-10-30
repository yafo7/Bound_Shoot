using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BoundShoot.Tests {
 public class TrainingTests {
  [Test] public void TrainingContainsEveryGunAndItem(){
   var map=TrainingMap.Create();
   Assert.AreEqual(4,map.pickups.Count(p=>p.weapon!=WeaponKind.None));
   Assert.AreEqual(8,map.pickups.Count(p=>p.item!=ItemKind.None));
   Assert.IsTrue(map.pickups.All(p=>p.respawnSeconds==2&&!p.randomizeOnRespawn));
   Assert.AreEqual(4,map.blueSpawns.Length);Assert.AreEqual(4,map.redSpawns.Length);
  }
 }
}
