using UnityEngine;

namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>
    /// An ability a job teaches. Units learn it by spending Proficiency earned in that job, then use it from the Act
    /// menu (actions) or equip it in its slot (reaction, support, movement). Effects beyond turn trades arrive with
    /// the combat system.
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

        public string DisplayName => _displayName;
        public string Description => _description;
        public AbilityKind Kind => _kind;
        public int LearnCost => _learnCost;
        public TurnTrade TurnTrade => _turnTrade;

        public void Define(string displayName, string description, AbilityKind kind, int learnCost, TurnTrade turnTrade = TurnTrade.None)
        {
            _displayName = displayName;
            _description = description;
            _kind = kind;
            _learnCost = learnCost;
            _turnTrade = turnTrade;
        }
    }
}
