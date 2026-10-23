using System;
using UnityEngine;

namespace FriendshipWarriors.Battle.Objectives
{
    /// <summary>
    /// What a battle asks of the player: a victory condition, optionally a target, and optionally Player-team units
    /// that must not be permanently lost. Units are referred to by their <see cref="Units.Unit.Key"/>, which the battle
    /// setup's roster assigns. Plain data, authored on a <see cref="BattleSetup"/>.
    /// </summary>
    [Serializable]
    public sealed class BattleObjective
    {
        [SerializeField] private VictoryCondition _victory = VictoryCondition.DefeatAllEnemies;

        [Tooltip("Key of the enemy to defeat, for Defeat Target.")]
        [SerializeField] private string _targetKey = "";

        [Tooltip("Keys of Player-team units that must not be permanently lost (KO'd and revived is fine).")]
        [SerializeField] private string[] _protectedKeys = Array.Empty<string>();

        public BattleObjective() { }

        public BattleObjective(VictoryCondition victory, string targetKey = "", params string[] protectedKeys)
        {
            _victory = victory;
            _targetKey = targetKey ?? "";
            _protectedKeys = protectedKeys ?? Array.Empty<string>();
        }

        /// <summary>FFT's standard battle: defeat every enemy, nobody to protect.</summary>
        public static BattleObjective Standard => new(VictoryCondition.DefeatAllEnemies);

        public VictoryCondition Victory => _victory;
        public string TargetKey => _targetKey;
        public string[] ProtectedKeys => _protectedKeys;

        public bool IsProtected(string key) => !string.IsNullOrEmpty(key) && Array.IndexOf(_protectedKeys, key) >= 0;
        public bool IsTarget(string key) =>
            _victory == VictoryCondition.DefeatTarget && !string.IsNullOrEmpty(key) && key == _targetKey;
    }
}
