using UnityEngine;

namespace FriendshipWarriors.Feedback
{
    /// <summary>How one popup looks: its text, colour and size relative to a plain damage number.</summary>
    public readonly struct PopupStyle
    {
        public readonly string Text;
        public readonly Color32 Color;
        public readonly float Size;

        public PopupStyle(string text, Color32 color, float size = 1f)
        {
            Text = text;
            Color = color;
            Size = size;
        }
    }
}
