using FriendshipWarriors.Grid;
using UnityEngine;

namespace FriendshipWarriors.Controls
{
    public static class CursorMath
    {
        /// <summary>
        /// Converts a directional input (x right, y up on screen) into one grid step for the current camera yaw.
        /// The camera sits on a diagonal, so screen-up falls exactly between two grid axes; like FFT, we turn the
        /// input 45° so each D-pad direction follows one grid axis (Up moves along the axis pointing up-left).
        /// </summary>
        /// <param name="cameraYaw">Camera yaw in degrees; 45 is the default view looking north-east.</param>
        public static GridCoord ToGridStep(Vector2 input, float cameraYaw)
        {
            if (input.sqrMagnitude < 0.0001f)
                return default;

            float inputAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg; // clockwise from screen-up
            float worldAngle = cameraYaw - 45f + inputAngle;                  // clockwise from north
            int quadrant = ((Mathf.RoundToInt(worldAngle / 90f) % 4) + 4) % 4;
            return quadrant switch
            {
                0 => GridCoord.North,
                1 => GridCoord.East,
                2 => GridCoord.South,
                _ => GridCoord.West,
            };
        }
    }
}
