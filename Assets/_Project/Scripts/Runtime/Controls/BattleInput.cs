using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Controls
{
    /// <summary>
    /// Owns the battle action map from <c>BattleControls.inputactions</c> and hands its actions to the components
    /// that read them. Actions resolve lazily so consumers can grab them from their own Awake/OnEnable.
    /// </summary>
    public sealed class BattleInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;

        private InputActionMap _map;

        public InputAction MoveCursor => Find("MoveCursor");
        public InputAction Point => Find("Point");
        public InputAction Click => Find("Click");
        public InputAction Confirm => Find("Confirm");
        public InputAction Cancel => Find("Cancel");
        public InputAction RotateLeft => Find("RotateLeft");
        public InputAction RotateRight => Find("RotateRight");
        public InputAction ToggleTilt => Find("ToggleTilt");
        public InputAction ZoomStep => Find("ZoomStep");
        public InputAction ZoomHold => Find("ZoomHold");
        public InputAction PanDrag => Find("PanDrag");
        public InputAction PanStick => Find("PanStick");

        /// <summary>Opens or closes the full unit details (Status) for the unit under the cursor (X / Tab).</summary>
        public InputAction Details => Find("Details");

        /// <summary>While aiming a charged spell, switches between locking onto the unit and onto the tile (X / L).</summary>
        public InputAction ToggleLock => Find("ToggleLock");

        /// <summary>Dev-only: rolls a new random map in test scenes (R / Select).</summary>
        public InputAction DebugRegenerate => Find("DebugRegenerate");

        private InputActionMap Map => _map ??= _actions.FindActionMap("Battle", throwIfNotFound: true);

        private void OnEnable() => Map.Enable();
        private void OnDisable() => Map.Disable();

        private InputAction Find(string actionName) => Map.FindAction(actionName, throwIfNotFound: true);
    }
}
