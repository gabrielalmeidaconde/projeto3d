using System;
using UnityEngine;

namespace Troia
{
    public enum AttackInput { Light, Heavy }
    public enum God { Zeus, Ares, Apollo, Hermes, Athena, Poseidon }
    public enum EnemyKind { Soldier, Archer, Captain, Hector }
    public enum PlayerState { Locomotion, Attacking, Dodging, Staggered, Dead }

    [Serializable]
    public struct DamageInfo
    {
        public GameObject source;
        public float damage;
        public Vector3 direction;
        public float knockback;
        public float launch;
        public float stagger;
        public bool heavy, aerial, finisher, divine;
        public string moveId;
    }

    public interface IDamageable { bool TakeDamage(DamageInfo info); }

    [Serializable]
    public struct EnemyGroup { public EnemyKind kind; [Min(1)] public int count; }

}
