using UnityEngine;

namespace Troia
{
    public static class ArsenalDefaults
    {
        public const int Version = 3;
        public static void Populate(GameplayConfig c)
        {
            var sword = Weapon("Xifos", "XIFOS", WeaponKind.Sword, new Color(1, .65f, .2f),
                "Equilibrio e cancelamentos. Corte triplo, estocadas e ruptura ascendente.");
            sword.light = c.light; sword.heavy = c.heavy; sword.launcher = c.launcher;
            sword.airLight = c.airLight; sword.slam = c.slam;
            sword.light.displayName = "Corte de abertura"; sword.light.command = "L L L / L P / L L (pausa) L L";
            sword.light.next.displayName = "Corte reverso"; sword.light.next.next.displayName = "Sentenca";
            sword.light.next.delayedNext.displayName = "Estocadas";
            sword.light.next.delayedNext.motion = AttackMotion.Thrust;
            sword.light.next.delayedNext.next.displayName = "Perfuracao";
            sword.light.next.delayedNext.next.motion = AttackMotion.Thrust;
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

            var scythe = Weapon("Foice", "FOICE / CEIFADORA", WeaponKind.Scythe, new Color(.18f, .82f, 1),
                "Alcance e controle. Cortes amplos, controle de grupos e avanco ao alvo.");
            scythe.light = Move("Foice_Estocada1", "Ponta de bronze", 20, .46f, AttackMotion.Thrust);
            var s2 = Move("Foice_Estocada2", "Avanco dorico", 23, .51f, AttackMotion.Thrust);
            var s3 = Move("Foice_Estocada3", "Perfuracao real", 36, .66f, AttackMotion.Thrust, false, true);
            scythe.light.next = s2; s2.next = s3;
            scythe.light.command = "L L L / L P / L L (pausa) L L";
            var sweep = Move("Foice_Varredura", "Varredura circular", 17, .66f, AttackMotion.Spin);
            sweep.angle = 300; sweep.hitCount = 2;
            sweep.next = Move("Foice_Repique", "Repique", 32, .55f, AttackMotion.Thrust, false, true);
            s2.delayedNext = sweep; s2.delayedThreshold = .36f; s2.comboClose = .49f;
            scythe.heavy = Move("Foice_Giro", "Giro de guerra", 22, .82f, AttackMotion.Spin, true);
            scythe.heavy.angle = 300; scythe.heavy.hitCount = 2;
            scythe.heavy.heavyNext = Move("Foice_Ceifa", "Ceifa", 42, .76f, AttackMotion.Slash, true, true);
            scythe.heavy.heavyNext.angle = 220;
            scythe.launcher = Move("Foice_Alavanca", "Alavanca", 24, .65f, AttackMotion.Uppercut, true);
            scythe.launcher.launch = 10;
            scythe.light.heavyNext = Move("Foice_Icaro", "Icaro", 26, .62f, AttackMotion.Uppercut, true);
            scythe.light.heavyNext.launch = 8;
            scythe.airLight = Move("Foice_Aereo1", "Ponta celeste", 18, .41f, AttackMotion.Thrust);
            scythe.airLight.next = Move("Foice_Aereo2", "Passo no ceu", 19, .41f, AttackMotion.Thrust);
            scythe.airLight.next.next = Move("Foice_Aereo3", "Queda de estrela", 30, .53f, AttackMotion.Slash, false, true);
            scythe.slam = Move("Foice_Meteoro", "Meteoro", 52, .78f, AttackMotion.Overhead, true, true);
            scythe.slam.slam = true; scythe.slam.range = 4.6f;
            scythe.dash = Move("Foice_Falange", "Falange", 34, .57f, AttackMotion.Thrust, true);
            scythe.dash.next = s2; scythe.dash.lunge = 4;
            scythe.airDash = Move("Foice_Perseguicao", "Perseguicao", 24, .44f, AttackMotion.Thrust);
            scythe.airDash.aerial = true; scythe.airDash.next = scythe.airLight.next;
            scythe.projectileDamage = 24; scythe.fireInterval = .65f; scythe.heavyProjectile = true; scythe.projectileSpeed = 38;
            Configure(scythe, 4.2f, 150);

            var axe = Weapon("Machado", "MACHADO / CARRASCO", WeaponKind.Axe, new Color(.95f, .28f, .1f),
                "Impacto e quebra de guarda. Puxe o alvo e finalize com golpes pesados.");
            axe.light = Move("Machado_Corte1", "Faisca", 9, .29f, AttackMotion.Slash);
            var b2 = Move("Machado_Corte2", "Corte cruzado", 6, .32f, AttackMotion.Slash); b2.hitCount = 2;
            axe.light.next = b2; b2.finisher = true; b2.next = null;
            axe.light.command = "L L / L P / L L (pausa) L L";
            var flurry = Move("Machado_Rajada", "Rajada", 8, .62f, AttackMotion.Spin); flurry.hitCount = 4;
            flurry.next = Move("Machado_CruzFinal", "Cruz final", 14, .48f, AttackMotion.Thrust, false, true); flurry.next.hitCount = 2;
            b2.delayedNext = flurry; b2.duration = .53f; b2.comboClose = .5f; b2.delayedThreshold = .35f;
            axe.heavy = Move("Machado_Tesoura", "Tesoura", 17, .57f, AttackMotion.Slash, true); axe.heavy.hitCount = 2;
            axe.heavy.heavyNext = Move("Machado_Furacao", "Furacao", 11, .76f, AttackMotion.Spin, true, true); axe.heavy.heavyNext.hitCount = 4;
            axe.heavy.heavyNext.angle = 300;
            axe.light.heavyNext = Move("Machado_Rasante", "Rasante", 24, .46f, AttackMotion.Thrust, true);
            b2.heavyNext = axe.light.heavyNext;
            axe.launcher = Move("Machado_Espiral", "Espiral ascendente", 22, .5f, AttackMotion.Uppercut, true); axe.launcher.launch = 9;
            axe.airLight = Move("Machado_Aereo1", "Asa esquerda", 10, .29f, AttackMotion.Slash);
            axe.airLight.next = Move("Machado_Aereo2", "Asa direita", 11, .29f, AttackMotion.Slash);
            axe.airLight.next.next = Move("Machado_Aereo3", "Espiral celeste", 7, .36f, AttackMotion.Spin); axe.airLight.next.next.hitCount = 2;
            axe.airLight.next.next.next = Move("Machado_Aereo4", "Eclipse", 24, .47f, AttackMotion.Slash, false, true);
            axe.slam = Move("Machado_Chuva", "Chuva de laminas", 38, .56f, AttackMotion.Overhead, true, true); axe.slam.slam = true; axe.slam.range = 3.8f;
            axe.dash = Move("Machado_Relampago", "Relampago", 12, .42f, AttackMotion.Spin); axe.dash.hitCount = 2; axe.dash.next = b2;
            axe.airDash = Move("Machado_Borboleta", "Borboleta", 11, .37f, AttackMotion.Spin); axe.airDash.hitCount = 2; axe.airDash.aerial = true; axe.airDash.next = axe.airLight.next;
            axe.projectileDamage = 5; axe.projectileCount = 2; axe.fireInterval = .22f;
            Configure(axe, 2.9f, 100);
            axe.light.displayName = "Talho do carrasco"; b2.displayName = "Veredito";
            axe.heavy.displayName = "Quebra de muralha"; axe.heavy.motion = AttackMotion.Overhead;
            axe.heavy.heavyNext.displayName = "Execucao brutal"; axe.heavy.heavyNext.motion = AttackMotion.Overhead;
            axe.launcher.displayName = "Erguer o condenado"; axe.dash.displayName = "Golpe de captura";
            axe.light.heavyNext.displayName = "Esmagamento"; axe.light.heavyNext.motion = AttackMotion.Overhead;
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
            scythe.light.motion = AttackMotion.Slash; s2.motion = AttackMotion.Spin; s3.motion = AttackMotion.Slash;
            scythe.light.displayName = "Ceifa de abertura"; s2.displayName = "Arco lunar"; s3.displayName = "Colheita";
            scythe.airLight.motion = scythe.airLight.next.motion = AttackMotion.Slash;
            axe.airLight.displayName = "Talho celeste"; axe.airLight.next.displayName = "Retorno de ferro";
            axe.airLight.next.next.displayName = "Roda do carrasco"; axe.airLight.next.next.next.displayName = "Condenacao";
            axe.slam.displayName = "Queda do carrasco"; axe.airDash.displayName = "Cacada de ferro";
            c.weapons = new[] { sword, scythe, axe }; c.combatVersion = Version;
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
                BalanceMove(root, seen, durationScale, damageScale, weapon.kind == WeaponKind.Axe);
            // Pistols are shared support weapons: switching melee weapons never changes their damage or cadence.
            weapon.projectileCount = 2; weapon.projectileDamage = 3; weapon.fireInterval = .22f;
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
