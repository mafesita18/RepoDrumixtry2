using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// Va en DRUMIX_ROOT. Lista lo que creó ARMAR TODO: al volver a armar, todo lo que NO esté aquí
    /// (lo que agregó Andrés/Samuel a mano) se guarda y se vuelve a poner en el mismo lugar.
    [DisallowMultipleComponent]
    public class DrumixRootInfo : MonoBehaviour
    {
        [HideInInspector] public List<GameObject> generated = new List<GameObject>();
        public string builtWith = "";
        [Tooltip("Versión de ARMAR TODO que la armó. Si el paquete trae una más nueva, se rearma sola al abrir Unity (una vez).")]
        public int builtVersion;
    }
}
