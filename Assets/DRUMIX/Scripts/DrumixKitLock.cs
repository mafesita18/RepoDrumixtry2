using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// Va en la raíz de la batería. Arregla por diseño el bug "el bombo se va al centro al dar Play":
    /// - quita/desactiva cualquier Grab Interactable de XRI en las piezas,
    /// - Rigidbody cinemático sin gravedad,
    /// - recuerda la pose de cada pieza y la restaura si algo (física, otro script) la mueve o la re-parenta.
    [DefaultExecutionOrder(-50)]
    public class DrumixKitLock : MonoBehaviour
    {
        [Tooltip("Sube/baja toda la batería al empezar (ajuste de altura sin tocar piezas).")]
        public float heightOffset = 0f;
        public bool lockPoses = true;
        public bool forceVisible = true;

        struct Pose { public Transform t; public Transform parent; public Vector3 pos; public Quaternion rot; public Vector3 scale; }
        readonly List<Pose> poses = new List<Pose>();
        Vector3 rootPos; Quaternion rootRot; Transform rootParent;
        Vector3 basePos; Quaternion baseRot; bool hasBase;
        Vector3 calOffset; float calYaw;

        /// Calibración del jugador (pantalla CALIBRAR BATERÍA): mueve/gira TODA la batería respecto a donde la dejó ARMAR TODO.
        public void SetCalibration(Vector3 offset, float yaw)
        {
            calOffset = offset; calYaw = yaw;
            if (!hasBase) return;
            rootPos = basePos + offset; rootRot = Quaternion.Euler(0f, yaw, 0f) * baseRot;
            transform.localPosition = rootPos; transform.localRotation = rootRot;
        }
        public Vector3 CalibrationOffset { get { return calOffset; } }
        public float CalibrationYaw { get { return calYaw; } }

        void Awake()
        {
            if (Mathf.Abs(heightOffset) > 0.0001f) transform.position += Vector3.up * heightOffset;
            int removed = DrumixUtil.NeutralizeGrab(gameObject, true);
            if (removed > 0) Debug.Log("[DRUMIX] Kit: " + removed + " Grab Interactable(s) desactivados para que la batería no se mueva.");
            foreach (var body in GetComponentsInChildren<Rigidbody>(true))
            {
                if (body.GetComponentInParent<DrumixStick>()) continue;
                body.isKinematic = true; body.useGravity = false;
            }
            if (forceVisible) DrumixUtil.ForceVisible(gameObject);
            Capture();
            basePos = rootPos; baseRot = rootRot; hasBase = true;
            var s = DrumixSettings.Current;
            SetCalibration(new Vector3(s.kitX, s.kitY, s.kitZ), s.kitYaw);
        }

        public void Capture()
        {
            poses.Clear();
            rootPos = transform.localPosition; rootRot = transform.localRotation; rootParent = transform.parent;
            foreach (var pad in GetComponentsInChildren<DrumixPad>(true))
            {
                for (Transform t = pad.transform; t && t != transform; t = t.parent)
                    poses.Add(new Pose { t = t, parent = t.parent, pos = t.localPosition, rot = t.localRotation, scale = t.localScale });
            }
        }

        void LateUpdate()
        {
            if (!lockPoses) return;
            if (transform.parent != rootParent) transform.SetParent(rootParent, false);
            if (transform.localPosition != rootPos) transform.localPosition = rootPos;
            if (transform.localRotation != rootRot) transform.localRotation = rootRot;
            for (int i = 0; i < poses.Count; i++)
            {
                var p = poses[i];
                if (!p.t) continue;
                if (p.t.parent != p.parent) p.t.SetParent(p.parent, false);
                if (p.t.localPosition != p.pos) p.t.localPosition = p.pos;
                if (p.t.localRotation != p.rot) p.t.localRotation = p.rot;
                if (p.t.localScale != p.scale) p.t.localScale = p.scale;
                if (!p.t.gameObject.activeSelf) p.t.gameObject.SetActive(true);
            }
        }
    }
}
