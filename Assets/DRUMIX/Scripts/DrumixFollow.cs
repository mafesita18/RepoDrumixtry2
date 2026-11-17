using UnityEngine;

namespace Drumix
{
    /// Copia la pose de otro Transform (p. ej. el control del XR Origin del equipo) sin ser su hijo:
    /// así DRUMIX no agrega nada dentro de los prefabs/objetos del equipo.
    [DefaultExecutionOrder(1000)]
    public class DrumixFollow : MonoBehaviour
    {
        public Transform target;
        void OnEnable() { Application.onBeforeRender += Sync; }
        void OnDisable() { Application.onBeforeRender -= Sync; }
        void LateUpdate() { Sync(); }
        void Sync() { if (target) transform.SetPositionAndRotation(target.position, target.rotation); }
    }
}
