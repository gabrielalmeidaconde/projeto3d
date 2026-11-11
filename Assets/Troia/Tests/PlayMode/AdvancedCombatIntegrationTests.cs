using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    public class AdvancedCombatIntegrationTests
    {
        GameManager manager;
        PlayerCombat player;
        readonly Vector3 origin = new Vector3(520, 0, 520);
        readonly List<GameObject> owned = new List<GameObject>();
        NavMeshData navData;
        NavMeshDataInstance nav;

        [UnitySetUp] public IEnumerator SetUp()
        {
            if (!GameManager.Instance) yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            manager = GameManager.Instance; manager.Restart(false); player = manager.player; player.readInput = false;
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(platform);
            platform.transform.position = origin + Vector3.down * .5f; platform.transform.localScale = new Vector3(40, 1, 40);
            var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(platform.transform.position, Quaternion.identity, Vector3.one), size = platform.transform.localScale };
            navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> { source }, new Bounds(origin, new Vector3(45, 12, 45)), Vector3.zero, Quaternion.identity);
            nav = NavMesh.AddNavMeshData(navData);
            player.Respawn(origin, Quaternion.identity); Physics.SyncTransforms(); yield return new WaitForSeconds(1.1f);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (manager) manager.Restart(false);
            foreach (var go in owned) if (go) Object.Destroy(go);
            owned.Clear(); yield return null;
            if (nav.valid) nav.Remove(); if (navData) Object.Destroy(navData);
            Time.timeScale = 1;
        }
        EnemyCombat Enemy(float distance = 2, EnemyKind kind = EnemyKind.Soldier)
        {
            var go = new GameObject("PracticeEnemy") { layer = 2 }; owned.Add(go);
            go.transform.position = origin + Vector3.forward * distance;
            var enemy = go.AddComponent<EnemyCombat>(); enemy.Initialize(kind, player, 100, 1); enemy.trainingDummy = true;
            Physics.SyncTransforms(); return enemy;
        }
        GameObject Wall()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(go);
            go.transform.position = origin + Vector3.forward * 4 + Vector3.up; go.transform.localScale = new Vector3(5, 4, .5f);
            Physics.SyncTransforms(); return go;
        }
        [UnityTest] public IEnumerator DoubleJumpHasExactlyTwoChargesAndResetsOnLanding()
        {
            player.RequestJump(); yield return new WaitForSeconds(.12f);
            float firstHeight = player.transform.position.y; Assert.That(player.JumpsUsed, Is.EqualTo(1));
            player.RequestJump(); yield return new WaitForSeconds(.1f);
            Assert.That(player.JumpsUsed, Is.EqualTo(2)); Assert.That(player.transform.position.y, Is.GreaterThan(firstHeight + .4f));
            player.RequestJump(); yield return new WaitForSeconds(.08f); Assert.That(player.JumpsUsed, Is.EqualTo(2));
            yield return new WaitForSeconds(1.7f); Assert.That(player.IsGrounded); Assert.That(player.JumpsUsed, Is.Zero);
        }
        [UnityTest] public IEnumerator HeavyCooldownAndRecoverySurviveJumpDodgeAndWeaponSwap()
        {
            var heavy = player.Weapon.heavy;
            Assert.That(player.RequestAttack(AttackInput.Heavy), Is.True);
            yield return new WaitForSeconds(.03f);
            player.RequestJump(); yield return new WaitForSeconds(.05f);
            player.SelectWeapon(1);
            Assert.That(player.RecoveryRemaining, Is.Positive);
            Assert.That(player.RequestAttack(AttackInput.Light), Is.False, "Cancelamento defensivo nao elimina recuperacao ofensiva");
            Assert.That(player.MoveCooldown(heavy), Is.GreaterThan(1));
            player.RequestDodge(Vector3.back); yield return new WaitForSeconds(.6f);
            player.SelectWeapon(0);
            // Wait for landing so Heavy resolves to the same ground move, rather than a slam.
            yield return new WaitForSeconds(.35f);
            Assert.That(player.IsGrounded, Is.True);
            Assert.That(player.RequestAttack(AttackInput.Heavy), Is.False, "Trocar de arma nao recarrega o golpe");
            yield return new WaitForSeconds(.8f);
            Assert.That(player.RequestAttack(AttackInput.Heavy), Is.True);
        }
        [UnityTest] public IEnumerator AnEarlyBufferedInputExpiresDuringProtectedRecovery()
        {
            player.RequestAttack(AttackInput.Heavy); yield return new WaitForSeconds(.03f);
            player.RequestJump(); yield return new WaitForSeconds(.04f);
            Assert.That(player.RequestAttack(AttackInput.Light), Is.False);
            Assert.That(player.RecoveryRemaining, Is.GreaterThan(player.config.inputBuffer));
            yield return new WaitForSeconds(.65f);
            Assert.That(player.CurrentAttack, Is.Null, "Uma entrada antiga nao se renova a cada frame de recuperacao");
            Assert.That(player.State, Is.EqualTo(PlayerState.Locomotion));
        }
        [UnityTest] public IEnumerator AxePullMovesOnlyEnemyAndRespectsArmorWallsAndCooldown()
        {
            player.SelectWeapon(2); var enemy = Enemy(10); player.Targeting.Lock(enemy);
            Vector3 position = player.transform.position;
            Assert.That(player.RequestApproach(), Is.True);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Pulled));
            yield return new WaitForSeconds(.5f);
            Assert.That(Vector3.Distance(player.transform.position, enemy.transform.position), Is.LessThan(2.3f));
            Assert.That(Vector3.Distance(player.transform.position, position), Is.LessThan(.1f));
            Assert.That(player.Style.Score, Is.Zero, "Puxar sem acertar nao gera estilo");
            Assert.That(player.RequestApproach(), Is.False);
            Object.Destroy(enemy.gameObject); yield return null;
            player.Respawn(origin, Quaternion.identity); player.SelectWeapon(2);
            var captain = Enemy(10, EnemyKind.Captain); player.Targeting.Lock(captain);
            Assert.That(player.RequestApproach(), Is.False, "Blindagem intacta resiste ao puxao");
            captain.Health.TakeDamage(new DamageInfo { damage=1, heavy=true, stagger=1 });
            Assert.That(player.RequestApproach(), Is.True, "Quebra de guarda abre uma oportunidade");
            yield return new WaitForSeconds(.7f);
            player.Respawn(origin, Quaternion.identity); player.SelectWeapon(2);
            captain.transform.position=origin+Vector3.forward*10; Physics.SyncTransforms(); Wall();
            Assert.That(player.RequestApproach(), Is.False, "Puxao exige linha de visao");
        }
        [UnityTest] public IEnumerator PistolsShareBurstRecoveryAcrossAllMeleeWeapons()
        {
            Assert.That(player.RequestShoot(), Is.True); yield return new WaitForSeconds(.23f);
            player.SelectWeapon(1); Assert.That(player.RequestShoot(), Is.True); yield return new WaitForSeconds(.23f);
            player.SelectWeapon(2); Assert.That(player.RequestShoot(), Is.True);
            yield return new WaitForSeconds(.25f); player.SelectWeapon(0);
            Assert.That(player.RequestShoot(), Is.False);
            Assert.That(player.ShotCooldown, Is.GreaterThan(.5f));
            yield return new WaitForSeconds(.75f); Assert.That(player.RequestShoot(), Is.True);
        }
        [UnityTest] public IEnumerator OnlyConfirmedDamageRewardsWeaponVariationAndDamageDropsTwoRanks()
        {
            var enemy = Enemy(); player.RequestAttack(AttackInput.Light); yield return new WaitForSeconds(.3f);
            float initial = player.Style.Score; Assert.That(initial, Is.Positive);
            player.SelectWeapon(1); Assert.That(player.Style.Score, Is.EqualTo(initial));
            player.RequestAttack(AttackInput.Light); yield return new WaitForSeconds(.25f);
            Assert.That(player.Style.LastGain, Is.GreaterThan(15), "Acertar com outra arma concede variedade");
            for(int i=0;i<30;i++) player.Style.RegisterHit("unique"+i, true, i%2==0?"sword":"axe", false, false, 1000+i, 1, "Teste");
            int rank=player.Style.Rank; Assert.That(rank, Is.GreaterThanOrEqualTo(3));
            Assert.That(player.Health.TakeDamage(new DamageInfo { damage=1 }), Is.True);
            Assert.That(player.Style.Rank, Is.EqualTo(rank-2));
        }
        [UnityTest] public IEnumerator EveryWeaponCanRouteLightIntoItsOwnHeavyBranch()
        {
            for (int i = 0; i < 3; i++)
            {
                player.Respawn(origin, Quaternion.identity); player.SelectWeapon(i); yield return new WaitForSeconds(.12f);
                var enemy = Enemy(); float hp = enemy.Health.Current; var profile = player.Weapon;
                player.RequestAttack(AttackInput.Light);
                yield return new WaitForSeconds(profile.light.comboOpen - .07f);
                player.RequestAttack(AttackInput.Heavy); yield return new WaitForSeconds(.1f);
                Assert.That(player.CurrentAttack, Is.SameAs(profile.light.heavyNext), profile.name);
                Assert.That(enemy.Health.Current, Is.LessThan(hp));
                Object.Destroy(enemy.gameObject); yield return null;
            }
        }
        [UnityTest] public IEnumerator QueuedWeaponSwapCommitsHitAndContinuesWithNewWeaponsNextMove()
        {
            var enemy = Enemy(); float hp = enemy.Health.Current;
            player.RequestAttack(AttackInput.Light); Assert.That(player.SelectWeapon(1), Is.True);
            Assert.That(player.WeaponIndex, Is.Zero); Assert.That(player.PendingWeapon, Is.EqualTo(1));
            yield return new WaitForSeconds(.29f);
            Assert.That(enemy.Health.Current, Is.LessThan(hp)); Assert.That(player.WeaponIndex, Is.EqualTo(1));
            player.RequestAttack(AttackInput.Light);
            Assert.That(player.CurrentAttack, Is.SameAs(player.Weapon.light.next));
            var move = player.CurrentAttack;
            Assert.That(player.RequestShoot(), Is.True); Assert.That(player.CurrentAttack, Is.SameAs(move), "Tiro nao interrompe melee");
        }
        [UnityTest] public IEnumerator ScytheMultiHitDealsTwoImpactsWithoutMultiplyingStyle()
        {
            player.SelectWeapon(1); var enemy = Enemy(); float hp = enemy.Health.Current;
            player.RequestAttack(AttackInput.Heavy); yield return new WaitForSeconds(.5f);
            Assert.That(hp - enemy.Health.Current, Is.EqualTo(player.Weapon.heavy.damage * 2).Within(.1f));
            Assert.That(player.Style.ChainHits, Is.EqualTo(2)); Assert.That(player.Style.Score, Is.EqualTo(15).Within(.1f));
            float after = enemy.Health.Current; yield return new WaitForSeconds(.35f);
            Assert.That(enemy.Health.Current, Is.EqualTo(after), "Alvo recebe apenas os pulsos configurados");
        }
        [UnityTest] public IEnumerator ProjectilesHaveCadenceHitEnemiesAndStopAtWalls()
        {
            var enemy = Enemy(9); Assert.That(player.Targeting.Lock(enemy), Is.True);
            float hp = enemy.Health.Current;
            Assert.That(player.RequestShoot(), Is.True); Assert.That(player.RequestShoot(), Is.False);
            yield return new WaitForSeconds(.4f);
            Assert.That(enemy.Health.Current, Is.LessThan(hp)); Assert.That(player.Style.Score, Is.InRange(1, 14));
            Wall(); player.Targeting.Clear(); hp = enemy.Health.Current;
            Assert.That(player.RequestApproach(), Is.False, "Aproximacao nao atravessa parede");
            Assert.That(player.RequestShoot(), Is.True); yield return new WaitForSeconds(.5f);
            Assert.That(enemy.Health.Current, Is.EqualTo(hp), "Projetil nao atravessa parede");
        }
        [UnityTest] public IEnumerator ApproachClosesDistanceAndChainsIntoDashAttack()
        {
            var enemy = Enemy(10); player.Targeting.Lock(enemy);
            Assert.That(player.RequestApproach(), Is.True); Assert.That(player.State, Is.EqualTo(PlayerState.Rushing));
            Assert.That(player.Invulnerable, Is.False);
            yield return new WaitForSeconds(.46f);
            Assert.That(Vector3.Distance(player.transform.position, enemy.transform.position), Is.LessThan(3.5f));
            Assert.That(player.CurrentAttack, Is.SameAs(player.Weapon.dash)); Assert.That(player.RequestApproach(), Is.False);
        }
        [UnityTest] public IEnumerator TwinProjectilesConvergeAndKeepTheirWeaponAfterSwitching()
        {
            var enemy = Enemy(18); player.Targeting.Lock(enemy); player.SelectWeapon(2);
            float hp = enemy.Health.Current;
            Assert.That(player.RequestShoot(), Is.True); player.SelectWeapon(1);
            yield return new WaitForSeconds(.8f);
            Assert.That(hp - enemy.Health.Current, Is.EqualTo(6).Within(.1f), "As duas pistolas acertam o alvo distante e mantem o dano da arma original");
            Assert.That(player.Style.ChainHits, Is.EqualTo(2));
            Assert.That(player.Style.Score, Is.EqualTo(15 * .35f).Within(.1f), "Duas pistolas pertencem a uma unica acao de estilo");
        }
        [UnityTest] public IEnumerator DivineAreaCreditsOneActionAcrossMultipleEnemies()
        {
            var first = Enemy(3); var second = Enemy(5);
            player.Favor.Grant(manager.config.blessings.First(b => b.god == God.Zeus)); player.Favor.Gain(100);
            Assert.That(player.Favor.Activate(), Is.True);
            Assert.That(player.Style.ChainHits, Is.EqualTo(2)); Assert.That(player.Style.Score, Is.EqualTo(15).Within(.1f));
            Assert.That(first.Health.Current, Is.LessThan(first.Health.maximum)); Assert.That(second.Health.Current, Is.LessThan(second.Health.maximum));
            yield return null;
        }
        [UnityTest] public IEnumerator AirApproachFollowsLaunchedTargetAndWhiffsCannotSustainFlight()
        {
            var enemy = Enemy(8); player.Targeting.Lock(enemy);
            enemy.Health.TakeDamage(new DamageInfo { damage = 1, launch = 10, heavy = true });
            player.RequestJump(); yield return new WaitForSeconds(.14f);
            Assert.That(player.RequestApproach(), Is.True); yield return new WaitForSeconds(.37f);
            Assert.That(player.IsGrounded, Is.False); Assert.That(player.CurrentAttack, Is.Not.Null);
            Assert.That(player.CurrentAttack.aerial, Is.True); Assert.That(Vector3.Distance(player.transform.position, enemy.transform.position), Is.LessThan(3.5f));
            Object.Destroy(enemy.gameObject); player.Respawn(origin, Quaternion.identity); yield return new WaitForSeconds(.15f);
            player.RequestJump(); yield return new WaitForSeconds(.1f); player.RequestJump();
            for (int i = 0; i < 30; i++) { player.RequestAttack(AttackInput.Light); yield return new WaitForSeconds(.1f); }
            Assert.That(player.IsGrounded, Is.True, "Golpes sem acerto nao renovam sustentacao aerea");
        }
        [UnityTest] public IEnumerator TrainingHasReachableDurableTargetsAndVisibleStyleGrades()
        {
            manager.BeginTraining(); yield return new WaitForSeconds(.2f);
            Assert.That(manager.TrainingMode, Is.True); Assert.That(manager.Alive, Is.EqualTo(2));
            foreach (var blessing in manager.config.blessings) Assert.That(player.Favor.Level(blessing.god), Is.EqualTo(1));
            Assert.That(player.Favor.Amount, Is.EqualTo(100));
            var targets = EnemyCombat.Combatants.Where(e => e && e.trainingDummy && !e.Health.IsDead).ToArray();
            Assert.That(targets.Length, Is.EqualTo(2));
            foreach (var target in targets)
            {
                Assert.That(target.Health.maximum, Is.EqualTo(5000));
                Assert.That(Vector3.Distance(player.transform.position, target.transform.position), Is.LessThan(18));
                Assert.That(player.Targeting.HasSight(target), Is.True);
            }
            float remaining = manager.RemainingTime; yield return new WaitForSeconds(.3f);
            Assert.That(manager.RemainingTime, Is.EqualTo(remaining));
            for (int i = 0; i < 40; i++) player.Style.RegisterHit("grade_" + i, false);
            yield return null;
            var hud = manager.GetComponent<GameplayHUD>();
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(hud.DisplayedRank, Is.EqualTo("SSS"));
            hud.ShowMoveList(); Assert.That(hud.MoveListVisible, Is.True);
            Assert.That(manager.State, Is.EqualTo(RunState.Paused)); Assert.That(Time.timeScale, Is.Zero);
            manager.TogglePause(); yield return null;
            Assert.That(hud.MoveListVisible, Is.False);
        }
    }
}
