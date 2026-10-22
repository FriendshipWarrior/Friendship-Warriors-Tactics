namespace FriendshipWarriors.UI
{
    public readonly struct MenuItem
    {
        public readonly string Id;
        public readonly string Label;
        public readonly bool Enabled;

        public MenuItem(string id, string label, bool enabled = true)
        {
            Id = id;
            Label = label;
            Enabled = enabled;
        }
    }
}
