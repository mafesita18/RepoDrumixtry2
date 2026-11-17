using UnityEngine;
using UnityEngine.XR;

namespace Drumix
{
    /// Punta de baqueta. Va en un hijo con collider trigger + Rigidbody cinemático.
    /// Mide su velocidad para que apoyar la baqueta quieta NO cuente como golpe.
    [DisallowMultipleComponent]
    public class DrumixStick : MonoBehaviour
    {
        public XRNode hand = XRNode.RightHand;
        [Range(0f, 1f)] public float hapticAmplitude = 0.45f;
        public float hapticDuration = 0.04f;
        public float Speed { get; private set; }
        Vector3 last;
        bool hasLast;

        void Reset() { Setup(); }
        void Awake() { Setup(); }
        void Setup()
        {
            var body = GetComponent<Rigidbody>();
            if (!body) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var col = GetComponent<Collider>();
            if (!col) { var s = gameObject.AddComponent<SphereCollider>(); s.radius = 0.025f; col = s; }
            col.isTrigger = true;
        }
        void OnEnable() { hasLast = false; Speed = 0; }
        void FixedUpdate()
        {
            Vector3 p = transform.position;
            if (hasLast) Speed = Mathf.Lerp(Speed, (p - last).magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f), 0.6f);
            last = p; hasLast = true;
        }
        public void Haptic(float strength)
        {
            if (!DrumixSettings.Current.haptics) return; // Ajustes > Vibración
            var device = InputDevices.GetDeviceAtXRNode(hand);
            if (device.isValid) device.SendHapticImpulse(0, Mathf.Clamp01(hapticAmplitude * strength), hapticDuration);
        }
    }
}
