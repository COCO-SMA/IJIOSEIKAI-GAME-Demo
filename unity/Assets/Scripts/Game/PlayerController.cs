using UnityEngine;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Handles player movement on the map.
    ///
    /// Movement is continuous (not grid-locked) with pixel-accurate collision
    /// against the fine walkability grid. AP is only consumed by interactions,
    /// never by walking.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("World units per second.")]
        public float moveSpeed = 4f;

        [Tooltip("Collision radius in world units. Should be slightly smaller than half the sprite size.")]
        public float collisionRadius = 0.1f;

        public SpriteRenderer spriteRenderer;
        public Animator animator;

        [Header("References")]
        public MapController mapController;

        private Vector2Int gridPosition;
        private int facing = 0; // 0=down, 1=left, 2=right, 3=up

        // Callbacks
        public System.Action<int, int> OnStepComplete; // gridX, gridY

        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving { get; private set; }
        public int Facing => facing;

        /// <summary>
        /// Place the player at a grid position instantly (no movement).
        /// </summary>
        public void SetPosition(int x, int y)
        {
            gridPosition = new Vector2Int(x, y);
            transform.position = mapController.GridToWorld(x, y);
            IsMoving = false;
        }

        void Update()
        {
            var input = Core.InputManager.Instance;
            if (input == null || !input.HasMovementInput())
            {
                IsMoving = false;
                if (animator != null)
                    animator.SetBool("walking", false);
                return;
            }

            Vector2 dir = input.Direction;
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            // Update facing
            if (dir.x < 0) facing = 1;
            else if (dir.x > 0) facing = 2;
            else if (dir.y > 0) facing = 3;
            else if (dir.y < 0) facing = 0;
            UpdateSpriteFacing();

            // Continuous movement with per-axis collision sliding
            float step = moveSpeed * Time.deltaTime;
            Vector3 current = transform.position;
            Vector3 desired = current + new Vector3(dir.x, dir.y, 0f) * step;

            // Try X first
            Vector3 afterX = current;
            if (mapController.IsWalkable(desired.x, current.y, collisionRadius))
            {
                afterX.x = desired.x;
            }

            // Then Y from the X-resolved position
            Vector3 afterY = afterX;
            if (mapController.IsWalkable(afterX.x, desired.y, collisionRadius))
            {
                afterY.y = desired.y;
            }

            transform.position = afterY;
            IsMoving = true;
            if (animator != null)
                animator.SetBool("walking", true);

            // Detect coarse-grid step changes
            Vector2Int currentGrid = mapController.WorldToGrid(transform.position);
            if (currentGrid != gridPosition)
            {
                gridPosition = currentGrid;
                OnStepComplete?.Invoke(gridPosition.x, gridPosition.y);
            }
        }

        private void UpdateSpriteFacing()
        {
            if (spriteRenderer == null) return;

            switch (facing)
            {
                case 1: // left
                    spriteRenderer.flipX = true;
                    break;
                case 2: // right
                    spriteRenderer.flipX = false;
                    break;
            }
        }

        /// <summary>
        /// Stop movement immediately.
        /// </summary>
        public void Stop()
        {
            IsMoving = false;
            if (animator != null)
                animator.SetBool("walking", false);
        }
    }
}
