using UnityEngine;

namespace FriendshipWarriors.Grid.Props
{
    /// <summary>
    /// A kind of map decoration (tree, rock, crate). Rule-relevant data lives here; visuals are a placeholder shape
    /// until real art exists, at which point <see cref="Prefab"/> replaces it.
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

        public string DisplayName => _displayName;
        public bool BlocksMovement => _blocksMovement;
        public int Height => _height;
        public PropShape Shape => _shape;
        public Color Color => _color;
        public float Footprint => _footprint;
        public GameObject Prefab => _prefab;
    }
}
