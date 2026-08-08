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
        public static InputManager Instance { get; private set; }

        // Input state
        private Vector2Int _direction;
        private bool _confirmPressed;
        private bool _cancelPressed;
        private bool _idlePressed;
        private bool _endYearPressed;

        // Buffered input (consumed once per frame)
        private Vector2Int _bufferedDirection;
        private bool _bufferedConfirm;
        private bool _bufferedCancel;
        private bool _bufferedIdle;
        private bool _bufferedEndYear;

        public Vector2Int Direction => _direction;
        public bool ConfirmPressed => _confirmPressed;
        public bool CancelPressed => _cancelPressed;
        public bool IdlePressed => _idlePressed;
        public bool EndYearPressed => _endYearPressed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

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
            _confirmPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
            _cancelPressed = Input.GetKeyDown(KeyCode.Escape);
            _idlePressed = Input.GetKeyDown(KeyCode.I);
            _endYearPressed = Input.GetKeyDown(KeyCode.E);
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

        /// <summary>
        /// Check if any movement key is currently held.
        /// </summary>
        public bool HasMovementInput()
        {
            return _direction.x != 0 || _direction.y != 0;
        }
    }
}
