namespace FriendshipWarriors.Units.View.Walking
{
    /// <summary>How a unit gets from one tile to the next on screen.</summary>
    public enum WalkMotion
    {
        /// <summary>A step between tiles whose shared edge meets (flat ground, onto or along a slope): follows the surface.</summary>
        Walk,

        /// <summary>A step up or down a ledge: a short hop, as FFT units do.</summary>
        Hop,

        /// <summary>A leap across a gap or over a lower enemy: a high arc over the tiles in between.</summary>
        Leap,

        /// <summary>Pushed (knockback): a quick slide or drop, keeping the unit's facing and not playing walk frames.</summary>
        Pushed,
    }
}
