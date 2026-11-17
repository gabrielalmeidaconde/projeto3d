using UnityEngine;

namespace Troia
{
    public static class ArsenalDefaults
    {
        public const int Version = 7;
        public static void Populate(GameplayConfig c)
        {
            var sword = Weapon("Xifos", "XIFOS", WeaponKind.Sword, new Color(1, .65f, .2f),
                "Equilibrio e cancelamentos. Corte triplo, estocadas e ruptura ascendente.");
            sword.light = c.light; sword.heavy = c.heavy; sword.launcher = c.launcher;
            sword.airLight = c.airLight; sword.slam = c.slam;
            sword.light.displayName = "Corte de abertura"; sword.light.command = "L L L / L P / L L (pausa) L L";
            sword.light.next.displayName = "Corte reverso"; sword.light.next.next.displayName = "Sentenca";
            // (uma configuração já salva não tem mais o ramo por pausa)
            var pausa = sword.light.next.delayedNext;
            if (pausa)
            {
                pausa.displayName = "Estocadas"; pausa.motion = AttackMotion.Thrust;
                if (pausa.next) { pausa.next.displayName = "Perfuracao"; pausa.next.motion = AttackMotion.Thrust; }
            }
            sword.heavy.displayName = "Quebra-guarda"; sword.heavy.motion = AttackMotion.Overhead;
            sword.launcher.displayName = "Ascensao"; sword.launcher.motion = AttackMotion.Uppercut;
            sword.slam.displayName = "Juizo do ceu"; sword.slam.motion = AttackMotion.Overhead;
            sword.airLight.displayName = "Corte celeste";
            sword.airLight.next.displayName = "Reverso celeste";
            sword.airLight.next.next.displayName = "Sentenca celeste";
            sword.light.heavyNext = Move("Xifos_Ruptura", "Ruptura", 26, .56f, AttackMotion.Overhead, true);
            sword.light.next.heavyNext = Move("Xifos_Ascendente", "Ruptura ascendente", 23, .58f, AttackMotion.Uppercut, true);
            sword.light.next.heavyNext.launch = 8;
            sword.heavy.heavyNext = Move("Xifos_Execucao", "Execucao", 40, .78f, AttackMotion.Overhead, true, true);
            sword.dash = Move("Xifos_Investida", "Investida perfurante", 25, .48f, AttackMotion.Thrust);
            sword.dash.range = 3.4f; sword.dash.lunge = 3; sword.dash.next = sword.light.next;
            sword.airDash = Move("Xifos_Cacada", "Cacada aerea", 18, .4f, AttackMotion.Thrust);
            sword.airDash.aerial = true; sword.airDash.next = sword.airLight.next;
            sword.projectileDamage = 9; sword.fireInterval = .28f;

            var scythe = Weapon("Lanca", "LANCA / DORY", WeaponKind.Spear, new Color(.18f, .82f, 1),
                "Alcance e controle. Estocadas da falange, varreduras com a haste e avanco ao alvo.");
            scythe.light = Move("Lanca_Estocada1", "Ponta de bronze", 20, .46f, AttackMotion.Thrust);
            var s2 = Move("Lanca_Estocada2", "Avanco dorico", 23, .51f, AttackMotion.Thrust);
            var s3 = Move("Lanca_Estocada3", "Perfuracao real", 36, .66f, AttackMotion.Thrust, false, true);
            scythe.light.next = s2; s2.next = s3;
            scythe.light.command = "L L L / L P / L L (pausa) L L";
            var sweep = Move("Lanca_Varredura", "Varredura circular", 17, .66f, AttackMotion.Spin);
            sweep.angle = 300; sweep.hitCount = 2;
            sweep.next = Move("Lanca_Repique", "Repique", 32, .55f, AttackMotion.Thrust, false, true);
            s2.delayedNext = sweep; s2.delayedThreshold = .36f; s2.comboClose = .49f;
            scythe.heavy = Move("Lanca_Giro", "Giro de guerra", 22, .82f, AttackMotion.Spin, true);
            scythe.heavy.angle = 300; scythe.heavy.hitCount = 2;
            scythe.heavy.heavyNext = Move("Lanca_Ceifa", "Ceifa", 42, .76f, AttackMotion.Slash, true, true);
            scythe.heavy.heavyNext.angle = 220;
            scythe.launcher = Move("Lanca_Alavanca", "Alavanca", 24, .65f, AttackMotion.Uppercut, true);
            scythe.launcher.launch = 10;
            scythe.light.heavyNext = Move("Lanca_Icaro", "Icaro", 26, .62f, AttackMotion.Uppercut, true);
            scythe.light.heavyNext.launch = 8;
            scythe.airLight = Move("Lanca_Aereo1", "Ponta celeste", 18, .41f, AttackMotion.Thrust);
            scythe.airLight.next = Move("Lanca_Aereo2", "Passo no ceu", 19, .41f, AttackMotion.Thrust);
            scythe.airLight.next.next = Move("Lanca_Aereo3", "Queda de estrela", 30, .53f, AttackMotion.Slash, false, true);
            scythe.slam = Move("Lanca_Meteoro", "Meteoro", 52, .78f, AttackMotion.Overhead, true, true);
            scythe.slam.slam = true; scythe.slam.range = 4.6f;
            scythe.dash = Move("Lanca_Falange", "Falange", 34, .57f, AttackMotion.Thrust, true);
            scythe.dash.next = s2; scythe.dash.lunge = 4;
            scythe.airDash = Move("Lanca_Perseguicao", "Perseguicao", 24, .44f, AttackMotion.Thrust);
            scythe.airDash.aerial = true; scythe.airDash.next = scythe.airLight.next;
            scythe.projectileDamage = 24; scythe.fireInterval = .65f; scythe.heavyProjectile = true; scythe.projectileSpeed = 38;
            Configure(scythe, 4.2f, 150);

            var axe = Weapon("Escudo", "ESCUDO / ASPIS", WeaponKind.Shield, new Color(.95f, .28f, .1f),
                "Impacto e quebra de guarda. O arpao puxa o alvo; a borda do escudo finaliza.");
            axe.light = Move("Escudo_Corte1", "Faisca", 9, .29f, AttackMotion.Slash);
            var b2 = Move("Escudo_Corte2", "Corte cruzado", 6, .32f, AttackMotion.Slash); b2.hitCount = 2;
            axe.light.next = b2; b2.finisher = true; b2.next = null;
            axe.light.command = "L L / L P / L L (pausa) L L";
            var flurry = Move("Escudo_Rajada", "Rajada", 8, .62f, AttackMotion.Spin); flurry.hitCount = 4;
            flurry.next = Move("Escudo_CruzFinal", "Cruz final", 14, .48f, AttackMotion.Thrust, false, true); flurry.next.hitCount = 2;
            b2.delayedNext = flurry; b2.duration = .53f; b2.comboClose = .5f; b2.delayedThreshold = .35f;
            axe.heavy = Move("Escudo_Tesoura", "Tesoura", 17, .57f, AttackMotion.Slash, true); axe.heavy.hitCount = 2;
            axe.heavy.heavyNext = Move("Escudo_Furacao", "Furacao", 11, .76f, AttackMotion.Spin, true, true); axe.heavy.heavyNext.hitCount = 4;
            axe.heavy.heavyNext.angle = 300;
            axe.light.heavyNext = Move("Escudo_Rasante", "Rasante", 24, .46f, AttackMotion.Thrust, true);
            b2.heavyNext = axe.light.heavyNext;
            axe.launcher = Move("Escudo_Espiral", "Espiral ascendente", 22, .5f, AttackMotion.Uppercut, true); axe.launcher.launch = 9;
            axe.airLight = Move("Escudo_Aereo1", "Asa esquerda", 10, .29f, AttackMotion.Slash);
            axe.airLight.next = Move("Escudo_Aereo2", "Asa direita", 11, .29f, AttackMotion.Slash);
            axe.airLight.next.next = Move("Escudo_Aereo3", "Espiral celeste", 7, .36f, AttackMotion.Spin); axe.airLight.next.next.hitCount = 2;
            axe.airLight.next.next.next = Move("Escudo_Aereo4", "Eclipse", 24, .47f, AttackMotion.Slash, false, true);
            axe.slam = Move("Escudo_Chuva", "Chuva de laminas", 38, .56f, AttackMotion.Overhead, true, true); axe.slam.slam = true; axe.slam.range = 3.8f;
            axe.dash = Move("Escudo_Relampago", "Relampago", 12, .42f, AttackMotion.Spin); axe.dash.hitCount = 2; axe.dash.next = b2;
            axe.airDash = Move("Escudo_Borboleta", "Borboleta", 11, .37f, AttackMotion.Spin); axe.airDash.hitCount = 2; axe.airDash.aerial = true; axe.airDash.next = axe.airLight.next;
            axe.projectileDamage = 5; axe.projectileCount = 2; axe.fireInterval = .22f;
            Configure(axe, 2.9f, 100);
            axe.heavy.motion = AttackMotion.Overhead; axe.heavy.heavyNext.motion = AttackMotion.Overhead;
            axe.light.heavyNext.motion = AttackMotion.Overhead;
            sword.utilityCooldown = 1.1f;
            scythe.utilityCooldown = .9f; scythe.utilityRange = 18;
            axe.utilityCooldown = 2.4f; axe.utilityRange = 14; axe.pullsEnemy = true;
            Balance(sword, 1, 1);
            Balance(scythe, 1.05f, .8f);
            Balance(axe, 1.7f, 1.7f);
            Tune(axe.light, 25, .62f, .24f, .34f, .06f);
            Tune(b2, 42, .82f, .31f, .45f, .06f); b2.delayedThreshold = .63f;
            Tune(axe.heavy, 58, 1.1f, .4f, .62f, .18f); axe.heavy.cooldown = 1;
            Tune(axe.heavy.heavyNext, 72, 1.25f, .5f, .7f, .18f); axe.heavy.heavyNext.cooldown = 1.3f;
            Tune(axe.launcher, 32, .95f, .34f, .5f, .18f);
            Tune(axe.light.heavyNext, 48, .95f, .32f, .52f, .18f);
            scythe.light.motion = AttackMotion.Thrust; s2.motion = AttackMotion.Slash; s3.motion = AttackMotion.Thrust;
            scythe.light.displayName = "Ponta de bronze"; s2.displayName = "Varredura da haste"; s3.displayName = "Perfuracao real";
            sweep.displayName = "Roda da falange"; sweep.next.displayName = "Repique";
            scythe.heavy.heavyNext.displayName = "Ceifa de Aquiles"; scythe.launcher.displayName = "Alavanca";
            scythe.airLight.motion = scythe.airLight.next.motion = AttackMotion.Thrust;
            scythe.airLight.next.next.motion = AttackMotion.Overhead;
            axe.light.displayName = "Golpe de borda"; b2.displayName = "Reves do aspis";
            flurry.displayName = "Tormenta de bronze"; flurry.next.displayName = "Ariete"; flurry.next.motion = AttackMotion.Thrust;
            axe.heavy.displayName = "Quebra-falange"; axe.heavy.heavyNext.displayName = "Esmagamento";
            axe.light.heavyNext.displayName = "Guilhotina"; axe.launcher.displayName = "Erguer o vencido";
            axe.dash.displayName = "Golpe do arpao"; axe.dash.motion = AttackMotion.Thrust;
            axe.airLight.displayName = "Borda celeste"; axe.airLight.next.displayName = "Reves celeste";
            axe.airLight.next.next.displayName = "Disco de bronze"; axe.airLight.next.next.next.displayName = "Eclipse";
            axe.slam.displayName = "Queda do aspis"; axe.airDash.displayName = "Cacada de bronze";
            c.weapons = new[] { sword, scythe, axe }; c.combatVersion = Version;
            CombatFlowTuning.Apply(c);
            foreach (var weapon in c.weapons) Simplify(weapon);
        }

        /// <summary>
        /// Combos simples: leves em sequência e um pesado no meio da sequência fecha com um finalizador próprio de cada
        /// ponto (L P e L L P são golpes diferentes). Sem ramos por pausa (saíam sem o jogador pedir).
        /// </summary>
        static void Simplify(WeaponSO weapon)
        {
            var first = weapon.light; var second = first.next; var third = second ? second.next : null;
            if (second) { second.delayedNext = null; if (!second.heavyNext) second.heavyNext = first.heavyNext; }
            if (third) third.heavyNext = null;
            first.command = third ? "L L L   /   L P   /   L L P" : "L L   /   L P   /   L L P";
        }
        static void Tune(ComboSO move, float damage, float duration, float start, float end, float recovery)
        {
            move.damage = damage; move.duration = duration; move.hitStart = start; move.hitEnd = end;
            move.minimumRecovery = recovery; move.comboOpen = end + recovery; move.comboClose = duration * .96f;
        }
        static void Balance(WeaponSO weapon, float durationScale, float damageScale)
        {
            var seen = new System.Collections.Generic.HashSet<ComboSO>();
            foreach (var root in new[] { weapon.light, weapon.heavy, weapon.launcher, weapon.airLight, weapon.slam, weapon.dash, weapon.airDash })
                BalanceMove(root, seen, durationScale, damageScale, weapon.kind == WeaponKind.Shield);
            // Os dardos são apoio compartilhado: trocar de arma nunca muda dano nem cadência.
            weapon.projectileCount = 1; weapon.projectileDamage = 6; weapon.fireInterval = .22f;
            weapon.projectileSpeed = 42; weapon.projectileRange = 28; weapon.heavyProjectile = false;
        }
        static void BalanceMove(ComboSO m, System.Collections.Generic.HashSet<ComboSO> seen, float speed, float damage, bool axe)
        {
            if (!m || !seen.Add(m)) return;
            m.duration *= speed; m.hitStart *= speed; m.hitEnd *= speed;
            m.comboOpen *= speed; m.comboClose *= speed; m.delayedThreshold *= speed;
            m.damage *= damage;
            if (axe) { m.damage *= m.hitCount; m.hitCount = 1; m.knockback *= .7f; }
            m.minimumRecovery = m.input == AttackInput.Heavy ? .12f : .02f;
            m.comboOpen = Mathf.Max(m.comboOpen, m.hitEnd + m.minimumRecovery);
            m.cooldown = m.slam ? 1.6f : m.launch > 0 ? 1.2f : m.input == AttackInput.Heavy ? .8f : m.finisher ? .55f : .12f;
            BalanceMove(m.next, seen, speed, damage, axe); BalanceMove(m.delayedNext, seen, speed, damage, axe); BalanceMove(m.heavyNext, seen, speed, damage, axe);
        }

        static WeaponSO Weapon(string id, string title, WeaponKind kind, Color color, string description)
        {
            var w = ScriptableObject.CreateInstance<WeaponSO>(); w.name = w.id = id;
            w.displayName = title; w.kind = kind; w.color = color; w.description = description; return w;
        }
        static ComboSO Move(string id, string title, float damage, float duration, AttackMotion motion, bool heavy = false, bool finisher = false)
        {
            var m = ScriptableObject.CreateInstance<ComboSO>(); m.name = m.id = m.animationState = id;
            m.displayName = title; m.damage = damage; m.duration = duration; m.motion = motion;
            m.input = heavy ? AttackInput.Heavy : AttackInput.Light; m.finisher = finisher;
            m.hitStart = duration * .22f; m.hitEnd = duration * .5f; m.comboOpen = duration * .52f; m.comboClose = duration * .95f;
            m.knockback = finisher ? 5 : .65f; m.lunge = motion == AttackMotion.Thrust ? 1.8f : .6f;
            return m;
        }
        static void Configure(WeaponSO weapon, float range, float angle)
        {
            var seen = new System.Collections.Generic.HashSet<ComboSO>();
            foreach (var root in new[] { weapon.light, weapon.heavy, weapon.launcher, weapon.airLight, weapon.slam, weapon.dash, weapon.airDash })
                ConfigureMove(root, seen, range, angle);
            var air = weapon.airLight;
            while (air) { air.aerial = true; air = air.next; }
            weapon.slam.aerial = true;
        }
        static void ConfigureMove(ComboSO move, System.Collections.Generic.HashSet<ComboSO> seen, float range, float angle)
        {
            if (!move || !seen.Add(move)) return;
            if (!move.slam) move.range = range;
            if (Mathf.Approximately(move.angle, 110)) move.angle = move.motion == AttackMotion.Spin ? 300 : angle;
            ConfigureMove(move.next, seen, range, angle); ConfigureMove(move.delayedNext, seen, range, angle); ConfigureMove(move.heavyNext, seen, range, angle);
        }
    }
}
