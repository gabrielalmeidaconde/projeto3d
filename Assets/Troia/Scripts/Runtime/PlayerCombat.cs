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
        public bool useAnimationEvents;
        public bool readInput = true;
        public PlayerState State { get; private set; }
        public ComboSO CurrentAttack { get; private set; }
        public StyleMeter Style { get; private set; }
        public DivineFavor Favor { get; private set; }
        public Health Health { get; private set; }
        public CombatEvents Events { get; private set; }
        public PlayerTargeting Targeting { get; private set; }
        public WeaponSO Weapon => config && config.weapons != null && config.weapons.Length > 0 ? config.weapons[WeaponIndex] : null;
        public int WeaponIndex { get; private set; }
        public int PendingWeapon => pendingWeapon;
        public int JumpsUsed { get; private set; }
        public int ComboDepth { get; private set; }
        public float AttackTime => attackTime;
        public float ShotCooldown => Mathf.Max(0, shotReady - Time.time);
        public float ApproachCooldown => Weapon && utilityReady.TryGetValue(Weapon.id, out var ready) ? Mathf.Max(0, ready - Time.time) : 0;
        public float RecoveryRemaining => Mathf.Max(0, offenseReady - Time.time);
        public float MoveCooldown(ComboSO move) => move && moveReady.TryGetValue(move, out var ready) ? Mathf.Max(0, ready - Time.time) : 0;
        readonly Dictionary<ComboSO, float> moveReady = new Dictionary<ComboSO, float>();
        readonly Dictionary<string, float> utilityReady = new Dictionary<string, float>();
        float offenseReady, lastShot = -10;
        int gunBurst;
        public bool IsGrounded => controller && controller.isGrounded;
        public bool Invulnerable => State == PlayerState.Dodging && stateTime < config.dodgeDuration * .8f;
        public bool CanAct => Health && !Health.IsDead && (!GameManager.Instance || GameManager.Instance.IsPlaying);
        public event System.Action<WeaponSO> WeaponChanged;
        CharacterController controller;
        PlayerPresentation presentation;
        readonly HashSet<Health> hitTargets = new HashSet<Health>();
        readonly List<Collider> ghostColliders = new List<Collider>();
        readonly Collider[] overlap = new Collider[96];
        Vector3 velocity, dodgeDirection, moveDirection;
        EnemyCombat rushTarget;
        float yaw, pitch = 16, cameraDistance = 6, stateTime, attackTime, dodgeReady, shotReady;
        float bufferExpiry, heavyPressed = -1, lastGrounded = -10, jumpQueued = -10, graceUntil, ghostUntil;
        float preservedTime, preserveUntil, swapUntil, shake, rushDuration;
        int pendingWeapon = -1, pulse, sequence, actionCounter;
        AttackInput? buffered;
        ComboSO preserved, swapContinuation;
        bool hitWindow, comboWindow, heavyConsumed, slamLanded, sprinting, rushAirborne;

        void Awake()
        {
            controller = GetComponent<CharacterController>(); Health = GetComponent<Health>();
            Events = GetComponent<CombatEvents>();
            Style = GetComponent<StyleMeter>() ?? gameObject.AddComponent<StyleMeter>();
            Favor = GetComponent<DivineFavor>() ?? gameObject.AddComponent<DivineFavor>();
            Targeting = GetComponent<PlayerTargeting>() ?? gameObject.AddComponent<PlayerTargeting>();
            Health.CanReceive = info => Time.time >= graceUntil && Favor.AllowDamage(info) && !Invulnerable;
            Health.Damaged += OnDamaged; Health.Died += OnDeath;
            yaw = transform.eulerAngles.y;
        }
        void Start()
        {
            if (config)
            {
                if (config.weapons == null || config.weapons.Length == 0) ArsenalDefaults.Populate(config);
                Health.ResetHealth(config.playerHealth); Targeting.range = config.lockRange;
            }
            if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
            presentation = GetComponent<PlayerPresentation>() ?? gameObject.AddComponent<PlayerPresentation>();
            presentation.Initialize(this);
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
            if (IsGrounded && velocity.y <= 0)
            {
                lastGrounded = Time.time; velocity.y = -2; JumpsUsed = 0;
            }
            TickJump();
            if (State == PlayerState.Attacking) TickAttack(dt);
            if (State == PlayerState.Dodging && stateTime >= config.dodgeDuration) SetState(PlayerState.Locomotion);
            if (State == PlayerState.Staggered && stateTime > .3f) SetState(PlayerState.Locomotion);
            if (State == PlayerState.Locomotion && buffered.HasValue && Time.time <= bufferExpiry)
            {
                float originalExpiry = bufferExpiry;
                var input = buffered.Value; buffered = null; RequestAttack(input);
                if (buffered.HasValue) bufferExpiry = originalExpiry;
            }
            if (buffered.HasValue && Time.time > bufferExpiry) buffered = null;
            if (State == PlayerState.Rushing) { TickRush(dt); return; }

            Vector3 horizontal = Vector3.zero;
            if (State == PlayerState.Locomotion)
            {
                horizontal = moveDirection * (sprinting ? config.sprintSpeed : config.moveSpeed);
                Vector3 facing = Targeting.IsLocked ? Targeting.LockedTarget.transform.position - transform.position : moveDirection;
                facing.y = 0;
                if (facing.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), 18 * dt);
            }
            else if (State == PlayerState.Dodging) horizontal = dodgeDirection * config.dodgeSpeed;
            else if (State == PlayerState.Attacking && CurrentAttack)
                horizontal = transform.forward * (CurrentAttack.slam ? 0 : CurrentAttack.lunge);

            float gravity = State == PlayerState.Attacking && CurrentAttack && CurrentAttack.aerial && !CurrentAttack.slam ? .55f : 1;
            velocity.y -= config.gravity * gravity * dt;
            if (CurrentAttack && CurrentAttack.slam && State == PlayerState.Attacking && !slamLanded) velocity.y = -23;
            controller.Move((horizontal + velocity) * dt);
            velocity.x = Mathf.MoveTowards(velocity.x, 0, 20 * dt); velocity.z = Mathf.MoveTowards(velocity.z, 0, 20 * dt);
            if (transform.position.y < -25) Health.TakeDamage(new DamageInfo { damage = 99999 });
        }
        void TickJump()
        {
            if (jumpQueued <= Time.time - .15f || State == PlayerState.Dodging || State == PlayerState.Staggered) return;
            bool groundJump = JumpsUsed == 0 && Time.time - lastGrounded < .12f;
            if (!groundJump && (IsGrounded || JumpsUsed >= 2)) return;
            JumpsUsed = groundJump ? 1 : Mathf.Max(1, JumpsUsed) + 1;
            velocity.y = groundJump ? config.jumpSpeed : config.doubleJumpSpeed;
            jumpQueued = lastGrounded = -10;
            rushTarget = null;
            CancelAttack(); SetState(PlayerState.Locomotion);
            presentation?.Jump(JumpsUsed == 2);
        }
        void ReadInput()
        {
            var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
            if ((k?.escapeKey.wasPressedThisFrame ?? false) || (g?.startButton.wasPressedThisFrame ?? false)) GameManager.Instance?.TogglePause();
            if (!CanAct) { heavyPressed = -1; moveDirection = Vector3.zero; return; }
            Vector2 input = k == null ? Vector2.zero : new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            if (g != null && g.leftStick.ReadValue().sqrMagnitude > input.sqrMagnitude) input = g.leftStick.ReadValue();
            SetMoveInput(input, yaw, (k?.leftShiftKey.isPressed ?? false) || (g?.leftStickButton.isPressed ?? false));
            if (Cursor.lockState == CursorLockMode.Locked && !Targeting.IsLocked)
            {
                Vector2 look = m != null ? m.delta.ReadValue() * .12f : Vector2.zero;
                if (g != null) look += g.rightStick.ReadValue() * (140 * Time.unscaledDeltaTime);
                yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -25, 65);
            }
            if (m != null) cameraDistance = Mathf.Clamp(cameraDistance - m.scroll.ReadValue().y * .008f, 3, 10);
            Vector3 viewForward = cameraTransform ? cameraTransform.forward : transform.forward;
            if ((k?.fKey.wasPressedThisFrame ?? false) || (m?.middleButton.wasPressedThisFrame ?? false) || (g?.leftTrigger.wasPressedThisFrame ?? false)) Targeting.Toggle(viewForward);
            if ((k?.tKey.wasPressedThisFrame ?? false) || (g?.rightStickButton.wasPressedThisFrame ?? false)) Targeting.Cycle(viewForward);
            if ((k?.digit1Key.wasPressedThisFrame ?? false)) SelectWeapon(0);
            if ((k?.digit2Key.wasPressedThisFrame ?? false)) SelectWeapon(1);
            if ((k?.digit3Key.wasPressedThisFrame ?? false)) SelectWeapon(2);
            if (g?.dpad.right.wasPressedThisFrame ?? false) SelectWeapon((WeaponIndex + 1) % config.weapons.Length);
            if (g?.dpad.left.wasPressedThisFrame ?? false) SelectWeapon((WeaponIndex + config.weapons.Length - 1) % config.weapons.Length);
            if ((k?.spaceKey.wasPressedThisFrame ?? false) || (g?.buttonSouth.wasPressedThisFrame ?? false)) RequestJump();
            if ((k?.leftCtrlKey.wasPressedThisFrame ?? false) || (g?.buttonEast.wasPressedThisFrame ?? false)) RequestDodge(moveDirection);
            if ((k?.eKey.wasPressedThisFrame ?? false) || (g?.rightShoulder.wasPressedThisFrame ?? false)) RequestApproach();
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
            if ((k?.rKey.isPressed ?? false) || (g?.rightTrigger.isPressed ?? false)) RequestShoot();
            if ((k?.qKey.wasPressedThisFrame ?? false) || (g?.leftShoulder.wasPressedThisFrame ?? false)) Favor.Activate();
            if ((k?.tabKey.wasPressedThisFrame ?? false) || (g?.dpad.down.wasPressedThisFrame ?? false)) Favor.Cycle();
        }
        public void RequestJump() { if (CanAct) jumpQueued = Time.time; }
        public void SetMoveInput(Vector2 input, float cameraYaw, bool sprint)
        {
            input = Vector2.ClampMagnitude(input, 1);
            moveDirection = Quaternion.Euler(0, cameraYaw, 0) * new Vector3(input.x, 0, input.y);
            sprinting = sprint;
        }
        ComboSO BaseAttack(AttackInput input)
        {
            if (Weapon) return Weapon.Entry(input, !IsGrounded);
            return input == AttackInput.Launcher ? config.launcher : input == AttackInput.Heavy ? (IsGrounded ? config.heavy : config.slam) : (IsGrounded ? config.light : config.airLight);
        }
        public bool RequestAttack(AttackInput input)
        {
            if (!CanAct || State == PlayerState.Staggered || State == PlayerState.Rushing) return false;
            if (State == PlayerState.Locomotion)
            {
                if (RecoveryRemaining > 0) { buffered = input; bufferExpiry = Time.time + config.inputBuffer; return false; }
                ComboSO next = null;
                if (input == AttackInput.Light && swapContinuation && Time.time < swapUntil) next = swapContinuation;
                else if (preserved && Time.time < preserveUntil && input == AttackInput.Light) next = ResolveNext(preserved, preservedTime);
                if (MoveCooldown(next ? next : BaseAttack(input)) > 0) return false;
                bool continuation = next;
                preserved = swapContinuation = null;
                BeginAttack(next ? next : BaseAttack(input), continuation); return true;
            }
            buffered = input; bufferExpiry = Time.time + config.inputBuffer; return true;
        }
        public bool RequestLauncher()
        {
            if (!CanAct || !IsGrounded || State == PlayerState.Staggered || State == PlayerState.Rushing) return false;
            return RequestAttack(AttackInput.Launcher);
        }
        public bool RequestDodge(Vector3 direction)
        {
            if (!CanAct || Time.time < dodgeReady || State == PlayerState.Staggered || State == PlayerState.Dodging) return false;
            preserved = CurrentAttack; preservedTime = attackTime; preserveUntil = Time.time + .8f;
            CurrentAttack = null; buffered = null; hitWindow = comboWindow = false; rushTarget = null;
            dodgeDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            dodgeDirection = dodgeDirection.sqrMagnitude > .01f ? dodgeDirection.normalized : transform.forward;
            dodgeReady = Time.time + config.dodgeCooldown;
            SetState(PlayerState.Dodging); Events.Dodge(); presentation?.Dodge(); return true;
        }
        public bool SelectWeapon(int index)
        {
            if (!config || config.weapons == null || index < 0 || index >= config.weapons.Length || !CanAct ||
                State == PlayerState.Staggered || State == PlayerState.Rushing) return false;
            if (index == WeaponIndex) { bool cancelled = pendingWeapon >= 0; pendingWeapon = -1; return cancelled; }
            if (CurrentAttack && attackTime < CurrentAttack.hitEnd + CurrentAttack.minimumRecovery)
            {
                pendingWeapon = index; return true; // Commit the active swing before swapping; never erase an upcoming hit.
            }
            Equip(index); return true;
        }
        void Equip(int index)
        {
            bool continueChain = CurrentAttack || preserved;
            WeaponIndex = index; pendingWeapon = -1;
            swapContinuation = continueChain && Weapon ? Weapon.Continuation(ComboDepth, !IsGrounded) : null;
            swapUntil = Time.time + .9f;
            CurrentAttack = preserved = null; hitWindow = comboWindow = false;
            if (State == PlayerState.Attacking) SetState(PlayerState.Locomotion);
            presentation?.Equip(Weapon); WeaponChanged?.Invoke(Weapon);
        }
        public bool RequestApproach()
        {
            if (!CanAct || !Weapon || State == PlayerState.Staggered || State == PlayerState.Rushing || State == PlayerState.Dodging || ApproachCooldown > 0 || RecoveryRemaining > 0) return false;
            Vector3 forward = cameraTransform ? cameraTransform.forward : transform.forward;
            var target = Targeting.FindTarget(forward, Weapon.utilityRange, 100);
            if (!target || Vector3.Distance(target.transform.position, transform.position) < 1.8f) return false;
            if (MoveCooldown(!IsGrounded ? Weapon.airDash : Weapon.dash) > 0) return false;
            if (Weapon.pullsEnemy)
            {
                if (!target.TryPull(this)) return false;
                utilityReady[Weapon.id] = Time.time + Weapon.utilityCooldown;
                offenseReady = Time.time + .28f;
                presentation?.Pull(target.transform); return true;
            }
            rushTarget = target; rushAirborne = !IsGrounded || target.State == EnemyState.Airborne;
            rushDuration = Mathf.Clamp(Vector3.Distance(target.transform.position, transform.position) / config.approachSpeed + .1f, .15f, .7f);
            utilityReady[Weapon.id] = Time.time + Weapon.utilityCooldown;
            CancelAttack(); SetState(PlayerState.Rushing); presentation?.Dodge(); return true;
        }
        void TickRush(float dt)
        {
            if (!Targeting.IsValid(rushTarget, config.approachRange + 3) || stateTime > rushDuration)
            { rushTarget = null; SetState(PlayerState.Locomotion); return; }
            Vector3 delta = rushTarget.transform.position - transform.position;
            if (!rushAirborne) delta.y = 0;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(flat);
            float stop = Mathf.Clamp(Weapon.dash.range * .55f, 1.3f, 2.1f);
            // A moving airborne target otherwise stays just beyond the exact stop shell every frame.
            if (delta.magnitude <= stop + .12f)
            {
                velocity.y = rushAirborne ? Mathf.Max(velocity.y, 1) : velocity.y;
                rushTarget = null; BeginAttack(rushAirborne ? Weapon.airDash : Weapon.dash, false); return;
            }
            Vector3 motion = delta.normalized * Mathf.Min(config.approachSpeed * dt, delta.magnitude - stop);
            if (!rushAirborne) { velocity.y -= config.gravity * dt; motion.y = velocity.y * dt; }
            else velocity.y = 0;
            controller.Move(motion);
        }
        public bool RequestShoot()
        {
            if (!CanAct || !Weapon || State == PlayerState.Staggered || State == PlayerState.Dodging || State == PlayerState.Rushing || Time.time < shotReady) return false;
            Vector3 center = transform.position + Vector3.up * 1.2f;
            Vector3 forward = cameraTransform ? cameraTransform.forward : transform.forward;
            var target = Targeting.FindTarget(forward, Weapon.projectileRange, 65);
            Vector3 direction = target ? (target.transform.position + Vector3.up - center).normalized : forward.normalized;
            Vector3 muzzle = center + direction * .65f;
            if (Physics.Linecast(center, muzzle, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            if (Time.time - lastShot > .8f) gunBurst = 0;
            lastShot = Time.time; gunBurst++;
            shotReady = Time.time + (gunBurst >= 3 ? .95f : Weapon.fireInterval);
            if (gunBurst >= 3) gunBurst = 0;
            int action = AllocateActionId();
            for (int i = 0; i < Weapon.projectileCount; i++)
            {
                var go = new GameObject("Arremesso_" + Weapon.id) { layer = 2 };
                Vector3 origin = muzzle + transform.right * ((i - (Weapon.projectileCount - 1) * .5f) * .24f);
                if (Physics.Linecast(center, origin, ~(1 << 2), QueryTriggerInteraction.Ignore)) { Destroy(go); continue; }
                go.transform.position = origin;
                Vector3 aimPoint = target ? target.transform.position + Vector3.up : center + direction * Weapon.projectileRange;
                // Separate hands converge on the same aim point instead of missing a distant locked target.
                go.AddComponent<PlayerProjectile>().Initialize(this, Weapon, (aimPoint - origin).normalized, action);
            }
            presentation?.Shoot(); return true;
        }
        public void GhostFor(float seconds)
        {
            ghostUntil = Time.time + seconds;
            foreach (var enemy in EnemyCombat.Combatants)
            {
                if (!enemy) continue;
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
        void BeginAttack(ComboSO move, bool continuation)
        {
            if (!move || MoveCooldown(move) > 0) { SetState(PlayerState.Locomotion); return; }
            moveReady[move] = Time.time + move.duration / Favor.AttackSpeed + move.cooldown;
            offenseReady = Mathf.Max(offenseReady, Time.time + (move.hitEnd + move.minimumRecovery) / Favor.AttackSpeed);
            CurrentAttack = move; attackTime = 0; buffered = null; hitTargets.Clear(); slamLanded = false; pulse = 0; sequence = AllocateActionId();
            ComboDepth = continuation ? ComboDepth + 1 : 1;
            hitWindow = comboWindow = false; SetState(PlayerState.Attacking);
            var closest = Targeting.FindTarget(transform.forward, Mathf.Max(4.5f, move.range), 70);
            if (closest)
            {
                Vector3 d = closest.transform.position - transform.position; d.y = 0;
                if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(d);
            }
            if (animator && !string.IsNullOrEmpty(move.animationState)) animator.CrossFade(move.animationState, .08f);
            // Whiffing never grants vertical velocity: juggle lift is awarded only on a confirmed hit.
            presentation?.Attack();
        }
        public static ComboSO ResolveNext(ComboSO move, float elapsed) => move.delayedNext && elapsed >= move.delayedThreshold ? move.delayedNext : move.next;
        void TickAttack(float dt)
        {
            float previous = attackTime; attackTime += dt * Favor.AttackSpeed;
            var move = CurrentAttack;
            if (!move) { SetState(PlayerState.Locomotion); return; }
            if (move.slam)
            {
                if (IsGrounded && !slamLanded && attackTime > .08f)
                {
                    slamLanded = true; Strike(move, true); attackTime = Mathf.Max(attackTime, move.hitEnd);
                }
                if (!slamLanded) attackTime = Mathf.Min(attackTime, move.hitStart);
            }
            else if (useAnimationEvents)
            {
                if (hitWindow) Strike(move, false);
            }
            else
            {
                int count = Mathf.Max(1, move.hitCount);
                // Each pulse has a separate target set. Swept intervals also work when a frame crosses every pulse.
                while (pulse < count)
                {
                    float at = count == 1 ? move.hitStart : Mathf.Lerp(move.hitStart, move.hitEnd, pulse / (float)(count - 1));
                    if (attackTime < at) break;
                    hitTargets.Clear(); Strike(move, false); pulse++;
                }
                if (count == 1 && attackTime <= move.hitEnd && previous >= move.hitStart) Strike(move, false);
            }
            if (pendingWeapon >= 0 && attackTime >= move.hitEnd + move.minimumRecovery) { Equip(pendingWeapon); return; }
            bool open = comboWindow || (!useAnimationEvents && attackTime >= move.comboOpen && attackTime <= move.comboClose);
            if (open && attackTime >= move.hitEnd + move.minimumRecovery && buffered.HasValue && Time.time <= bufferExpiry)
            {
                var input = buffered.Value;
                var next = input == AttackInput.Light ? ResolveNext(move, attackTime) : input == AttackInput.Heavy ? move.heavyNext : null;
                if (!next && input != AttackInput.Light) next = BaseAttack(input);
                if (next && MoveCooldown(next) <= 0) { BeginAttack(next, true); return; }
            }
            if (attackTime >= move.duration) { CurrentAttack = null; SetState(PlayerState.Locomotion); }
        }
        void Strike(ComboSO move, bool area)
        {
            Vector3 origin = transform.position + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(origin, move.range, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = overlap[i].GetComponentInParent<Health>();
                if (!target || target == Health || target.IsDead || hitTargets.Contains(target) || !target.GetComponent<EnemyCombat>()) continue;
                Vector3 flat = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
                if (!area && Vector3.Angle(transform.forward, flat) > move.angle * .5f) continue;
                if (Physics.Linecast(origin, target.transform.position + Vector3.up, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                var info = new DamageInfo { source = gameObject, damage = move.damage * Favor.DamageMultiplier, direction = flat.normalized,
                    knockback = move.knockback, launch = move.launch, stagger = .4f, heavy = move.input != AttackInput.Light,
                    aerial = move.aerial, finisher = move.finisher, moveId = (Weapon ? Weapon.id + "." : "") + move.id, sequence = sequence };
                if (!target.TakeDamage(info)) continue;
                hitTargets.Add(target);
                ConfirmHit(info, target, Weapon, string.IsNullOrEmpty(move.displayName) ? move.id : move.displayName, true);
                if (move.aerial && !move.slam && !IsGrounded) velocity.y = Mathf.Max(velocity.y, 3.6f);
            }
            if (area) CombatVisuals.Burst(transform.position + Vector3.up * .1f, Weapon ? Weapon.color : Color.yellow, move.range);
        }
        public void ConfirmHit(DamageInfo info, Health target, WeaponSO profile, string title, bool hitStop)
        {
            Style.RegisterHit(info.moveId, info.finisher, info.projectile ? "pistols" : profile ? profile.id : "", info.aerial, info.projectile, info.sequence, info.damage, title);
            Favor.Gain((info.projectile ? .7f : 2) * Style.Multiplier);
            Events.Hit(info, target);
            CombatVisuals.Burst(target.transform.position + Vector3.up, profile ? profile.color : Color.yellow, .25f);
            presentation?.Impact(info.heavy);
            if (hitStop) { shake = info.finisher ? .24f : .12f; GameManager.Instance?.HitStop(info.finisher ? .055f : .03f); }
        }
        public int AllocateActionId() { if (++actionCounter == int.MaxValue) actionCounter = 1; return actionCounter; }
        public void OpenHitbox() { hitWindow = true; hitTargets.Clear(); }
        public void CloseHitbox() => hitWindow = false;
        public void OpenComboWindow() => comboWindow = true;
        public void CloseComboWindow() => comboWindow = false;
        void SetState(PlayerState state) { State = state; stateTime = 0; }
        void CancelAttack()
        {
            CurrentAttack = preserved = swapContinuation = null; buffered = null;
            pendingWeapon = -1; hitWindow = comboWindow = false;
        }
        void OnDamaged(DamageInfo info)
        {
            Style.TakeHit(); CancelAttack(); rushTarget = null; heavyPressed = -1;
            velocity += info.direction * info.knockback; graceUntil = Time.time + .55f;
            SetState(PlayerState.Staggered); shake = .3f;
        }
        void OnDeath(DamageInfo info) { CancelAttack(); SetState(PlayerState.Dead); GameManager.Instance?.EndRun(false); }
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            EndGhost(); CancelAttack(); moveDirection = velocity = Vector3.zero; rushTarget = null;
            controller.enabled = false; transform.SetPositionAndRotation(position, rotation); controller.enabled = true;
            heavyPressed = -1; JumpsUsed = ComboDepth = 0; jumpQueued = lastGrounded = -10;
            Health.ResetHealth(config.playerHealth); graceUntil = Time.time + 1; dodgeReady = shotReady = 0;
            moveReady.Clear(); utilityReady.Clear(); offenseReady = 0; gunBurst = 0; lastShot = -10;
            WeaponIndex = 0; Targeting.Clear(); presentation?.Equip(Weapon);
            yaw = rotation.eulerAngles.y; SetState(PlayerState.Locomotion);
        }
        void LateUpdate()
        {
            if (!cameraTransform) return;
            Vector3 pivot = transform.position + Vector3.up * 1.5f;
            float distance = cameraDistance;
            if (Targeting.IsLocked)
            {
                Vector3 delta = Targeting.LockedTarget.transform.position - transform.position;
                float targetYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, targetYaw, 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
                pivot += delta * .2f;
                distance = Mathf.Clamp(cameraDistance + delta.magnitude * .15f, cameraDistance, 11);
            }
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 backward = rotation * Vector3.back;
            if (Physics.SphereCast(pivot, .23f, backward, out var hit, distance, ~(1 << 2), QueryTriggerInteraction.Ignore)) distance = Mathf.Max(.4f, hit.distance - .1f);
            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime);
            cameraTransform.SetPositionAndRotation(pivot + backward * distance + Random.insideUnitSphere * shake * .15f, rotation);
        }
        void OnDestroy() { EndGhost(); if (Health) { Health.Damaged -= OnDamaged; Health.Died -= OnDeath; } }
    }
}
