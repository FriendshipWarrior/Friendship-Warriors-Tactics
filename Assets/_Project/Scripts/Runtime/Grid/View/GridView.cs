using System.Collections.Generic;
using FriendshipWarriors.Grid.Props;
using UnityEngine;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// Presents a <see cref="BattleMapData"/>: one combined battlefield mesh, placeholder props, and translucent
    /// overlays for highlighted tiles. Holds no rules; the <see cref="Grid"/> it exposes is the source of truth.
    /// Builds itself in Play mode; editor tools (the map editor preview) call <see cref="Configure"/> and
    /// <see cref="Build"/> directly and <see cref="Release"/> when done.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] private BattleMapData _map;
        [SerializeField] private TerrainPalette _palette;

        [Tooltip("Opaque lit material each terrain look is cloned from.")]
        [SerializeField] private Material _terrainMaterial;

        [Tooltip("Transparent unlit material for tile highlights; tinted per tile.")]
        [SerializeField] private Material _highlightMaterial;

        [Tooltip("How far the battlefield extends below height 0.")]
        [SerializeField, Min(0.1f)] private float _baseDepth = 1f;

        [Header("Highlight Colors")]
        [SerializeField] private Color _moveRange = new(0.25f, 0.55f, 1.00f, 0.55f);
        [SerializeField] private Color _path = new(1.00f, 0.85f, 0.25f, 0.65f);
        [SerializeField] private Color _hover = new(1.00f, 1.00f, 1.00f, 0.60f);
        [SerializeField] private Color _selected = new(1.00f, 0.35f, 0.25f, 0.65f);
        [SerializeField] private Color _playerDeployment = new(0.30f, 0.90f, 0.40f, 0.50f);
        [SerializeField] private Color _enemyDeployment = new(0.95f, 0.30f, 0.60f, 0.50f);

        private readonly Dictionary<GridCoord, MeshRenderer> _overlays = new();
        private readonly List<Material> _ownedMaterials = new();
        private readonly Vector3[] _corners = new Vector3[4];
        private MaterialPropertyBlock _block;
        private Texture2D _placeholderTopTexture;
        private Mesh _terrainMesh;
        private MeshCollider _collider;
        private Transform _propRoot;
        private Transform _overlayRoot;

        public BattleGrid Grid { get; private set; }
        public BattleMapData Map => _map;

        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        private void Awake() => Build();

        private void OnDestroy() => Release();

        /// <summary>Points the view at a map and its looks, for tools that create views from code.</summary>
        public void Configure(BattleMapData map, TerrainPalette palette, Material terrainMaterial, Material highlightMaterial)
        {
            _map = map;
            _palette = palette;
            _terrainMaterial = terrainMaterial;
            _highlightMaterial = highlightMaterial;
        }

        /// <summary>(Re)builds everything from the map, freeing whatever a previous build generated.</summary>
        public void Build()
        {
            Release();
            Grid = _map.CreateGrid();
            BuildTerrain();
            BuildProps();
            _overlayRoot = CreateChild("Highlights");
        }

        /// <summary>Destroys generated meshes, materials, textures and child objects.</summary>
        public void Release()
        {
            foreach (var overlay in _overlays.Values)
            {
                if (overlay != null)
                    DestroySafe(overlay.GetComponent<MeshFilter>().sharedMesh);
            }
            _overlays.Clear();
            if (_overlayRoot != null)
                DestroySafe(_overlayRoot.gameObject);
            if (_propRoot != null)
                DestroySafe(_propRoot.gameObject);
            foreach (var material in _ownedMaterials)
                DestroySafe(material);
            _ownedMaterials.Clear();
            if (_terrainMesh != null)
                DestroySafe(_terrainMesh);
            if (_placeholderTopTexture != null)
                DestroySafe(_placeholderTopTexture);
        }

        /// <summary>World position a unit stands at on this tile.</summary>
        public Vector3 SurfaceCenter(GridCoord coord) => transform.TransformPoint(GridMetrics.SurfaceCenter(coord, Grid[coord]));

        /// <summary>Finds the tile under a ray, ignoring props and units. Hitting a cliff face picks the tile above it.</summary>
        public bool TryPickTile(Ray ray, out GridCoord coord)
        {
            coord = default;
            if (!_collider.Raycast(ray, out var hit, 1000f))
                return false;

            // Step slightly into the surface so a hit on a wall resolves to the tile that owns the wall.
            var local = transform.InverseTransformPoint(hit.point - hit.normal * 0.01f);
            coord = GridMetrics.WorldToCoord(local);
            return Grid.TryGetTile(coord, out var tile) && tile.Terrain != TerrainType.Void;
        }

        public void SetHighlight(GridCoord coord, TileHighlight highlight)
        {
            if (highlight == TileHighlight.None)
            {
                if (_overlays.TryGetValue(coord, out var existing))
                    existing.enabled = false;
                return;
            }

            if (!Grid.TryGetTile(coord, out var tile) || tile.Terrain == TerrainType.Void)
                return;

            var overlay = GetOrCreateOverlay(coord, tile);
            overlay.enabled = true;
            Block.SetColor(BaseColorId, HighlightColor(highlight));
            overlay.SetPropertyBlock(Block);
        }

        public void ClearHighlights()
        {
            foreach (var overlay in _overlays.Values)
                overlay.enabled = false;
        }

        private void BuildTerrain()
        {
            var meshFilter = GetOrAdd<MeshFilter>();
            var meshRenderer = GetOrAdd<MeshRenderer>();
            _collider = GetOrAdd<MeshCollider>();

            _terrainMesh = BattlefieldMeshBuilder.Build(Grid, _baseDepth);
            _terrainMesh.hideFlags = HideFlags.DontSave;
            meshFilter.sharedMesh = _terrainMesh;
            _collider.sharedMesh = _terrainMesh;

            var materials = new Material[BattlefieldMeshBuilder.SubmeshCount];
            for (int t = 0; t < BattlefieldMeshBuilder.TerrainCount; t++)
            {
                var look = _palette.Get((TerrainType)t);
                materials[BattlefieldMeshBuilder.TopSubmesh((TerrainType)t)] = CreateMaterial($"{look.Terrain} Top", look.TopColor, look.TopTexture ? look.TopTexture : PlaceholderTopTexture());
                materials[BattlefieldMeshBuilder.SideSubmesh((TerrainType)t)] = CreateMaterial($"{look.Terrain} Side", look.SideColor, look.SideTexture);
            }
            meshRenderer.sharedMaterials = materials;
        }

        private void BuildProps()
        {
            _propRoot = CreateChild("Props");

            foreach (var placement in _map.Props)
            {
                var prop = placement.Prop;
                if (prop == null || !Grid.Contains(placement.Coord))
                    continue;

                var surface = GridMetrics.SurfaceCenter(placement.Coord, Grid[placement.Coord]);
                GameObject instance;
                if (prop.Prefab != null)
                {
                    instance = Instantiate(prop.Prefab, _propRoot);
                    instance.transform.localPosition = surface;
                }
                else
                {
                    instance = GameObject.CreatePrimitive(prop.Shape == PropShape.Pillar ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    DestroySafe(instance.GetComponent<Collider>());
                    instance.transform.SetParent(_propRoot, false);
                    float height = Mathf.Max(prop.Height, 1) * GridMetrics.StepHeight;
                    // Unity's cylinder is 2 units tall; the cube is 1.
                    float yScale = prop.Shape == PropShape.Pillar ? height * 0.5f : height;
                    instance.transform.localScale = new Vector3(prop.Footprint, yScale, prop.Footprint);
                    instance.transform.localPosition = surface + Vector3.up * (height * 0.5f);
                    instance.GetComponent<Renderer>().sharedMaterial = CreateMaterial(prop.DisplayName, prop.Color, null);
                }
                instance.name = $"{prop.DisplayName} {placement.Coord}";
            }
        }

        private MeshRenderer GetOrCreateOverlay(GridCoord coord, in Tile tile)
        {
            if (_overlays.TryGetValue(coord, out var overlay))
                return overlay;

            BattlefieldMeshBuilder.TopCorners(coord, tile, _corners, lift: 0.01f);
            var mesh = new Mesh { name = $"Highlight {coord}", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(_corners);
            mesh.SetTriangles(BattlefieldMeshBuilder.TopTriangles(tile.Slope), 0);
            mesh.RecalculateNormals();

            var go = new GameObject($"Highlight {coord}");
            go.transform.SetParent(_overlayRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            overlay = go.AddComponent<MeshRenderer>();
            overlay.sharedMaterial = _highlightMaterial;
            overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            _overlays.Add(coord, overlay);
            return overlay;
        }

        private Material CreateMaterial(string materialName, Color color, Texture texture)
        {
            var material = new Material(_terrainMaterial) { name = materialName, hideFlags = HideFlags.DontSave };
            material.SetColor(BaseColorId, color);
            if (texture != null)
                material.SetTexture(BaseMapId, texture);
            _ownedMaterials.Add(material);
            return material;
        }

        /// <summary>White with a soft dark border, so tile edges read until terrain has real textures.</summary>
        private Texture2D PlaceholderTopTexture()
        {
            if (_placeholderTopTexture != null)
                return _placeholderTopTexture;

            const int size = 32;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                byte value = edge == 0 ? (byte)150 : edge == 1 ? (byte)215 : (byte)255;
                pixels[y * size + x] = new Color32(value, value, value, 255);
            }

            _placeholderTopTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "Placeholder Tile Border",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
            };
            _placeholderTopTexture.SetPixels32(pixels);
            _placeholderTopTexture.Apply();
            return _placeholderTopTexture;
        }

        private Transform CreateChild(string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }

        /// <summary>Destroy in Play mode, DestroyImmediate in the editor, where deferred destruction never runs.</summary>
        private static void DestroySafe(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private T GetOrAdd<T>() where T : Component =>
            TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();

        private Color HighlightColor(TileHighlight highlight) => highlight switch
        {
            TileHighlight.MoveRange => _moveRange,
            TileHighlight.Path => _path,
            TileHighlight.Hover => _hover,
            TileHighlight.Selected => _selected,
            TileHighlight.PlayerDeployment => _playerDeployment,
            TileHighlight.EnemyDeployment => _enemyDeployment,
            _ => Color.clear,
        };
    }
}
