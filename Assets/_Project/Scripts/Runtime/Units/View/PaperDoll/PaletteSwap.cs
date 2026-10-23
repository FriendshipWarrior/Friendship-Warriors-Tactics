using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    public static class PaletteSwap
    {
        /// <summary>RGB packed into an int (alpha ignored), the lookup key for swaps.</summary>
        public static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        /// <summary>Replaces opaque pixels whose colour is exactly a key; everything else is left untouched.</summary>
        public static void Apply(Color32[] pixels, Dictionary<int, Color32> map)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a == 0 || !map.TryGetValue(Pack(p), out var replacement))
                    continue;
                replacement.a = p.a;
                pixels[i] = replacement;
            }
        }
    }
}
