using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    // Runtime fallback also makes a freshly regenerated map immediately playable.
    public static class GameplayDefaults
    {
        public static GameplayConfig Create()
        {
            var c = ScriptableObject.CreateInstance<GameplayConfig>(); c.name = "Gameplay";
            c.light = Move("Corte_1", AttackInput.Light, 14, .44f);
            var second = Move("Corte_2", AttackInput.Light, 16, .7f);
            var third = Move("Corte_3", AttackInput.Light, 25, .65f); third.finisher = true; third.knockback = 7;
            c.light.next = second; second.next = third; second.delayedThreshold = .47f;
            // The pause branch shares a long recovery window but is distinct from early LLL.
            second.comboClose = .65f;
            var thrust = Move("Estocada_1", AttackInput.Light, 13, .34f); thrust.angle = 65; thrust.range = 3.5f; thrust.lunge = 2;
            var thrustEnd = Move("Estocada_2", AttackInput.Light, 28, .5f); thrustEnd.finisher = true; thrustEnd.knockback = 5;
            thrust.next = thrustEnd; second.delayedNext = thrust;
            c.heavy = Move("Pesado", AttackInput.Heavy, 34, .8f); c.heavy.knockback = 6; c.heavy.angle = 145;
            c.launcher = Move("Lancador", AttackInput.Heavy, 22, .7f); c.launcher.launch = 9; c.launcher.knockback = 1;
            c.airLight = Move("Aereo_1", AttackInput.Light, 13, .38f); c.airLight.aerial = true; c.airLight.range = 3.2f;
            var air2 = Move("Aereo_2", AttackInput.Light, 15, .38f); air2.aerial = true; air2.range = 3.2f;
            var air3 = Move("Aereo_3", AttackInput.Light, 22, .48f); air3.aerial = true; air3.range = 3.2f; air3.finisher = true; air3.knockback = 4;
            c.airLight.next = air2; air2.next = air3;
            c.slam = Move("Queda", AttackInput.Heavy, 40, .65f); c.slam.slam = c.slam.aerial = c.slam.finisher = true; c.slam.range = 4; c.slam.knockback = 6;
            c.floors = new WaveSO[20];
            for (int i = 0; i < 20; i++)
            {
                var wave = ScriptableObject.CreateInstance<WaveSO>(); wave.name = $"Andar_{i + 1:00}"; wave.floor = i + 1;
                wave.arena = i < 3 ? 1 : i < 6 ? 2 : i < 10 ? 3 : i < 14 ? 4 : i < 19 ? 5 : 6;
                wave.healthMultiplier = 1 + i * .025f; wave.damageMultiplier = 1 + i * .025f;
                var groups = new List<EnemyGroup>();
                groups.Add(new EnemyGroup { kind = EnemyKind.Soldier, count = 2 + i / 4 });
                if (i >= 3) groups.Add(new EnemyGroup { kind = EnemyKind.Archer, count = 1 + i / 10 });
                if (i == 9 || (i >= 14 && i < 19)) groups.Add(new EnemyGroup { kind = EnemyKind.Captain, count = 1 });
                if (i == 19) { groups.Clear(); groups.Add(new EnemyGroup { kind = EnemyKind.Hector, count = 1 }); groups.Add(new EnemyGroup { kind = EnemyKind.Soldier, count = 2 }); }
                wave.enemies = groups.ToArray(); c.floors[i] = wave;
            }
            string[] titles = { "Zeus", "Ares", "Apolo", "Hermes", "Atena", "Poseidon" };
            string[] descriptions = {
                "Q: raio em area. Finalizadores soltam raios; niveis ampliam dano e alcance.",
                "Q: furia por alguns segundos. Dano aumenta com a nota de estilo.",
                "Q: cura. Subir para S ou mais recupera vida.",
                "Q: dash ofensivo. Depois de esquivar, dano do proximo combo aumenta.",
                "Q: escudo temporario. Esquivar no momento do ataque realiza parry.",
                "Q: onda que lanca inimigos. Lancadores levantam inimigos proximos." };
            c.blessings = new BlessingSO[6];
            for (int i = 0; i < 6; i++)
            {
                var b = ScriptableObject.CreateInstance<BlessingSO>(); b.name = titles[i]; b.god = (God)i;
                b.title = titles[i]; b.description = descriptions[i]; c.blessings[i] = b;
            }
            return c;
        }
        static ComboSO Move(string id, AttackInput input, float damage, float duration)
        {
            var m = ScriptableObject.CreateInstance<ComboSO>(); m.name = m.id = m.animationState = id; m.input = input; m.damage = damage; m.duration = duration;
            m.hitStart = duration * .25f; m.hitEnd = duration * .48f; m.comboOpen = duration * .53f; m.comboClose = duration * .95f;
            return m;
        }
    }
}
