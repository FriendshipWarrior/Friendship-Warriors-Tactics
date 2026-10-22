namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>FFT's ability categories, each with its own slot on a unit.</summary>
    public enum AbilityKind
    {
        /// <summary>Used from the Act menu: the current job's command set, or one other job's as the secondary.</summary>
        Action,

        /// <summary>Triggers in response to being targeted (FFT's reactions, e.g. Counter).</summary>
        Reaction,

        /// <summary>Always-on passive (FFT's support abilities).</summary>
        Support,

        /// <summary>Changes how the unit moves (FFT's movement abilities, e.g. Move +1).</summary>
        Movement,
    }
}
