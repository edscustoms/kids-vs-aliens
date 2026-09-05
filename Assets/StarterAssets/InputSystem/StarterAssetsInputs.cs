using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;
        public bool shoot;

        public event Action<bool> ShootStateChanged;
        public event Action ShootCanceled;
        public event Action PauseRequested;

        public bool GameplayInputBlocked { get; private set; }
        private int blockedThroughFrame = -1;
        private bool rawShoot,
            rawJump,
            rawSprint;
        private Vector2 rawMove,
            rawLook;
        private bool waitShootNeutral,
            waitJumpNeutral,
            waitSprintNeutral,
            waitMoveNeutral,
            waitLookNeutral;
        private GameplayPointerInputFilter pointerFilter;

        private void Awake() => pointerFilter = GetComponent<GameplayPointerInputFilter>();

        public bool CanProcessGameplayInput =>
            !GameplayInputBlocked && Time.frameCount > blockedThroughFrame;

        public void SetGameplayInputBlocked(bool blocked)
        {
            if (blocked == GameplayInputBlocked)
                return;
            GameplayInputBlocked = blocked;
            // Never emit ordinary shoot-release here: it commits a grenade throw.
            waitShootNeutral = rawShoot || shoot;
            waitJumpNeutral = rawJump || jump;
            waitSprintNeutral = rawSprint || sprint;
            waitMoveNeutral = rawMove.sqrMagnitude > 0.001f || move.sqrMagnitude > 0.001f;
            waitLookNeutral = rawLook.sqrMagnitude > 0.001f || look.sqrMagnitude > 0.001f;
            if (blocked)
                CancelShootInput();
            else
                shoot = false;
            move = look = Vector2.zero;
            jump = sprint = false;
            if (!blocked)
                blockedThroughFrame = Time.frameCount;
        }

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
        public void OnPause(InputValue value)
        {
            if (value.isPressed && !InputModeController.IsMobile)
                PauseInput();
        }

        public void OnMove(InputValue value)
        {
            MoveInput(value.Get<Vector2>());
        }

        public void OnLook(InputValue value)
        {
            if (cursorInputForLook)
            {
                LookInput(value.Get<Vector2>());
            }
        }

        public void OnJump(InputValue value)
        {
            JumpInput(value.isPressed);
        }

        public void OnSprint(InputValue value)
        {
            SprintInput(value.isPressed);
        }

        public void OnShoot(InputValue value)
        {
            // Desktop Input Action.
            // In Mobile mode shooting comes only from the mobile UI.
            if (InputModeController.IsMobile)
                return;

            if (value.isPressed && pointerFilter != null && pointerFilter.BlocksPrimaryPress())
            {
                rawShoot = true;
                waitShootNeutral = true;
                // This press belongs to UI. The pause owner will cancel any
                // active gesture with suspension semantics when it acquires.
                return;
            }
            ShootInput(value.isPressed);
        }
#endif

        public void MoveInput(Vector2 newMoveDirection)
        {
            rawMove = newMoveDirection;
            if (newMoveDirection.sqrMagnitude < 0.001f)
                waitMoveNeutral = false;
            if (!CanProcessGameplayInput || waitMoveNeutral)
            {
                move = Vector2.zero;
                return;
            }
            move = newMoveDirection;
        }

        public void LookInput(Vector2 newLookDirection)
        {
            rawLook = newLookDirection;
            if (newLookDirection.sqrMagnitude < 0.001f)
                waitLookNeutral = false;
            if (!CanProcessGameplayInput || waitLookNeutral)
            {
                look = Vector2.zero;
                return;
            }
            look = newLookDirection;
        }

        public void JumpInput(bool newJumpState)
        {
            rawJump = newJumpState;
            if (!newJumpState)
                waitJumpNeutral = false;
            if (!CanProcessGameplayInput || waitJumpNeutral)
            {
                jump = false;
                return;
            }
            jump = newJumpState;
        }

        public void SprintInput(bool newSprintState)
        {
            rawSprint = newSprintState;
            if (!newSprintState)
                waitSprintNeutral = false;
            if (!CanProcessGameplayInput || waitSprintNeutral)
            {
                sprint = false;
                return;
            }
            sprint = newSprintState;
        }

        public void ShootInput(bool newShootState)
        {
            rawShoot = newShootState;
            if (!CanProcessGameplayInput)
            {
                waitShootNeutral = newShootState;
                return;
            }
            if (waitShootNeutral)
            {
                if (!newShootState)
                    waitShootNeutral = false;
                return;
            }
            if (shoot == newShootState)
                return;

            shoot = newShootState;

            ShootStateChanged?.Invoke(newShootState);
        }

        public void CancelShootInput()
        {
            shoot = false;

            // Cancellation is deliberately separate from a normal false
            // state. In grenade mode, false means throw while cancellation
            // means restore the weapon without consuming the grenade.
            ShootCanceled?.Invoke();
        }

        // UI intent remains available while gameplay input is blocked.
        public void PauseInput() => PauseRequested?.Invoke();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelShootInput();
            }

            if (!GameplayInputBlocked)
                SetCursorState(cursorLocked);
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
