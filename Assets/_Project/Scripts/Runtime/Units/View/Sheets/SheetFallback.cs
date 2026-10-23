namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// Which animation to show when a sheet lacks the one asked for: a missing pose falls back to the nearest one it
    /// has, ending at Idle (then Walk), so a sheet with only a few strips still works.
    /// </summary>
    public static class SheetFallback
    {
        public static SheetAnimation Resolve(SheetAnimation wanted, ISheetTiming timing)
        {
            var animation = wanted;
            for (int guard = 0; guard < 8; guard++)
            {
                if (timing.FrameCount(animation) > 0)
                    return animation;
                animation = Next(animation);
            }
            return timing.FrameCount(SheetAnimation.Walk) > 0 ? SheetAnimation.Walk : SheetAnimation.Idle;
        }

        private static SheetAnimation Next(SheetAnimation animation) => animation switch
        {
            SheetAnimation.Release => SheetAnimation.Charging,
            SheetAnimation.Charging => SheetAnimation.Attack,
            SheetAnimation.Weak => SheetAnimation.Hurt,
            SheetAnimation.KnockedOut => SheetAnimation.Hurt,
            SheetAnimation.Hurt => SheetAnimation.Idle,
            SheetAnimation.Idle => SheetAnimation.Walk,
            _ => SheetAnimation.Idle,
        };
    }
}
