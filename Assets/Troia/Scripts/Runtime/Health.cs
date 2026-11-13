using System;
using UnityEngine;

namespace Troia
{
    public class Health : MonoBehaviour, IDamageable
    {
        public float maximum = 100;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0;
        public Func<DamageInfo, bool> CanReceive;
        public event Action<DamageInfo> Damaged, Died;
        void Awake() => ResetHealth();
        public void ResetHealth(float max = -1)
        {
            if (max > 0) maximum = max;
            Current = maximum;
        }
        public void Heal(float amount) { if (!IsDead) Current = Mathf.Min(maximum, Current + Mathf.Max(0, amount)); }
        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead || info.damage <= 0 || (CanReceive != null && !CanReceive(info))) return false;
            Current = Mathf.Max(0, Current - info.damage);
            Damaged?.Invoke(info);
            if (IsDead) Died?.Invoke(info);
            return true;
        }
    }
}
