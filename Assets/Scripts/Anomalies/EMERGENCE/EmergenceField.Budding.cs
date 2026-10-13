using UnityEngine;

namespace Massive.Emergence
{
    public sealed partial class EmergenceField
    {
        readonly EmergenceMassNugget[] budPickups = new EmergenceMassNugget[EmergenceBud.Capacity];
        readonly uint[] budSerials = new uint[EmergenceBud.Capacity];
        int ReleasedPickupCount
        {
            get { int count = 0; if (nuggets != null) foreach (var p in nuggets) if (p && p.gameObject.activeSelf && !p.IsAttachedPresentation) count++; return count; }
        }
        Vector3 BudWorldPosition(EmergenceBud bud, EmergenceMassNugget pickup)
        {
            float rewardSize = Mathf.Abs(pickup.PresentationBaseScale.x) / Mathf.Max(.0001f, Mathf.Abs(nuggetPrefab.transform.localScale.x));
            Vector3 point = arena.ClampWorldPointInside(WorldPosition(bud.Tip(profile, rewardSize)), .35f * rewardSize); point.y = 0;
            return point;
        }
        EmergenceMassNugget GetBudPickup(int index, EmergenceBud bud)
        {
            if (budPickups[index] && budSerials[index] == bud.serial)
            {
                if (!budPickups[index].gameObject.activeSelf) budPickups[index].BeginAttachedPresentation(BudWorldPosition(bud, budPickups[index]));
                return budPickups[index];
            }
            int slot = -1;
            for (int i = 0; i < nuggets.Length; i++) if (!nuggets[i] || !nuggets[i].gameObject.activeSelf) { slot = i; break; }
            if (slot < 0) throw new System.InvalidOperationException("A reward bud needs a reserved pickup slot.");
            var pickup = RentPickup(profile.pickupChances.Select(pickupRandom.NextDouble()));
            nuggets[slot] = pickup; budPickups[index] = pickup; budSerials[index] = bud.serial;
            if (!Application.isPlaying) pickup.gameObject.hideFlags = HideFlags.HideAndDontSave;
            pickup.BeginAttachedPresentation(BudWorldPosition(bud, pickup));
            pickup.AdvanceAttachedPreview(1);
            return pickup;
        }
        void UpdateRewardBuds()
        {
            for (int i = 0; i < budPickups.Length; i++)
            {
                var bud = Simulation.RewardBuds[i];
                var pickup = budPickups[i];
                if (pickup && (bud.serial != budSerials[i] || bud.phase == 0 || bud.phase == 4))
                {
                    pickup.UpdateAttachedPresentation(pickup.transform.position, 0); pickup.gameObject.SetActive(false);
                    budPickups[i] = null;
                }
                if (!bud.Attached || profile.pickupChances == null || !profile.pickupChances.IsValid) continue;
                pickup = GetBudPickup(i, bud);
                pickup.UpdateAttachedPresentation(BudWorldPosition(bud, pickup), bud.Scale(profile));
            }
        }
    }
}
