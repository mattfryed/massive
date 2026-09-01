using UnityEngine;

namespace Massive.PowerUps
{
    public struct PowerUpInputState
    {
        public bool shieldHeld;

        public bool attackDown;
        public bool attackHeld;
        public bool attackUp;

        public Vector2 moveInput;   // XZ
        public Vector3 aimDirWS;    // where projectiles go
    }
}