using System.Collections.Generic;
using NUnit.Framework;

namespace VertigoDemo.BattlePass.Tests
{
    public sealed class BattlePassProgressTests
    {
        [Test]
        public void RewardsAboveTheCurrentLevelAreLocked()
        {
            var progress = new BattlePassProgress(levelCount: 10, xpPerLevel: 100, level: 3);

            Assert.AreEqual(RewardState.Claimable, progress.GetState(3, RewardTrack.Free));
            Assert.AreEqual(RewardState.Locked, progress.GetState(4, RewardTrack.Free));
            Assert.AreEqual(RewardState.Locked, progress.GetState(4, RewardTrack.Premium));
        }

        [Test]
        public void PremiumRewardsNeedThePass()
        {
            var progress = new BattlePassProgress(10, 100, level: 2);

            Assert.AreEqual(RewardState.PremiumLocked, progress.GetState(1, RewardTrack.Premium));
            Assert.IsFalse(progress.TryClaim(1, RewardTrack.Premium));
        }

        [Test]
        public void ClaimingMovesAClaimableRewardToClaimedOnce()
        {
            var progress = new BattlePassProgress(10, 100, level: 2);
            var changes = Record(progress);

            Assert.IsTrue(progress.TryClaim(2, RewardTrack.Free));
            Assert.IsFalse(progress.TryClaim(2, RewardTrack.Free));
            Assert.AreEqual(RewardState.Claimed, progress.GetState(2, RewardTrack.Free));
            CollectionAssert.AreEqual(new[] { "2 Free Claimable>Claimed" }, changes);
        }

        [Test]
        public void GainingXpUnlocksEveryLevelReached()
        {
            var progress = new BattlePassProgress(10, 100, level: 1, xp: 50);
            var changes = Record(progress);

            int gained = progress.AddXp(170);

            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, progress.Level);
            Assert.AreEqual(20, progress.Xp);
            CollectionAssert.AreEqual(new[]
            {
                "2 Free Locked>Claimable", "2 Premium Locked>PremiumLocked",
                "3 Free Locked>Claimable", "3 Premium Locked>PremiumLocked",
            }, changes);
        }

        [Test]
        public void BuyingALevelKeepsCollectedXp()
        {
            var progress = new BattlePassProgress(10, 200, level: 3, xp: 80);

            Assert.IsTrue(progress.BuyLevel());
            Assert.AreEqual(4, progress.Level);
            Assert.AreEqual(80, progress.Xp);
        }

        [Test]
        public void MaxLevelStopsProgress()
        {
            var progress = new BattlePassProgress(3, 100, level: 2, xp: 90);

            progress.AddXp(500);

            Assert.IsTrue(progress.IsMaxLevel);
            Assert.AreEqual(0, progress.Xp);
            Assert.IsFalse(progress.BuyLevel());
            Assert.AreEqual(0, progress.AddXp(100));
        }

        [Test]
        public void UnlockingPremiumOnlyUnlocksReachedUnclaimedRewards()
        {
            var progress = new BattlePassProgress(10, 100, level: 3);
            progress.MarkClaimed(2, RewardTrack.Premium);
            var changes = Record(progress);

            Assert.IsTrue(progress.UnlockPremium());
            Assert.IsFalse(progress.UnlockPremium());

            CollectionAssert.AreEqual(new[]
            {
                "1 Premium PremiumLocked>Claimable", "3 Premium PremiumLocked>Claimable",
            }, changes);
            Assert.AreEqual(RewardState.Locked, progress.GetState(4, RewardTrack.Premium));
        }

        static List<string> Record(BattlePassProgress progress)
        {
            var log = new List<string>();
            progress.RewardStateChanged += (level, track, from, to) => log.Add($"{level} {track} {from}>{to}");
            return log;
        }
    }
}
