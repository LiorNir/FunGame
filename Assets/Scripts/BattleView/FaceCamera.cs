using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// Keeps the object facing the main camera whatever its parents' rotation, so a text on the tilted board stands
    /// upright instead of lying flat on it. Its position still follows the parent.
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        private Camera _camera;

        private void OnEnable()
        {
            Face();
        }

        private void LateUpdate()
        {
            Face();
        }

        public void Face()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }
    }
}
