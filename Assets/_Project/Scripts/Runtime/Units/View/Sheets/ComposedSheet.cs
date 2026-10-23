using System.Collections.Generic;
using FriendshipWarriors.Units.Equipment;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// A unit sheet recoloured for one palette and packed into one texture, with a sprite per frame. Each strip's
    /// pivot sits on the lowest opaque row of its frames (its feet; the lying body for knock-outs), centred
    /// horizontally. Weapon sprites share the attack strip's pivot point. Timing comes from the source sheet.
    /// </summary>
    public sealed class ComposedSheet : ISheetTiming
    {
        private readonly UnitSheet _source;
        private readonly Dictionary<int, Sprite> _sprites = new();
        private readonly Dictionary<(WeaponType, int, bool), Sprite> _weapons = new();
        private readonly List<Object> _owned = new();
        private readonly int _attackBaseline;

        public ComposedSheet(UnitSheet source, Dictionary<int, Color32> palette)
        {
            _source = source;
            var size = source.FrameSize;
            int maxFrames = 1;
            foreach (var strip in source.Strips)
                maxFrames = Mathf.Max(maxFrames, strip.FrameCount);

            int width = maxFrames * 2 * size.x, height = Mathf.Max(1, source.Strips.Count) * size.y;
            var pixels = new Color32[width * height];
            var baselines = new int[source.Strips.Count];
            for (int s = 0; s < source.Strips.Count; s++)
            {
                var strip = source.Strips[s];
                int lowest = size.y;
                for (int pose = 0; pose < 2; pose++)
                {
                    var texture = pose == 0 ? strip.Front : strip.Back ? strip.Back : strip.Front;
                    if (texture == null)
                        continue;
                    var src = texture.GetPixels32();
                    for (int f = 0; f < strip.FrameCount; f++)
                    {
                        int sx = strip.Origin.x + f * size.x;
                        int sy = texture.height - strip.Origin.y - size.y;
                        int dx = (pose * maxFrames + f) * size.x, dy = height - (s + 1) * size.y;
                        for (int y = 0; y < size.y; y++)
                        for (int x = 0; x < size.x; x++)
                        {
                            int tx = sx + x, ty = sy + y;
                            if (tx < 0 || ty < 0 || tx >= texture.width || ty >= texture.height)
                                continue;
                            var p = src[ty * texture.width + tx];
                            if (p.a == 0)
                                continue;
                            if (palette.TryGetValue(PaperDoll.PaletteSwap.Pack(p), out var swap))
                            {
                                swap.a = p.a;
                                p = swap;
                            }
                            pixels[(dy + y) * width + dx + x] = p;
                            lowest = Mathf.Min(lowest, y);
                        }
                    }
                }
                baselines[s] = lowest >= size.y ? 0 : lowest;
            }

            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"Sheet {source.name}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            Texture.SetPixels32(pixels);
            // Left readable (sheets are small) so previews and tests can check the recoloured pixels.
            Texture.Apply(false, false);
            _owned.Add(Texture);

            for (int s = 0; s < source.Strips.Count; s++)
            {
                var strip = source.Strips[s];
                if (strip.Animation == SheetAnimation.Attack)
                    _attackBaseline = baselines[s];
                for (int f = 0; f < strip.FrameCount; f++)
                for (int pose = 0; pose < 2; pose++)
                {
                    var rect = new Rect((pose * maxFrames + f) * size.x, height - (s + 1) * size.y, size.x, size.y);
                    var sprite = Sprite.Create(Texture, rect, new Vector2(0.5f, (float)baselines[s] / size.y), source.PixelsPerUnit,
                        0, SpriteMeshType.FullRect);
                    sprite.name = $"{source.name} {strip.Animation} {f} {(pose == 1 ? "back" : "front")}";
                    sprite.hideFlags = HideFlags.DontSave;
                    _owned.Add(sprite);
                    _sprites[Index(strip.Animation, f, pose == 1)] = sprite;
                }
            }
        }

        public Texture2D Texture { get; }

        public bool IsAlive => Texture != null;

        public int FrameCount(SheetAnimation animation) => _source.FrameCount(animation);

        public float FrameSeconds(SheetAnimation animation) => _source.FrameSeconds(animation);

        /// <summary>The frame's sprite (missing animations fall back; the frame is clamped), or null for an empty sheet.</summary>
        public Sprite Get(SheetAnimation animation, int frame, bool back)
        {
            var resolved = SheetFallback.Resolve(animation, this);
            int count = FrameCount(resolved);
            if (count == 0)
                return null;
            return _sprites.TryGetValue(Index(resolved, Mathf.Clamp(frame, 0, count - 1), back), out var sprite) ? sprite : null;
        }

        /// <summary>The weapon swing sprite for an attack frame, or null when the sheet has no art for that weapon.</summary>
        public Sprite Weapon(WeaponType type, int frame, bool back)
        {
            var entry = _source.Weapons != null ? _source.Weapons.For(type) : null;
            var texture = entry == null ? null : back && entry.Back ? entry.Back : entry.Front;
            if (texture == null)
                return null;
            var key = (type, frame, back);
            if (_weapons.TryGetValue(key, out var cached))
                return cached;

            var size = _source.Weapons.FrameSize;
            int columns = texture.width / size.x;
            Sprite sprite = null;
            if (frame >= 0 && frame < columns)
            {
                // The unit's frame sits centred in the bigger weapon frame, so the pivots line up.
                float offsetY = (size.y - _source.FrameSize.y) * 0.5f;
                var rect = new Rect(frame * size.x, texture.height - size.y, size.x, size.y);
                sprite = Sprite.Create(texture, rect, new Vector2(0.5f, (offsetY + _attackBaseline) / size.y), _source.PixelsPerUnit,
                    0, SpriteMeshType.FullRect);
                sprite.name = $"{texture.name} {frame}";
                sprite.hideFlags = HideFlags.DontSave;
                _owned.Add(sprite);
            }
            _weapons[key] = sprite;
            return sprite;
        }

        public void Release()
        {
            foreach (var asset in _owned)
            {
                if (asset == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(asset);
                else
                    Object.DestroyImmediate(asset);
            }
            _owned.Clear();
            _sprites.Clear();
            _weapons.Clear();
        }

        private static int Index(SheetAnimation animation, int frame, bool back) => ((int)animation * 64 + frame) * 2 + (back ? 1 : 0);
    }
}
