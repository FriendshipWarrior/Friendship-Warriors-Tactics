using System;

namespace FriendshipWarriors.Audio
{
    /// <summary>
    /// Synthesizes short placeholder sounds per cue (a tone sweep with a fast attack and decay, plus noise for hits),
    /// so the game has audible feedback before real audio exists. Pure sample generation, deterministic per cue.
    /// </summary>
    public static class PlaceholderBlips
    {
        public const int SampleRate = 22050;

        /// <summary>Start and end frequency (Hz), length (s), how much noise, and loudness for a cue.</summary>
        public readonly struct Recipe
        {
            public readonly float StartHz, EndHz, Seconds, Noise, Gain;

            public Recipe(float startHz, float endHz, float seconds, float noise, float gain)
            {
                StartHz = startHz;
                EndHz = endHz;
                Seconds = seconds;
                Noise = noise;
                Gain = gain;
            }
        }

        public static Recipe RecipeFor(SoundCue cue) => cue switch
        {
            SoundCue.Hit => new Recipe(220f, 90f, 0.12f, 0.55f, 0.8f),
            SoundCue.CriticalHit => new Recipe(320f, 70f, 0.22f, 0.65f, 1f),
            SoundCue.Miss => new Recipe(900f, 1500f, 0.10f, 0.35f, 0.35f),
            SoundCue.Block => new Recipe(1300f, 1100f, 0.09f, 0.25f, 0.6f),
            SoundCue.Heal => new Recipe(520f, 1040f, 0.28f, 0f, 0.45f),
            SoundCue.CastStart => new Recipe(300f, 600f, 0.30f, 0.05f, 0.35f),
            SoundCue.SpellResolve => new Recipe(700f, 180f, 0.35f, 0.3f, 0.6f),
            SoundCue.KnockOut => new Recipe(260f, 60f, 0.45f, 0.2f, 0.7f),
            SoundCue.MenuMove => new Recipe(1200f, 1200f, 0.03f, 0f, 0.2f),
            SoundCue.MenuConfirm => new Recipe(880f, 1320f, 0.06f, 0f, 0.3f),
            SoundCue.MenuCancel => new Recipe(660f, 440f, 0.06f, 0f, 0.3f),
            SoundCue.TimedPressSuccess => new Recipe(990f, 1980f, 0.12f, 0f, 0.4f),
            SoundCue.TimedPressFail => new Recipe(200f, 160f, 0.12f, 0.1f, 0.35f),
            SoundCue.StatusGained => new Recipe(440f, 330f, 0.2f, 0.05f, 0.35f),
            SoundCue.Revive => new Recipe(392f, 1175f, 0.4f, 0f, 0.45f),
            SoundCue.DropAppears => new Recipe(1568f, 1760f, 0.25f, 0.02f, 0.3f),
            SoundCue.RodTaken => new Recipe(660f, 1980f, 0.45f, 0f, 0.45f),
            SoundCue.ChestOpened => new Recipe(180f, 520f, 0.22f, 0.3f, 0.5f),
            _ => new Recipe(0f, 0f, 0f, 0f, 0f),
        };

        /// <summary>Mono samples in -1…1 for the cue; empty for <see cref="SoundCue.None"/>.</summary>
        public static float[] Samples(SoundCue cue)
        {
            var recipe = RecipeFor(cue);
            int count = (int)(recipe.Seconds * SampleRate);
            var samples = new float[Math.Max(0, count)];
            var noise = new Random((int)cue * 7919 + 17);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float hz = recipe.StartHz + (recipe.EndHz - recipe.StartHz) * t;
                phase += 2 * Math.PI * hz / SampleRate;
                // Square-ish tone (a little retro), softened by mixing in the sine.
                float tone = (float)(0.6 * Math.Sin(phase) + 0.4 * Math.Sign(Math.Sin(phase)) * 0.5);
                float n = (float)(noise.NextDouble() * 2 - 1);
                float attack = Math.Min(1f, i / (SampleRate * 0.004f));
                float decay = (1f - t) * (1f - t);
                samples[i] = (tone * (1f - recipe.Noise) + n * recipe.Noise) * attack * decay * recipe.Gain;
            }
            return samples;
        }
    }
}
