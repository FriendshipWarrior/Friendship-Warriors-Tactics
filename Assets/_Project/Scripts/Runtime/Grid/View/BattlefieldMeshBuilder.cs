using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// Builds the whole battlefield as one mesh: a (possibly sloped) top face per tile plus side walls wherever a
    /// tile stands above its neighbor. Faces don't share vertices, giving flat shading. Each terrain type gets two
    /// submeshes, tops at <c>2 * terrain</c> and sides at <c>2 * terrain + 1</c>, so each can use its own material;
    /// the last submesh holds the wall lips (see <see cref="TerrainAtlas"/>), drawn with alpha cut-out.
    /// With an atlas, covered terrain types get atlas UVs: a variant per tile top, and walls cut into bands one cell
    /// tall at fixed world heights (<see cref="TerrainTiling.SliceWall"/>) so cliff textures line up across columns.
    /// </summary>
    public static class BattlefieldMeshBuilder
    {
        public const int TerrainCount = (int)TerrainType.Void;
        public const int SubmeshCount = TerrainCount * 2 + 1;

        /// <summary>Submesh of the grass-over-cliff lips at the top of walls.</summary>
        public const int LipSubmesh = TerrainCount * 2;

        /// <summary>How far lips sit in front of their wall, so they never z-fight it.</summary>
        public const float LipOffset = 0.004f;

        public static int TopSubmesh(TerrainType terrain) => (int)terrain * 2;
        public static int SideSubmesh(TerrainType terrain) => (int)terrain * 2 + 1;

        /// <summary>World-space corners of a tile's top surface: SW, SE, NE, NW.</summary>
        public static void TopCorners(GridCoord coord, in Tile tile, Vector3[] corners, float lift = 0f)
        {
            var heights = TileShape.Corners(tile);
            float x = coord.X * GridMetrics.TileSize;
            float z = coord.Y * GridMetrics.TileSize;
            float h = GridMetrics.TileSize * 0.5f;
            corners[0] = new Vector3(x - h, heights.SouthWest * GridMetrics.StepHeight + lift, z - h);
            corners[1] = new Vector3(x + h, heights.SouthEast * GridMetrics.StepHeight + lift, z - h);
            corners[2] = new Vector3(x + h, heights.NorthEast * GridMetrics.StepHeight + lift, z + h);
            corners[3] = new Vector3(x - h, heights.NorthWest * GridMetrics.StepHeight + lift, z + h);
        }

        /// <summary>Triangle indices (into the SW, SE, NE, NW corner order) for a tile's top face, clockwise from above.</summary>
        public static int[] TopTriangles(SlopeType slope) =>
            TileShape.SplitsNorthWestToSouthEast(slope) ? SplitNwSe : SplitSwNe;

        private static readonly int[] SplitSwNe = { 0, 3, 2, 0, 2, 1 };
        private static readonly int[] SplitNwSe = { 0, 3, 1, 3, 2, 1 };

        // Where each top corner (SW, SE, NE, NW) sits inside a cell: north is up in the art.
        private static readonly Vector2[] CornerCellUv = { new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f) };

        /// <summary>Salt for top variants; walls salt by direction and band (see <see cref="SideSalt"/>).</summary>
        public const int TopSalt = 0;

        /// <summary>Variant salt for the wall facing <paramref name="direction"/> (0 N, 1 E, 2 S, 3 W) in a band.</summary>
        public static int SideSalt(int direction, int band) => (direction + 1) * 7919 + band;

        public static Mesh Build(BattleGrid grid, float baseDepth, TerrainAtlas atlas = null)
        {
            var b = new Builder(atlas);
            var corners = new Vector3[4];
            float floor = -baseDepth;

            foreach (var coord in grid.AllCoords())
            {
                var tile = grid[coord];
                if (tile.Terrain == TerrainType.Void)
                    continue;

                TerrainAtlas.Tiles tiles = null;
                if (atlas != null && atlas.Texture != null)
                    atlas.TryGet(tile.Terrain, out tiles);

                TopCorners(coord, tile, corners);
                b.AddTop(TopSubmesh(tile.Terrain), corners, TopTriangles(tile.Slope), coord, tiles);

                int sides = SideSubmesh(tile.Terrain);
                // Each edge lists its corners left-to-right as seen from outside the tile, so faces wind clockwise.
                b.AddSide(sides, corners[2], corners[3], NeighborCorner(grid, coord, GridCoord.North, 1, floor), NeighborCorner(grid, coord, GridCoord.North, 0, floor), coord, 0, tiles);
                b.AddSide(sides, corners[1], corners[2], NeighborCorner(grid, coord, GridCoord.East, 0, floor), NeighborCorner(grid, coord, GridCoord.East, 3, floor), coord, 1, tiles);
                b.AddSide(sides, corners[0], corners[1], NeighborCorner(grid, coord, GridCoord.South, 3, floor), NeighborCorner(grid, coord, GridCoord.South, 2, floor), coord, 2, tiles);
                b.AddSide(sides, corners[3], corners[0], NeighborCorner(grid, coord, GridCoord.West, 2, floor), NeighborCorner(grid, coord, GridCoord.West, 1, floor), coord, 3, tiles);
            }

            return b.ToMesh();
        }

        /// <summary>The atlas cell and mirroring a tile's top uses.</summary>
        public static (int cell, bool mirrored) TopCell(TerrainAtlas.Tiles tiles, GridCoord coord)
        {
            int variant = TerrainTiling.PickVariant(coord.X, coord.Y, TopSalt, tiles.TopVariantCount);
            return (tiles.Tops[variant % tiles.Tops.Length], variant >= tiles.Tops.Length);
        }

        /// <summary>The atlas cell a wall band uses.</summary>
        public static int SideCell(TerrainAtlas.Tiles tiles, GridCoord coord, int direction, int band) =>
            tiles.Sides[TerrainTiling.PickVariant(coord.X, coord.Y, SideSalt(direction, band), tiles.Sides.Length)];

        private sealed class Builder
        {
            private readonly TerrainAtlas _atlas;
            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector2> _uvs = new();
            private readonly List<int>[] _triangles = new List<int>[SubmeshCount];

            public Builder(TerrainAtlas atlas)
            {
                _atlas = atlas;
                for (int i = 0; i < SubmeshCount; i++)
                    _triangles[i] = new List<int>();
            }

            public void AddTop(int submesh, Vector3[] corners, int[] order, GridCoord coord, TerrainAtlas.Tiles tiles)
            {
                int start = _vertices.Count;
                Rect rect = default;
                bool mirrored = false;
                if (tiles != null)
                {
                    (int cell, bool mirror) = TopCell(tiles, coord);
                    rect = _atlas.CellUv(cell);
                    mirrored = mirror;
                }

                for (int i = 0; i < 4; i++)
                {
                    var corner = corners[i];
                    _vertices.Add(corner);
                    if (tiles == null)
                    {
                        // Tile edges sit at half-integers in world space; offset so each tile spans exactly one UV unit.
                        _uvs.Add(new Vector2(corner.x / GridMetrics.TileSize + 0.5f, corner.z / GridMetrics.TileSize + 0.5f));
                        continue;
                    }
                    var local = CornerCellUv[i];
                    if (mirrored)
                        local.x = 1f - local.x;
                    _uvs.Add(rect.min + Vector2.Scale(local, rect.size));
                }
                foreach (int index in order)
                    _triangles[submesh].Add(start + index);
            }

            /// <summary>
            /// Adds a wall below the edge from <paramref name="left"/> to <paramref name="right"/> (as seen from outside)
            /// down to the neighbor's matching corner heights. Skipped where the neighbor is as high or higher.
            /// </summary>
            public void AddSide(int submesh, Vector3 left, Vector3 right, float leftBottom, float rightBottom, GridCoord coord,
                int direction, TerrainAtlas.Tiles tiles)
            {
                leftBottom = Mathf.Min(leftBottom, left.y);
                rightBottom = Mathf.Min(rightBottom, right.y);
                if (left.y - leftBottom < 0.0001f && right.y - rightBottom < 0.0001f)
                    return;

                float width = Vector2.Distance(new Vector2(left.x, left.z), new Vector2(right.x, right.z));
                if (tiles == null || tiles.Sides.Length == 0)
                {
                    AddPlainSide(submesh, left, right, leftBottom, rightBottom, width);
                    return;
                }

                foreach (var band in TerrainTiling.SliceWall(width, left.y, right.y, leftBottom, rightBottom))
                {
                    var rect = _atlas.CellUv(SideCell(tiles, coord, direction, band.Band));
                    int start = _vertices.Count;
                    foreach (var point in band.Points)
                    {
                        _vertices.Add(WallPosition(left, right, width, point.S, point.Y));
                        var local = new Vector2(point.S / width, TerrainTiling.BandV(point.Y, band.Band));
                        _uvs.Add(rect.min + Vector2.Scale(local, rect.size));
                    }
                    for (int i = 1; i + 1 < band.Points.Count; i++)
                    {
                        _triangles[submesh].Add(start);
                        _triangles[submesh].Add(start + i);
                        _triangles[submesh].Add(start + i + 1);
                    }
                }

                if (tiles.HasLip)
                    AddLip(left, right, leftBottom, rightBottom, tiles);
            }

            /// <summary>The lip cell's top rows hang down the top of the wall, nudged outwards, cut out by alpha.</summary>
            private void AddLip(Vector3 left, Vector3 right, float leftBottom, float rightBottom, TerrainAtlas.Tiles tiles)
            {
                float drop = tiles.LipPixels / (float)_atlas.CellPixels * TerrainTiling.SideCellHeight;
                var outward = Vector3.Cross(Vector3.up, right - left).normalized * LipOffset;
                float leftLow = Mathf.Max(leftBottom, left.y - drop);
                float rightLow = Mathf.Max(rightBottom, right.y - drop);
                var rect = _atlas.CellUv(tiles.Lip);
                float V(float depthBelowTop) => 1f - depthBelowTop / TerrainTiling.SideCellHeight;

                int start = _vertices.Count;
                _vertices.Add(left + outward);
                _vertices.Add(right + outward);
                _vertices.Add(new Vector3(right.x, rightLow, right.z) + outward);
                _vertices.Add(new Vector3(left.x, leftLow, left.z) + outward);
                _uvs.Add(rect.min + Vector2.Scale(new Vector2(0f, 1f), rect.size));
                _uvs.Add(rect.min + Vector2.Scale(new Vector2(1f, 1f), rect.size));
                _uvs.Add(rect.min + Vector2.Scale(new Vector2(1f, V(right.y - rightLow)), rect.size));
                _uvs.Add(rect.min + Vector2.Scale(new Vector2(0f, V(left.y - leftLow)), rect.size));
                AddQuad(LipSubmesh, start);
            }

            private void AddPlainSide(int submesh, Vector3 left, Vector3 right, float leftBottom, float rightBottom, float width)
            {
                int start = _vertices.Count;
                _vertices.Add(left);
                _vertices.Add(right);
                _vertices.Add(new Vector3(right.x, rightBottom, right.z));
                _vertices.Add(new Vector3(left.x, leftBottom, left.z));
                _uvs.Add(new Vector2(0f, left.y));
                _uvs.Add(new Vector2(width, right.y));
                _uvs.Add(new Vector2(width, rightBottom));
                _uvs.Add(new Vector2(0f, leftBottom));
                AddQuad(submesh, start);
            }

            private void AddQuad(int submesh, int start)
            {
                var triangles = _triangles[submesh];
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            private static Vector3 WallPosition(Vector3 left, Vector3 right, float width, float s, float y)
            {
                float t = width > 0f ? s / width : 0f;
                return new Vector3(Mathf.Lerp(left.x, right.x, t), y, Mathf.Lerp(left.z, right.z, t));
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh { name = "Battlefield", indexFormat = IndexFormat.UInt32, subMeshCount = SubmeshCount };
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                for (int i = 0; i < SubmeshCount; i++)
                    mesh.SetTriangles(_triangles[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>World height of one corner (0 SW, 1 SE, 2 NE, 3 NW) of the neighboring tile, or the floor if there is none.</summary>
        private static float NeighborCorner(BattleGrid grid, GridCoord coord, GridCoord direction, int corner, float floor)
        {
            if (!grid.TryGetTile(coord + direction, out var neighbor) || neighbor.Terrain == TerrainType.Void)
                return floor;

            var heights = TileShape.Corners(neighbor);
            int steps = corner switch
            {
                0 => heights.SouthWest,
                1 => heights.SouthEast,
                2 => heights.NorthEast,
                _ => heights.NorthWest,
            };
            return steps * GridMetrics.StepHeight;
        }
    }
}
