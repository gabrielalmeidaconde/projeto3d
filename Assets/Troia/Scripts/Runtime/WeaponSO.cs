using UnityEngine;

namespace Troia
{
    [CreateAssetMenu(menuName = "Troia/Arma")]
    public class WeaponSO : ScriptableObject
    {
        public string id, displayName;
        [TextArea] public string description;
        public WeaponKind kind;
        public Color color = new Color(1, .65f, .18f);
        public ComboSO light, heavy, launcher, airLight, slam, dash, airDash;
        public float projectileDamage = 8, projectileSpeed = 32, projectileRange = 28, fireInterval = .28f;
        [Range(1, 3)] public int projectileCount = 1;
        public bool heavyProjectile;
        public float utilityRange = 18, utilityCooldown = .9f;
        public bool pullsEnemy;

        public ComboSO Entry(AttackInput input, bool airborne)
        {
            if (input == AttackInput.Launcher) return airborne ? slam : launcher;
            if (input == AttackInput.Heavy) return airborne ? slam : heavy;
            return airborne ? airLight : light;
        }

        public ComboSO Continuation(int completedSteps, bool airborne)
        {
            var root = airborne ? airLight : light;
            var move = root;
            for (int i = 0; i < completedSteps; i++)
            {
                if (!move || !move.next) return root;
                move = move.next;
            }
            return move;
        }
    }
}
