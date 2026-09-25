using System;
using UnityEngine;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierCoreGameplay
    {
        private UnityEngine.Object _transitOwner;
        private bool _transitKinematic, _transitDetectCollisions, _transitColliderEnabled;

        /// <summary>Optional presentation/motion lease; never means captured and
        /// never awards score. Standalone cores never acquire this state.</summary>
        public bool IsInExternalTransit => _transitOwner != null;
        public event Action<UnityEngine.Object> ExternalTransitRevoked;

        public bool IsTransitOwnedBy(UnityEngine.Object owner)
        { return owner != null && _transitOwner == owner; }

        public bool TryAcquireTransit(UnityEngine.Object owner)
        {
            ResolveReferences();
            if (!owner || _transitOwner || !isActiveAndEnabled || presentationOnly || IsCaptured ||
                body == null || body.isKinematic) return false;
            _transitOwner = owner;
            _transitKinematic = body.isKinematic;
            _transitDetectCollisions = body.detectCollisions;
            _transitColliderEnabled = collisionShape != null && collisionShape.enabled;
            SetVelocity(body, Vector3.zero);
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
            if (collisionShape != null) collisionShape.enabled = false;
            _incomingVelocity = Vector3.zero;
            return true;
        }

        public void ReleaseTransit(UnityEngine.Object owner, Vector3 velocity)
        {
            if (!IsTransitOwnedBy(owner)) return;
            _transitOwner = null;
            if (body != null)
            {
                body.isKinematic = _transitKinematic;
                body.detectCollisions = _transitDetectCollisions;
                if (!body.isKinematic)
                {
                    SetVelocity(body, LimitPlanarSpeed(velocity, Effective_maximumPlanarSpeed));
                    body.angularVelocity = Vector3.zero;
                }
            }
            if (collisionShape != null) collisionShape.enabled = _transitColliderEnabled;
            _incomingVelocity = Vector3.zero;
        }

        private void RevokeExternalTransit()
        {
            if (!_transitOwner) return;
            var owner = _transitOwner;
            ReleaseTransit(owner, Vector3.zero);
            ExternalTransitRevoked?.Invoke(owner);
        }
    }
}
