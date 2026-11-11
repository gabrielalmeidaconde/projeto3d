using UnityEngine;
using UnityEngine.InputSystem;

namespace Troia
{
    /// <summary>
    /// Controle TEMPORÁRIO em terceira pessoa, só para explorar e conferir a escala do
    /// mapa. Não é o personagem final do hack and slash (combate vem na próxima etapa).
    /// WASD mover | Shift correr | Espaço pular | F voo livre | Scroll zoom | Esc solta o mouse
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MapPreviewWalker : MonoBehaviour
    {
        public Transform cameraTransform;
        public float walkSpeed = 5f;
        public float runSpeed = 10f;
        public float flySpeed = 22f;
        public float jumpSpeed = 6f;
        public float gravity = -20f;
        public float mouseSensitivity = 0.12f;
        public float cameraDistance = 6f;
        public float cameraHeight = 1.7f;
        public Vector2 distanceLimits = new Vector2(2.5f, 30f);
        public bool showHelp = true;

        CharacterController _cc;
        float _yaw, _pitch = 12f, _vy;
        bool _fly;
        const int IgnoreRaycastLayer = 2;

        void Start()
        {
            _cc = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            SetCursor(true);
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (kb.escapeKey.wasPressedThisFrame) SetCursor(false);
            if (mouse.leftButton.wasPressedThisFrame) SetCursor(true);
            if (kb.fKey.wasPressedThisFrame) { _fly = !_fly; _vy = 0f; }
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
                _yaw += d.x;
                _pitch = Mathf.Clamp(_pitch - d.y, -35f, 80f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                cameraDistance = Mathf.Clamp(cameraDistance - Mathf.Sign(scroll) * 1f, distanceLimits.x, distanceLimits.y);

            Vector2 input = new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 move = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(input.x, 0f, input.y);

            if (_fly)
            {
                float up = (kb.spaceKey.isPressed ? 1f : 0f) - (kb.leftCtrlKey.isPressed ? 1f : 0f);
                float speed = flySpeed * (kb.leftShiftKey.isPressed ? 3f : 1f);
                _cc.enabled = false;
                transform.position += (move + Vector3.up * up) * speed * Time.deltaTime;
                _cc.enabled = true;
            }
            else
            {
                float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
                if (_cc.isGrounded)
                {
                    _vy = -2f;
                    if (kb.spaceKey.wasPressedThisFrame) _vy = jumpSpeed;
                }
                _vy += gravity * Time.deltaTime;
                _cc.Move((move * speed + Vector3.up * _vy) * Time.deltaTime);
            }

            if (move.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 12f * Time.deltaTime);
        }

        void LateUpdate()
        {
            if (cameraTransform == null) return;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = transform.position + Vector3.up * cameraHeight;
            Vector3 dir = rot * Vector3.back;
            float dist = cameraDistance;
            int mask = ~(1 << IgnoreRaycastLayer);
            if (Physics.SphereCast(pivot, 0.25f, dir, out RaycastHit hit, cameraDistance, mask, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.6f, hit.distance - 0.1f);
            cameraTransform.SetPositionAndRotation(pivot + dir * dist, rot);
        }

        void OnGUI()
        {
            if (!showHelp) return;
            const string text = "EXPLORAR MAPA (temporário)\n" +
                                "WASD mover | Shift correr | Espaço pular\n" +
                                "F voo livre (Espaço/Ctrl sobe/desce) | Scroll zoom\n" +
                                "Esc solta o mouse | Clique prende | H esconde ajuda";
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(new Rect(10, 10, 360, 78), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(18, 14, 350, 74), text);
            GUI.Label(new Rect(18, 92, 350, 20), _fly ? "Modo: VOO" : "Modo: A PÉ");
        }
    }
}
