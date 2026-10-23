using System;
using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>
    /// The drop rules as an asset (<c>Resources/Battle/DropTable</c>), so they can be tuned without code: rod vs chest
    /// odds, how rods teach, and what monsters' chests hold by level. The battle loads it at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Drop Table", fileName = "DropTable")]
    public sealed class DropTable : ScriptableObject
    {
        public const string ResourcePath = "Battle/DropTable";

        [Tooltip("Percent chance a lost unit leaves a steel rod rather than a treasure chest (FFT's odds are unknown).")]
        [SerializeField, Range(0, 100)] private int _rodChance = 50;
        [Tooltip("Percent chance each of the fallen unit's abilities is learned from its rod. 100 teaches them all.")]
        [SerializeField, Range(0, 100)] private int _inheritChance = 100;
        [Tooltip("FFT: rods only teach abilities from jobs the picker has unlocked.")]
        [SerializeField] private bool _requireUnlockedJob = true;
        [Tooltip("What a monster's chest holds: the highest row its level reaches.")]
        [SerializeField] private MonsterLootTier[] _monsterLoot = Array.Empty<MonsterLootTier>();

        public DropSettings Settings => new(_rodChance, _inheritChance, _requireUnlockedJob);
        public IReadOnlyList<MonsterLootTier> MonsterLoot => _monsterLoot;

        public void Define(DropSettings settings, MonsterLootTier[] monsterLoot)
        {
            _rodChance = settings.RodChance;
            _inheritChance = settings.InheritChance;
            _requireUnlockedJob = settings.RequireUnlockedJob;
            _monsterLoot = monsterLoot ?? Array.Empty<MonsterLootTier>();
        }
    }
}
