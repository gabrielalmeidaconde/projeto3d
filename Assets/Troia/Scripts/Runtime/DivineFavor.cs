using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    public class DivineFavor : MonoBehaviour
    {
        public float Amount { get; private set; }
        public God Selected { get; private set; }
        readonly Dictionary<God, int> levels = new Dictionary<God, int>();
        readonly Dictionary<God, float> cooldowns = new Dictionary<God, float>();
        PlayerCombat player;
        int lastRank;
        float furyUntil, shieldUntil, dodgeBoostUntil, parryUntil;
        public float AttackSpeed => Time.time < furyUntil ? 1.35f : 1;
        public float DamageMultiplier => (Time.time < furyUntil ? 1.6f : 1) * (1 + Level(God.Ares) * .045f * (player ? player.Style.Rank : 0)) * (Time.time < dodgeBoostUntil ? 1.3f : 1);
        public float CooldownRemaining => cooldowns.TryGetValue(Selected, out float until) ? Mathf.Max(0, until - Time.time) : 0;
        void Start()
        {
            player = GetComponent<PlayerCombat>();
            player.Events.OnFinisher += Finisher; player.Events.OnLaunch += Launch;
            player.Events.OnDodge += Dodge; player.Events.OnStyleRankChanged += RankChanged;
        }
        public int Level(God god) => levels.TryGetValue(god, out int level) ? level : 0;
        public void Gain(float amount) => Amount = Mathf.Clamp(Amount + amount, 0, 100);
        public void Grant(BlessingSO blessing)
        {
            levels[blessing.god] = Level(blessing.god) + 1; Selected = blessing.god;
        }
        public void Cycle()
        {
            for (int i = 1; i <= 6; i++)
            {
                var next = (God)(((int)Selected + i) % 6);
                if (Level(next) > 0) { Selected = next; return; }
            }
        }
        public bool Activate()
        {
            if (!player || !player.CanAct || Level(Selected) == 0 || CooldownRemaining > 0) return false;
            BlessingSO blessing = System.Array.Find(player.config.blessings, b => b.god == Selected);
            if (!blessing || Amount < blessing.cost) return false;
            if (Selected == God.Hermes && !player.RequestDodge(player.transform.forward)) return false;
            Amount -= blessing.cost; cooldowns[Selected] = Time.time + blessing.cooldown;
            int level = Level(Selected);
            switch (Selected)
            {
                case God.Zeus: Area(player.transform.position, 9, 38 * level, 0, 4, God.Zeus); break;
                case God.Ares: furyUntil = Time.time + 6 + level; break;
                case God.Apollo: player.Health.Heal(30 + 10 * level); break;
                case God.Hermes: player.GhostFor(player.config.dodgeDuration); Area(player.transform.position + player.transform.forward * 2, 5, 20 * level, 0, 6, God.Hermes); break;
                case God.Athena: shieldUntil = Time.time + 4 + level; break;
                case God.Poseidon: Area(player.transform.position + player.transform.forward * 2, 7, 28 * level, 8, 7, God.Poseidon); break;
            }
            CombatVisuals.Burst(player.transform.position + Vector3.up, Color.cyan, 2);
            return true;
        }
        public bool AllowDamage(DamageInfo info)
        {
            if (Time.time < parryUntil && Level(God.Athena) > 0)
            {
                parryUntil = 0; Gain(8);
                if (info.source)
                {
                    var health = info.source.GetComponent<Health>();
                    health?.TakeDamage(new DamageInfo { source = gameObject, damage = 20, divine = true, stagger = 1.2f, knockback = 2, direction = -info.direction });
                }
                return false;
            }
            return Time.time >= shieldUntil;
        }
        void Finisher(DamageInfo info, Health target)
        {
            int level = Level(God.Zeus);
            if (level > 0) Area(target.transform.position, 2 + level, 12 * level, 0, 1, God.Zeus, info.sequence);
        }
        void Launch(DamageInfo info, Health target)
        {
            int level = Level(God.Poseidon);
            if (level > 0) Area(target.transform.position, 2 + level, 8 * level, 7, 2, God.Poseidon, info.sequence);
        }
        void Dodge()
        {
            if (Level(God.Hermes) > 0) dodgeBoostUntil = Time.time + 1.2f;
            if (Level(God.Athena) > 0) parryUntil = Time.time + .4f;
        }
        void RankChanged(int rank)
        {
            if (rank > lastRank && rank >= 4 && Level(God.Apollo) > 0) player.Health.Heal(5 * Level(God.Apollo));
            lastRank = rank;
        }
        void Area(Vector3 point, float radius, float damage, float launch, float push, God deity, int sequence = 0)
        {
            int action = sequence != 0 ? sequence : player.AllocateActionId();
            foreach (var enemy in EnemyCombat.Combatants)
            {
                if (!enemy || !enemy.gameObject.activeInHierarchy) continue;
                Vector3 delta = enemy.transform.position - point;
                if (delta.sqrMagnitude > radius * radius || enemy.Health.IsDead) continue;
                if (Physics.Linecast(point + Vector3.up, enemy.transform.position + Vector3.up, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                if (enemy.Health.TakeDamage(new DamageInfo { source = gameObject, damage = damage, direction = delta.normalized, launch = launch, knockback = push, stagger = .6f, heavy = true, divine = true, moveId = "divine_" + deity, sequence = action }))
                {
                    player.Style.RegisterHit("divine_" + deity, false, "divine", false, false, action, damage, "Poder / " + deity);
                    CombatVisuals.Burst(enemy.transform.position + Vector3.up, Color.cyan);
                }
            }
        }
        public void ResetFavor()
        {
            Amount = 0; levels.Clear(); cooldowns.Clear(); Selected = God.Zeus;
            lastRank = 0;
            furyUntil = shieldUntil = dodgeBoostUntil = parryUntil = 0;
        }
        // Checkpoints snapshot passive levels, not transient cooldowns or buffs.
        public Dictionary<God, int> Snapshot() => new Dictionary<God, int>(levels);
        public void Restore(Dictionary<God, int> snapshot)
        {
            ResetFavor(); foreach (var entry in snapshot) levels[entry.Key] = entry.Value;
            foreach (var entry in levels) { Selected = entry.Key; break; }
        }
        void OnDestroy()
        {
            if (!player || !player.Events) return;
            player.Events.OnFinisher -= Finisher; player.Events.OnLaunch -= Launch;
            player.Events.OnDodge -= Dodge; player.Events.OnStyleRankChanged -= RankChanged;
        }
    }
}
