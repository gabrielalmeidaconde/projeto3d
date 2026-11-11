using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Troia
{
    [RequireComponent(typeof(CharacterController), typeof(Health), typeof(CombatEvents))]
    public class PlayerCombat : MonoBehaviour
    {
        public GameplayConfig config;
        public Transform cameraTransform;
        public Animator animator;
        [Tooltip("Ative quando os clips tiverem eventos Open/CloseHitbox e Open/CloseComboWindow.")]
        public bool useAnimationEvents;
        public bool readInput = true;
        public PlayerState State { get; private set; }
        public ComboSO CurrentAttack { get; private set; }
        public bool IsGrounded => controller && controller.isGrounded;
        public bool Invulnerable => State == PlayerState.Dodging && stateTime < config.dodgeDuration * .8f;
        public float AttackTime => attackTime;
        public StyleMeter Style { get; private set; }
        public DivineFavor Favor { get; private set; }
        public Health Health { get; private set; }
        public CombatEvents Events { get; private set; }
        public bool CanAct => Health && !Health.IsDead && (!GameManager.Instance || GameManager.Instance.IsPlaying);
        CharacterController controller;
        readonly HashSet<Health> hitTargets = new HashSet<Health>();
        readonly List<Collider> ghostColliders = new List<Collider>();
        float ghostUntil;
        Vector3 velocity, dodgeDirection, moveDirection;
        float yaw, pitch = 16, cameraDistance = 6, stateTime, attackTime, dodgeReady, bufferExpiry;
        float heavyPressed = -1, lastGrounded = -10, jumpQueued = -10, graceUntil;
        AttackInput? buffered;
        ComboSO preserved;
        float preservedTime, preserveUntil;
        bool hitWindow, comboWindow, heavyConsumed, slamLanded;
        Transform weapon;
        float shake;

        void Awake()
        {
            controller = GetComponent<CharacterController>(); Health = GetComponent<Health>();
            Events = GetComponent<CombatEvents>();
            Style = GetComponent<StyleMeter>() ?? gameObject.AddComponent<StyleMeter>();
            Favor = GetComponent<DivineFavor>() ?? gameObject.AddComponent<DivineFavor>();
            Health.CanReceive = info => Time.time >= graceUntil && Favor.AllowDamage(info) && !Invulnerable;
            Health.Damaged += OnDamaged; Health.Died += OnDeath;
            yaw = transform.eulerAngles.y;
        }
        void Start()
        {
            if (config) Health.ResetHealth(config.playerHealth);
            if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
            weapon = CombatVisuals.Weapon(transform, new Color(.85f, .85f, .7f));
            LockCursor(true);
        }
        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked;
        }
        void Update()
        {
            if (!config) return;
            if (ghostColliders.Count > 0 && Time.time >= ghostUntil) EndGhost();
            if (readInput) ReadInput();
            if (!CanAct) return;
            float dt = Time.deltaTime;
            Style.Tick(dt); stateTime += dt;
            if (IsGrounded) { lastGrounded = Time.time; if (velocity.y < 0) velocity.y = -2; }
            if (jumpQueued > Time.time - .15f && Time.time - lastGrounded < .12f && State != PlayerState.Dodging)
            {
                velocity.y = config.jumpSpeed; jumpQueued = -10; lastGrounded = -10;
            }
            if (State == PlayerState.Attacking) TickAttack(dt);
            if (State == PlayerState.Dodging && stateTime >= config.dodgeDuration) SetState(PlayerState.Locomotion);
            if (State == PlayerState.Staggered && stateTime > .3f) SetState(PlayerState.Locomotion);
            if (State == PlayerState.Locomotion && buffered.HasValue && Time.time <= bufferExpiry)
            {
                var input = buffered.Value; buffered = null; RequestAttack(input);
            }
            if (buffered.HasValue && Time.time > bufferExpiry) buffered = null;
            Vector3 horizontal = Vector3.zero;
            if (State == PlayerState.Locomotion)
            {
                horizontal = moveDirection * (sprinting ? config.sprintSpeed : config.moveSpeed);
                if (moveDirection.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDirection), 18 * dt);
            }
            else if (State == PlayerState.Dodging) horizontal = dodgeDirection * config.dodgeSpeed;
            else if (State == PlayerState.Attacking && CurrentAttack)
                horizontal = transform.forward * (CurrentAttack.slam ? 0 : CurrentAttack.lunge);
            velocity.y -= config.gravity * dt;
            if (CurrentAttack && CurrentAttack.slam && State == PlayerState.Attacking) velocity.y = -23;
            controller.Move((horizontal + velocity) * dt);
            velocity.x = Mathf.MoveTowards(velocity.x, 0, 20 * dt); velocity.z = Mathf.MoveTowards(velocity.z, 0, 20 * dt);
            if (transform.position.y < -25) Health.TakeDamage(new DamageInfo { damage = 99999 });
            PoseWeapon();
        }
        bool sprinting;
        void ReadInput()
        {
            var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
            if ((k?.escapeKey.wasPressedThisFrame ?? false) || (g?.startButton.wasPressedThisFrame ?? false))
            {
                if (GameManager.Instance) GameManager.Instance.TogglePause();
            }
            if (!CanAct) { heavyPressed = -1; moveDirection = Vector3.zero; return; }
            Vector2 move = k == null ? Vector2.zero : new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            if (g != null && g.leftStick.ReadValue().sqrMagnitude > move.sqrMagnitude) move = g.leftStick.ReadValue();
            SetMoveInput(move, yaw, (k?.leftShiftKey.isPressed ?? false) || (g?.leftStickButton.isPressed ?? false));
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = m != null ? m.delta.ReadValue() * .12f : Vector2.zero;
                if (g != null) look += g.rightStick.ReadValue() * (140 * Time.unscaledDeltaTime);
                yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -25, 65);
            }
            if (m != null) cameraDistance = Mathf.Clamp(cameraDistance - m.scroll.ReadValue().y * .008f, 3, 10);
            if ((k?.spaceKey.wasPressedThisFrame ?? false) || (g?.buttonSouth.wasPressedThisFrame ?? false)) RequestJump();
            if ((k?.leftCtrlKey.wasPressedThisFrame ?? false) || (g?.buttonEast.wasPressedThisFrame ?? false)) RequestDodge(moveDirection);
            if ((m?.leftButton.wasPressedThisFrame ?? false) || (k?.jKey.wasPressedThisFrame ?? false) || (g?.buttonWest.wasPressedThisFrame ?? false)) RequestAttack(AttackInput.Light);
            bool press = (m?.rightButton.wasPressedThisFrame ?? false) || (k?.kKey.wasPressedThisFrame ?? false) || (g?.buttonNorth.wasPressedThisFrame ?? false);
            bool held = (m?.rightButton.isPressed ?? false) || (k?.kKey.isPressed ?? false) || (g?.buttonNorth.isPressed ?? false);
            if (press)
            {
                heavyPressed = Time.time; heavyConsumed = false;
                if (!IsGrounded) { RequestAttack(AttackInput.Heavy); heavyConsumed = true; }
            }
            if (held && !heavyConsumed && heavyPressed >= 0 && Time.time - heavyPressed >= config.heavyHold)
            { RequestLauncher(); heavyConsumed = true; }
            if (!held && heavyPressed >= 0)
            {
                if (!heavyConsumed) RequestAttack(AttackInput.Heavy);
                heavyPressed = -1;
            }
            if ((k?.qKey.wasPressedThisFrame ?? false) || (g?.rightShoulder.wasPressedThisFrame ?? false)) Favor.Activate();
            if ((k?.tabKey.wasPressedThisFrame ?? false) || (g?.dpad.right.wasPressedThisFrame ?? false)) Favor.Cycle();
        }
        public void RequestJump() { if (CanAct) jumpQueued = Time.time; }
        public void SetMoveInput(Vector2 input, float cameraYaw, bool sprint)
        {
            input = Vector2.ClampMagnitude(input, 1);
            moveDirection = Quaternion.Euler(0, cameraYaw, 0) * new Vector3(input.x, 0, input.y);
            sprinting = sprint;
        }
        ComboSO BaseAttack(AttackInput input) => input == AttackInput.Heavy ? (IsGrounded ? config.heavy : config.slam) : (IsGrounded ? config.light : config.airLight);
        public bool RequestAttack(AttackInput input)
        {
            if (!CanAct || State == PlayerState.Staggered) return false;
            if (State == PlayerState.Locomotion)
            {
                if (preserved && Time.time < preserveUntil && input == AttackInput.Light)
                {
                    var next = ResolveNext(preserved, preservedTime); preserved = null;
                    BeginAttack(next ? next : BaseAttack(input));
                }
                else BeginAttack(BaseAttack(input));
                return true;
            }
            buffered = input; bufferExpiry = Time.time + config.inputBuffer;
            return true;
        }
        public bool RequestLauncher()
        {
            if (!CanAct || State != PlayerState.Locomotion || !IsGrounded) return false;
            BeginAttack(config.launcher); return true;
        }
        public bool RequestDodge(Vector3 direction)
        {
            if (!CanAct || Time.time < dodgeReady || State == PlayerState.Staggered || State == PlayerState.Dodging) return false;
            preserved = CurrentAttack; preservedTime = attackTime; preserveUntil = Time.time + .8f;
            CurrentAttack = null; buffered = null; hitWindow = comboWindow = false;
            dodgeDirection = direction.sqrMagnitude > .01f ? direction.normalized : transform.forward;
            dodgeDirection.y = 0; dodgeReady = Time.time + config.dodgeCooldown;
            SetState(PlayerState.Dodging); Events.Dodge(); return true;
        }
        public void GhostFor(float seconds)
        {
            ghostUntil = Time.time + seconds;
            foreach (var enemy in FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None))
            {
                var collider = enemy.GetComponent<CharacterController>();
                if (!collider || ghostColliders.Contains(collider) || Physics.GetIgnoreCollision(controller, collider)) continue;
                Physics.IgnoreCollision(controller, collider, true); ghostColliders.Add(collider);
            }
        }
        void EndGhost()
        {
            foreach (var collider in ghostColliders) if (collider && controller) Physics.IgnoreCollision(controller, collider, false);
            ghostColliders.Clear();
        }
        void BeginAttack(ComboSO move)
        {
            if (!move) return;
            CurrentAttack = move; attackTime = 0; buffered = null; hitTargets.Clear(); slamLanded = false;
            hitWindow = comboWindow = false; SetState(PlayerState.Attacking);
            // Gentle assist only at attack start; never retarget during an active swing.
            EnemyCombat closest = null; float best = 4.5f;
            foreach (var enemy in FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None))
            {
                Vector3 delta = enemy.transform.position - transform.position; delta.y = 0;
                if (!enemy.Health.IsDead && delta.magnitude < best && Vector3.Angle(transform.forward, delta) < 70)
                { closest = enemy; best = delta.magnitude; }
            }
            if (closest)
            {
                Vector3 d = closest.transform.position - transform.position; d.y = 0;
                if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(d);
            }
            if (animator && !string.IsNullOrEmpty(move.animationState)) animator.CrossFade(move.animationState, .08f);
            if (move.aerial && !move.slam) velocity.y = Mathf.Max(velocity.y, 2.8f);
        }
        public static ComboSO ResolveNext(ComboSO move, float elapsed) => move.delayedNext && elapsed >= move.delayedThreshold ? move.delayedNext : move.next;
        void TickAttack(float dt)
        {
            float previous = attackTime; attackTime += dt * Favor.AttackSpeed;
            var move = CurrentAttack;
            if (!move) { SetState(PlayerState.Locomotion); return; }
            // Swept time intervals prevent a low frame rate from skipping a complete hit window.
            if (move.slam)
            {
                if (IsGrounded && !slamLanded && attackTime > .08f)
                { slamLanded = true; Strike(move, true); attackTime = Mathf.Max(attackTime, move.hitEnd); }
                if (!slamLanded) attackTime = Mathf.Min(attackTime, move.hitStart);
            }
            else if (hitWindow || (!useAnimationEvents && attackTime >= move.hitStart && previous <= move.hitEnd)) Strike(move, false);
            bool open = comboWindow || (!useAnimationEvents && attackTime >= move.comboOpen && attackTime <= move.comboClose);
            if (open && buffered.HasValue && Time.time <= bufferExpiry)
            {
                var input = buffered.Value;
                var next = input == AttackInput.Light ? ResolveNext(move, attackTime) : BaseAttack(input);
                if (next) { BeginAttack(next); return; }
            }
            if (attackTime >= move.duration) { CurrentAttack = null; SetState(PlayerState.Locomotion); }
        }
        void Strike(ComboSO move, bool area)
        {
            Vector3 origin = transform.position + Vector3.up;
            foreach (var collider in Physics.OverlapSphere(origin, move.range, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<Health>();
                if (!target || target == Health || target.IsDead || hitTargets.Contains(target) || !target.GetComponent<EnemyCombat>()) continue;
                Vector3 delta = target.transform.position + Vector3.up - origin;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (!area && Vector3.Angle(transform.forward, flat) > move.angle * .5f) continue;
                if (Physics.Linecast(origin, target.transform.position + Vector3.up, out var block, ~(1 << 2), QueryTriggerInteraction.Ignore) && block.collider.GetComponentInParent<Health>() != target) continue;
                var info = new DamageInfo { source = gameObject, damage = move.damage * Favor.DamageMultiplier, direction = flat.normalized,
                    knockback = move.knockback, launch = move.launch, stagger = .4f, heavy = move.input == AttackInput.Heavy,
                    aerial = move.aerial, finisher = move.finisher, moveId = move.id };
                if (!target.TakeDamage(info)) continue;
                hitTargets.Add(target); Style.RegisterHit(move.id, move.finisher); Favor.Gain(2 * Style.Multiplier);
                Events.Hit(info, target); CombatVisuals.Burst(target.transform.position + Vector3.up, new Color(1, .7f, .2f));
                shake = .16f; GameManager.Instance?.HitStop(.045f);
            }
            if (area) CombatVisuals.Burst(transform.position + Vector3.up * .1f, new Color(1, .5f, .1f), move.range);
        }
        // Animation events can drive the same windows when an authored Animator is supplied.
        public void OpenHitbox() => hitWindow = true;
        public void CloseHitbox() => hitWindow = false;
        public void OpenComboWindow() => comboWindow = true;
        public void CloseComboWindow() => comboWindow = false;
        void SetState(PlayerState state) { State = state; stateTime = 0; }
        void OnDamaged(DamageInfo info)
        {
            Style.TakeHit(); CurrentAttack = preserved = null; buffered = null; heavyPressed = -1;
            velocity += info.direction * info.knockback; graceUntil = Time.time + .55f;
            SetState(PlayerState.Staggered); shake = .3f;
        }
        void OnDeath(DamageInfo info) { CurrentAttack = null; SetState(PlayerState.Dead); GameManager.Instance?.EndRun(false); }
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            EndGhost(); moveDirection = Vector3.zero;
            controller.enabled = false; transform.SetPositionAndRotation(position, rotation); controller.enabled = true;
            velocity = Vector3.zero; CurrentAttack = preserved = null; buffered = null; heavyPressed = -1;
            Health.ResetHealth(config.playerHealth); graceUntil = Time.time + 1; dodgeReady = 0;
            yaw = rotation.eulerAngles.y; SetState(PlayerState.Locomotion);
        }
        void PoseWeapon()
        {
            if (!weapon) return;
            float t = CurrentAttack ? Mathf.Clamp01(attackTime / CurrentAttack.duration) : 0;
            weapon.localRotation = CurrentAttack ? Quaternion.Euler(CurrentAttack.slam ? t * 180 : 0, Mathf.Lerp(-100, 110, t), -15) : Quaternion.Euler(0, 15, -15);
        }
        void LateUpdate()
        {
            if (!cameraTransform) return;
            Vector3 pivot = transform.position + Vector3.up * 1.5f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 backward = rotation * Vector3.back;
            float distance = cameraDistance;
            if (Physics.SphereCast(pivot, .23f, backward, out var hit, distance, ~(1 << 2), QueryTriggerInteraction.Ignore)) distance = Mathf.Max(.4f, hit.distance - .1f);
            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime);
            cameraTransform.SetPositionAndRotation(pivot + backward * distance + Random.insideUnitSphere * shake * .15f, rotation);
        }
        void OnDestroy() { EndGhost(); if (Health) { Health.Damaged -= OnDamaged; Health.Died -= OnDeath; } }
    }
}
