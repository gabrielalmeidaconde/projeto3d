using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Troia.Tests
{
    public class CombatRulesTests
    {
        readonly List<Object> owned = new List<Object>();
        T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
        [TearDown] public void TearDown() { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]); owned.Clear(); }
        GameplayConfig Config()
        {
            var c = Own(GameplayDefaults.Create());
            foreach (var m in new[] { c.light, c.heavy, c.launcher, c.airLight, c.slam }) Collect(m, new HashSet<ComboSO>());
            foreach (var w in c.floors) Own(w); foreach (var b in c.blessings) Own(b); return c;
        }
        void Collect(ComboSO m, HashSet<ComboSO> visited) { if (!m || !visited.Add(m)) return; Own(m); Collect(m.next, visited); Collect(m.delayedNext, visited); }
        [Test] public void GroundComboBranchesOnPauseAndTerminates()
        {
            var c = Config(); var second = PlayerCombat.ResolveNext(c.light, c.light.comboOpen);
            Assert.That(second, Is.SameAs(c.light.next));
            Assert.That(PlayerCombat.ResolveNext(second, second.comboOpen), Is.SameAs(second.next));
            Assert.That(PlayerCombat.ResolveNext(second, second.delayedThreshold + .01f), Is.SameAs(second.delayedNext));
            Assert.That(second.next.finisher, Is.True); Assert.That(second.delayedNext.next.finisher, Is.True);
        }
        [Test] public void SavedConfigRetainsComboLinksAcrossEditorReload()
        {
            var config = Resources.Load<GameplayConfig>("Troia/Gameplay");
            Assert.That(config, Is.Not.Null); Assert.That(config.light.next, Is.Not.Null);
            Assert.That(config.light.next.next.finisher, Is.True);
            Assert.That(config.light.next.delayedNext.next.finisher, Is.True);
            Assert.That(config.airLight.next.next.aerial, Is.True);
        }
        [Test] public void AllMovesHaveValidHitAndComboWindows()
        {
            var c = Config(); var pending = new Queue<ComboSO>(new[] { c.light, c.heavy, c.launcher, c.airLight, c.slam }); var visited = new HashSet<ComboSO>();
            while (pending.Count > 0)
            {
                var move = pending.Dequeue(); if (!move || !visited.Add(move)) continue;
                Assert.That(move.hitStart, Is.GreaterThanOrEqualTo(0), move.name);
                Assert.That(move.hitEnd, Is.GreaterThan(move.hitStart), move.name);
                Assert.That(move.comboOpen, Is.GreaterThanOrEqualTo(move.hitEnd), move.name);
                Assert.That(move.comboClose, Is.LessThanOrEqualTo(move.duration), move.name);
                pending.Enqueue(move.next); pending.Enqueue(move.delayedNext);
            }
            Assert.That(visited.Count, Is.EqualTo(11));
        }
        [Test] public void TwentyFloorsContainCaptainAndHectorAndMatchMapArenas()
        {
            var c = Config(); Assert.That(c.floors.Length, Is.EqualTo(20));
            for (int i = 0; i < 20; i++)
            {
                Assert.That(c.floors[i].floor, Is.EqualTo(i + 1)); Assert.That(c.floors[i].arena, Is.InRange(1, 6));
                Assert.That(c.floors[i].enemies, Is.Not.Empty);
                foreach (var g in c.floors[i].enemies) Assert.That(g.count, Is.Positive);
            }
            Assert.That(System.Array.Exists(c.floors[9].enemies, g => g.kind == EnemyKind.Captain));
            Assert.That(System.Array.Exists(c.floors[19].enemies, g => g.kind == EnemyKind.Hector));
        }
        [Test] public void HealthBlocksDamageHealsAndDiesExactlyOnce()
        {
            var h = Own(new GameObject("HealthTest")).AddComponent<Health>(); h.ResetHealth(100); int deaths = 0; h.Died += _ => deaths++;
            h.CanReceive = _ => false; Assert.That(h.TakeDamage(new DamageInfo { damage = 25 }), Is.False);
            Assert.That(h.Current, Is.EqualTo(100)); h.CanReceive = null;
            Assert.That(h.TakeDamage(new DamageInfo { damage = 25 }), Is.True); h.Heal(10); Assert.That(h.Current, Is.EqualTo(85));
            h.TakeDamage(new DamageInfo { damage = 100 }); h.TakeDamage(new DamageInfo { damage = 100 }); h.Heal(100);
            Assert.That(deaths, Is.EqualTo(1)); Assert.That(h.Current, Is.Zero);
        }
        StyleMeter Meter() { var go = Own(new GameObject("StyleTest")); go.AddComponent<CombatEvents>(); return go.AddComponent<StyleMeter>(); }
        [Test] public void VarietyOutscoresRepeatedAttacks()
        {
            var repeated = Meter(); var varied = Meter();
            for (int i = 0; i < 20; i++) { repeated.RegisterHit("same", false); varied.RegisterHit("move" + i, false); }
            Assert.That(varied.Score, Is.GreaterThan(repeated.Score)); Assert.That(varied.Rank, Is.GreaterThan(repeated.Rank));
        }
        [Test] public void DamageDropsTwoRanksAndIdleDecays()
        {
            var meter = Meter(); for (int i = 0; i < 40; i++) meter.RegisterHit("move" + i, false);
            Assert.That(meter.Rank, Is.EqualTo(6)); meter.TakeHit(); Assert.That(meter.Rank, Is.EqualTo(4));
            float before = meter.Score; meter.Tick(4); Assert.That(meter.Score, Is.LessThan(before)); meter.ResetMeter(); Assert.That(meter.Rank, Is.Zero);
        }
        [Test] public void CombatHooksOnlyEmitMatchingFinisherAndLaunch()
        {
            var events = Own(new GameObject("EventsTest")).AddComponent<CombatEvents>(); int hits = 0, finishers = 0, launches = 0;
            events.OnComboHit += (_, __) => hits++; events.OnFinisher += (_, __) => finishers++; events.OnLaunch += (_, __) => launches++;
            events.Hit(new DamageInfo(), null); events.Hit(new DamageInfo { finisher = true, launch = 8 }, null);
            Assert.That(hits, Is.EqualTo(2)); Assert.That(finishers, Is.EqualTo(1)); Assert.That(launches, Is.EqualTo(1));
        }
        [Test] public void BlessingsStackAndCheckpointCopiesAreIndependent()
        {
            var favor = Own(new GameObject("FavorTest")).AddComponent<DivineFavor>(); var b = Own(ScriptableObject.CreateInstance<BlessingSO>()); b.god = God.Zeus;
            favor.Grant(b); var snapshot = favor.Snapshot(); favor.Grant(b); Assert.That(favor.Level(God.Zeus), Is.EqualTo(2));
            favor.Restore(snapshot); Assert.That(favor.Level(God.Zeus), Is.EqualTo(1)); favor.Gain(200); Assert.That(favor.Amount, Is.EqualTo(100));
        }
        [Test] public void ArenaContainmentUsesLocalRotationAndHeight()
        {
            var m = Own(new GameObject("ArenaTest")).AddComponent<LevelMarker>(); m.size = new Vector3(10, 6, 4); m.transform.rotation = Quaternion.Euler(0, 90, 0);
            Assert.That(GameManager.Contains(m, new Vector3(0, 1, 4)), Is.True);
            Assert.That(GameManager.Contains(m, new Vector3(4, 1, 0)), Is.False);
            Assert.That(GameManager.Contains(m, new Vector3(0, 20, 0)), Is.False);
        }
    }
}
