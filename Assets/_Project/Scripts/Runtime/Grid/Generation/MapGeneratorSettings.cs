using System;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.Validation;
using UnityEngine;

namespace FriendshipWarriors.Grid.Generation
{
    /// <summary>Tuning for <see cref="MapGenerator"/>. Make one asset per biome or difficulty as the game grows.</summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Map Generator Settings", fileName = "MapGeneratorSettings")]
    public sealed class MapGeneratorSettings : ScriptableObject
    {
        [Serializable]
        public sealed class PropChoice
        {
            [SerializeField] private PropDefinition _prop;
            [SerializeField, Min(0f)] private float _weight = 1f;

            public PropChoice(PropDefinition prop, float weight)
            {
                _prop = prop;
                _weight = weight;
            }

            public PropDefinition Prop => _prop;
            public float Weight => _weight;
        }

        [Header("Size (tiles)")]
        [SerializeField] private Vector2Int _widthRange = new(10, 14);
        [SerializeField] private Vector2Int _depthRange = new(10, 14);

        [Header("Shape")]
        [Tooltip("Ground level, in h.")]
        [SerializeField, Min(0)] private int _baseHeight = 1;
        [Tooltip("Tallest terrain allowed, in h.")]
        [SerializeField, Range(1, 15)] private int _maxHeight = 6;
        [SerializeField] private Vector2Int _hillCount = new(3, 6);
        [SerializeField] private Vector2 _hillRadius = new(2f, 4.5f);
        [Tooltip("Hill peak above ground, in h.")]
        [SerializeField] private Vector2 _hillAmplitude = new(1f, 3.5f);
        [Tooltip("Chance of a flat-topped plateau with cliff edges, FFT-style.")]
        [SerializeField, Range(0f, 1f)] private float _plateauChance = 0.5f;
        [Tooltip("Chance a 1h step becomes a ramp instead.")]
        [SerializeField, Range(0f, 1f)] private float _rampChance = 0.6f;

        [Header("Water and Gaps")]
        [SerializeField] private Vector2Int _pondCount = new(0, 2);
        [SerializeField] private Vector2Int _chasmCount = new(0, 1);

        [Header("Props")]
        [SerializeField] private PropChoice[] _props = Array.Empty<PropChoice>();
        [Tooltip("Share of eligible tiles that get a prop.")]
        [SerializeField, Range(0f, 0.3f)] private float _propDensity = 0.06f;

        [Header("Deployment")]
        [Tooltip("Tiles per side. FFT fields 5 units.")]
        [SerializeField, Range(1, 12)] private int _deploymentSize = 5;

        [Header("Validation")]
        [SerializeField, Range(1, 7)] private int _referenceJump = MapValidator.DefaultReferenceJump;
        [SerializeField, Range(1, 100)] private int _maxAttempts = 25;

        public Vector2Int WidthRange => _widthRange;
        public Vector2Int DepthRange => _depthRange;
        public int BaseHeight => _baseHeight;
        public int MaxHeight => _maxHeight;
        public Vector2Int HillCount => _hillCount;
        public Vector2 HillRadius => _hillRadius;
        public Vector2 HillAmplitude => _hillAmplitude;
        public float PlateauChance => _plateauChance;
        public float RampChance => _rampChance;
        public Vector2Int PondCount => _pondCount;
        public Vector2Int ChasmCount => _chasmCount;
        public PropChoice[] Props => _props;
        public float PropDensity => _propDensity;
        public int DeploymentSize => _deploymentSize;
        public int ReferenceJump => _referenceJump;
        public int MaxAttempts => _maxAttempts;

        public void SetProps(PropChoice[] props) => _props = props;
    }
}
