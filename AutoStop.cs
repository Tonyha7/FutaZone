using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FutaZone
{
    public class AutoStop
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        // Scan codes for WASD
        private const ushort SCANCODE_W = 0x11;
        private const ushort SCANCODE_A = 0x1E;
        private const ushort SCANCODE_S = 0x1F;
        private const ushort SCANCODE_D = 0x20;

        // Virtual keys for WASD
        private const int VK_W = 0x57;
        private const int VK_A = 0x41;
        private const int VK_S = 0x53;
        private const int VK_D = 0x44;

        private static readonly AutoStop _instance = new AutoStop();
        public static AutoStop Instance => _instance;

        public bool Enabled { get; set; } = false;
        public float TriggerThreshold { get; set; } = 50f;
        public float StopThreshold { get; set; } = 15f;

        private bool isSimulatingW = false;
        private bool isSimulatingA = false;
        private bool isSimulatingS = false;
        private bool isSimulatingD = false;

        public void Process(Entity localPlayer)
        {
            if (!Enabled || localPlayer == null || localPlayer.health <= 0)
            {
                ReleaseAll();
                return;
            }

            // Check if user is pressing keys (ignoring our simulated keys)
            bool isUserW = (GetAsyncKeyState(VK_W) & 0x8000) != 0 && !isSimulatingW;
            bool isUserA = (GetAsyncKeyState(VK_A) & 0x8000) != 0 && !isSimulatingA;
            bool isUserS = (GetAsyncKeyState(VK_S) & 0x8000) != 0 && !isSimulatingS;
            bool isUserD = (GetAsyncKeyState(VK_D) & 0x8000) != 0 && !isSimulatingD;

            // If player is manually pressing any movement key, cancel auto stop and let them move
            if (isUserW || isUserA || isUserS || isUserD)
            {
                ReleaseAll();
                return;
            }

            float yaw = localPlayer.viewAngles.Y;
            float yawRad = yaw * (float)Math.PI / 180f;
            float cosYaw = (float)Math.Cos(yawRad);
            float sinYaw = (float)Math.Sin(yawRad);

            float velX = localPlayer.velocityVec.X;
            float velY = localPlayer.velocityVec.Y;

            // Calculate local velocity relative to view angles
            float localForwardVel = velX * cosYaw + velY * sinYaw;
            float localRightVel = velX * sinYaw - velY * cosYaw;

            // Determines if we should be pressing a key based on current state and thresholds
            // Hysteresis logic: 
            // - If not pressing, start pressing if speed > TriggerThreshold
            // - If already pressing, keep pressing until speed < StopThreshold

            // Calculates the velocity component in the direction that this key counters.
            // e.g., if we are considering pressing W (forward), we look at backward velocity (-localForwardVel).
            // If backward velocity is high, we need to press W.

            bool needW = DetermineState(isSimulatingW, -localForwardVel); // Moving backward -> press W
            bool needS = DetermineState(isSimulatingS, localForwardVel);  // Moving forward -> press S
            bool needA = DetermineState(isSimulatingA, localRightVel);    // Moving right -> press A
            bool needD = DetermineState(isSimulatingD, -localRightVel);   // Moving left -> press D

            // Apply inputs
            UpdateKey(SCANCODE_W, needW, ref isSimulatingW);
            UpdateKey(SCANCODE_A, needA, ref isSimulatingA);
            UpdateKey(SCANCODE_S, needS, ref isSimulatingS);
            UpdateKey(SCANCODE_D, needD, ref isSimulatingD);
        }

        private bool DetermineState(bool currentlySimulating, float velocityInAxis)
        {
            if (currentlySimulating)
            {
                // If we are already countering force, keep doing it until velocity drops below StopThreshold
                return velocityInAxis > StopThreshold;
            }
            else
            {
                // If not currently countering, start if velocity exceeds TriggerThreshold
                return velocityInAxis > TriggerThreshold;
            }
        }

        private void UpdateKey(ushort scanCode, bool needPress, ref bool isSimulating)
        {
            if (needPress && !isSimulating)
            {
                SendKey(scanCode, true);
                isSimulating = true;
            }
            else if (!needPress && isSimulating)
            {
                SendKey(scanCode, false);
                isSimulating = false;
            }
        }

        private void ReleaseAll()
        {
            if (isSimulatingW) { SendKey(SCANCODE_W, false); isSimulatingW = false; }
            if (isSimulatingA) { SendKey(SCANCODE_A, false); isSimulatingA = false; }
            if (isSimulatingS) { SendKey(SCANCODE_S, false); isSimulatingS = false; }
            if (isSimulatingD) { SendKey(SCANCODE_D, false); isSimulatingD = false; }
        }

        private void SendKey(ushort scanCode, bool keyDown)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wScan = scanCode;
            inputs[0].u.ki.dwFlags = KEYEVENTF_SCANCODE | (keyDown ? 0 : KEYEVENTF_KEYUP);
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
