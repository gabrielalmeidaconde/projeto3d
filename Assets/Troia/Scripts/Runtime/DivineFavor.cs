using System;
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
        readonly Dictionary<God, float> cooldownTotals = new Dictionary<God, float>();
        PlayerCombat player;
        int lastRank;
        float furyUntil, shieldUntil, dodgeBoostUntil, parryUntil, dashUntil;
        // O poder é pago na hora; nos deuses com gesto de preparo (Zeus, Poseidon) o efeito cai quando o golpe do herói chega.
        bool pending;
        float impactAt, castStart = -10;
        int castLevel;
        public float AttackSpeed => Time.time < furyUntil ? 1.35f : 1;
        public float DamageMultiplier => (Time.time < furyUntil ? 1.6f : 1) * (1 + Level(God.Ares) * .045f * (player ? player.Style.Rank : 0)) * (Time.time < dodgeBoostUntil ? 1.3f : 1);
        public float CooldownRemaining => cooldowns.TryGetValue(Selected, out float until) ? Mathf.Max(0, until - Time.time) : 0;
        /// <summary>Recarga do deus selecionado, de 1 (acabou de usar) a 0 (pronta).</summary>
        public float CooldownFraction => cooldownTotals.TryGetValue(Selected, out float total) && total > 0 ? Mathf.Clamp01(CooldownRemaining / total) : 0;
        public float SelectedCost { get { var b = Blessing(Selected); return b ? b.cost : 0; } }
        /// <summary>No treino a recarga é curta, para experimentar os poderes em sequência.</summary>
        public bool QuickCooldown { get; set; }
        public bool Casting => pending;
        public God CastGod { get; private set; }
        public float CastElapsed => Time.time - castStart;
        public float FuryRemaining => Mathf.Max(0, furyUntil - Time.time);
        public float ShieldRemaining => Mathf.Max(0, shieldUntil - Time.time);
        public float BoostRemaining => Mathf.Max(0, dodgeBoostUntil - Time.time);
        public bool DivineDash => Time.time < dashUntil;
        /// <summary>O herói começou o gesto (deus, nível).</summary>
        public event Action<God, int> CastStarted;
        /// <summary>O poder caiu (deus, nível, centro do efeito, raio; raio 0 quando não é em área).</summary>
        public event Action<God, int, Vector3, float> Unleashed;
        /// <summary>Um inimigo foi atingido por dano divino (o último valor diz se foi efeito passivo).</summary>
        public event Action<God, EnemyCombat, bool> DivineHit;
        /// <summary>Efeito passivo disparou (raio no finalizador, jato no lançador, cura de Apolo, vento de Hermes).</summary>
        public event Action<God, Vector3, float> Proc;
        /// <summary>A égide absorveu um golpe / a esquiva no tempo certo aparou o golpe.</summary>
        public event Action<DamageInfo> Blocked, Parried;
        public event Action<God> SelectionChanged;

        void Start()
        {
            player = GetComponent<PlayerCombat>();
            player.Events.OnFinisher += Finisher; player.Events.OnLaunch += Launch;
            player.Events.OnDodge += Dodge; player.Events.OnStyleRankChanged += RankChanged;
        }
        void Update()
        {
            if (!pending || Time.time < impactAt) return;
            // derrotado (ou partida encerrada) antes de o poder cair: o favor gasto se perde
            if (!player || !player.CanAct) { pending = false; return; }
            Unleash();
        }
        public int Level(God god) => levels.TryGetValue(god, out int level) ? level : 0;
        public void Gain(float amount) => Amount = Mathf.Clamp(Amount + amount, 0, 100);
        public void Grant(BlessingSO blessing)
        {
            levels[blessing.god] = Level(blessing.god) + 1; Select(blessing.god);
        }
        /// <summary>Passa para o próximo deus já abençoado (direction -1 volta).</summary>
        public void Cycle(int direction = 1)
        {
            int step = direction < 0 ? 5 : 1;
            for (int i = 1; i <= 6; i++)
            {
                var next = (God)(((int)Selected + i * step) % 6);
                if (Level(next) > 0) { Select(next); return; }
            }
        }
        public bool Select(God god)
        {
            if (Level(god) == 0) return false;
            if (Selected == god) return true;
            Selected = god; SelectionChanged?.Invoke(god); return true;
        }
        BlessingSO Blessing(God god) => player && player.config && player.config.blessings != null ? Array.Find(player.config.blessings, b => b && b.god == god) : null;
        public bool Activate()
        {
            if (!player || !player.CanAct || pending || Level(Selected) == 0 || CooldownRemaining > 0) return false;
            BlessingSO blessing = Blessing(Selected);
            if (!blessing || Amount < blessing.cost) return false;
            if (Selected == God.Hermes && !player.RequestDodge(player.transform.forward)) return false;
            float cooldown = QuickCooldown ? Mathf.Min(blessing.cooldown, 1.5f) : blessing.cooldown;
            Amount -= blessing.cost; cooldowns[Selected] = Time.time + cooldown; cooldownTotals[Selected] = cooldown;
            CastGod = Selected; castLevel = Level(Selected); castStart = Time.time;
            float delay = DivineLore.Atraso(Selected);
            if (Selected != God.Hermes) player.BeginCast(DivineLore.Trava(Selected), delay);
            if (CastStarted != null) CastStarted(CastGod, castLevel);
            else CombatVisuals.Burst(player.transform.position + Vector3.up, DivineLore.Cor(CastGod), 2);
            if (delay > 0) { pending = true; impactAt = Time.time + delay; }
            else Unleash();
            return true;
        }
        void Unleash()
        {
            pending = false;
            God god = CastGod; int level = castLevel;
            Vector3 origin = player.transform.position, ahead = origin + player.transform.forward * 2, center = origin;
            float radius = 0;
            switch (god)
            {
                case God.Zeus: radius = 9; Area(center, radius, 38 * level, 0, 4, God.Zeus); break;
                case God.Ares: furyUntil = Time.time + 6 + level; break;
                case God.Apollo: player.Health.Heal(30 + 10 * level); break;
                case God.Hermes:
                    player.GhostFor(player.config.dodgeDuration); dashUntil = Time.time + player.config.dodgeDuration;
                    center = ahead; radius = 5; Area(center, radius, 20 * level, 0, 6, God.Hermes); break;
                case God.Athena: shieldUntil = Time.time + 4 + level; break;
                case God.Poseidon: center = ahead; radius = 7; Area(center, radius, 28 * level, 8, 7, God.Poseidon); break;
            }
            Unleashed?.Invoke(god, level, center, radius);
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
                Parried?.Invoke(info);
                return false;
            }
            if (Time.time >= shieldUntil) return true;
            Blocked?.Invoke(info);
            return false;
        }
        void Finisher(DamageInfo info, Health target)
        {
            int level = Level(God.Zeus);
            if (level <= 0) return;
            Proc?.Invoke(God.Zeus, target.transform.position, 2 + level);
            Area(target.transform.position, 2 + level, 12 * level, 0, 1, God.Zeus, info.sequence);
        }
        void Launch(DamageInfo info, Health target)
        {
            int level = Level(God.Poseidon);
            if (level <= 0) return;
            Proc?.Invoke(God.Poseidon, target.transform.position, 2 + level);
            Area(target.transform.position, 2 + level, 8 * level, 7, 2, God.Poseidon, info.sequence);
        }
        void Dodge()
        {
            if (Level(God.Hermes) > 0) { dodgeBoostUntil = Time.time + 1.2f; Proc?.Invoke(God.Hermes, transform.position, 1.2f); }
            if (Level(God.Athena) > 0) parryUntil = Time.time + .4f;
        }
        void RankChanged(int rank)
        {
            if (rank > lastRank && rank >= 4 && Level(God.Apollo) > 0)
            {
                player.Health.Heal(5 * Level(God.Apollo)); Proc?.Invoke(God.Apollo, transform.position, 0);
            }
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
                    player.Style.RegisterHit("divine_" + deity, false, "divine", false, false, action, damage, DivineLore.Poder(deity));
                    if (DivineHit != null) DivineHit(deity, enemy, sequence != 0);
                    else CombatVisuals.Burst(enemy.transform.position + Vector3.up, DivineLore.Cor(deity));
                }
            }
        }
        public void ResetFavor()
        {
            Amount = 0; levels.Clear(); cooldowns.Clear(); cooldownTotals.Clear(); Selected = God.Zeus;
            lastRank = 0; pending = false; castStart = -10;
            furyUntil = shieldUntil = dodgeBoostUntil = parryUntil = dashUntil = 0;
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
