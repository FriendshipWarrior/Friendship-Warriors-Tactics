using UnityEngine;

namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>
    /// An ability a job teaches. Units learn it by spending Proficiency earned in that job, then use it from the Act
    /// menu (actions) or equip it in its slot (reaction, support, movement). Effects beyond turn trades arrive with
    /// the combat system. Action abilities describe their effect here (range, targets, amounts); passives describe
    /// their bonuses. Items are action abilities that also use up one of the team's stock.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Ability", fileName = "Ability")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Ability";
        [SerializeField, TextArea] private string _description;
        [SerializeField] private AbilityKind _kind = AbilityKind.Action;
        [Tooltip("Proficiency spent to learn it.")]
        [SerializeField, Min(0)] private int _learnCost = 100;
        [Tooltip("Support/movement only: lets the unit trade one part of its turn for the other.")]
        [SerializeField] private TurnTrade _turnTrade = TurnTrade.None;

        [Header("Action")]
        [SerializeField] private AbilityEffect _effect;
        [SerializeField] private AbilityTargeting _targeting = AbilityTargeting.Standing;
        [SerializeField, Min(0)] private int _minRange = 1;
        [SerializeField, Min(0)] private int _maxRange = 1;
        [Tooltip("Largest height difference, in h, the ability can reach.")]
        [SerializeField, Min(0)] private int _verticalReach = 3;
        [Tooltip("HP/MP restored, or P.Atk added. 0 with Restore All means to full.")]
        [SerializeField, Min(0)] private int _amount;
        [Tooltip("Damage: P.Atk × a random whole number from this range.")]
        [SerializeField] private Vector2Int _randomMultiplier = new(1, 1);
        [SerializeField] private bool _knockback;
        [Tooltip("Uses up one of the team's stock of this item; range comes from the user's item throwing.")]
        [SerializeField] private bool _isItem;
        [SerializeField, Min(0)] private int _mpCost;

        [Header("Passive")]
        [Tooltip("Movement abilities: extra Move.")]
        [SerializeField, Min(0)] private int _moveBonus;
        [Tooltip("Support abilities: extra Prof earned, in percent.")]
        [SerializeField, Min(0)] private int _proficiencyBonusPercent;
        [Tooltip("Lets the unit throw items at range instead of handing them to adjacent units.")]
        [SerializeField] private bool _throwsItems;

        public string DisplayName => _displayName;
        public string Description => _description;
        public AbilityKind Kind => _kind;
        public int LearnCost => _learnCost;
        public TurnTrade TurnTrade => _turnTrade;
        public AbilityEffect Effect => _effect;
        public AbilityTargeting Targeting => _targeting;
        public int MinRange => _minRange;
        public int MaxRange => _maxRange;
        public int VerticalReach => _verticalReach;
        public int Amount => _amount;
        public Vector2Int RandomMultiplier => _randomMultiplier;
        public bool Knockback => _knockback;
        public bool IsItem => _isItem;
        public int MpCost => _mpCost;
        public int MoveBonus => _moveBonus;
        public int ProficiencyBonusPercent => _proficiencyBonusPercent;
        public bool ThrowsItems => _throwsItems;

        /// <summary>Heals, buffs, items and revives (anything that isn't damage) earn support EXP.</summary>
        public bool IsSupportAction => _effect != AbilityEffect.Damage;

        public void Define(string displayName, string description, AbilityKind kind, int learnCost, TurnTrade turnTrade = TurnTrade.None)
        {
            _displayName = displayName;
            _description = description;
            _kind = kind;
            _learnCost = learnCost;
            _turnTrade = turnTrade;
        }

        /// <summary>Sets what an action ability does; used by tooling and tests.</summary>
        public void SetAction(AbilityEffect effect, AbilityTargeting targeting, int minRange, int maxRange, int verticalReach,
            int amount = 0, Vector2Int? randomMultiplier = null, bool knockback = false, bool isItem = false, int mpCost = 0)
        {
            _effect = effect;
            _targeting = targeting;
            _minRange = minRange;
            _maxRange = maxRange;
            _verticalReach = verticalReach;
            _amount = amount;
            _randomMultiplier = randomMultiplier ?? new Vector2Int(1, 1);
            _knockback = knockback;
            _isItem = isItem;
            _mpCost = mpCost;
        }

        /// <summary>Sets a passive's bonuses; used by tooling and tests.</summary>
        public void SetPassive(int moveBonus = 0, int proficiencyBonusPercent = 0, bool throwsItems = false)
        {
            _moveBonus = moveBonus;
            _proficiencyBonusPercent = proficiencyBonusPercent;
            _throwsItems = throwsItems;
        }
    }
}
