using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    public class PlayerTargeting : MonoBehaviour
    {
        public EnemyCombat LockedTarget { get; private set; }
        public float range = 24;
        readonly List<EnemyCombat> candidates = new List<EnemyCombat>();
        public bool IsLocked => LockedTarget && IsValid(LockedTarget, range);

        public bool IsValid(EnemyCombat enemy, float distance)
        {
            return enemy && enemy.Health && !enemy.Health.IsDead && enemy.gameObject.activeInHierarchy &&
                (enemy.transform.position - transform.position).sqrMagnitude <= distance * distance;
        }

        public bool HasSight(EnemyCombat enemy)
        {
            return enemy && !Physics.Linecast(transform.position + Vector3.up * 1.2f,
                enemy.transform.position + Vector3.up, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        public EnemyCombat FindTarget(Vector3 forward, float distance, float angle = 85)
        {
            if (IsValid(LockedTarget, distance) && HasSight(LockedTarget)) return LockedTarget;
            EnemyCombat best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in EnemyCombat.Combatants)
            {
                if (!IsValid(enemy, distance)) continue;
                Vector3 delta = enemy.transform.position - transform.position;
                float offAxis = Vector3.Angle(forward, delta);
                if (offAxis > angle || !HasSight(enemy)) continue;
                float score = delta.magnitude + offAxis * .09f;
                if (score < bestScore) { best = enemy; bestScore = score; }
            }
            return best;
        }

        public bool Lock(EnemyCombat enemy)
        {
            if (!IsValid(enemy, range) || !HasSight(enemy)) return false;
            LockedTarget = enemy; return true;
        }
        public void Toggle(Vector3 forward)
        {
            if (LockedTarget) Clear(); else LockedTarget = FindTarget(forward, range, 180);
        }
        public void Clear() => LockedTarget = null;
        public void Cycle(Vector3 forward)
        {
            candidates.Clear();
            foreach (var enemy in EnemyCombat.Combatants)
                if (IsValid(enemy, range) && HasSight(enemy)) candidates.Add(enemy);
            candidates.Sort((a, b) => Vector3.SignedAngle(forward, a.transform.position - transform.position, Vector3.up)
                .CompareTo(Vector3.SignedAngle(forward, b.transform.position - transform.position, Vector3.up)));
            if (candidates.Count == 0) { Clear(); return; }
            int index = candidates.IndexOf(LockedTarget);
            LockedTarget = candidates[(index + 1) % candidates.Count];
        }
        void Update()
        {
            if (!LockedTarget) return;
            if (!IsValid(LockedTarget, range))
            {
                var forward = transform.forward;
                LockedTarget = null;
                LockedTarget = FindTarget(forward, range, 180);
            }
            else if (!HasSight(LockedTarget)) Clear();
        }
    }
}
