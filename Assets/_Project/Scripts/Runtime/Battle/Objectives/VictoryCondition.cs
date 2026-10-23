namespace FriendshipWarriors.Battle.Objectives
{
    /// <summary>How the player wins. Values are serialized into setup assets, so append rather than reorder.</summary>
    public enum VictoryCondition
    {
        /// <summary>Knock out every enemy (FFT's standard condition; quick play always uses it).</summary>
        DefeatAllEnemies = 0,

        /// <summary>Knock out one specific enemy; done the moment it is knocked out (user decision).</summary>
        DefeatTarget = 1,
    }
}
