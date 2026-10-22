using System;
using UnityEngine;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// How each terrain type looks. Placeholder colors today; drop textures in later and they take over without
    /// code changes. Textures repeat once per tile on tops and once per world unit on sides.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Terrain Palette", fileName = "TerrainPalette")]
    public sealed class TerrainPalette : ScriptableObject
    {
        [Serializable]
        public sealed class Look
        {
            [SerializeField] private TerrainType _terrain;
            [SerializeField] private Color _topColor = Color.white;
            [SerializeField] private Color _sideColor = Color.gray;
            [SerializeField] private Texture2D _topTexture;
            [SerializeField] private Texture2D _sideTexture;

            public Look(TerrainType terrain, Color topColor, Color sideColor)
            {
                _terrain = terrain;
                _topColor = topColor;
                _sideColor = sideColor;
            }

            public TerrainType Terrain => _terrain;
            public Color TopColor => _topColor;
            public Color SideColor => _sideColor;
            public Texture2D TopTexture => _topTexture;
            public Texture2D SideTexture => _sideTexture;
        }

        [SerializeField] private Look[] _looks = Array.Empty<Look>();

        public Look Get(TerrainType terrain)
        {
            foreach (var look in _looks)
            {
                if (look.Terrain == terrain)
                    return look;
            }
            return new Look(terrain, Color.magenta, Color.magenta);
        }

        public void SetLooks(Look[] looks) => _looks = looks;
    }
}
