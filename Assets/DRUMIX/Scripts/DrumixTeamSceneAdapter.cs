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
                      (off > 0 ? ", locomoción apagada (" + off + ")" : "") + ".");
        }
    }
}
