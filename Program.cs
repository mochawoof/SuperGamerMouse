
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

using SharpDX.XInput;

public struct PNT
{
    public int x;
    public int y;
}

class Program
{

    // DllImports because I hate Forms

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out PNT lpPoint);

    // Mouse constants
    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;

    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);

    // Keyboard constants

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_BACK = 0x08;
    private const byte VK_RETURN = 0x0D;

    private const byte VK_UP = 0x26;
    private const byte VK_DOWN = 0x28;
    private const byte VK_LEFT = 0x25;
    private const byte VK_RIGHT = 0x27;

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags,  uint dwExtraInfo);

    // Math constants

    const int POLLING_RATE = 10;
    const int TRIGGER_UPPER_CUTOFF = 255;
    const int TRIGGER_LOWER_CUTOFF = 55;
    const int LEFT_THUMB_DIVISOR = 300;
    const int RIGHT_THUMB_DIVISOR = 300;

    const int BACKSPACE_BUTTON_COOLDOWN_MS = 80;
    const int ARROW_BUTTONS_COOLDOWN_MS = 80;

    // Input constants

    const GamepadButtonFlags KEYBOARD_BUTTON = GamepadButtonFlags.X;
    const GamepadButtonFlags BACKSPACE_BUTTON = GamepadButtonFlags.B;
    const GamepadButtonFlags ENTER_BUTTON = GamepadButtonFlags.A;

    // For on screen keyboard
    [ComImport, Guid("4ce576fa-83dc-4F88-951c-9d0782b4e376")]
    class UIHostNoLaunch
    {
    }

    [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITipInvocation
    {
        void Toggle(IntPtr hwnd);
    }

    [DllImport("user32.dll", SetLastError = false)]
    static extern IntPtr GetDesktopWindow();

    static void keyPress(byte vk)
    {
        keybd_event(vk, 0, 0, 0);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
    }

    static void error(Exception e, string msg)
    {
        Console.Error.WriteLine(e);
        Console.WriteLine();
        Console.WriteLine(msg);
    }

    static void Main(string[] args)
    {
        Console.WriteLine("Super Gamer Mouse 1.0.2");

        Controller controller = new Controller(UserIndex.One);

        while (!controller.IsConnected)
        {
            Console.WriteLine("Please connect your controller...");
            Thread.Sleep(3000);
        }

        Console.WriteLine();
        Console.WriteLine("Controller connected!");

        State oldState = controller.GetState();

        bool isMouseDownLeft = false;
        bool isMouseDownRight = false;

        // Buttons

        bool isOnScreenKeyboardButtonDown = false;
        long backSpaceButtonLastPressed = 0;
        bool isEnterButtonDown = false;

        while (controller.IsConnected)
        {

            State state = controller.GetState();
            if (oldState.PacketNumber != state.PacketNumber)
            {
                // Get input

                Gamepad gamepad = state.Gamepad;
                int x = gamepad.LeftThumbX;
                int y = gamepad.LeftThumbY;
                int leftTrigger = gamepad.LeftTrigger;
                int rightTrigger = gamepad.RightTrigger;

                int mouseX = x / LEFT_THUMB_DIVISOR;
                int mouseY = -y / LEFT_THUMB_DIVISOR;

                uint uMouseX = (uint) mouseX;
                uint uMouseY = (uint) mouseY;

                // Offset cursor position

                PNT currentCursorPosition;
                GetCursorPos(out currentCursorPosition);

                SetCursorPos(currentCursorPosition.x + mouseX, currentCursorPosition.y + mouseY);

                // Left click with right trigger

                if (rightTrigger >= TRIGGER_UPPER_CUTOFF && !isMouseDownLeft)
                {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, uMouseX, uMouseY, 0, 0);
                    isMouseDownLeft = true;
                }
                else if (rightTrigger <= TRIGGER_LOWER_CUTOFF && isMouseDownLeft)
                {
                    mouse_event(MOUSEEVENTF_LEFTUP, uMouseX, uMouseY, 0, 0);
                    isMouseDownLeft = false;
                }

                // Right click with left trigger

                if (leftTrigger >= TRIGGER_UPPER_CUTOFF && !isMouseDownRight)
                {
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, uMouseX, uMouseY, 0, 0);
                    isMouseDownRight = true;
                }
                else if (leftTrigger <= TRIGGER_LOWER_CUTOFF && isMouseDownRight)
                {
                    mouse_event(MOUSEEVENTF_RIGHTUP, uMouseX, uMouseY, 0, 0);
                    isMouseDownRight = false;
                }

                // Get scroll input

                int rightX = gamepad.RightThumbX;
                int rightY = gamepad.RightThumbY;

                int scrollX = rightX / RIGHT_THUMB_DIVISOR;
                int scrollY = rightY / RIGHT_THUMB_DIVISOR;

                uint uScrollX = (uint) scrollX;
                uint uScrollY = (uint) scrollY;

                // Scroll

                mouse_event(MOUSEEVENTF_WHEEL, uMouseX, uMouseY, uScrollY, 0);

                // Buttons

                GamepadButtonFlags buttonFlags = gamepad.Buttons;

                // Handle buttons

                // On screen keyboard

                if (buttonFlags.HasFlag(KEYBOARD_BUTTON))
                {
                    if (!isOnScreenKeyboardButtonDown)
                    {

                        try
                        {
                            var uiHostNoLaunch = new UIHostNoLaunch();
                            var tipInvocation = (ITipInvocation)uiHostNoLaunch;
                            tipInvocation.Toggle(GetDesktopWindow());
                            Marshal.ReleaseComObject(uiHostNoLaunch);
                        } catch (Exception e)
                        {
                            error(e, "Keyboard launch failed... Attempting to re-register keyboard COM...");

                            // Launch tabtip manually
                            try {
                                string workingDirectory = "C:\\Program Files\\Common Files\\microsoft shared\\ink";

                                ProcessStartInfo processInfo = new ProcessStartInfo(Path.Join(workingDirectory, "TabTip.exe"));
                                processInfo.UseShellExecute = true;
                                processInfo.WorkingDirectory = workingDirectory;

                                Process.Start(processInfo);
                            } catch (Exception ex)
                            {
                                error(ex, "Keyboard COM registration failed!");
                            }
                        }

                        isOnScreenKeyboardButtonDown = true;

                    }
                }
                else
                {
                    isOnScreenKeyboardButtonDown = false;
                }

                // Backspace


                if (buttonFlags.HasFlag(BACKSPACE_BUTTON))
                {
                    if (DateTime.UtcNow.Ticks > backSpaceButtonLastPressed + (BACKSPACE_BUTTON_COOLDOWN_MS * TimeSpan.TicksPerMillisecond))
                    {
                        keyPress(VK_BACK);

                        backSpaceButtonLastPressed = DateTime.UtcNow.Ticks;
                    }
                } else
                {
                    backSpaceButtonLastPressed = 0;
                }

                // Enter

                if (buttonFlags.HasFlag(ENTER_BUTTON))
                {
                    if (!isEnterButtonDown)
                    {
                        keyPress(VK_RETURN);

                        isEnterButtonDown = true;
                    }
                } else
                {
                    isEnterButtonDown = false;
                }

            }

            oldState = state;
            Thread.Sleep(POLLING_RATE);
        }
    }
}