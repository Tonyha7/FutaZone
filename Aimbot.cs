using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace FutaZone
{
    public class Aimbot
    {
        // Structs required for SendInput
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; }

        struct INPUT { public uint type; public INPUTUNION u; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        
        public int AimKey { get; set; } = 0x10; // Default to VK_SHIFT

        private static readonly Aimbot _instance = new Aimbot();
        public static Aimbot Instance => _instance;

        public bool Enabled { get; set; } = true;
        public float FOV = 250f;
        public bool AimAtTeammates { get; set; } = false;
        public int TargetBoneIndex { get; set; } = 2; // Default to Head (index 2 in BoneIds)

        private Stopwatch timer = new Stopwatch();
        private float _errorX = 0;
        private float _errorY = 0;

        // SETTINGS
        public float Smoothness = 3.0f;     // Division factor (higher = slower/smoother)

        public Aimbot() { timer.Start(); }

        public void Process(Entity localPlayer, IEnumerable<Entity> entities, Vector2 screenSize)
        {
            // 100ms Pulse Timing
            if (timer.ElapsedMilliseconds < 10) return;
            timer.Restart();

            if (!Enabled || (GetAsyncKeyState(AimKey) & 0x8000) == 0)
            {
                _errorX = 0;
                _errorY = 0;
                return;
            }

            Vector2 screenCenter = new Vector2(screenSize.X / 2f, screenSize.Y / 2f);

            Entity? bestTarget = null;
            float bestDist = float.MaxValue;
            Vector2 targetAimPoint = Vector2.Zero;

            foreach (var e in entities)
            {
                if (e == null) continue;

                if (e.position == Vector3.Zero) continue;

                // Skip spectators/observers completely for aimbot
                if (e.team == 1) continue;
                
                // Check team
                if (!AimAtTeammates && e.team == localPlayer.team) continue;

                // THE BONE CALCULATION
                Vector2 targetBonePos = Vector2.Zero;
                if (e.bones2d != null && e.bones2d.Count > TargetBoneIndex)
                {
                    targetBonePos = e.bones2d[TargetBoneIndex];
                }
                else
                {
                    // Fallback to viewPosition2D if bones are not available
                    targetBonePos = e.viewPosition2D;
                }

                // Distance check from crosshair to target bone
                float dist = Vector2.Distance(targetBonePos, screenCenter);
                if (dist < bestDist && dist < FOV)
                {
                    bestDist = dist;
                    bestTarget = e;
                    targetAimPoint = targetBonePos;
                }
            }

            if (bestTarget != null)
            {
                // Calculate the smooth delta
                float deltaX = (targetAimPoint.X - screenCenter.X) / Smoothness;
                float deltaY = (targetAimPoint.Y - screenCenter.Y) / Smoothness;

                // Accumulate error to prevent 'jumpy' truncation
                float totalX = deltaX + _errorX;
                float totalY = deltaY + _errorY;

                int moveX = (int)totalX;
                int moveY = (int)totalY;

                _errorX = totalX - moveX;
                _errorY = totalY - moveY;

                if (moveX != 0 || moveY != 0)
                {
                    MoveMouse(moveX, moveY);
                }
            }
        }

        private void MoveMouse(int x, int y)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = 0; // INPUT_MOUSE
            inputs[0].u.mi.dx = x;
            inputs[0].u.mi.dy = y;
            inputs[0].u.mi.dwFlags = 0x0001; // MOUSEEVENTF_MOVE
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
