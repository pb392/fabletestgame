using UnityEngine;

namespace Fable
{
    /// <summary>Keeps a flat sprite facing the camera: the "2.5D" in a 3D world.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Game.Cam;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
