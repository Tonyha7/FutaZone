using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Swed64;

namespace FutaZone
{
    public class TriggerBot
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
        }

        struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private static readonly TriggerBot _instance = new TriggerBot();
        public static TriggerBot Instance => _instance;

        public bool Enabled { get; set; } = false;
        public int TriggerKey { get; set; } = 0x12; // Default to ALT
        public int DelayMs { get; set; } = 15;
        public bool TriggerOnTeammates { get; set; } = false;
        public float MaxVelocityThreshold { get; set; } = 18f;

        private Stopwatch timer = new Stopwatch();

        public TriggerBot()
        {
            timer.Start();
        }

        public void Process(Swed swed, IntPtr client, Entity localPlayer)
        {
            if (!Enabled || (GetAsyncKeyState(TriggerKey) & 0x8000) == 0)
            {
                return;
            }

            if (timer.ElapsedMilliseconds < DelayMs) return;

            IntPtr localPlayerPawn = swed.ReadPointer(client, Offsets.dwLocalPlayerPawn);
            if (localPlayerPawn == IntPtr.Zero) return;

            int entityId = swed.ReadInt(localPlayerPawn, Offsets.m_iIDEntIndex);
            if (entityId <= 0) return;

            IntPtr entityList = swed.ReadPointer(client, Offsets.dwEntityList);
            if (entityList == IntPtr.Zero) return;

            IntPtr listEntry = swed.ReadPointer(entityList, 0x8 * (entityId >> 9) + 0x10);
            if (listEntry == IntPtr.Zero) return;

            IntPtr currentPawn = swed.ReadPointer(listEntry, 0x70 * (entityId & 0x1FF));
            if (currentPawn == IntPtr.Zero) return;

            int entityTeam = swed.ReadInt(currentPawn, Offsets.m_iTeamNum);
            int lifeState = swed.ReadInt(currentPawn, Offsets.m_lifeState);

            // Check if alive
            if (lifeState != 256) return;

            // Check team
            bool isDifferentTeam = entityTeam != localPlayer.team;
            if (!TriggerOnTeammates && !isDifferentTeam) return;

            // Check velocity and flags
            int fFlags = swed.ReadInt(localPlayerPawn, Offsets.m_fFlags);
            Vector3 velocity = swed.ReadVec(localPlayerPawn, Offsets.m_vecAbsVelocity);
            
            bool isSpecialCondition = fFlags == 65664;
            bool isWithinVelocityLimit = Math.Abs(velocity.Z) <= MaxVelocityThreshold;

            if (!isSpecialCondition && !isWithinVelocityLimit) return;

            // Fire
            Click();
            timer.Restart();
        }

        private void Click()
        {
            INPUT[] inputs = new INPUT[2];
            
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            
            inputs[1].type = INPUT_MOUSE;
            inputs[1].u.mi.dwFlags = MOUSEEVENTF_LEFTUP;

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
