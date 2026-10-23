using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>A composed, recoloured unit sheet and a sprite for every frame.</summary>
    public sealed class DollSheet
    {
        private readonly Sprite[] _sprites;

        public DollSheet(Texture2D texture)
        {
            Texture = texture;
            _sprites = new Sprite[PaperDollLayout.Rows * PaperDollLayout.ColumnsPerPose * 2];
            for (int row = 0; row < PaperDollLayout.Rows; row++)
            {
                var animation = (DollAnimation)row;
                for (int frame = 0; frame < PaperDollLayout.FrameCount(animation); frame++)
                for (int pose = 0; pose < 2; pose++)
                {
                    var rect = PaperDollLayout.FrameRect(animation, frame, pose == 1);
                    var sprite = Sprite.Create(texture, new Rect(rect.x, rect.y, rect.width, rect.height),
                        new Vector2(0.5f, 0f), PaperDollLayout.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"{texture.name} {animation} {frame} {(pose == 1 ? "back" : "front")}";
                    sprite.hideFlags = HideFlags.DontSave;
                    _sprites[Index(animation, frame, pose == 1)] = sprite;
                }
            }
        }

        public Texture2D Texture { get; }

        public bool IsAlive => Texture != null;

        public Sprite Get(DollAnimation animation, int frame, bool back) =>
            _sprites[Index(animation, Mathf.Clamp(frame, 0, PaperDollLayout.FrameCount(animation) - 1), back)];

        /// <summary>Destroys the sprites and texture; the sheet is dead afterwards.</summary>
        public void Release()
        {
            foreach (var sprite in _sprites)
                DestroyAsset(sprite);
            DestroyAsset(Texture);
        }

        private static void DestroyAsset(Object asset)
        {
            if (asset == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(asset);
            else
                Object.DestroyImmediate(asset);
        }

        private static int Index(DollAnimation animation, int frame, bool back) =>
            ((int)animation * PaperDollLayout.ColumnsPerPose + frame) * 2 + (back ? 1 : 0);
    }
}
