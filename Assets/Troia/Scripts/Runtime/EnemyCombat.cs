using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace Troia
{
    public enum EnemyState { Chasing, Windup, Recovery, Staggered, Airborne, Dead, Pulled }

    [RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(CharacterController))]
    public class EnemyCombat : MonoBehaviour
    {
        static readonly List<EnemyCombat> combatants = new List<EnemyCombat>();
        public static IReadOnlyList<EnemyCombat> Combatants => combatants;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearRegistry() => combatants.Clear();
        public EnemyKind kind;
        public PlayerCombat target;
        public bool trainingDummy;
        public float damageMultiplier = 1;
        public EnemySO Data { get; private set; }
        public Health Health { get; private set; }
        public EnemyState State { get; private set; }
        public bool Armored => Data && Data.armored;
        public float StateTime => timer;
        public float WindupDuration => windup;
        NavMeshAgent agent;
        CharacterController controller;
        Vector3 velocity, aim;
        float timer, windup, groundY, stuckTime;
        Vector3 lastPosition;
        bool launched;
        PlayerCombat puller;
        LineRenderer warning;
        EnemyPresentation presentation;

        void Awake()
        {
            combatants.Add(this);
            Health = GetComponent<Health>(); agent = GetComponent<NavMeshAgent>(); controller = GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .4f; controller.center = Vector3.up * .9f;
            controller.enabled = true;
            agent.radius = .4f; agent.height = 1.8f; agent.speed = 3.4f; agent.angularSpeed = 540;
            agent.acceleration = 20; agent.stoppingDistance = 1.65f;
            Health.Damaged += React; Health.Died += Die;
        }
        public void Initialize(EnemyKind type, PlayerCombat player, float healthMultiplier, float attackMultiplier) =>
            Initialize(EnemySO.Get(type), player, healthMultiplier, attackMultiplier);
        public void Initialize(EnemySO data, PlayerCombat player, float healthMultiplier, float attackMultiplier)
        {
            Data = data; kind = data.kind; target = player; damageMultiplier = attackMultiplier;
            Health.ResetHealth(data.maxHealth * healthMultiplier);
            agent.speed = data.moveSpeed; agent.stoppingDistance = data.stoppingDistance;
            presentation = GetComponent<EnemyPresentation>() ?? gameObject.AddComponent<EnemyPresentation>();
            presentation.Initialize(this);
            groundY = transform.position.y; lastPosition = transform.position;
            SetState(EnemyState.Chasing);
        }
        void Update()
        {
            if (State == EnemyState.Dead || !target || !target.CanAct) return;
            float dt = Time.deltaTime; timer -= dt; UpdateWarning();
            if (State == EnemyState.Pulled) { TickPull(dt); return; }
            if (State == EnemyState.Airborne || State == EnemyState.Staggered)
            {
                TickReaction(dt); return;
            }
            if (!agent.enabled || !agent.isOnNavMesh) { RecoverNavigation(); return; }
            if (trainingDummy) { agent.isStopped = true; return; }
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (State == EnemyState.Chasing)
            {
                agent.isStopped = false;
                agent.SetDestination(target.transform.position);
                float range = Data.attackRange;
                if (distance < range && HasSight() && CanCommitAttack()) BeginWindup();
                else if (Data.circleRadius > 0 && distance < Data.circleRadius + .55f)
                {
                    float phase=Time.time*.55f+GetInstanceID()*2.399963f;
                    agent.SetDestination(target.transform.position+new Vector3(Mathf.Cos(phase),0,Mathf.Sin(phase))*Data.circleRadius);
                }
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
                    Attack(); SetState(EnemyState.Recovery, Data.recovery);
                }
            }
            else if (State == EnemyState.Recovery && timer <= 0) SetState(EnemyState.Chasing);
        }
        public bool CanCommitAttack()
        {
            int attackers=0;
            foreach(var enemy in combatants)
                if(enemy && enemy!=this && enemy.target==target && enemy.State==EnemyState.Windup) attackers++;
            if(attackers >= 2) return false;
            if(Camera.main)
            {
                Vector3 view=Camera.main.WorldToViewportPoint(transform.position+Vector3.up);
                if(view.z<=0 || view.x<.02f || view.x>.98f || view.y<.02f || view.y>.98f) return false;
            }
            return true;
        }
        void UpdateWarning()
        {
            if(warning) warning.enabled=State==EnemyState.Windup;
            if(!warning || !warning.enabled) return;
            if(Data.ranged)
            {
                warning.positionCount=2; warning.SetPosition(0,Vector3.up*1.3f+Vector3.forward*.6f);
                warning.SetPosition(1,transform.InverseTransformPoint(aim));
                warning.startWidth=warning.endWidth=.035f; return;
            }
            warning.positionCount=25;
            float radius=Data.hitReach;
            for(int i=0;i<warning.positionCount;i++)
            {
                float angle=Mathf.Lerp(-Data.hitArc,Data.hitArc,i/(float)(warning.positionCount-1))*Mathf.Deg2Rad;
                warning.SetPosition(i,new Vector3(Mathf.Sin(angle)*radius,.065f,Mathf.Cos(angle)*radius));
            }
            warning.startWidth=warning.endWidth=Mathf.Lerp(.035f,.12f,1-Mathf.Clamp01(timer/windup));
        }
        bool HasSight()
        {
            Vector3 a = transform.position + Vector3.up * 1.2f, b = target.transform.position + Vector3.up;
            // Both actors live on Ignore Raycast; this tests level geometry only.
            return !Physics.Linecast(a, b, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        void BeginWindup()
        {
            if(!warning)
            {
                warning=new GameObject("Aviso_ataque").AddComponent<LineRenderer>(); warning.transform.SetParent(transform,false);
                warning.useWorldSpace=false; warning.positionCount=25;
                var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); material.color=new Color(1,.2f,.06f);
                warning.sharedMaterial=material; warning.gameObject.AddComponent<OwnedMaterial>().material=material;
            }
            agent.isStopped = true; Face(target.transform.position); aim = target.transform.position + Vector3.up;
            windup = Data.windup;
            SetState(EnemyState.Windup, windup);
        }
        void Face(Vector3 point)
        {
            Vector3 d = point - transform.position; d.y = 0;
            if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(d);
        }
        void Attack()
        {
            if (presentation) presentation.Strike();
            if (Data.ranged)
            {
                var go = new GameObject("Flecha"); go.transform.position = transform.position + Vector3.up * 1.3f + transform.forward * .6f;
                go.AddComponent<EnemyProjectile>().Initialize((aim - go.transform.position).normalized, target, Data.damage * damageMultiplier, gameObject, Data.projectileSpeed);
                return;
            }
            Vector3 d = target.transform.position - transform.position;
            if (d.magnitude <= Data.hitReach && Vector3.Angle(transform.forward, d) < Data.hitArc && HasSight())
                target.Health.TakeDamage(new DamageInfo { source = gameObject, damage = Data.damage * damageMultiplier, direction = d.normalized, knockback = Data.knockback, heavy = Armored });
            CombatVisuals.Burst(transform.position + transform.forward * 1.3f + Vector3.up, Color.red, .25f);
        }
        void React(DamageInfo info)
        {
            if (Health.IsDead) return;
            // Armor resists light stagger, but damage still applies.
            if (Armored && State != EnemyState.Airborne && !info.heavy && !info.aerial && info.launch <= 0 && !info.divine) return;
            if (State == EnemyState.Airborne && info.projectile)
            {
                velocity.y = Mathf.Max(velocity.y, .8f);
                return;
            }
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
            else { velocity.y = -1; SetState(EnemyState.Staggered, Mathf.Max(info.projectile ? .08f : .25f, info.stagger)); }
        }
        public bool TryPull(PlayerCombat source)
        {
            if (!source || Health.IsDead || (Armored && State != EnemyState.Staggered && State != EnemyState.Airborne)) return false;
            if (Physics.Linecast(transform.position + Vector3.up, source.transform.position + Vector3.up, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            if (agent.enabled) { if (agent.isOnNavMesh) agent.ResetPath(); agent.enabled = false; }
            puller = source; velocity = Vector3.zero; SetState(EnemyState.Pulled, .65f); return true;
        }
        void TickPull(float dt)
        {
            if (!puller || !puller.CanAct || timer <= 0) { puller = null; SetState(EnemyState.Staggered, .3f); return; }
            Vector3 delta = puller.transform.position - transform.position;
            float remaining = delta.magnitude - 1.9f;
            if (remaining <= .12f) { puller = null; velocity.y = -1; SetState(EnemyState.Staggered, .4f); return; }
            // CharacterController collisions stop the pull against walls; no teleport or navigation warp.
            controller.Move(delta.normalized * Mathf.Min(26 * dt, remaining));
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
        void SetState(EnemyState state, float duration = 0) { State = state; timer = duration; if(warning) warning.enabled=state==EnemyState.Windup; }
        void Die(DamageInfo info)
        {
            SetState(EnemyState.Dead); agent.enabled = false; controller.enabled = false;
            GameManager.Instance?.EnemyKilled(this, info);
            CombatVisuals.Burst(transform.position + Vector3.up, new Color(.85f, .2f, .1f));
            combatants.Remove(this);   // o corpo ainda cai na tela, mas já não conta como alvo
            Destroy(gameObject, presentation && presentation.TemMorte ? 1.8f : .1f);
        }
        void OnGUI()
        {
            if (!Health || Health.IsDead || !Camera.main) return;
            Vector3 p = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * (2.25f * (Data ? Data.scale : 1)));
            if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height) return;
            GUI.color = State == EnemyState.Windup ? Color.red : Color.white;
            GUI.Box(new Rect(p.x - 32, Screen.height - p.y, 64, 8), "");
            GUI.DrawTexture(new Rect(p.x - 30, Screen.height - p.y + 2, 60 * Health.Current / Health.maximum, 4), Texture2D.whiteTexture);
            if (Armored) GUI.Label(new Rect(p.x - 50, Screen.height - p.y - 23, 140, 24), Data.displayName);
            GUI.color = Color.white;
        }
        void OnDestroy()
        {
            combatants.Remove(this);
            if (Health) { Health.Damaged -= React; Health.Died -= Die; }
        }
    }

    public class EnemyProjectile : MonoBehaviour
    {
        Vector3 direction; PlayerCombat target; GameObject shooter; float damage, speed = 16, lifetime = 4;
        public void Initialize(Vector3 d, PlayerCombat player, float amount, GameObject attacker = null, float velocity = 16)
        {
            direction = d; target = player; damage = amount; shooter = attacker; speed = velocity;
            transform.rotation = Quaternion.LookRotation(d);
            CombatVisuals.Part(transform, "Haste", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.03f, .35f, .03f), new Color(.45f, .3f, .15f)).localRotation = Quaternion.Euler(90, 0, 0);
            CombatVisuals.Part(transform, "Ponta", PrimitiveType.Sphere, Vector3.forward * .36f, new Vector3(.07f, .07f, .14f), new Color(.8f, .6f, .25f));
        }
        void Update()
        {
            if (!target || !target.CanAct) return;
            float step = speed * Time.deltaTime; Vector3 next = transform.position + direction * step;
            // Segment test against the player capsule also catches fast arrows between frames.
            Vector3 center = target.transform.position + Vector3.up;
            Vector3 closest = transform.position + direction * Mathf.Clamp(Vector3.Dot(center - transform.position, direction), 0, step);
            if ((closest - center).sqrMagnitude < .7f * .7f)
            {
                if (!Physics.Linecast(transform.position, closest, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    target.Health.TakeDamage(new DamageInfo { source = shooter, damage = damage, direction = direction, knockback = 2, projectile = true });
                Destroy(gameObject); return;
            }
            if (Physics.Linecast(transform.position, next, ~(1 << 2), QueryTriggerInteraction.Ignore)) { Destroy(gameObject); return; }
            transform.position = next; lifetime -= Time.deltaTime;
            if (lifetime <= 0) Destroy(gameObject);
        }
    }
}
