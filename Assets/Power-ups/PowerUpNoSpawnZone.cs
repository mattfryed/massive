using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class PowerUpNoSpawnZone : MonoBehaviour
    {
        private void Reset()
        {
            var c = GetComponent<Collider>();
            c.isTrigger = true; // recommended so it doesn't affect gameplay collisions
        }
    }
}