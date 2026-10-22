namespace FriendshipWarriors.Units.Equipment
{
    /// <summary>A unit's five equipment slots, as in FFT: two hands, head, body and an accessory.</summary>
    public enum EquipmentSlot
    {
        MainHand,

        /// <summary>A shield, or a second weapon once a dual-wield ability exists. Empty when the main hand is two-handed.</summary>
        OffHand,

        Head,
        Body,
        Accessory,
    }
}
