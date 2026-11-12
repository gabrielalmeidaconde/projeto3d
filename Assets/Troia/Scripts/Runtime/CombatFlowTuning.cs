using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    public static class CombatFlowTuning
    {
        // Idempotent tuning: existing inspector assets can migrate without multiplying their damage/timings.
        public static void Apply(GameplayConfig config)
        {
            var seen = new HashSet<ComboSO>();
            foreach (var weapon in config.weapons)
                foreach (var root in new[] { weapon.light, weapon.heavy, weapon.launcher, weapon.airLight, weapon.slam, weapon.dash, weapon.airDash }) Visit(root, seen);
            var sword=config.weapons[0]; var axe=config.weapons[2];
            Timing(sword.light,.38f,.085f,.16f,.19f,.36f,.02f);
            Timing(sword.light.next,.62f,.10f,.22f,.26f,.59f,.025f); sword.light.next.delayedThreshold=.4f;
            Timing(sword.light.next.next,.52f,.14f,.27f,.32f,.49f,.04f);
            Timing(axe.light,.52f,.17f,.27f,.32f,.50f,.05f);
            Timing(axe.light.next,.72f,.26f,.39f,.45f,.69f,.06f); axe.light.next.delayedThreshold=.55f;
            Timing(axe.heavy,.9f,.28f,.5f,.64f,.87f,.14f); axe.heavy.cooldown=.45f;
            config.combatVersion=ArsenalDefaults.Version;
        }
        static void Visit(ComboSO move, HashSet<ComboSO> seen)
        {
            if(!move || !seen.Add(move)) return;
            move.cooldown=move.slam ? .8f : move.launch>0 ? .55f : move.input==AttackInput.Heavy ? .35f : move.finisher ? .12f : 0;
            Visit(move.next,seen); Visit(move.delayedNext,seen); Visit(move.heavyNext,seen);
        }
        static void Timing(ComboSO m,float duration,float start,float end,float open,float close,float recovery)
        {
            m.duration=duration; m.hitStart=start; m.hitEnd=end; m.comboOpen=open; m.comboClose=close; m.minimumRecovery=recovery;
        }
    }
}
