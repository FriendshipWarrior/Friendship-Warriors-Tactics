using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// How the AI aims a charged spell (our weights; FFT's AI logic isn't public): single-target spells follow the unit
    /// they're meant for, so a target can't simply step away, while area spells stay on the tile chosen for the cluster
    /// they cover (following one unit would drag the area off the others).
    /// </summary>
    public static class AiSpellLock
    {
        public static SpellLock For(AbilityDefinition spell, Unit onTile)
        {
            if (spell == null || spell.ChargeSpeed <= 0 || onTile == null)
                return SpellLock.Tile;
            return spell.AreaRadius <= 0 ? SpellLock.Unit : SpellLock.Tile;
        }
    }
}
