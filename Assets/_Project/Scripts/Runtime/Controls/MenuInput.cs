using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Controls
{
    /// <summary>
    /// Owns the "Menu" action map from <c>BattleControls.inputactions</c>: navigation, submit, back and pause for the
    /// front-end and pause menus. Separate from the battle map so it keeps working while battle input is switched off.
    /// </summary>
    public sealed class MenuInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;

        private InputActionMap _map;

        public InputAction Navigate => Find("Navigate");
        public InputAction Submit => Find("Submit");
        public InputAction Back => Find("Back");

        /// <summary>Opens or closes the pause menu (gamepad Start, keyboard P).</summary>
        public InputAction Pause => Find("Pause");

        private InputActionMap Map => _map ??= _actions.FindActionMap("Menu", throwIfNotFound: true);

        private void OnEnable() => Map.Enable();
        private void OnDisable() => Map.Disable();

        private InputAction Find(string actionName) => Map.FindAction(actionName, throwIfNotFound: true);
    }
}
