using System;
using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// Whole-body unit art: animation strips drawn front ¾ and back ¾, both facing screen-right (mirrored for the other
    /// two views, like paper dolls). A job that references a sheet shows it instead of its paper doll; without one the
    /// paper doll is used, so switching art is data only. Colour groups are recoloured per unit (job tint, team colour,
    /// hair) by exact RGB, and the result is cached by <see cref="SheetComposer"/>.
    /// Strips read frames left to right from a top-left pixel origin (as an image editor shows it); front and back can
    /// come from different textures, which must be readable.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Unit Sheet", fileName = "UnitSheet")]
    public sealed class UnitSheet : ScriptableObject, ISheetTiming
    {
        [Serializable]
        public sealed class Strip
        {
            [SerializeField] private SheetAnimation _animation;
            [SerializeField] private Texture2D _front;
            [SerializeField] private Texture2D _back;
            [Tooltip("Top-left pixel of the first frame in both textures (image coordinates, origin top-left).")]
            [SerializeField] private Vector2Int _origin;
            [SerializeField, Min(1)] private int _frameCount = 1;
            [SerializeField, Min(0.01f)] private float _frameSeconds = 0.2f;

            public Strip(SheetAnimation animation, Texture2D front, Texture2D back, Vector2Int origin, int frameCount, float frameSeconds)
            {
                _animation = animation;
                _front = front;
                _back = back;
                _origin = origin;
                _frameCount = frameCount;
                _frameSeconds = frameSeconds;
            }

            public SheetAnimation Animation => _animation;
            public Texture2D Front => _front;
            public Texture2D Back => _back;
            public Vector2Int Origin => _origin;
            public int FrameCount => _frameCount;
            public float FrameSeconds => _frameSeconds;
        }

        [Serializable]
        public sealed class RecolorGroup
        {
            [SerializeField] private RecolorRole _role;
            [Tooltip("The exact colours to recolour, ideally one shading ramp.")]
            [SerializeField] private Color32[] _colors = Array.Empty<Color32>();

            public RecolorGroup(RecolorRole role, Color32[] colors)
            {
                _role = role;
                _colors = colors;
            }

            public RecolorRole Role => _role;
            public IReadOnlyList<Color32> Colors => _colors;
        }

        [SerializeField] private Vector2Int _frameSize = new(32, 32);
        [Tooltip("Art pixels per world unit (before UnitView.SpriteScale). Lower shows the art bigger.")]
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 32f;
        [SerializeField] private Strip[] _strips = Array.Empty<Strip>();

        [Header("Recolouring")]
        [SerializeField] private RecolorGroup[] _recolors = Array.Empty<RecolorGroup>();
        [SerializeField] private Color _playerTint = new(0.31f, 0.57f, 0.98f);
        [SerializeField] private Color _enemyTint = new(0.89f, 0.27f, 0.26f);
        [Tooltip("Hair colours units pick from by name; empty keeps the painted hair.")]
        [SerializeField] private Color[] _hairColors = Array.Empty<Color>();

        [Header("Weapons")]
        [Tooltip("Weapon strips played over the attack animation, by the unit's weapon type.")]
        [SerializeField] private WeaponArt _weapons;

        public Vector2Int FrameSize => _frameSize;
        public float PixelsPerUnit => _pixelsPerUnit;
        public IReadOnlyList<Strip> Strips => _strips;
        public IReadOnlyList<RecolorGroup> Recolors => _recolors;
        public Color PlayerTint => _playerTint;
        public Color EnemyTint => _enemyTint;
        public IReadOnlyList<Color> HairColors => _hairColors;
        public WeaponArt Weapons => _weapons;

        public Strip StripFor(SheetAnimation animation)
        {
            foreach (var strip in _strips)
            {
                if (strip != null && strip.Animation == animation)
                    return strip;
            }
            return null;
        }

        public int FrameCount(SheetAnimation animation) => StripFor(animation)?.FrameCount ?? 0;

        public float FrameSeconds(SheetAnimation animation) => StripFor(animation)?.FrameSeconds ?? 0f;

        public void Define(Vector2Int frameSize, float pixelsPerUnit, Strip[] strips, RecolorGroup[] recolors, Color[] hairColors,
            WeaponArt weapons)
        {
            _frameSize = frameSize;
            _pixelsPerUnit = pixelsPerUnit;
            _strips = strips;
            _recolors = recolors;
            _hairColors = hairColors;
            _weapons = weapons;
        }
    }
}
