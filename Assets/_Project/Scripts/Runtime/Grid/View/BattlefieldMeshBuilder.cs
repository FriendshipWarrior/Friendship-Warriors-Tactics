using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// Builds the whole battlefield as one mesh: a (possibly sloped) top face per tile plus side walls wherever a
    /// tile stands above its neighbor. Faces don't share vertices, giving flat shading. Each terrain type gets two
    /// submeshes, tops at <c>2 * terrain</c> and sides at <c>2 * terrain + 1</c>, so each can use its own material.
    /// </summary>
    public static class BattlefieldMeshBuilder
    {
        public const int TerrainCount = (int)TerrainType.Void;
        public const int SubmeshCount = TerrainCount * 2;

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

        public static Mesh Build(BattleGrid grid, float baseDepth)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>[SubmeshCount];
            for (int i = 0; i < SubmeshCount; i++)
                triangles[i] = new List<int>();

            var corners = new Vector3[4];
            float floor = -baseDepth;

            foreach (var coord in grid.AllCoords())
            {
                var tile = grid[coord];
                if (tile.Terrain == TerrainType.Void)
                    continue;

                TopCorners(coord, tile, corners);
                AddTop(vertices, uvs, triangles[TopSubmesh(tile.Terrain)], corners, TopTriangles(tile.Slope));

                var sides = triangles[SideSubmesh(tile.Terrain)];
                // Each edge lists its corners left-to-right as seen from outside the tile, so faces wind clockwise.
                AddSide(vertices, uvs, sides, corners[2], corners[3], NeighborCorner(grid, coord, GridCoord.North, 1, floor), NeighborCorner(grid, coord, GridCoord.North, 0, floor));
                AddSide(vertices, uvs, sides, corners[1], corners[2], NeighborCorner(grid, coord, GridCoord.East, 0, floor), NeighborCorner(grid, coord, GridCoord.East, 3, floor));
                AddSide(vertices, uvs, sides, corners[0], corners[1], NeighborCorner(grid, coord, GridCoord.South, 3, floor), NeighborCorner(grid, coord, GridCoord.South, 2, floor));
                AddSide(vertices, uvs, sides, corners[3], corners[0], NeighborCorner(grid, coord, GridCoord.West, 2, floor), NeighborCorner(grid, coord, GridCoord.West, 1, floor));
            }

            var mesh = new Mesh { name = "Battlefield", indexFormat = IndexFormat.UInt32, subMeshCount = SubmeshCount };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            for (int i = 0; i < SubmeshCount; i++)
                mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTop(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3[] corners, int[] order)
        {
            int start = vertices.Count;
            foreach (var corner in corners)
            {
                vertices.Add(corner);
                // Tile edges sit at half-integers in world space; offset so each tile spans exactly one UV unit.
                uvs.Add(new Vector2(corner.x / GridMetrics.TileSize + 0.5f, corner.z / GridMetrics.TileSize + 0.5f));
            }
            foreach (int index in order)
                triangles.Add(start + index);
        }

        /// <summary>
        /// Adds a wall below the edge from <paramref name="left"/> to <paramref name="right"/> (as seen from outside)
        /// down to the neighbor's matching corner heights. Skipped where the neighbor is as high or higher.
        /// </summary>
        private static void AddSide(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
            Vector3 left, Vector3 right, float leftBottom, float rightBottom)
        {
            leftBottom = Mathf.Min(leftBottom, left.y);
            rightBottom = Mathf.Min(rightBottom, right.y);
            if (left.y - leftBottom < 0.0001f && right.y - rightBottom < 0.0001f)
                return;

            int start = vertices.Count;
            float width = Vector2.Distance(new Vector2(left.x, left.z), new Vector2(right.x, right.z));
            vertices.Add(left);
            vertices.Add(right);
            vertices.Add(new Vector3(right.x, rightBottom, right.z));
            vertices.Add(new Vector3(left.x, leftBottom, left.z));
            uvs.Add(new Vector2(0f, left.y));
            uvs.Add(new Vector2(width, right.y));
            uvs.Add(new Vector2(width, rightBottom));
            uvs.Add(new Vector2(0f, leftBottom));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
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
