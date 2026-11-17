using UnityEngine;

namespace Drumix
{
    /// Objeto que tira el público (zanahoria, cerveza, tomate...). NUNCA cuenta como golpe:
    /// DrumixPad lo ignora y además el director ignora colisiones contra la batería.
    [DisallowMultipleComponent]
    public class DrumixProjectile : MonoBehaviour
    {
        public float lifetime = 4f;
        internal DrumixThrowDirector owner;
        float age;
        Rigidbody body;

        public Rigidbody Body
        {
            get
            {
                if (!body)
                {
                    body = GetComponent<Rigidbody>();
                    if (!body) body = gameObject.AddComponent<Rigidbody>();
                    body.mass = 0.3f; body.linearDamping = 0.05f; body.angularDamping = 0.2f;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    if (!GetComponentInChildren<Collider>()) gameObject.AddComponent<SphereCollider>().radius = 0.06f;
                    foreach (var c in GetComponentsInChildren<Collider>()) c.isTrigger = false;
                }
                return body;
            }
        }

        public void Launch(Vector3 position, Vector3 velocity)
        {
            age = 0;
            transform.SetPositionAndRotation(position, Random.rotation);
            gameObject.SetActive(true);
            var b = Body;
            b.isKinematic = false; b.useGravity = true;
            b.linearVelocity = velocity;
            b.angularVelocity = Random.insideUnitSphere * 8f;
            DrumixFX.Throw(position);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime || transform.position.y < -5f) Release();
        }

        public void Release()
        {
            if (owner) owner.Return(this);
            else gameObject.SetActive(false);
        }
    }
}
