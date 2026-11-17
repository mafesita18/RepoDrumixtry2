using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// Va en DRUMIX_ROOT dentro de las escenas DEL EQUIPO (1.NivelUno, 2.NivelDos, 3.NivelTres).
    /// SOLO al dar Play (nunca en el editor, nunca guarda nada): oculta la batería/sensores/UI viejos del equipo que
    /// chocarían con DRUMIX, sienta al XR Origin del equipo en la batería DRUMIX y apaga su locomoción.
    /// Para volver a la escena original del equipo basta con borrar DRUMIX_ROOT (o desmarcar este componente).
    [DefaultExecutionOrder(-500)]
    public class DrumixTeamSceneAdapter : MonoBehaviour
    {
        [Tooltip("Objetos del equipo que se apagan al dar Play (su batería con DrumPad, sus baquetas, su UI vieja). En el editor siguen ahí.")]
        public List<GameObject> hideOnPlay = new List<GameObject>();
        [Tooltip("XR Origin del equipo (se reutiliza: no se crea otro).")]
        public Transform teamRig;
        [Tooltip("Asiento del baterista DRUMIX (detrás de la caja, sobre el piso/tarima).")]
        public Transform seat;
        public bool moveRigToSeat = true;
        [Tooltip("Apaga teletransporte/movimiento/giro del rig: el baterista se queda sentado en la batería.")]
        public bool disableLocomotion = true;

        void Awake()
        {
            if (!Application.isPlaying) return;
            int hidden = 0;
            foreach (var go in hideOnPlay) if (go && go.activeSelf) { go.SetActive(false); hidden++; }
            int off = 0;
            string rigNote = EnsureSingleRig();
            if (teamRig)
            {
                if (moveRigToSeat && seat)
                    teamRig.SetPositionAndRotation(seat.position, Quaternion.Euler(0f, seat.eulerAngles.y, 0f));
                if (disableLocomotion)
                    foreach (var mb in teamRig.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!mb) continue;
                        string ns = mb.GetType().Namespace ?? ""; string n = mb.GetType().Name;
                        if (ns.Contains("Locomotion") || n.Contains("MoveProvider") || n.Contains("TurnProvider") || n.Contains("Teleport")) { mb.enabled = false; off++; }
                    }
            }
            Debug.Log("[DRUMIX] Escena del equipo: " + hidden + " objeto(s) viejos ocultos al jugar, rig " + (teamRig ? "sentado en la batería" : "DRUMIX") +
                      (off > 0 ? ", locomoción apagada (" + off + ")" : "") + rigNote + ".");
        }

        /// Exactamente UN XR Origin activo (el del equipo), a escala 1: si hay otro (copias, un rig viejo de DRUMIX),
        /// se apaga solo en Play. Un rig escalado hace que todo se vea en miniatura y las manos gigantes.
        string EnsureSingleRig()
        {
            var origins = DrumixXR.AllOrigins();
            if (!teamRig) foreach (var o in origins) if (o.gameObject.activeInHierarchy) { teamRig = o; break; }
            if (!teamRig) return "";
            string note = "";
            int extra = 0;
            foreach (var o in origins)
                if (o != teamRig && o.gameObject.activeInHierarchy && !teamRig.IsChildOf(o) && !o.IsChildOf(teamRig)) { o.gameObject.SetActive(false); extra++; }
            if (extra > 0) note += ", " + extra + " XR Origin de más apagado(s)";
            if (!teamRig.gameObject.activeInHierarchy) teamRig.gameObject.SetActive(true);
            Vector3 ls = teamRig.lossyScale;
            if (Mathf.Abs(ls.x - 1f) > 0.01f || Mathf.Abs(ls.y - 1f) > 0.01f || Mathf.Abs(ls.z - 1f) > 0.01f)
            {
                Vector3 ps = teamRig.parent ? teamRig.parent.lossyScale : Vector3.one;
                teamRig.localScale = new Vector3(1f / Mathf.Max(1e-4f, ps.x), 1f / Mathf.Max(1e-4f, ps.y), 1f / Mathf.Max(1e-4f, ps.z));
                note += ", rig a escala 1 (estaba " + ls.x.ToString("0.##") + ")";
            }
            Vector3 rs = transform.lossyScale;
            if (Mathf.Abs(rs.x - 1f) > 0.01f || Mathf.Abs(rs.y - 1f) > 0.01f) Debug.LogWarning("[DRUMIX] DRUMIX_ROOT está escalado (" + rs + "): déjalo en 1,1,1 o usa DRUMIX > ARMAR TODO.");
            return note;
        }
    }
}
