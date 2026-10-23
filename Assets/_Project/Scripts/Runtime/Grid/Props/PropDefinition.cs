using UnityEngine;

namespace FriendshipWarriors.Grid.Props
{
    /// <summary>
    /// A kind of map decoration (tree, rock, crate). Rule-relevant data lives here. It shows as, in order: its
    /// <see cref="Prefab"/>, an upright sprite cut from <see cref="SpriteTexture"/> that turns to face the camera (like
    /// units), or a placeholder shape.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Prop", fileName = "Prop")]
    public sealed class PropDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Prop";
        [SerializeField] private bool _blocksMovement = true;

        [Tooltip("Height above the tile surface, in half-h steps.")]
        [SerializeField, Min(0)] private int _height = 2;

        [Header("Placeholder Look")]
        [SerializeField] private PropShape _shape = PropShape.Box;
        [SerializeField] private Color _color = Color.gray;
        [SerializeField, Range(0.1f, 1f)] private float _footprint = 0.6f;

        [Tooltip("Replaces the placeholder shape when set.")]
        [SerializeField] private GameObject _prefab;

        [Header("Sprite Look")]
        [Tooltip("Sheet to cut the prop's sprite from (point-filtered pixel art). Replaces the placeholder shape when set.")]
        [SerializeField] private Texture2D _spriteTexture;
        [Tooltip("The sprite's rectangle in the texture, in image coordinates (origin top-left).")]
        [SerializeField] private RectInt _spriteRect;
        [Tooltip("Art pixels per world unit. The bottom-centre of the rectangle stands on the tile.")]
        [SerializeField, Min(1f)] private float _spritePixelsPerUnit = 32f;

        [System.NonSerialized] private Sprite _sprite;

        public string DisplayName => _displayName;
        public bool BlocksMovement => _blocksMovement;
        public int Height => _height;
        public PropShape Shape => _shape;
        public Color Color => _color;
        public float Footprint => _footprint;
        public GameObject Prefab => _prefab;
        public Texture2D SpriteTexture => _spriteTexture;
        public RectInt SpriteRect => _spriteRect;
        public float SpritePixelsPerUnit => _spritePixelsPerUnit;

        /// <summary>The prop's sprite, cut on first use, or null when it has no sprite look.</summary>
        public Sprite Sprite
        {
            get
            {
                if (_sprite != null || _spriteTexture == null || _spriteRect.width <= 0 || _spriteRect.height <= 0)
                    return _sprite;
                var rect = new Rect(_spriteRect.x, _spriteTexture.height - _spriteRect.y - _spriteRect.height, _spriteRect.width,
                    _spriteRect.height);
                _sprite = Sprite.Create(_spriteTexture, rect, new Vector2(0.5f, 0f), _spritePixelsPerUnit, 0, SpriteMeshType.FullRect);
                _sprite.name = _displayName;
                _sprite.hideFlags = HideFlags.DontSave;
                return _sprite;
            }
        }

        /// <summary>Sets the sprite look; used by art tooling.</summary>
        public void SetSprite(Texture2D texture, RectInt rect, float pixelsPerUnit)
        {
            _spriteTexture = texture;
            _spriteRect = rect;
            _spritePixelsPerUnit = pixelsPerUnit;
            _sprite = null;
        }
    }
}
