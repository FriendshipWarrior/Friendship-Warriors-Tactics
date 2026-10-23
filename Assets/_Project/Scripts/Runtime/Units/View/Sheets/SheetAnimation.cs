namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// The animations a whole-body unit sheet can hold. Serialized into <see cref="UnitSheet"/> assets, so append rather
    /// than reorder.
    /// </summary>
    public enum SheetAnimation
    {
        Idle = 0,
        Walk = 1,
        Attack = 2,
        /// <summary>Loops while the unit is Charging a spell.</summary>
        Charging = 3,
        /// <summary>One-shot when a spell is cast or released.</summary>
        Release = 4,
        Hurt = 5,
        /// <summary>Loops while the unit is at critical HP (FFT's kneeling pose).</summary>
        Weak = 6,
        KnockedOut = 7,
    }
}
