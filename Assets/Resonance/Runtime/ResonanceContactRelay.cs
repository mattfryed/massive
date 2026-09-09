using UnityEngine;

namespace Massive.Resonance
{
    public sealed class ResonanceContactRelay : MonoBehaviour
    {
        public ResonanceSegment owner;
        private void OnCollisionEnter(Collision collision) { if (owner != null) owner.WallContact(collision); }
        private void OnTriggerEnter(Collider other) { if (owner != null) owner.Contact(other); }
        private void OnTriggerStay(Collider other) { if (owner != null) owner.Contact(other); }
    }
}
