using UnityEngine;
using System.Collections;

namespace KunchengRPG.Game
{
    /// <summary>
    /// Handles player movement on the tilemap grid.
    /// Grid-based movement with smooth interpolation between tiles.
    /// Walking is FREE (no AP cost). AP is consumed by interactions only.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f; // Tiles per second
        public SpriteRenderer spriteRenderer;
        public Animator animator;

        [Header("References")]
        public MapController mapController;

        private bool isMoving;
        private Vector3 targetPosition;
        private Vector2Int gridPosition;
        private int facing = 0; // 0=down, 1=left, 2=right, 3=up

        // Callbacks
        public System.Action<int, int> OnStepComplete; // gridX, gridY

        public Vector2Int GridPosition => gridPosition;
        public bool IsMoving => isMoving;
        public int Facing => facing;

        /// <summary>
        /// Place the player at a grid position instantly (no movement).
        /// </summary>
        public void SetPosition(int x, int y)
        {
            gridPosition = new Vector2Int(x, y);
            targetPosition = mapController.GridToWorld(x, y);
            transform.position = targetPosition;
            isMoving = false;
        }

        void Update()
        {
            if (isMoving)
            {
                // Move towards target
                float step = moveSpeed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

                if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
                {
                    transform.position = targetPosition;
                    isMoving = false;
                    OnStepComplete?.Invoke(gridPosition.x, gridPosition.y);
                }
                return;
            }

            // Read input
            var input = Core.InputManager.Instance;
            if (input == null || !input.HasMovementInput()) return;

            Vector2Int dir = input.Direction;

            // Update facing
            if (dir.x < 0) facing = 1;
            else if (dir.x > 0) facing = 2;
            else if (dir.y > 0) facing = 3;
            else if (dir.y < 0) facing = 0;

            UpdateSpriteFacing();

            // Try to move
            int targetX = gridPosition.x + dir.x;
            int targetY = gridPosition.y + dir.y;

            if (mapController.IsWalkable(targetX, targetY))
            {
                gridPosition = new Vector2Int(targetX, targetY);
                targetPosition = mapController.GridToWorld(targetX, targetY);
                isMoving = true;

                if (animator != null)
                    animator.SetBool("walking", true);
            }
            else
            {
                // Bumped into something
                if (animator != null)
                    animator.SetBool("walking", false);
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
            isMoving = false;
            if (animator != null)
                animator.SetBool("walking", false);
        }
    }
}
