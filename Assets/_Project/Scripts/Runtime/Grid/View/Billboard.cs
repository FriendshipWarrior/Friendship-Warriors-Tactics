using UnityEngine;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// Keeps a flat sprite facing the main camera by turning around the vertical axis only, so it stays upright (the
    /// way units are drawn). Used for sprite props.
    /// </summary>
    public sealed class Billboard : MonoBehaviour
    {
        private Camera _camera;

        private void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;
            transform.rotation = Quaternion.Euler(0f, _camera.transform.eulerAngles.y, 0f);
        }
    }
}
