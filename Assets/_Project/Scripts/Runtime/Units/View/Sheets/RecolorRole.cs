namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>What a group of a sheet's colours is recoloured to, per unit. Serialized; append rather than reorder.</summary>
    public enum RecolorRole
    {
        /// <summary>Tinted with the job's colour, so jobs that share a sheet look different.</summary>
        JobOutfit = 0,
        /// <summary>Player blue or Enemy red.</summary>
        TeamAccent = 1,
        /// <summary>One of the sheet's hair colours, picked from the unit's name.</summary>
        Hair = 2,
    }
}
