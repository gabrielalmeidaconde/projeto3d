using UnityEngine;
using UnityEngine.AI;

namespace Troia
{
    public enum EnemyState { Chasing, Windup, Recovery, Staggered, Airborne, Dead }

    [RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(CharacterController))]
    public class EnemyCombat : MonoBehaviour
    {
        public EnemyKind kind;
        public PlayerCombat target;
        public float damageMultiplier = 1;
        public Health Health { get; private set; }
        public EnemyState State { get; private set; }
        public bool Armored => kind == EnemyKind.Captain || kind == EnemyKind.Hector;
        NavMeshAgent agent;
        CharacterController controller;
        Vector3 velocity, aim;
        float timer, windup, groundY, stuckTime;
        Vector3 lastPosition;
        Renderer body;
        Transform weapon;
        bool launched;

        void Awake()
        {
            Health = GetComponent<Health>(); agent = GetComponent<NavMeshAgent>(); controller = GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .4f; controller.center = Vector3.up * .9f;
            controller.enabled = true;
            agent.radius = .4f; agent.height = 1.8f; agent.speed = 3.4f; agent.angularSpeed = 540;
            agent.acceleration = 20; agent.stoppingDistance = 1.65f;
            Health.Damaged += React; Health.Died += Die;
        }
        public void Initialize(EnemyKind type, PlayerCombat player, float healthMultiplier, float attackMultiplier)
        {
            kind = type; target = player; damageMultiplier = attackMultiplier;
            Health.ResetHealth((kind == EnemyKind.Hector ? 650 : kind == EnemyKind.Captain ? 220 : kind == EnemyKind.Archer ? 38 : 55) * healthMultiplier);
            Color color = kind == EnemyKind.Hector ? new Color(.65f, .1f, .18f) : Armored ? new Color(.55f, .45f, .15f) : kind == EnemyKind.Archer ? new Color(.2f, .5f, .3f) : new Color(.55f, .25f, .18f);
            body = CombatVisuals.Part(transform, "Corpo", PrimitiveType.Capsule, Vector3.up * .9f, new Vector3(.8f, .9f, .8f), color).GetComponent<Renderer>();
            weapon = CombatVisuals.Weapon(transform, new Color(.6f, .6f, .65f));
            if (Armored) CombatVisuals.Part(transform, "Elmo", PrimitiveType.Cube, Vector3.up * 1.85f, new Vector3(.5f, .25f, .5f), Color.yellow);
            if (kind == EnemyKind.Archer) agent.stoppingDistance = 9;
            groundY = transform.position.y; lastPosition = transform.position;
            SetState(EnemyState.Chasing);
        }
        void Update()
        {
            if (State == EnemyState.Dead || !target || !target.CanAct) return;
            float dt = Time.deltaTime; timer -= dt;
            if (State == EnemyState.Airborne || State == EnemyState.Staggered)
            {
                TickReaction(dt); return;
            }
            if (!agent.enabled || !agent.isOnNavMesh) { RecoverNavigation(); return; }
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (State == EnemyState.Chasing)
            {
                agent.isStopped = false;
                agent.SetDestination(target.transform.position);
                float range = kind == EnemyKind.Archer ? 13 : kind == EnemyKind.Hector ? 3 : 2.15f;
                if (distance < range && HasSight()) BeginWindup();
                if ((transform.position - lastPosition).sqrMagnitude < .0025f && distance > range) stuckTime += dt;
                else stuckTime = 0;
                lastPosition = transform.position;
                // Re-evaluate paths rather than leaving an unreachable enemy permanently idle.
                if (stuckTime > 3) { agent.ResetPath(); stuckTime = 0; }
            }
            else if (State == EnemyState.Windup)
            {
                if (timer > windup * .35f) Face(target.transform.position);
                if (timer <= 0)
                {
                    Attack(); SetState(EnemyState.Recovery, Armored ? .85f : 1.1f);
                }
            }
            else if (State == EnemyState.Recovery && timer <= 0) SetState(EnemyState.Chasing);
            if (body) body.transform.localScale = new Vector3(.8f, .9f, .8f) * (State == EnemyState.Windup ? 1.08f : 1);
            if (weapon) weapon.localRotation = Quaternion.Euler(0, State == EnemyState.Windup ? -75 : State == EnemyState.Recovery ? 80 : 0, 0);
        }
        bool HasSight()
        {
            Vector3 a = transform.position + Vector3.up * 1.2f, b = target.transform.position + Vector3.up;
            // Both actors live on Ignore Raycast; this tests level geometry only.
            return !Physics.Linecast(a, b, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        void BeginWindup()
        {
            agent.isStopped = true; Face(target.transform.position); aim = target.transform.position + Vector3.up;
            windup = kind == EnemyKind.Archer ? .95f : kind == EnemyKind.Hector ? .8f : .65f;
            SetState(EnemyState.Windup, windup);
        }
        void Face(Vector3 point)
        {
            Vector3 d = point - transform.position; d.y = 0;
            if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(d);
        }
        void Attack()
        {
            if (kind == EnemyKind.Archer)
            {
                var go = new GameObject("Flecha"); go.transform.position = transform.position + Vector3.up * 1.3f + transform.forward * .6f;
                go.AddComponent<EnemyProjectile>().Initialize((aim - go.transform.position).normalized, target, 10 * damageMultiplier);
                return;
            }
            float range = kind == EnemyKind.Hector ? 3.5f : 2.5f;
            Vector3 d = target.transform.position - transform.position;
            if (d.magnitude <= range && Vector3.Angle(transform.forward, d) < 65 && HasSight())
                target.Health.TakeDamage(new DamageInfo { source = gameObject, damage = (kind == EnemyKind.Hector ? 24 : Armored ? 18 : 10) * damageMultiplier, direction = d.normalized, knockback = 4, heavy = Armored });
            CombatVisuals.Burst(transform.position + transform.forward * 1.3f + Vector3.up, Color.red, .25f);
        }
        void React(DamageInfo info)
        {
            if (Health.IsDead) return;
            // Armor resists light stagger, but damage still applies.
            if (Armored && !info.heavy && !info.aerial && info.launch <= 0 && !info.divine) return;
            if (agent.enabled) { if (agent.isOnNavMesh) agent.ResetPath(); agent.enabled = false; }
            controller.enabled = true;
            velocity = info.direction * info.knockback;
            if (info.launch > 0)
            {
                velocity.y = info.launch; launched = true; SetState(EnemyState.Airborne, .25f);
            }
            else if (State == EnemyState.Airborne && info.aerial && !info.heavy)
            {
                velocity.y = 3.5f; SetState(EnemyState.Airborne, .2f);
            }
            else { velocity.y = -1; SetState(EnemyState.Staggered, Mathf.Max(.25f, info.stagger)); }
        }
        void TickReaction(float dt)
        {
            velocity.y -= 20 * dt;
            controller.Move(velocity * dt);
            velocity.x = Mathf.MoveTowards(velocity.x, 0, dt * 8); velocity.z = Mathf.MoveTowards(velocity.z, 0, dt * 8);
            bool landed = controller.isGrounded || (transform.position.y <= groundY && velocity.y <= 0);
            if (State == EnemyState.Airborne && landed && timer <= 0)
            {
                if (launched) CombatVisuals.Burst(transform.position, new Color(.7f, .5f, .3f));
                launched = false; SetState(EnemyState.Staggered, .45f);
            }
            else if (State == EnemyState.Staggered && timer <= 0 && landed) RecoverNavigation();
            if (transform.position.y < -20) Health.TakeDamage(new DamageInfo { source = target.gameObject, damage = 99999 });
        }
        void RecoverNavigation()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 5, NavMesh.AllAreas))
            {
                controller.enabled = false; transform.position = hit.position; controller.enabled = true;
                agent.enabled = true; agent.Warp(hit.position); groundY = hit.position.y;
                SetState(EnemyState.Chasing);
            }
        }
        void SetState(EnemyState state, float duration = 0) { State = state; timer = duration; }
        void Die(DamageInfo info)
        {
            SetState(EnemyState.Dead); agent.enabled = false; controller.enabled = false;
            GameManager.Instance?.EnemyKilled(this, info);
            CombatVisuals.Burst(transform.position + Vector3.up, new Color(.85f, .2f, .1f));
            Destroy(gameObject, .1f);
        }
        void OnGUI()
        {
            if (!Health || Health.IsDead || !Camera.main) return;
            Vector3 p = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.25f);
            if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height) return;
            GUI.color = State == EnemyState.Windup ? Color.red : Color.white;
            GUI.Box(new Rect(p.x - 32, Screen.height - p.y, 64, 8), "");
            GUI.DrawTexture(new Rect(p.x - 30, Screen.height - p.y + 2, 60 * Health.Current / Health.maximum, 4), Texture2D.whiteTexture);
            if (Armored) GUI.Label(new Rect(p.x - 50, Screen.height - p.y - 23, 140, 24), kind == EnemyKind.Hector ? "HEITOR" : "CAPITAO");
            GUI.color = Color.white;
        }
    }

    public class EnemyProjectile : MonoBehaviour
    {
        Vector3 direction; PlayerCombat target; float damage, lifetime = 4;
        public void Initialize(Vector3 d, PlayerCombat player, float amount)
        {
            direction = d; target = player; damage = amount;
            CombatVisuals.Part(transform, "Ponta", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .14f, Color.yellow);
        }
        void Update()
        {
            if (!target || !target.CanAct) return;
            float step = 16 * Time.deltaTime; Vector3 next = transform.position + direction * step;
            // Segment test against the player capsule also catches fast arrows between frames.
            Vector3 center = target.transform.position + Vector3.up;
            Vector3 closest = transform.position + direction * Mathf.Clamp(Vector3.Dot(center - transform.position, direction), 0, step);
            if ((closest - center).sqrMagnitude < .7f * .7f)
            {
                if (!Physics.Linecast(transform.position, closest, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    target.Health.TakeDamage(new DamageInfo { damage = damage, direction = direction, knockback = 2 });
                Destroy(gameObject); return;
            }
            if (Physics.Linecast(transform.position, next, ~(1 << 2), QueryTriggerInteraction.Ignore)) { Destroy(gameObject); return; }
            transform.position = next; lifetime -= Time.deltaTime;
            if (lifetime <= 0) Destroy(gameObject);
        }
    }
}
