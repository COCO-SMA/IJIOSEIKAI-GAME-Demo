using UnityEngine;

namespace KunchengRPG.Core
{
    /// <summary>
    /// Centralized input handling. Supports keyboard.
    /// Movement: WASD / Arrow keys
    /// Confirm: Space / Enter
    /// Cancel: Escape
    /// Idle: I
    /// End Year: E
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        private static InputManager _instance;
        private static bool _quitting;

        /// <summary>
        /// Self-bootstrapping: no scene contains an InputManager, and every consumer
        /// bails out on a null Instance, so a missing one silently kills all keyboard
        /// input instead of throwing. Creating it on first access means play-from-any-scene
        /// works in the editor and in the player without a manager object per scene.
        /// </summary>
        public static InputManager Instance
        {
            get
            {
                if (_instance != null || _quitting) return _instance;

                _instance = FindObjectOfType<InputManager>();
                if (_instance == null)
                {
                    var go = new GameObject("InputManager");
                    _instance = go.AddComponent<InputManager>();
                }
                return _instance;
            }
        }

        // Input state
        private Vector2Int _direction;
        private Vector2Int _directionPressed;
        private bool _confirmPressed;
        private bool _cancelPressed;
        private bool _idlePressed;
        private bool _endYearPressed;
        private bool _menuPressed;

        // Buffered input (consumed once per frame)
        private Vector2Int _bufferedDirection;
        private bool _bufferedConfirm;
        private bool _bufferedCancel;
        private bool _bufferedIdle;
        private bool _bufferedEndYear;

        public Vector2Int Direction => _direction;

        /// <summary>One step per keypress. What menus want; Direction repeats every frame.</summary>
        public Vector2Int DirectionPressed => _directionPressed;

        public bool ConfirmPressed => _confirmPressed;
        public bool CancelPressed => _cancelPressed;
        public bool IdlePressed => _idlePressed;
        public bool EndYearPressed => _endYearPressed;

        /// <summary>Tab: opens and closes the pause menu (equipment / backpack).</summary>
        public bool MenuPressed => _menuPressed;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnApplicationQuit() => _quitting = true;

        void Update()
        {
            // Read raw input
            int h = 0, v = 0;

            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
                h -= 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
                h += 1;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
                v += 1;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
                v -= 1;

            // Prioritize one axis (no diagonal movement in tile-based)
            if (h != 0 && v != 0)
            {
                // Prefer horizontal if both pressed
                v = 0;
            }

            _direction = new Vector2Int(h, v);

            // Edge-triggered inputs
            // Edge-triggered direction for menus. Direction itself stays held-state
            // because tile movement and the combat cursor want their own repeat rates.
            int dh = 0, dv = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) dh -= 1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dh += 1;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dv += 1;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dv -= 1;
            if (dh != 0 && dv != 0) dv = 0;
            _directionPressed = new Vector2Int(dh, dv);

            // KeypadEnter is a distinct KeyCode from Return; players on a full
            // keyboard reach for it and would otherwise get nothing.
            _confirmPressed = Input.GetKeyDown(KeyCode.Space)
                              || Input.GetKeyDown(KeyCode.Return)
                              || Input.GetKeyDown(KeyCode.KeypadEnter);
            _cancelPressed = Input.GetKeyDown(KeyCode.Escape);
            _idlePressed = Input.GetKeyDown(KeyCode.I);
            _endYearPressed = Input.GetKeyDown(KeyCode.E);
            _menuPressed = Input.GetKeyDown(KeyCode.Tab);
        }

        /// <summary>
        /// Consume confirm press (call after handling to prevent double-trigger).
        /// </summary>
        public void ConsumeConfirm()
        {
            _confirmPressed = false;
        }

        public void ConsumeCancel()
        {
            _cancelPressed = false;
        }

        public void ConsumeIdle()
        {
            _idlePressed = false;
        }

        public void ConsumeEndYear()
        {
            _endYearPressed = false;
        }

        public void ConsumeMenu()
        {
            _menuPressed = false;
        }

        /// <summary>
        /// Check if any movement key is currently held.
        /// </summary>
        public bool HasMovementInput()
        {
            return _direction.x != 0 || _direction.y != 0;
        }
    }
}
