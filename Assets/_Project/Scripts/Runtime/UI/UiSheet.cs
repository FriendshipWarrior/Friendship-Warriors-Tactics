using System;
using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// A strip of equal frames in one texture (the GUI pack's "-Sheet" files: panels, button states, cursor
    /// animation frames), with the 9-slice border every frame shares. Frames run left to right, then top to bottom.
    /// </summary>
    [Serializable]
    public sealed class UiSheet
    {
        [SerializeField] private Texture2D _texture;
        [SerializeField, Min(1)] private int _frameWidth = 1;
        [SerializeField, Min(1)] private int _frameHeight = 1;
        [Tooltip("9-slice border in pixels: left, bottom, right, top (Unity's sprite border order). Zero for simple sprites.")]
        [SerializeField] private Vector4 _border;

        public UiSheet(Texture2D texture, int frameWidth, int frameHeight, Vector4 border = default)
        {
            _texture = texture;
            _frameWidth = frameWidth;
            _frameHeight = frameHeight;
            _border = border;
        }

        public Texture2D Texture => _texture;
        public int FrameWidth => _frameWidth;
        public int FrameHeight => _frameHeight;
        public Vector4 Border => _border;
        public int FrameCount => _texture == null ? 0 : FrameCountFor(_texture.width, _texture.height, _frameWidth, _frameHeight);

        public static int FrameCountFor(int textureWidth, int textureHeight, int frameWidth, int frameHeight) =>
            frameWidth <= 0 || frameHeight <= 0 ? 0 : (textureWidth / frameWidth) * (textureHeight / frameHeight);

        /// <summary>The pixel rect of frame <paramref name="index"/> in Unity texture space (origin bottom-left).</summary>
        public static RectInt FrameRect(int textureWidth, int textureHeight, int frameWidth, int frameHeight, int index)
        {
            int columns = Mathf.Max(1, textureWidth / frameWidth);
            int rows = Mathf.Max(1, textureHeight / frameHeight);
            index = Mathf.Clamp(index, 0, columns * rows - 1);
            int column = index % columns;
            int rowFromTop = index / columns;
            return new RectInt(column * frameWidth, textureHeight - (rowFromTop + 1) * frameHeight, frameWidth, frameHeight);
        }

        public RectInt FrameRect(int index) =>
            FrameRect(_texture.width, _texture.height, _frameWidth, _frameHeight, index);
    }
}
