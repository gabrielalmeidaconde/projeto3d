using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    public class GameplayIntegrationTests
    {
        const string Map = "Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity";
        static IEnumerator LoadMap()
        {
            if (!GameManager.Instance) yield return SceneManager.LoadSceneAsync(Map);
            else GameManager.Instance.Restart(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapBootstrapsAndAllTwentyFloorsCanBeCompleted()
        {
            yield return LoadMap();
            var gm = GameManager.Instance;
            Assert.That(gm, Is.Not.Null, "Map bootstrap");
            gm.player.readInput = false;
            Assert.That(gm.player.config, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<MapPreviewWalker>(), Is.Null);
            Assert.That(gm.player.Health.Current, Is.EqualTo(gm.config.playerHealth));
            Assert.That(gm.config.floors.Length, Is.EqualTo(20));
            Assert.That(gm.player.cameraTransform, Is.Not.Null);
            var markers = Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);

            gm.TogglePause(); float pausedTime = gm.RemainingTime;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(gm.RemainingTime, Is.EqualTo(pausedTime));
            gm.TogglePause();

            for (int floor = 1; floor <= 20; floor++)
            {
                Assert.That(gm.Floor, Is.EqualTo(floor));
                int arenaNumber = gm.config.floors[floor - 1].arena;
                var arena = markers.First(m => m.kind == MarkerKind.CombatArena && m.order == arenaNumber);
                var point = markers.First(m => m.kind == MarkerKind.EnemySpawn && m.arena == arenaNumber);
                gm.player.Respawn(point.transform.position + Vector3.up * .15f, point.transform.rotation);
                gm.StartFloor();
                Assert.That(gm.FloorActive, Is.True, "Andar " + floor);
                int expected = gm.config.floors[floor - 1].enemies.Sum(g => g.count);
                Assert.That(gm.Alive, Is.EqualTo(expected), "Spawns no andar " + floor);
                yield return null; // Let obstacle carving and agents join the navigation update.
                foreach (var barrier in markers.Where(m => m.kind == MarkerKind.Barrier && m.arena == arenaNumber))
                    Assert.That(barrier.GetComponent<Collider>().enabled, Is.True);
                var enemies = Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Where(e => !e.Health.IsDead).ToArray();
                Assert.That(NavMesh.SamplePosition(gm.player.transform.position, out var playerNav, 3, NavMesh.AllAreas), Is.True);
                foreach (var enemy in enemies)
                {
                    var path = new NavMeshPath();
                    Assert.That(enemy.GetComponent<NavMeshAgent>().CalculatePath(playerNav.position, path), Is.True, $"Caminho {enemy.name}, andar {floor}");
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), $"Spawn alcancavel, andar {floor}");
                }
                if (floor == 10) Assert.That(enemies.Any(e => e.kind == EnemyKind.Captain));
                if (floor == 20) Assert.That(enemies.Any(e => e.kind == EnemyKind.Hector));
                float before = gm.RemainingTime;
                foreach (var enemy in enemies) enemy.Health.TakeDamage(new DamageInfo { source = gm.player.gameObject, damage = 99999, heavy = true });
                Assert.That(gm.RemainingTime, Is.GreaterThan(before));
                gm.CompleteFloor();
                if (floor < 20 && floor % 5 == 0)
                {
                    Assert.That(gm.State, Is.EqualTo(RunState.ChoosingBlessing));
                    Assert.That(gm.Choices.Length, Is.EqualTo(3));
                    var selected = gm.Choices[0]; int level = gm.player.Favor.Level(selected.god);
                    gm.ChooseBlessing(0); Assert.That(gm.player.Favor.Level(selected.god), Is.EqualTo(level + 1));
                    Assert.That(gm.HasCheckpoint, Is.True);
                }
                yield return null;
            }
            Assert.That(gm.State, Is.EqualTo(RunState.Won)); Assert.That(gm.Kills, Is.GreaterThan(70));
            gm.Restart(true); Assert.That(gm.Floor, Is.EqualTo(16)); Assert.That(gm.player.Health.IsDead, Is.False);
            gm.Restart(false); Assert.That(gm.Floor, Is.EqualTo(1)); Assert.That(gm.RemainingTime, Is.EqualTo(gm.config.startingTime));
            Assert.That(gm.player.Favor.Amount, Is.Zero); Assert.That(gm.player.Style.Rank, Is.Zero);
            // End by exercising timeout and death, with no orphaned actors after restart.
            gm.config.startingTime = .02f; gm.Restart(false); yield return new WaitForSeconds(.08f);
            Assert.That(gm.State, Is.EqualTo(RunState.Lost)); gm.config.startingTime = 180; gm.Restart(false);
            gm.player.Health.TakeDamage(new DamageInfo { damage = 99999 });
            // Respawn grace intentionally ignores the first second of damage.
            yield return new WaitForSeconds(1.1f); gm.player.Health.TakeDamage(new DamageInfo { damage = 99999 });
            Assert.That(gm.State, Is.EqualTo(RunState.Lost));
            gm.Restart(false);
            Assert.That(Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).All(e => e.Health.IsDead), Is.True);
        }

        [UnityTest]
        public IEnumerator CombatMovementBufferDodgeLaunchAndAirSlamWorkWithPhysics()
        {
            yield return LoadMap();
            var gm = GameManager.Instance; gm.player.readInput = false;
            var player = gm.player;
            // A separate test platform avoids map props influencing collision assertions.
            Vector3 origin = new Vector3(400, 0, 400);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "TestPlatform"; floor.transform.position = origin + Vector3.down * .5f; floor.transform.localScale = new Vector3(30, 1, 30);
            var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one), size = floor.transform.localScale, area = 0 };
            var navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new System.Collections.Generic.List<NavMeshBuildSource> { source }, new Bounds(origin, new Vector3(35, 10, 35)), Vector3.zero, Quaternion.identity);
            var nav = NavMesh.AddNavMeshData(navData);
            player.Respawn(origin, Quaternion.identity); Physics.SyncTransforms(); yield return new WaitForSeconds(.15f);
            Assert.That(player.IsGrounded, Is.True);
            player.SetMoveInput(new Vector2(1, 1), 0, false);
            Vector3 walkStart = player.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(walkStart, player.transform.position), Is.InRange(.7f, 1.5f), "Diagonal normalizada");
            player.SetMoveInput(Vector2.zero, 0, false); player.Respawn(origin, Quaternion.identity);
            yield return new WaitForSeconds(.1f);
            var enemyGo = new GameObject("CombatTestEnemy") { layer = 2 }; enemyGo.transform.position = origin + Vector3.forward * 2;
            var enemy = enemyGo.AddComponent<EnemyCombat>(); enemy.Initialize(EnemyKind.Soldier, player, 50, 1); enemy.enabled = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.1f); // Expire respawn grace before checking dodge immunity.
            float health = enemy.Health.Current;
            Assert.That(player.RequestAttack(AttackInput.Light), Is.True);
            yield return new WaitForSeconds(.15f);
            player.RequestAttack(AttackInput.Light); // Buffer just before the first recovery window.
            yield return new WaitForSeconds(.14f);
            Assert.That(player.CurrentAttack, Is.SameAs(gm.config.light.next));
            Assert.That(enemy.Health.Current, Is.LessThan(health));
            Assert.That(player.Style.Score, Is.Positive);
            Assert.That(player.RequestDodge(Vector3.right), Is.True);
            Assert.That(player.State, Is.EqualTo(PlayerState.Dodging));
            float hp = player.Health.Current;
            Assert.That(player.Health.TakeDamage(new DamageInfo { damage = 20 }), Is.False);
            Assert.That(player.Health.Current, Is.EqualTo(hp));
            float x = player.transform.position.x;
            yield return new WaitForSeconds(.2f);
            player.RequestAttack(AttackInput.Light); // Buffer while dodging must preserve the combo too.
            yield return new WaitForSeconds(.2f);
            Assert.That(player.transform.position.x, Is.GreaterThan(x + 1));
            Assert.That(player.State, Is.EqualTo(PlayerState.Attacking));
            Assert.That(player.CurrentAttack, Is.SameAs(gm.config.light.next.next));
            // Return to a stable launch alignment after testing dodge displacement.
            player.Respawn(origin, Quaternion.identity); enemy.GetComponent<NavMeshAgent>().enabled = false;
            enemyGo.transform.position = origin + Vector3.forward * 2; enemy.GetComponent<CharacterController>().enabled = true;
            yield return new WaitForSeconds(.15f); Physics.SyncTransforms();
            Assert.That(player.RequestLauncher(), Is.True);
            yield return new WaitForSeconds(.25f);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Airborne));
            // The script was disabled to avoid AI; enable now to test launched physics.
            enemy.enabled = true;
            player.RequestJump(); yield return new WaitForSeconds(.16f);
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(enemy.transform.position.y, Is.GreaterThan(.2f));
            player.RequestDodge(Vector3.forward); yield return new WaitForSeconds(.36f);
            player.RequestAttack(AttackInput.Heavy);
            Assert.That(player.CurrentAttack, Is.SameAs(gm.config.slam));
            yield return new WaitForSeconds(1);
            Assert.That(player.IsGrounded, Is.True);
            Assert.That(player.State, Is.EqualTo(PlayerState.Locomotion));
            Object.Destroy(enemyGo); Object.Destroy(floor); nav.Remove(); Object.Destroy(navData);
            gm.EndRun(false);
        }

        [UnityTest]
        public IEnumerator DivinePowersArmorEnemyAttacksAndProjectilesWork()
        {
            yield return LoadMap();
            var gm = GameManager.Instance; var player = gm.player; player.readInput = false;
            Vector3 origin = new Vector3(450, 0, 450);
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube); platform.transform.position = origin + Vector3.down * .5f; platform.transform.localScale = new Vector3(30, 1, 30);
            var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(platform.transform.position, Quaternion.identity, Vector3.one), size = platform.transform.localScale };
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new System.Collections.Generic.List<NavMeshBuildSource> { source }, new Bounds(origin, new Vector3(35, 10, 35)), Vector3.zero, Quaternion.identity);
            var nav = NavMesh.AddNavMeshData(data);
            player.Respawn(origin, Quaternion.identity);
            var go = new GameObject("AI_Test") { layer = 2 }; go.transform.position = origin + Vector3.forward * 2;
            var enemy = go.AddComponent<EnemyCombat>(); enemy.Initialize(EnemyKind.Soldier, player, 30, 1);
            yield return new WaitForSeconds(3f);
            Assert.That(player.Health.Current, Is.LessThan(player.Health.maximum), "IA precisa acertar apos antecipacao");
            enemy.enabled = false;
            var favor = player.Favor;
            foreach (var blessing in gm.config.blessings)
            {
                favor.Grant(blessing); favor.Gain(100);
                float health = player.Health.Current, enemyHealth = enemy.Health.Current;
                Assert.That(favor.Activate(), Is.True, blessing.title);
                Assert.That(favor.Activate(), Is.False, "Recarga " + blessing.title);
                Assert.That(favor.Amount, Is.EqualTo(100 - blessing.cost).Within(.01f));
                switch (blessing.god)
                {
                    case God.Zeus: Assert.That(enemy.Health.Current, Is.LessThan(enemyHealth)); break;
                    case God.Ares: Assert.That(favor.AttackSpeed, Is.GreaterThan(1)); Assert.That(favor.DamageMultiplier, Is.GreaterThan(1)); break;
                    case God.Apollo: Assert.That(player.Health.Current, Is.GreaterThan(health)); break;
                    case God.Hermes:
                        Assert.That(player.State, Is.EqualTo(PlayerState.Dodging));
                        Assert.That(Physics.GetIgnoreCollision(player.GetComponent<CharacterController>(), enemy.GetComponent<CharacterController>()));
                        yield return new WaitForSeconds(.4f);
                        Assert.That(Physics.GetIgnoreCollision(player.GetComponent<CharacterController>(), enemy.GetComponent<CharacterController>()), Is.False);
                        // Reset position to keep the remaining area powers in range.
                        player.Respawn(origin, Quaternion.identity); break;
                    case God.Athena: Assert.That(player.Health.TakeDamage(new DamageInfo { damage = 30 }), Is.False); break;
                    case God.Poseidon: Assert.That(enemy.State, Is.EqualTo(EnemyState.Airborne)); break;
                }
            }
            var captainGo = new GameObject("Armor_Test") { layer = 2 }; captainGo.transform.position = origin + Vector3.right * 4;
            var captain = captainGo.AddComponent<EnemyCombat>(); captain.Initialize(EnemyKind.Captain, player, 1, 1); captain.enabled = false;
            captain.Health.TakeDamage(new DamageInfo { damage = 10 }); Assert.That(captain.State, Is.EqualTo(EnemyState.Chasing));
            captain.Health.TakeDamage(new DamageInfo { damage = 10, heavy = true }); Assert.That(captain.State, Is.EqualTo(EnemyState.Staggered));
            captain.Health.TakeDamage(new DamageInfo { damage = 10, launch = 8, heavy = true }); Assert.That(captain.State, Is.EqualTo(EnemyState.Airborne));
            captain.Health.TakeDamage(new DamageInfo { damage = 10, aerial = true, heavy = true }); Assert.That(captain.State, Is.EqualTo(EnemyState.Staggered), "Queda pesada encerra juggle");
            // Shield has expired before testing the actual swept projectile hit.
            enemy.enabled = false; yield return new WaitForSeconds(6);
            var arrow = new GameObject("Projectile_Test"); arrow.transform.position = player.transform.position + Vector3.up + Vector3.left * 3;
            float beforeArrow = player.Health.Current; arrow.AddComponent<EnemyProjectile>().Initialize(Vector3.right, player, 10);
            yield return new WaitForSeconds(.3f);
            Assert.That(player.Health.Current, Is.LessThan(beforeArrow)); Assert.That(arrow == null, Is.True);
            yield return new WaitForSeconds(.6f);
            player.Health.TakeDamage(new DamageInfo { damage = 30 });
            for (int i = 0; i < 40; i++) player.Style.RegisterHit("passive_test_" + i, false);
            Assert.That(player.Style.Rank, Is.EqualTo(6));
            yield return new WaitForSeconds(.6f);
            float beforeDrop = player.Health.Current;
            player.Health.TakeDamage(new DamageInfo { damage = 10 });
            Assert.That(player.Style.Rank, Is.EqualTo(4));
            Assert.That(player.Health.Current, Is.EqualTo(beforeDrop - 10), "Apolo nao cura ao perder nota por dano");
            Object.Destroy(go); Object.Destroy(captainGo); Object.Destroy(platform); nav.Remove(); Object.Destroy(data); gm.EndRun(false);
        }
    }
}
