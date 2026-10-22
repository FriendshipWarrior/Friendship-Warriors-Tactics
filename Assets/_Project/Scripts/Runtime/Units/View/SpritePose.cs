namespace FriendshipWarriors.Units.View
{
    /// <summary>Which drawn pose to show and whether to mirror it.</summary>
    public readonly struct SpritePose
    {
        public readonly bool ShowsFront;
        public readonly bool FlipX;

        public SpritePose(bool showsFront, bool flipX)
        {
            ShowsFront = showsFront;
            FlipX = flipX;
        }

        public override string ToString() => $"{(ShowsFront ? "front" : "back")}{(FlipX ? " flipped" : "")}";
    }
}
