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

        public enum AimMode
        {
            Linear,         // Original
            FastThenSlow,   // 先快后慢
            SlowThenFast,   // 先慢后快
            Overshoot,      // 先快超过目标后再拉回
            Random          // 上述状态随机
        }

        public bool Enabled { get; set; } = true;
        public float FOV = 250f;
        public bool AimAtTeammates { get; set; } = false;
        public bool VisibleCheck { get; set; } = true;
        public bool DisableWhenFlashed { get; set; } = false;
        public float FlashDurationThreshold { get; set; } = 5.0f;
        public int TargetBoneIndex { get; set; } = 2; // Default to Head (index 2 in BoneIds)
        
        // Mode Settings
        public AimMode Mode { get; set; } = AimMode.Linear;
        public bool RandomizeSpeed { get; set; } = false;
        public int SpeedChangeDuration { get; set; } = 500; // ms

        private Stopwatch timer = new Stopwatch();
        private Stopwatch stateTimer = new Stopwatch();
        private Entity _lastTarget = null;
        private AimMode _currentRandomMode = AimMode.Linear;
        private float _overshootFactor = 1.0f;
        private bool _isOvershooting = false;

        private float _errorX = 0;
        private float _errorY = 0;

        // SETTINGS
        public float Smoothness = 3.0f;     // Division factor (higher = slower/smoother)
        public float OvershootScale { get; set; } = 1.2f; // Multiplier for overshoot

        public Vector2? TargetPosition { get; private set; }

        public Aimbot() { timer.Start(); stateTimer.Start(); }

        public void Process(Entity localPlayer, IEnumerable<Entity> entities, Vector2 screenSize)
        {
            // 10ms Pulse Timing
            if (timer.ElapsedMilliseconds < 10) return;
            timer.Restart();

            if (!Enabled)
            {
                _errorX = 0;
                _errorY = 0;
                _lastTarget = null;
                TargetPosition = null;
                return;
            }

            if (DisableWhenFlashed && localPlayer.flashDuration > FlashDurationThreshold)
            {
                _errorX = 0;
                _errorY = 0;
                _lastTarget = null;
                TargetPosition = null;
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
                if (e.team == 1 || e.team == 0) continue;
                
                // Check team
                if (!AimAtTeammates && e.team == localPlayer.team) continue;

                if (VisibleCheck && !e.isSpotted) continue;

                // THE BONE CALCULATION
                Vector2 targetBonePos = Vector2.Zero;
                if (e.bones2d != null && e.bones2d.Count > 0)
                {
                    int boneIdVal = (int)((BoneIds[])Enum.GetValues(typeof(BoneIds)))[TargetBoneIndex];
                    if (boneIdVal < e.bones2d.Count)
                    {
                        targetBonePos = e.bones2d[boneIdVal];
                    }
                    else
                    {
                        targetBonePos = e.viewPosition2D;
                    }
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
                TargetPosition = targetAimPoint;

                // Logic for Modes
                if (_lastTarget != bestTarget)
                {
                    _lastTarget = bestTarget;
                    stateTimer.Restart();
                    _isOvershooting = true; // For Overshoot mode

                    if (Mode == AimMode.Random)
                    {
                        var values = Enum.GetValues(typeof(AimMode));
                        // Exclude Random itself to prevent recursion, and maybe Linear if we want strict "special" modes
                        // But user said "Above states random", which implies Linear, FastThenSlow, SlowThenFast, Overshoot
                        Random rng = new Random();
                        int idx = rng.Next(0, 4); // 0 to 3
                        _currentRandomMode = (AimMode)idx;
                    }
                    else
                    {
                        _currentRandomMode = Mode;
                    }
                }

                float currentSmoothness = Smoothness;
                AimMode activeMode = (Mode == AimMode.Random) ? _currentRandomMode : Mode;
                long elapsed = stateTimer.ElapsedMilliseconds;
                
                // Determine duration based on RandomizeSpeed
                int duration = SpeedChangeDuration;
                if (RandomizeSpeed)
                {
                    // Use a deterministic random based on target entity and tick to vary duration between 50% and 150%
                    int seed = (bestTarget.GetHashCode() + (int)stateTimer.ElapsedTicks) & 0xFF;
                    float factor = 0.5f + (seed / 255.0f); // 0.5 to 1.5
                    duration = (int)(SpeedChangeDuration * factor);
                }

                switch (activeMode)
                {
                    case AimMode.FastThenSlow:
                        // Fast (low smooth) then Slow (high smooth)
                        if (elapsed < duration)
                            currentSmoothness = Math.Max(0.1f, Smoothness * 0.5f); // Fast
                        else
                            currentSmoothness = Smoothness * 2.0f; // Slow
                        break;
                    
                    case AimMode.SlowThenFast:
                        // Slow (high smooth) then Fast (low smooth)
                        if (elapsed < duration)
                            currentSmoothness = Smoothness * 2.0f; // Slow
                        else
                            currentSmoothness = Math.Max(0.1f, Smoothness * 0.5f); // Fast
                        break;

                    case AimMode.Overshoot:
                        // Move past target then come back.
                        // "Fast exceed target then pull back"
                        // Logic: If in first phase, target is extended past real target.
                        // Smoothness is fast.
                        if (elapsed < duration)
                        {
                            currentSmoothness = Math.Max(0.1f, Smoothness * 0.3f); // Faster for overshoot
                            Vector2 dir = targetAimPoint - screenCenter;
                            
                            // Calculate overshoot. 
                            // If we are close, we still want to overshoot by a visible amount (e.g. 50 pixels minimum if scale > 1)
                            float dist = dir.Length();
                            if (dist > 1.0f)
                            {
                                // Base overshoot on distance * scale
                                float extraScale = OvershootScale;
                                
                                // Add a "minimum" overshoot component so it's noticeable at close range
                                // e.g. add 20-50 pixels in the direction of the target
                                float addedDistance = 30.0f * (OvershootScale - 1.0f); // only adds if scale > 1
                                
                                Vector2 normalizedDir = Vector2.Normalize(dir);
                                Vector2 overshootVector = normalizedDir * ((dist * (extraScale - 1.0f)) + addedDistance);
                                
                                targetAimPoint = targetAimPoint + overshootVector; 
                            }
                        }
                        else
                        {
                            // Return to normal aiming (pull back)
                            // Make the return slightly slower/smoother than normal to simulate correction
                            // or just ensure control. 
                            currentSmoothness = Smoothness * 1.5f; 
                        }
                        break;
                    
                    case AimMode.Linear:
                    default:
                        currentSmoothness = Smoothness;
                        break;
                }

                // Calculate the smooth delta
                float deltaX = (targetAimPoint.X - screenCenter.X) / currentSmoothness;
                float deltaY = (targetAimPoint.Y - screenCenter.Y) / currentSmoothness;

                // Accumulate error to prevent 'jumpy' truncation
                float totalX = deltaX + _errorX;
                float totalY = deltaY + _errorY;

                int moveX = (int)totalX;
                int moveY = (int)totalY;

                _errorX = totalX - moveX;
                _errorY = totalY - moveY;

                if ((GetAsyncKeyState(AimKey) & 0x8000) != 0)
                {
                    if (moveX != 0 || moveY != 0)
                    {
                        MoveMouse(moveX, moveY);
                    }
                }
                else
                {
                    // Reset error and last target if key is not pressed to restart cycle when pressed
                    _errorX = 0;
                    _errorY = 0;
                    _lastTarget = null;
                }
            }
            else
            {
                TargetPosition = null;
                _lastTarget = null;
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
