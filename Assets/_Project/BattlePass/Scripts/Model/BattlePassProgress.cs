using System;

namespace VertigoDemo.BattlePass
{
    public enum RewardTrack { Free, Premium }

    public enum RewardState
    {
        /// <summary>Level not reached yet.</summary>
        Locked,
        /// <summary>Level reached, but the reward is on the premium track and the pass is not owned.</summary>
        PremiumLocked,
        /// <summary>Unlocked and waiting to be claimed.</summary>
        Claimable,
        /// <summary>Already collected.</summary>
        Claimed,
    }

    /// <summary>
    /// Where a reward sits on the road. Level 0 is the start of the road, before the first level, and
    /// can hold several rewards on each track; every other level holds one, at index 0.
    /// </summary>
    public readonly struct RewardSlot : IEquatable<RewardSlot>
    {
        public readonly int Level;
        public readonly RewardTrack Track;
        public readonly int Index;

        public RewardSlot(int level, RewardTrack track, int index = 0)
        {
            Level = level;
            Track = track;
            Index = index;
        }

        public bool Equals(RewardSlot other) => Level == other.Level && Track == other.Track && Index == other.Index;

        public override bool Equals(object obj) => obj is RewardSlot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Level, Track, Index);

        public override string ToString() => Index == 0 ? $"{Level} {Track}" : $"{Level} {Track} #{Index}";
    }

    /// <summary>
    /// A player's progress through one season. Plain C# with no Unity dependencies,
    /// so the rules can be unit tested and the views only react to its events.
    /// </summary>
    public sealed class BattlePassProgress
    {
        readonly bool[][] claimedFree;
        readonly bool[][] claimedPremium;

        public int LevelCount { get; }
        public int XpPerLevel { get; }

        /// <summary>Highest level reached; 0 at the start of the road, whose rewards are always within reach.</summary>
        public int Level { get; private set; }

        /// <summary>Experience collected towards <see cref="Level"/> + 1.</summary>
        public int Xp { get; private set; }

        public bool PremiumOwned { get; private set; }
        public bool IsMaxLevel => Level >= LevelCount;
        public float LevelFraction => IsMaxLevel ? 0f : (float)Xp / XpPerLevel;

        /// <summary>Raised for every reward whose state changes: where it is, previous state, new state.</summary>
        public event Action<RewardSlot, RewardState, RewardState> RewardStateChanged;

        /// <param name="startPremium">Premium rewards at level 0, which come with the pass itself.</param>
        /// <param name="startFree">Free rewards at level 0, ready from the start.</param>
        public BattlePassProgress(int levelCount, int xpPerLevel, int level = 0, int xp = 0, bool premiumOwned = false,
            int startPremium = 0, int startFree = 0)
        {
            if (levelCount <= 0) throw new ArgumentOutOfRangeException(nameof(levelCount));
            if (xpPerLevel <= 0) throw new ArgumentOutOfRangeException(nameof(xpPerLevel));
            if (startPremium < 0) throw new ArgumentOutOfRangeException(nameof(startPremium));
            if (startFree < 0) throw new ArgumentOutOfRangeException(nameof(startFree));

            LevelCount = levelCount;
            XpPerLevel = xpPerLevel;
            Level = Math.Clamp(level, 0, levelCount);
            Xp = IsMaxLevel ? 0 : Math.Clamp(xp, 0, xpPerLevel - 1);
            PremiumOwned = premiumOwned;
            claimedFree = NewClaimedFlags(levelCount, startFree);
            claimedPremium = NewClaimedFlags(levelCount, startPremium);
        }

        public int RewardCount(int level, RewardTrack track) => Claimed(track)[level].Length;

        public RewardState GetState(int level, RewardTrack track, int index = 0) => GetState(new RewardSlot(level, track, index));

        public RewardState GetState(RewardSlot slot)
        {
            if (slot.Level > Level)
                return RewardState.Locked;
            if (IsClaimed(slot))
                return RewardState.Claimed;
            if (slot.Track == RewardTrack.Premium && !PremiumOwned)
                return RewardState.PremiumLocked;
            return RewardState.Claimable;
        }

        public bool IsClaimed(RewardSlot slot) => Claimed(slot.Track)[slot.Level][slot.Index];

        /// <summary>Sets up an already claimed reward without raising events (initial placeholder state).</summary>
        public void MarkClaimed(int level, RewardTrack track, int index = 0) => Claimed(track)[level][index] = true;

        public bool TryClaim(int level, RewardTrack track, int index = 0) => TryClaim(new RewardSlot(level, track, index));

        public bool TryClaim(RewardSlot slot)
        {
            if (GetState(slot) != RewardState.Claimable)
                return false;

            Claimed(slot.Track)[slot.Level][slot.Index] = true;
            RewardStateChanged?.Invoke(slot, RewardState.Claimable, RewardState.Claimed);
            return true;
        }

        /// <summary>Adds experience and returns the number of levels gained.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0 || IsMaxLevel)
                return 0;

            int gained = 0;
            Xp += amount;
            while (Xp >= XpPerLevel && !IsMaxLevel)
            {
                Xp -= XpPerLevel;
                Level++;
                gained++;
                NotifyLevelReached(Level);
            }
            if (IsMaxLevel)
                Xp = 0;
            return gained;
        }

        /// <summary>Buys the next level outright; experience already collected carries over.</summary>
        public bool BuyLevel()
        {
            if (IsMaxLevel)
                return false;

            Level++;
            if (IsMaxLevel)
                Xp = 0;
            NotifyLevelReached(Level);
            return true;
        }

        /// <summary>Unlocks every reached premium reward, starting with those that come with the pass itself.</summary>
        public bool UnlockPremium()
        {
            if (PremiumOwned)
                return false;

            PremiumOwned = true;
            for (int level = 0; level <= Level; level++)
            {
                for (int index = 0; index < RewardCount(level, RewardTrack.Premium); index++)
                {
                    var slot = new RewardSlot(level, RewardTrack.Premium, index);
                    if (!IsClaimed(slot))
                        RewardStateChanged?.Invoke(slot, RewardState.PremiumLocked, RewardState.Claimable);
                }
            }
            return true;
        }

        void NotifyLevelReached(int level)
        {
            var free = new RewardSlot(level, RewardTrack.Free);
            var premium = new RewardSlot(level, RewardTrack.Premium);
            RewardStateChanged?.Invoke(free, RewardState.Locked, GetState(free));
            RewardStateChanged?.Invoke(premium, RewardState.Locked, GetState(premium));
        }

        static bool[][] NewClaimedFlags(int levelCount, int startRewards)
        {
            var flags = new bool[levelCount + 1][];
            flags[0] = new bool[startRewards];
            for (int level = 1; level <= levelCount; level++)
                flags[level] = new bool[1];
            return flags;
        }

        bool[][] Claimed(RewardTrack track) => track == RewardTrack.Free ? claimedFree : claimedPremium;
    }
}
