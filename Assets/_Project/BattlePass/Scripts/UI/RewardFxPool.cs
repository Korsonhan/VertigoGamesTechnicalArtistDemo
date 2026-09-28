using UnityEngine;

namespace VertigoDemo.BattlePass.UI
{
    /// <summary>
    /// Small round-robin pool of the reward particle bursts, so claiming never instantiates
    /// or garbage-collects anything. Bursts live on top of the canvas and are tinted per rarity.
    /// </summary>
    public sealed class RewardFxPool : MonoBehaviour
    {
        [SerializeField] ParticleSystem claimBurst;
        [SerializeField] ParticleSystem unlockBurst;
        [SerializeField, Min(1)] int instancesPerEffect = 3;

        ParticleSystem[] claimPool;
        ParticleSystem[] unlockPool;
        int nextClaim;
        int nextUnlock;

        void Awake()
        {
            claimPool = CreatePool(claimBurst);
            unlockPool = CreatePool(unlockBurst);
        }

        public void PlayClaim(Vector3 position, Color tint) => Play(claimPool, ref nextClaim, position, tint);

        public void PlayUnlock(Vector3 position, Color tint) => Play(unlockPool, ref nextUnlock, position, tint);

        ParticleSystem[] CreatePool(ParticleSystem prefab)
        {
            var pool = new ParticleSystem[instancesPerEffect];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(prefab, transform);
                pool[i].name = $"{prefab.name}_{i}";
            }
            return pool;
        }

        static void Play(ParticleSystem[] pool, ref int next, Vector3 position, Color tint)
        {
            if (pool == null)
                return;

            var system = pool[next];
            next = (next + 1) % pool.Length;

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.transform.position = position;
            var main = system.main;
            main.startColor = tint;
            system.Play(true);
        }
    }
}
