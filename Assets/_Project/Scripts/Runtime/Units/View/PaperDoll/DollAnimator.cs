namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// Picks the animation and frame to show. One-shot animations (attack, hurt) play once and return to idle;
    /// Charging shows the cast loop; a knocked-out unit shows its lying frame; a walking unit shows the walk frame its
    /// walk is on. Plain C# driven by a clock you pass in.
    /// </summary>
    public sealed class DollAnimator
    {
        private DollAnimation _oneShot = DollAnimation.Idle;
        private float _oneShotStart = -1f;

        public void Play(DollAnimation animation, float now)
        {
            _oneShot = animation;
            _oneShotStart = now;
        }

        /// <param name="walkFrame">The walk-cycle frame while the unit is walking, or -1.</param>
        public (DollAnimation animation, int frame) Current(float now, bool knockedOut, bool charging, int walkFrame = -1)
        {
            if (knockedOut)
                return (DollAnimation.KnockedOut, 0);

            if (_oneShotStart >= 0f)
            {
                float elapsed = now - _oneShotStart;
                int frame = (int)(elapsed / PaperDollLayout.FrameDuration(_oneShot));
                if (frame < PaperDollLayout.FrameCount(_oneShot))
                    return (_oneShot, frame);
                _oneShotStart = -1f;
            }

            if (walkFrame >= 0)
                return (DollAnimation.Walk, walkFrame % PaperDollLayout.FrameCount(DollAnimation.Walk));

            var loop = charging ? DollAnimation.Cast : DollAnimation.Idle;
            int loopFrame = (int)(now / PaperDollLayout.FrameDuration(loop)) % PaperDollLayout.FrameCount(loop);
            return (loop, loopFrame);
        }
    }
}
