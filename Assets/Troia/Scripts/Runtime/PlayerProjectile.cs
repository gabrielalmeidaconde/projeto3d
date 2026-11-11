using UnityEngine;

namespace Troia
{
    public class PlayerProjectile : MonoBehaviour
    {
        readonly RaycastHit[] hits = new RaycastHit[32];
        PlayerCombat owner;
        WeaponSO weapon;
        Vector3 direction;
        float travelled, age;
        int sequence;

        public void Initialize(PlayerCombat player, WeaponSO profile, Vector3 aim, int action)
        {
            owner = player; weapon = profile; direction = aim.normalized; sequence = action;
            transform.rotation = Quaternion.LookRotation(direction);
            CombatVisuals.Part(transform, "Projetil", profile.kind == WeaponKind.Sword ? PrimitiveType.Sphere : PrimitiveType.Cube,
                Vector3.zero, profile.kind == WeaponKind.Spear ? new Vector3(.07f, .07f, 1.1f) : new Vector3(.12f, .12f, .5f), profile.color);
        }
        void Update()
        {
            if (!owner || !weapon) { Destroy(gameObject); return; }
            if (!owner.CanAct) return;
            float step = Mathf.Min(weapon.projectileSpeed * Time.deltaTime, weapon.projectileRange - travelled);
            if (step <= 0) { Destroy(gameObject); return; }
            int count = Physics.SphereCastNonAlloc(transform.position, .12f, direction, hits, step, ~0, QueryTriggerInteraction.Ignore);
            int nearest = -1; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var health = hits[i].collider.GetComponentInParent<Health>();
                if (health == owner.Health || (health && health.IsDead)) continue;
                if (hits[i].distance < distance) { nearest = i; distance = hits[i].distance; }
            }
            if (nearest >= 0)
            {
                var health = hits[nearest].collider.GetComponentInParent<Health>();
                if (health && health.GetComponent<EnemyCombat>())
                {
                    var info = new DamageInfo { source = owner.gameObject, damage = weapon.projectileDamage * owner.Favor.DamageMultiplier,
                        direction = direction, knockback = weapon.heavyProjectile ? 2 : .2f, stagger = .09f, projectile = true,
                        heavy = weapon.heavyProjectile, moveId = weapon.id + ".projectile", sequence = sequence };
                    if (health.TakeDamage(info)) owner.ConfirmHit(info, health, weapon, "Arremesso", false);
                }
                CombatVisuals.Burst(hits[nearest].point, weapon.color, .15f); Destroy(gameObject); return;
            }
            transform.position += direction * step; travelled += step; age += Time.deltaTime;
            if (age > 4 || travelled >= weapon.projectileRange) Destroy(gameObject);
        }
    }
}
