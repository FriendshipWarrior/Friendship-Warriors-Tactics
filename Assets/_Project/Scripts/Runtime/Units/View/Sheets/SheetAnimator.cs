namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// Picks the animation and frame of a whole-body sheet, like <see cref="PaperDoll.DollAnimator"/> does for paper
    /// dolls: one-shots (attack, release, hurt) play once; a knocked-out unit shows its lying frame; a walking unit
    /// shows its walk frame; Charging loops the charging pose; critical HP loops the weak pose; otherwise idle.
    /// Animations the sheet lacks fall back through <see cref="SheetFallback"/>. Plain C# driven by a clock you pass in.
    /// </summary>
    public sealed class SheetAnimator
    {
        private SheetAnimation _oneShot = SheetAnimation.Idle;
        private float _oneShotStart = -1f;

        public void Play(SheetAnimation animation, float now)
        {
            _oneShot = animation;
            _oneShotStart = now;
        }

        /// <param name="walkFrame">The walk-cycle frame while the unit is walking, or -1.</param>
        public (SheetAnimation animation, int frame) Current(float now, ISheetTiming timing, bool knockedOut, bool charging,
            bool weak, int walkFrame = -1)
        {
            if (knockedOut)
                return (SheetFallback.Resolve(SheetAnimation.KnockedOut, timing), 0);

            if (_oneShotStart >= 0f)
            {
                var shot = SheetFallback.Resolve(_oneShot, timing);
                float seconds = timing.FrameSeconds(shot);
                int frame = seconds > 0f ? (int)((now - _oneShotStart) / seconds) : int.MaxValue;
                if (frame < timing.FrameCount(shot))
                    return (shot, frame);
                _oneShotStart = -1f;
            }

            if (walkFrame >= 0)
            {
                var walk = SheetFallback.Resolve(SheetAnimation.Walk, timing);
                return (walk, walkFrame % timing.FrameCount(walk));
            }

            var loop = SheetFallback.Resolve(charging ? SheetAnimation.Charging : weak ? SheetAnimation.Weak : SheetAnimation.Idle, timing);
            float loopSeconds = timing.FrameSeconds(loop);
            int count = timing.FrameCount(loop);
            int loopFrame = loopSeconds > 0f ? (int)(now / loopSeconds) % count : 0;
            return (loop, loopFrame);
        }
    }
}
