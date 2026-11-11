using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace Troia
{
    public enum RunState { Playing, BetweenFloors, ChoosingBlessing, Paused, Won, Lost }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public GameplayConfig config;
        public PlayerCombat player;
        public RunState State { get; private set; }
        public bool IsPlaying => State == RunState.Playing || State == RunState.BetweenFloors;
        public int Floor { get; private set; } = 1;
        public float RemainingTime { get; private set; }
        public int Kills { get; private set; }
        public int Alive => enemies.Count(e => e && !e.Health.IsDead);
        public string Message { get; private set; }
        public bool FloorActive { get; private set; }
        public int HighlightedChoice { get; private set; }
        readonly List<EnemyCombat> enemies = new List<EnemyCombat>();
        LevelMarker[] markers;
        LevelMarker arena;
        BlessingSO[] choices;
        float transition, hitStopUntil;
        int checkpointFloor = 1;
        float checkpointTime;
        Dictionary<God, int> checkpointBlessings = new Dictionary<God, int>();
        Vector3 startPosition;
        Quaternion startRotation;
        bool pendingSpawn;
        bool gateOpen;
        RunState beforePause;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; SceneManager.sceneLoaded -= Bootstrap; SceneManager.sceneLoaded += Bootstrap; }
        static void Bootstrap(Scene scene, LoadSceneMode mode)
        {
            if (Instance || FindFirstObjectByType<GameManager>()) return;
            var spawn = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).FirstOrDefault(m => m.kind == MarkerKind.PlayerSpawn);
            if (!spawn) return;
            var go = new GameObject("Gameplay_Ilíada");
            var manager = go.AddComponent<GameManager>(); manager.Initialize(spawn);
        }
        void Awake() { Instance = this; }
        public void Initialize(LevelMarker spawn)
        {
            config = Resources.Load<GameplayConfig>("Troia/Gameplay") ?? GameplayDefaults.Create();
            markers = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);
            var preview = FindFirstObjectByType<MapPreviewWalker>();
            GameObject actor;
            if (preview)
            {
                actor = preview.gameObject; preview.enabled = false;
                // Keep the authored map character and camera; remove only its temporary controls.
                Destroy(preview);
            }
            else
            {
                actor = new GameObject("Heroi"); actor.AddComponent<CharacterController>();
                CombatVisuals.Part(actor.transform, "Corpo", PrimitiveType.Capsule, Vector3.up * .9f, new Vector3(.8f, .9f, .8f), new Color(.7f, .5f, .2f));
            }
            actor.name = "Heroi_Ilíada"; actor.layer = 2;
            var cc = actor.GetComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .4f; cc.center = Vector3.up * .9f; cc.stepOffset = .4f;
            if (!actor.GetComponent<Health>()) actor.AddComponent<Health>();
            if (!actor.GetComponent<CombatEvents>()) actor.AddComponent<CombatEvents>();
            player = actor.GetComponent<PlayerCombat>() ?? actor.AddComponent<PlayerCombat>(); player.config = config;
            player.cameraTransform = Camera.main ? Camera.main.transform : null;
            startPosition = spawn.transform.position + Vector3.up * .1f; startRotation = spawn.transform.rotation;
            gameObject.AddComponent<GameplayHUD>();
            Restart(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-troia-smoke") >= 0) gameObject.AddComponent<GameplaySmoke>();
#endif
        }
        void Update()
        {
            if (IsPlaying && Time.timeScale == 0 && Time.unscaledTime >= hitStopUntil) Time.timeScale = 1;
            if (!player || !IsPlaying) return;
            float dt = Time.deltaTime;
            if (FloorActive)
            {
                RemainingTime = Mathf.Max(0, RemainingTime - dt);
                if (RemainingTime <= 0) { EndRun(false); return; }
                if (Alive == 0 && !pendingSpawn) CompleteFloor();
            }
            else
            {
                // Timer also runs while travelling: it is global for the entire run.
                RemainingTime = Mathf.Max(0, RemainingTime - dt);
                if (RemainingTime <= 0) { EndRun(false); return; }
                transition -= dt;
                if (transition <= 0 && arena && Contains(arena, player.transform.position)) StartFloor();
            }
        }
        public static bool Contains(LevelMarker marker, Vector3 point)
        {
            Vector3 local = marker.transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) < marker.size.x * .5f && Mathf.Abs(local.z) < marker.size.z * .5f && local.y > -2 && local.y < marker.size.y + 3;
        }
        void SetDestination()
        {
            int number = config.floors[Floor - 1].arena;
            arena = markers.FirstOrDefault(m => m.kind == MarkerKind.CombatArena && m.order == number);
            Message = arena ? $"Andar {Floor}: siga para {arena.label}" : "Arena sem marcador: confira a configuracao.";
            State = RunState.BetweenFloors;
        }
        public void StartFloor()
        {
            if (FloorActive || !arena || !IsPlaying) return;
            var wave = config.floors[Floor - 1];
            var points = markers.Where(m => m.kind == MarkerKind.EnemySpawn && m.arena == wave.arena && (wave.spawnPoints == null || wave.spawnPoints.Length == 0 || wave.spawnPoints.Contains(m.order))).OrderBy(m => m.order).ToArray();
            if (points.Length == 0) { Message = "Sem pontos de spawn configurados."; return; }
            FloorActive = true; State = RunState.Playing; pendingSpawn = true;
            SetBarriers(wave.arena, true);
            int index = 0;
            foreach (var group in wave.enemies)
                for (int i = 0; i < group.count; i++)
                {
                    Vector3 desired = points[index % points.Length].transform.position;
                    // Later enemies use small offsets to avoid coincident capsules.
                    desired += new Vector3((index / points.Length) * 1.1f, 0, 0); index++;
                    if (!NavMesh.SamplePosition(desired, out var nav, 5, NavMesh.AllAreas))
                    {
                        Debug.LogError($"Sem NavMesh no spawn de {arena.label}: {desired}"); continue;
                    }
                    var go = new GameObject(group.kind.ToString()) { layer = 2 };
                    go.transform.position = nav.position;
                    var enemy = go.AddComponent<EnemyCombat>();
                    enemy.Initialize(group.kind, player, wave.healthMultiplier, wave.damageMultiplier); enemies.Add(enemy);
                }
            pendingSpawn = false;
            if (Alive == 0) { FloorActive = false; SetBarriers(wave.arena, false); Message = "Falha ao gerar inimigos: confira o NavMesh."; return; }
            Message = $"Andar {Floor} / {config.floors.Length}";
        }
        public void EnemyKilled(EnemyCombat enemy, DamageInfo info)
        {
            if (!enemies.Remove(enemy) || !IsPlaying) return;
            Kills++; RemainingTime += config.secondsPerKill * player.Style.Multiplier;
            player.Favor.Gain(6 * player.Style.Multiplier);
        }
        public void CompleteFloor()
        {
            if (!FloorActive || Alive > 0) return;
            FloorActive = false;
            int completed = Floor;
            int oldArena = config.floors[completed - 1].arena;
            bool changedArena = completed == config.floors.Length || config.floors[completed].arena != oldArena;
            if (changedArena) SetBarriers(oldArena, false);
            if (completed == config.floors.Length) { EndRun(true); return; }
            if (oldArena == 3 && changedArena) OpenGate();
            Floor++;
            if (completed % 5 == 0)
            {
                State = RunState.ChoosingBlessing;
                choices = config.blessings.OrderBy(_ => Random.value).Take(3).ToArray();
                HighlightedChoice = 0; Time.timeScale = 0;
                PlayerCombat.LockCursor(false); Message = "Escolha uma bencao: 1, 2 ou 3";
            }
            else { transition = 2; SetDestination(); }
        }
        public void ChooseBlessing(int index)
        {
            if (State != RunState.ChoosingBlessing || index < 0 || index >= choices.Length) return;
            player.Favor.Grant(choices[index]); player.Health.Heal(25);
            Time.timeScale = 1;
            checkpointFloor = Floor; checkpointTime = RemainingTime; checkpointBlessings = player.Favor.Snapshot();
            transition = 2; SetDestination(); PlayerCombat.LockCursor(true);
        }
        public BlessingSO[] Choices => choices;
        void ReadBlessingKeys(Keyboard kb)
        {
            if (kb.digit1Key.wasPressedThisFrame) ChooseBlessing(0);
            if (kb.digit2Key.wasPressedThisFrame) ChooseBlessing(1);
            if (kb.digit3Key.wasPressedThisFrame) ChooseBlessing(2);
        }
        void LateUpdate()
        {
            // Choice input remains live while gameplay simulation is suspended.
            if (State == RunState.ChoosingBlessing && Keyboard.current != null) ReadBlessingKeys(Keyboard.current);
            var pad = Gamepad.current;
            if (State == RunState.ChoosingBlessing && pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) HighlightedChoice = (HighlightedChoice + choices.Length - 1) % choices.Length;
                if (pad.dpad.right.wasPressedThisFrame) HighlightedChoice = (HighlightedChoice + 1) % choices.Length;
                if (pad.buttonSouth.wasPressedThisFrame) ChooseBlessing(HighlightedChoice);
            }
            if ((State == RunState.Won || State == RunState.Lost) && (Keyboard.current?.rKey.wasPressedThisFrame ?? false)) Restart(false);
            if ((State == RunState.Won || State == RunState.Lost) && (pad?.buttonSouth.wasPressedThisFrame ?? false)) Restart(false);
        }
        public void EndRun(bool won)
        {
            State = won ? RunState.Won : RunState.Lost; FloorActive = false;
            Message = won ? "Heitor derrotado! Troia caiu." : RemainingTime <= 0 ? "O tempo acabou." : "O heroi caiu.";
            Time.timeScale = 1; hitStopUntil = 0; PlayerCombat.LockCursor(false);
        }
        public void TogglePause()
        {
            if (State == RunState.Paused) { State = beforePause; Time.timeScale = 1; PlayerCombat.LockCursor(true); }
            else if (IsPlaying) { beforePause = State; State = RunState.Paused; Time.timeScale = 0; PlayerCombat.LockCursor(false); }
        }
        public void HitStop(float seconds)
        {
            if (!IsPlaying || seconds <= 0) return;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds); Time.timeScale = 0;
        }
        public void Restart(bool checkpoint)
        {
            Time.timeScale = 1; hitStopUntil = 0;
            foreach (var enemy in enemies) if (enemy) Destroy(enemy.gameObject);
            foreach (var projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            enemies.Clear(); pendingSpawn = FloorActive = false;
            foreach (var m in markers.Where(m => m.kind == MarkerKind.Barrier)) SetBarrier(m, false);
            Floor = checkpoint ? checkpointFloor : 1; Kills = 0;
            RemainingTime = checkpoint ? checkpointTime : config.startingTime;
            player.Style.ResetMeter();
            if (checkpoint) player.Favor.Restore(checkpointBlessings); else { player.Favor.ResetFavor(); checkpointFloor = 1; checkpointTime = config.startingTime; checkpointBlessings.Clear(); }
            SetDestination();
            Vector3 position = startPosition; Quaternion rotation = startRotation;
            if (checkpoint && Floor > 1 && arena)
            {
                var spawn = markers.FirstOrDefault(m => m.kind == MarkerKind.EnemySpawn && m.arena == config.floors[Floor - 1].arena);
                if (spawn && NavMesh.SamplePosition(spawn.transform.position, out var hit, 5, NavMesh.AllAreas)) position = hit.position + Vector3.up * .1f;
            }
            SetGate(Floor > 10); player.Respawn(position, rotation);
            transition = 2; PlayerCombat.LockCursor(true);
        }
        void SetBarriers(int number, bool active)
        {
            foreach (var m in markers.Where(m => m.kind == MarkerKind.Barrier && m.arena == number)) SetBarrier(m, active);
        }
        static void SetBarrier(LevelMarker marker, bool active)
        {
            var collider = marker.GetComponent<Collider>(); if (collider) collider.enabled = active;
            var obstacle = marker.GetComponent<NavMeshObstacle>();
            if (!obstacle)
            {
                obstacle = marker.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = Vector3.up * marker.size.y * .5f;
                obstacle.size = marker.size; obstacle.carving = true;
            }
            obstacle.enabled = active;
            for (int i = 0; i < marker.transform.childCount; i++) marker.transform.GetChild(i).gameObject.SetActive(active);
        }
        void OpenGate() => SetGate(true);
        void SetGate(bool open)
        {
            if (gateOpen == open) return; gateOpen = open;
            var bar = GameObject.Find("Tranca_do_Portao");
            if (bar)
            {
                foreach (var c in bar.GetComponents<Collider>()) c.enabled = !open;
                foreach (var renderer in bar.GetComponents<Renderer>()) renderer.enabled = !open;
            }
            foreach (string name in new[] { "Portao_Folha_Esquerda", "Portao_Folha_Direita" })
            {
                var leaf = GameObject.Find(name); if (leaf) leaf.transform.localRotation = Quaternion.Euler(0, open ? (name.Contains("Esquerda") ? -100 : 100) : 0, 0);
            }
        }
        public Vector3 Destination => arena ? arena.transform.position : startPosition;
        public bool HasCheckpoint => checkpointFloor > 1;
        void OnDestroy() { if (Instance == this) { Instance = null; Time.timeScale = 1; PlayerCombat.LockCursor(false); } }
    }
}
