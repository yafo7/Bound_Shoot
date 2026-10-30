using NUnit.Framework;
namespace BoundShoot.Tests {
 public class WeaponPickupNoticeTests {
  [Test]public void NoticeOnlyAppearsOnNewWeaponAndExpiresThreeSecondsLater(){
   var notice=new WeaponPickupNotice();notice.Observe(0,10);Assert.IsFalse(notice.Visible(10));
   notice.Observe(3,11);Assert.IsTrue(notice.Visible(13.99f));notice.Observe(3,13.99f);Assert.IsFalse(notice.Visible(14));
   notice.Observe(2,15);Assert.IsTrue(notice.Visible(17.99f));Assert.IsFalse(notice.Visible(18));
  }
  [Test]public void RapidSwitchRestartsTimerWhileDeathAndNewRoomClearIt(){
   var notice=new WeaponPickupNotice();notice.Observe(1,10);notice.Observe(4,12);Assert.AreEqual(4,notice.Weapon);Assert.IsTrue(notice.Visible(14.99f));Assert.IsFalse(notice.Visible(15));
   notice.Observe(0,15);Assert.IsFalse(notice.Visible(15));notice.Observe(2,16);Assert.IsTrue(notice.Visible(16));notice.Clear();Assert.IsFalse(notice.Visible(16));
  }
 }
}
