using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Fable
{
    /// <summary>
    /// One place for all controls, read once per frame by Game.
    ///
    ///   Action      Keyboard                 Gamepad
    ///   Move        WASD / arrow keys        Left stick / D-pad
    ///   Interact    E or Space               A (south button)
    ///   Menu        Tab or Esc               Start, or Y (north button)
    ///   Confirm     Enter, Space or E        A (south button)
    ///   Cancel      Esc or Backspace         B (east button)
    ///   Run         Left Shift               X (west button) or right trigger
    ///
    /// Works with the Input System package when it is active, and falls back to the
    /// built-in Input Manager otherwise, so the project runs under either setting.
    /// </summary>
    public static class GameInput
    {
        public static Vector2 Move;
        public static bool ConfirmDown, CancelDown, MenuDown, InteractDown, RunHeld;
        public static bool NavUp, NavDown, NavLeft, NavRight;

        static int heldX, heldY;
        static float repeatX, repeatY;
        const float FirstRepeat = 0.35f, NextRepeat = 0.11f;

        public static void Poll()
        {
            Vector2 move = Vector2.zero;
            bool confirm = false, cancel = false, menu = false, interact = false, run = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                confirm |= kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
                cancel |= kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                menu |= kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame;
                interact |= kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                run |= kb.leftShiftKey.isPressed;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                Vector2 dpad = pad.dpad.ReadValue();
                if (stick.magnitude > 0.25f) move += stick;
                move += dpad;
                confirm |= pad.buttonSouth.wasPressedThisFrame;
                cancel |= pad.buttonEast.wasPressedThisFrame;
                menu |= pad.startButton.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame;
                interact |= pad.buttonSouth.wasPressedThisFrame;
                run |= pad.buttonWest.isPressed || pad.rightTrigger.isPressed;
            }
#else
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (move == Vector2.zero)
            {
                // the default Horizontal/Vertical axes also read the first gamepad's left stick
                try
                {
                    Vector2 stick = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                    if (stick.magnitude > 0.25f) move = stick;
                }
                catch (System.Exception) { }
            }
            confirm |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)
                    || Input.GetKeyDown(KeyCode.JoystickButton0);
            cancel |= Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1);
            menu |= Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape)
                 || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton3);
            interact |= Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
            run |= Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton2);
#endif

            if (move.sqrMagnitude > 1f) move.Normalize();
            Move = move;
            ConfirmDown = confirm;
            CancelDown = cancel;
            MenuDown = menu;
            InteractDown = interact;
            RunHeld = run;

            int x = move.x > 0.5f ? 1 : (move.x < -0.5f ? -1 : 0);
            int y = move.y > 0.5f ? 1 : (move.y < -0.5f ? -1 : 0);
            int stepX = Repeat(x, ref heldX, ref repeatX);
            int stepY = Repeat(y, ref heldY, ref repeatY);
            NavRight = stepX > 0;
            NavLeft = stepX < 0;
            NavUp = stepY > 0;
            NavDown = stepY < 0;
        }

        static int Repeat(int dir, ref int held, ref float timer)
        {
            if (dir == 0)
            {
                held = 0;
                return 0;
            }
            if (dir != held)
            {
                held = dir;
                timer = FirstRepeat;
                return dir;
            }
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                timer = NextRepeat;
                return dir;
            }
            return 0;
        }

        /// <summary>Swallows this frame's button presses so one press cannot trigger two screens.</summary>
        public static void Consume()
        {
            ConfirmDown = CancelDown = MenuDown = InteractDown = false;
            NavUp = NavDown = NavLeft = NavRight = false;
        }
    }
}
