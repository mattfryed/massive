using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    public sealed partial class SeekerVisuals
    {
        [Header("Thrust outline ghosts")]
        public bool thrustGhosts = true;
        [Range(1, 16)] public int maxThrustGhosts = 12;
        [Min(.05f)] public float ghostSpacing = .4f;
        [Min(.02f)] public float ghostLifetime = .24f;
        [Range(.05f, 1f)] public float ghostLineWidthMultiplier = .45f;
        public int LiveGhostCount { get; private set; }
        public int TotalGhosts { get; private set; }
        public int NewestGhostSlot => (nextGhost + Mathf.Clamp(maxThrustGhosts,1,GhostCapacity) - 1) % Mathf.Clamp(maxThrustGhosts,1,GhostCapacity);
        private const int GhostCapacity = 16, GhostCorners = FaceCount * 3;
        private readonly Vector3[] ghostCorners = new Vector3[GhostCapacity * GhostCorners];
        private readonly Vector3[] previousGhostPose = new Vector3[GhostCorners], currentGhostPose = new Vector3[GhostCorners];
        private readonly float[] ghostAges = new float[GhostCapacity];
        private readonly bool[] ghostActive = new bool[GhostCapacity];
        private int nextGhost, ghostLungeSerial;
        private Vector3 previousGhostPosition;
        private float ghostDistanceRemainder;
        public Vector3 GhostCornerWorld(int slot, int corner) => ghostCorners[Mathf.Clamp(slot,0,GhostCapacity-1) * GhostCorners + Mathf.Clamp(corner,0,GhostCorners-1)];
        private void ClearGhosts()
        {
            for (int i = 0; i < GhostCapacity; i++) ghostActive[i] = false;
            LiveGhostCount = TotalGhosts = nextGhost = ghostLungeSerial = 0; ghostDistanceRemainder = 0f;
        }
        private void AdvanceGhosts(float dt)
        {
            if (!Application.isPlaying || !thrustGhosts || deathAge >= 0f)
            { if (LiveGhostCount > 0) ClearGhosts(); return; }
            int capacity = Mathf.Clamp(maxThrustGhosts, 1, GhostCapacity);
            for (int i = 0; i < GhostCapacity; i++)
            {
                ghostAges[i] += dt;
                if (i >= capacity || ghostAges[i] >= ghostLifetime) ghostActive[i] = false;
            }
            if (seeker && seeker.Phase == SeekerController.AttackPhase.Firing)
            {
                for (int i = 0; i < GhostCorners; i++) currentGhostPose[i] = transform.TransformPoint(corners[i]);
                if (ghostLungeSerial != seeker.TotalLunges)
                {
                    ghostLungeSerial = seeker.TotalLunges; ghostDistanceRemainder = 0f;
                    previousGhostPosition = seeker.LungeStartPosition;
                    Vector3 offset = previousGhostPosition - transform.position;
                    for (int i = 0; i < GhostCorners; i++) previousGhostPose[i] = currentGhostPose[i] + offset;
                    EmitGhost(0f, capacity);
                }
                float distance = Vector3.Distance(previousGhostPosition, transform.position);
                float spacing = Mathf.Max(.05f, ghostSpacing);
                if (distance > .0001f)
                {
                    float along = spacing - ghostDistanceRemainder; int emitted = 0;
                    while (along <= distance && emitted++ < GhostCapacity)
                    { EmitGhost(along / distance, capacity); along += spacing; }
                    ghostDistanceRemainder = Mathf.Repeat(ghostDistanceRemainder + distance, spacing);
                }
                previousGhostPosition = transform.position;
                for (int i = 0; i < GhostCorners; i++) previousGhostPose[i] = currentGhostPose[i];
            }
            LiveGhostCount = 0; for (int i = 0; i < capacity; i++) if (ghostActive[i]) LiveGhostCount++;
        }
        private void EmitGhost(float t, int capacity)
        {
            nextGhost %= capacity; int start = nextGhost * GhostCorners;
            for (int i = 0; i < GhostCorners; i++) ghostCorners[start+i] = Vector3.Lerp(previousGhostPose[i], currentGhostPose[i], t);
            ghostActive[nextGhost] = true; ghostAges[nextGhost] = 0f;
            nextGhost = (nextGhost + 1) % capacity; TotalGhosts++;
        }
        private void DrawGhosts()
        {
            if (LiveGhostCount == 0 || !thrustGhosts) return;
            Draw.Matrix = Matrix4x4.identity; Draw.ZTest = CompareFunction.LessEqual;
            Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
            Draw.Color = Color.white;
            for (int g = 0; g < GhostCapacity; g++)
            {
                if (!ghostActive[g]) continue;
                float remain = 1f - Mathf.Clamp01(ghostAges[g] / Mathf.Max(.02f, ghostLifetime));
                // Contract the line width instead of introducing grey/translucent geometry.
                Draw.Thickness = outlineWidth * ghostLineWidthMultiplier * remain * remain;
                if (Draw.Thickness < .0001f) continue;
                for (int f = 0; f < FaceCount; f++) for (int j = 0; j < 3; j++)
                {
                    int start = g * GhostCorners + f * 3;
                    Vector3 a = ghostCorners[start+j], b = ghostCorners[start+(j+1)%3];
                    if ((b-a).sqrMagnitude > .000001f) Draw.Line(a,b);
                }
            }
        }
    }
}
