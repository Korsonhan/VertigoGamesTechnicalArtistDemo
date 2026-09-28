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
        Claimed,
    }

    /// <summary>
    /// A player's progress through one season. Plain C# with no Unity dependencies,
    /// so the rules can be unit tested and the views only react to its events.
    /// </summary>
    public sealed class BattlePassProgress
    {
        readonly bool[] claimedFree;
        readonly bool[] claimedPremium;

        public int LevelCount { get; }
        public int XpPerLevel { get; }

        /// <summary>Highest level reached; 0 before the first level.</summary>
        public int Level { get; private set; }

        /// <summary>Experience collected towards <see cref="Level"/> + 1.</summary>
        public int Xp { get; private set; }

        public bool PremiumOwned { get; private set; }
        public bool IsMaxLevel => Level >= LevelCount;
        public float LevelFraction => IsMaxLevel ? 0f : (float)Xp / XpPerLevel;

        /// <summary>Raised for every reward whose state changes: level, track, previous state, new state.</summary>
        public event Action<int, RewardTrack, RewardState, RewardState> RewardStateChanged;

        public BattlePassProgress(int levelCount, int xpPerLevel, int level = 0, int xp = 0, bool premiumOwned = false)
        {
            if (levelCount <= 0) throw new ArgumentOutOfRangeException(nameof(levelCount));
            if (xpPerLevel <= 0) throw new ArgumentOutOfRangeException(nameof(xpPerLevel));

            LevelCount = levelCount;
            XpPerLevel = xpPerLevel;
            Level = Math.Clamp(level, 0, levelCount);
            Xp = IsMaxLevel ? 0 : Math.Clamp(xp, 0, xpPerLevel - 1);
            PremiumOwned = premiumOwned;
            claimedFree = new bool[levelCount];
            claimedPremium = new bool[levelCount];
        }

        public RewardState GetState(int level, RewardTrack track)
        {
            if (level > Level)
                return RewardState.Locked;
            if (IsClaimed(level, track))
                return RewardState.Claimed;
            if (track == RewardTrack.Premium && !PremiumOwned)
                return RewardState.PremiumLocked;
            return RewardState.Claimable;
        }

        public bool IsClaimed(int level, RewardTrack track) => Claimed(track)[level - 1];

        /// <summary>Sets up an already claimed reward without raising events (initial placeholder state).</summary>
        public void MarkClaimed(int level, RewardTrack track) => Claimed(track)[level - 1] = true;

        public bool TryClaim(int level, RewardTrack track)
        {
            if (GetState(level, track) != RewardState.Claimable)
                return false;

            Claimed(track)[level - 1] = true;
            RewardStateChanged?.Invoke(level, track, RewardState.Claimable, RewardState.Claimed);
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

        public bool UnlockPremium()
        {
            if (PremiumOwned)
                return false;

            PremiumOwned = true;
            for (int level = 1; level <= Level; level++)
            {
                if (!IsClaimed(level, RewardTrack.Premium))
                    RewardStateChanged?.Invoke(level, RewardTrack.Premium, RewardState.PremiumLocked, RewardState.Claimable);
            }
            return true;
        }

        void NotifyLevelReached(int level)
        {
            RewardStateChanged?.Invoke(level, RewardTrack.Free, RewardState.Locked, GetState(level, RewardTrack.Free));
            RewardStateChanged?.Invoke(level, RewardTrack.Premium, RewardState.Locked, GetState(level, RewardTrack.Premium));
        }

        bool[] Claimed(RewardTrack track) => track == RewardTrack.Free ? claimedFree : claimedPremium;
    }
}
