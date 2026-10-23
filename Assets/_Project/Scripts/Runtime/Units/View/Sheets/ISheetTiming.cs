namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>How many frames each animation has and how long each shows; 0 frames means the sheet lacks it.</summary>
    public interface ISheetTiming
    {
        int FrameCount(SheetAnimation animation);
        float FrameSeconds(SheetAnimation animation);
    }
}
