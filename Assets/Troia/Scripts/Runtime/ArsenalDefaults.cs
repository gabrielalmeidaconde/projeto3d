using UnityEngine;

namespace Troia
{
    public static class ArsenalDefaults
    {
        public const int Version = 2;
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

            var spear = Weapon("Dory", "DORY / LANCA", WeaponKind.Spear, new Color(.18f, .82f, 1),
                "Alcance e controle. Estocadas longas, varreduras e arremesso pesado.");
            spear.light = Move("Dory_Estocada1", "Ponta de bronze", 20, .46f, AttackMotion.Thrust);
            var s2 = Move("Dory_Estocada2", "Avanco dorico", 23, .51f, AttackMotion.Thrust);
            var s3 = Move("Dory_Estocada3", "Perfuracao real", 36, .66f, AttackMotion.Thrust, false, true);
            spear.light.next = s2; s2.next = s3;
            spear.light.command = "L L L / L P / L L (pausa) L L";
            var sweep = Move("Dory_Varredura", "Varredura circular", 17, .66f, AttackMotion.Spin);
            sweep.angle = 300; sweep.hitCount = 2;
            sweep.next = Move("Dory_Repique", "Repique", 32, .55f, AttackMotion.Thrust, false, true);
            s2.delayedNext = sweep; s2.delayedThreshold = .36f; s2.comboClose = .49f;
            spear.heavy = Move("Dory_Giro", "Giro de guerra", 22, .82f, AttackMotion.Spin, true);
            spear.heavy.angle = 300; spear.heavy.hitCount = 2;
            spear.heavy.heavyNext = Move("Dory_Ceifa", "Ceifa", 42, .76f, AttackMotion.Slash, true, true);
            spear.heavy.heavyNext.angle = 220;
            spear.launcher = Move("Dory_Alavanca", "Alavanca", 24, .65f, AttackMotion.Uppercut, true);
            spear.launcher.launch = 10;
            spear.light.heavyNext = Move("Dory_Icaro", "Icaro", 26, .62f, AttackMotion.Uppercut, true);
            spear.light.heavyNext.launch = 8;
            spear.airLight = Move("Dory_Aereo1", "Ponta celeste", 18, .41f, AttackMotion.Thrust);
            spear.airLight.next = Move("Dory_Aereo2", "Passo no ceu", 19, .41f, AttackMotion.Thrust);
            spear.airLight.next.next = Move("Dory_Aereo3", "Queda de estrela", 30, .53f, AttackMotion.Slash, false, true);
            spear.slam = Move("Dory_Meteoro", "Meteoro", 52, .78f, AttackMotion.Overhead, true, true);
            spear.slam.slam = true; spear.slam.range = 4.6f;
            spear.dash = Move("Dory_Falange", "Falange", 34, .57f, AttackMotion.Thrust, true);
            spear.dash.next = s2; spear.dash.lunge = 4;
            spear.airDash = Move("Dory_Perseguicao", "Perseguicao", 24, .44f, AttackMotion.Thrust);
            spear.airDash.aerial = true; spear.airDash.next = spear.airLight.next;
            spear.projectileDamage = 24; spear.fireInterval = .65f; spear.heavyProjectile = true; spear.projectileSpeed = 38;
            Configure(spear, 4.2f, 70);

            var blades = Weapon("Gemeas", "LAMINAS GEMEAS", WeaponKind.TwinBlades, new Color(.85f, .3f, 1),
                "Velocidade e juggle. Quatro cortes, multiplos impactos e facas em dupla.");
            blades.light = Move("Gemeas_Corte1", "Faisca", 9, .29f, AttackMotion.Slash);
            var b2 = Move("Gemeas_Corte2", "Corte cruzado", 6, .32f, AttackMotion.Slash); b2.hitCount = 2;
            var b3 = Move("Gemeas_Corte3", "Danca de bronze", 12, .33f, AttackMotion.Spin);
            var b4 = Move("Gemeas_Corte4", "Tempestade", 10, .54f, AttackMotion.Spin, false, true); b4.hitCount = 3;
            blades.light.next = b2; b2.next = b3; b3.next = b4;
            blades.light.command = "L L L L / L P / L L (pausa) L L";
            var flurry = Move("Gemeas_Rajada", "Rajada", 8, .62f, AttackMotion.Spin); flurry.hitCount = 4;
            flurry.next = Move("Gemeas_CruzFinal", "Cruz final", 14, .48f, AttackMotion.Thrust, false, true); flurry.next.hitCount = 2;
            b2.delayedNext = flurry; b2.duration = .53f; b2.comboClose = .5f; b2.delayedThreshold = .35f;
            blades.heavy = Move("Gemeas_Tesoura", "Tesoura", 17, .57f, AttackMotion.Slash, true); blades.heavy.hitCount = 2;
            blades.heavy.heavyNext = Move("Gemeas_Furacao", "Furacao", 11, .76f, AttackMotion.Spin, true, true); blades.heavy.heavyNext.hitCount = 4;
            blades.heavy.heavyNext.angle = 300;
            blades.light.heavyNext = Move("Gemeas_Rasante", "Rasante", 24, .46f, AttackMotion.Thrust, true);
            b2.heavyNext = blades.light.heavyNext; b3.heavyNext = blades.heavy;
            blades.launcher = Move("Gemeas_Espiral", "Espiral ascendente", 22, .5f, AttackMotion.Uppercut, true); blades.launcher.launch = 9;
            blades.airLight = Move("Gemeas_Aereo1", "Asa esquerda", 10, .29f, AttackMotion.Slash);
            blades.airLight.next = Move("Gemeas_Aereo2", "Asa direita", 11, .29f, AttackMotion.Slash);
            blades.airLight.next.next = Move("Gemeas_Aereo3", "Espiral celeste", 7, .36f, AttackMotion.Spin); blades.airLight.next.next.hitCount = 2;
            blades.airLight.next.next.next = Move("Gemeas_Aereo4", "Eclipse", 24, .47f, AttackMotion.Slash, false, true);
            blades.slam = Move("Gemeas_Chuva", "Chuva de laminas", 38, .56f, AttackMotion.Overhead, true, true); blades.slam.slam = true; blades.slam.range = 3.8f;
            blades.dash = Move("Gemeas_Relampago", "Relampago", 12, .42f, AttackMotion.Spin); blades.dash.hitCount = 2; blades.dash.next = b2;
            blades.airDash = Move("Gemeas_Borboleta", "Borboleta", 11, .37f, AttackMotion.Spin); blades.airDash.hitCount = 2; blades.airDash.aerial = true; blades.airDash.next = blades.airLight.next;
            blades.projectileDamage = 5; blades.projectileCount = 2; blades.fireInterval = .22f;
            Configure(blades, 2.6f, 130);
            c.weapons = new[] { sword, spear, blades }; c.combatVersion = Version;
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
