using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PurrTales
{
    /// <summary>
    /// Capa única de lectura de input para Purr-tales (Input System).
    /// Usa el asset del proyecto base (Assets/InputSystem_Actions.inputactions) clonándolo en runtime
    /// para no modificar el original. Si falta el asset o alguna acción, crea acciones equivalentes por código.
    ///
    /// Mapeo:
    ///   Move        -> Player/Move    (WASD, flechas, stick izq.)
    ///   Saltar      -> Player/Jump    (Espacio, botón Sur)
    ///   Dash        -> Player/Sprint  (Shift izq. + Shift der. + RB)
    ///   Portal azul -> Player/Attack  (Clic izq. + LT)
    ///   Portal nar. -> UI/RightClick  (Clic der. + RT)
    ///   Apuntar     -> UI/Point       (posición del mouse) o stick derecho
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Asset de acciones del proyecto base. Si se deja vacío se crean acciones equivalentes por código.")]
        public InputActionAsset actionsAsset;

        [Header("Rutas de acciones dentro del asset (Mapa/Acción)")]
        public string moveAction = "Player/Move";
        public string jumpAction = "Player/Jump";
        public string dashAction = "Player/Sprint";
        public string fireBlueAction = "Player/Attack";
        public string fireOrangeAction = "UI/RightClick";
        public string pointAction = "UI/Point";

        [Header("Apuntado con gamepad")]
        [Range(0.1f, 0.9f)] public float stickAimDeadzone = 0.35f;

        InputActionAsset runtimeAsset;
        InputAction move, jump, dash, fireBlue, fireOrange, point, aimStick;
        readonly List<InputAction> ownedActions = new List<InputAction>();
        readonly List<InputAction> assetActions = new List<InputAction>();

        Vector2 lastStickAim = Vector2.right;
        Vector2 lastPointerScreen;
        bool usingStickAim;

        /// <summary>Si es true, todas las lecturas devuelven "sin input" (muerte, cinemáticas...).</summary>
        public bool InputLocked { get; set; }

        public Vector2 Move
        {
            get
            {
                if (InputLocked || move == null) return Vector2.zero;
                return Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            }
        }

        public bool JumpPressed { get { return Pressed(jump); } }
        public bool JumpReleased { get { return !InputLocked && jump != null && jump.WasReleasedThisFrame(); } }
        public bool JumpHeld { get { return !InputLocked && jump != null && jump.IsPressed(); } }
        public bool DashPressed { get { return Pressed(dash); } }
        public bool FireBluePressed { get { return Pressed(fireBlue); } }
        public bool FireOrangePressed { get { return Pressed(fireOrange); } }

        bool Pressed(InputAction a)
        {
            return !InputLocked && a != null && a.WasPressedThisFrame();
        }

        void Awake()
        {
            SetupActions();
        }

        void OnEnable()
        {
            SetEnabled(true);
        }

        void OnDisable()
        {
            SetEnabled(false);
        }

        void OnDestroy()
        {
            for (int i = 0; i < ownedActions.Count; i++) ownedActions[i].Dispose();
            ownedActions.Clear();
            if (runtimeAsset != null) Destroy(runtimeAsset);
        }

        void SetupActions()
        {
            if (actionsAsset != null)
            {
                // Clon para poder añadir bindings extra sin ensuciar el asset original.
                runtimeAsset = Instantiate(actionsAsset);
            }

            move = FindInAsset(moveAction);
            if (move == null) move = CreateMoveAction();

            jump = FindInAsset(jumpAction);
            if (jump == null) jump = CreateButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");

            dash = FindInAsset(dashAction);
            if (dash == null) dash = CreateButton("Dash", "<Keyboard>/leftShift");
            EnsureBinding(dash, "<Keyboard>/leftShift");
            EnsureBinding(dash, "<Keyboard>/rightShift");
            EnsureBinding(dash, "<Gamepad>/rightShoulder");

            fireBlue = FindInAsset(fireBlueAction);
            if (fireBlue == null) fireBlue = CreateButton("FireBlue", "<Mouse>/leftButton");
            EnsureBinding(fireBlue, "<Gamepad>/leftTrigger");

            fireOrange = FindInAsset(fireOrangeAction);
            if (fireOrange == null) fireOrange = CreateButton("FireOrange", "<Mouse>/rightButton");
            EnsureBinding(fireOrange, "<Gamepad>/rightTrigger");

            point = FindInAsset(pointAction);
            if (point == null)
            {
                point = new InputAction("Point", InputActionType.PassThrough, "<Pointer>/position", null, null, "Vector2");
                ownedActions.Add(point);
            }

            aimStick = new InputAction("AimStick", InputActionType.Value, "<Gamepad>/rightStick", null, null, "Vector2");
            ownedActions.Add(aimStick);
        }

        InputAction FindInAsset(string path)
        {
            if (runtimeAsset == null || string.IsNullOrEmpty(path)) return null;
            InputAction a = runtimeAsset.FindAction(path, false);
            if (a != null && !assetActions.Contains(a)) assetActions.Add(a);
            return a;
        }

        InputAction CreateButton(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            for (int i = 0; i < bindings.Length; i++) a.AddBinding(bindings[i]);
            ownedActions.Add(a);
            return a;
        }

        InputAction CreateMoveAction()
        {
            var a = new InputAction("Move", InputActionType.Value, null, null, null, "Vector2");
            a.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            a.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            a.AddBinding("<Gamepad>/leftStick");
            a.AddBinding("<Gamepad>/dpad");
            ownedActions.Add(a);
            return a;
        }

        static void EnsureBinding(InputAction action, string path)
        {
            if (action == null) return;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].path == path) return;
            }
            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();
            action.AddBinding(path);
            if (wasEnabled) action.Enable();
        }

        void SetEnabled(bool value)
        {
            for (int i = 0; i < assetActions.Count; i++)
            {
                if (value) assetActions[i].Enable(); else assetActions[i].Disable();
            }
            for (int i = 0; i < ownedActions.Count; i++)
            {
                if (value) ownedActions[i].Enable(); else ownedActions[i].Disable();
            }
        }

        /// <summary>
        /// Dirección normalizada de apuntado desde <paramref name="origin"/> (mundo).
        /// Stick derecho si se está usando; si no, hacia el cursor del mouse.
        /// </summary>
        public Vector2 GetAimDirection(Vector2 origin, Camera cam)
        {
            Vector2 stick = aimStick != null ? aimStick.ReadValue<Vector2>() : Vector2.zero;
            if (stick.sqrMagnitude >= stickAimDeadzone * stickAimDeadzone)
            {
                lastStickAim = stick.normalized;
                usingStickAim = true;
                return lastStickAim;
            }

            Vector2 screen = ReadPointerScreen();
            if ((screen - lastPointerScreen).sqrMagnitude > 4f)
            {
                usingStickAim = false;
                lastPointerScreen = screen;
            }
            if (usingStickAim || cam == null) return lastStickAim;

            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            Vector2 dir = (Vector2)world - origin;
            if (dir.sqrMagnitude < 0.0001f) return lastStickAim;
            return dir.normalized;
        }

        /// <summary>Posición del cursor en coordenadas de mundo (para HUD/debug).</summary>
        public Vector2 GetPointerWorld(Camera cam)
        {
            if (cam == null) return Vector2.zero;
            Vector2 screen = ReadPointerScreen();
            return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
        }

        Vector2 ReadPointerScreen()
        {
            if (point != null) return point.ReadValue<Vector2>();
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }
    }
}
