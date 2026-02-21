using System;
using Swed64;

namespace FutaZone
{
    public static class BombTimer
    {
        public static string BombPlantedText = string.Empty;
        public static string TimerText = string.Empty;
        public static string DefuseText = string.Empty;
        public static float TimeLeft = 0f;
        public static float DefuseLeft = 0f;
        
        public static bool IsBombPlanted { get; private set; }

        private static bool _wasBombPlanted = false;
        private static long _plantTimeTick = 0;
        private static long _bombDisappearedTick = 0;
        private static float _c4Duration = 40.0f; // Standard C4 duration
        
        public static void Update(Swed swed, IntPtr client)
        {
            // 1. Read GlobalVars to get current time
            float currentTime = 0;
            IntPtr globalVarsPtr = swed.ReadPointer(client, Offsets.dwGlobalVars);
            if (globalVarsPtr == IntPtr.Zero)
            {
                 // Fallback
                 IntPtr structAddr = client + Offsets.dwGlobalVars;
                 currentTime = swed.ReadFloat(structAddr, 0x2C);
                 if (currentTime == 0) currentTime = swed.ReadFloat(structAddr, 0x34);
                 if (currentTime == 0) currentTime = swed.ReadFloat(structAddr, 0x10);
            }
            else
            {
                currentTime = swed.ReadFloat(globalVarsPtr, 0x2C);
                if (currentTime == 0) currentTime = swed.ReadFloat(globalVarsPtr, 0x34);
                if (currentTime == 0) currentTime = swed.ReadFloat(globalVarsPtr, 0x10);
            }
            
            // 2. Read Planted C4 info
            IntPtr tempC4 = swed.ReadPointer(client, Offsets.dwPlantedC4);
            IntPtr plantedC4 = swed.ReadPointer(tempC4, 0x0); // Read the pointer at tempC4
            
            // ReadInt returns 4 bytes. We only care if it's 1 (true).
            // Sometimes it might be garbage if not properly masked?
            // Let's assume >0 is fine but add checks.
            bool isBombPlanted = swed.ReadInt(client, Offsets.dwPlantedC4 - 0x8) > 0;
            
            // Additionally check if the plantedC4 pointer is valid.
            if (plantedC4 == IntPtr.Zero) isBombPlanted = false;
            
            IsBombPlanted = isBombPlanted;
            
            if (IsBombPlanted)
            {
                _bombDisappearedTick = 0;
                bool bombDefused = swed.ReadInt(plantedC4, Offsets.m_bBombDefused) > 0;
                
                // If it's defused, we shouldn't show it as "Planted" and counting down necessarily.
                if (bombDefused)
                {
                    IsBombPlanted = false;
                    _wasBombPlanted = false;
                    TimeLeft = 0;
                    DefuseLeft = 0;
                    BombPlantedText = "Defused!";
                    TimerText = "";
                    DefuseText = "";
                    return;
                }
                
                // Verify with C4Blow time if available
                float c4Blow = swed.ReadFloat(plantedC4, Offsets.m_flC4Blow);
                
                // If we have a valid currentTime and c4Blow is in the past, it exploded.
                // But we had issues with currentTime. 
                // However, c4Blow shouldn't prevent us from starting the timer if we just planted.
                
                // Detect NEW plant
                if (!_wasBombPlanted)
                {
                    _plantTimeTick = Environment.TickCount64;
                    _wasBombPlanted = true;
                    _bombDisappearedTick = 0;
                }
                else
                {
                    // If bomb was planted, but now we detect a new plant (maybe position changed? unlikely to check here easily without reading pos)
                    // We rely on the debounce time being short enough to not overlap rounds.
                }

                // If C4 exploded, usually the entity is removed or isBombPlanted becomes false.
                // If it restarts, maybe we are reading a false positive?
                // check if c4Blow is > 0.
                if (c4Blow > 0)
                {
                     // If c4Blow is very significantly in the past, maybe we shouldn't show it?
                     // But we rely on local timer now.
                }

                // Calculate elapsed seconds since detection
                float elapsedSeconds = (Environment.TickCount64 - _plantTimeTick) / 1000.0f;
                float timeLeft = Math.Max(_c4Duration - elapsedSeconds, 0);

                // If time left is 0, we can stop showing "Time: 0.00" or show "Exploded!"
                // If the user says it "restarts", it means it goes back to 40?
                // That means _wasBombPlanted became false then true.
                
                if (timeLeft == 0)
                {
                    TimerText = "Boom!";
                    BombPlantedText = "";
                    DefuseText = "";
                    TimeLeft = 0;
                    DefuseLeft = 0;
                     // Don't reset _wasBombPlanted here, wait for game to clear the flag
                    return;
                }

                // Still read other data for defusions
                float defuseCountDown = swed.ReadFloat(plantedC4, Offsets.m_flDefuseCountDown);
                bool beingDefused = swed.ReadInt(plantedC4, Offsets.m_bBeingDefused) > 0;
                int bombSite = swed.ReadInt(plantedC4, Offsets.m_nBombSite);
                
                float defuseLeft = 0;
                if (beingDefused && currentTime > 0)
                {
                    defuseLeft = Math.Max(defuseCountDown - currentTime, 0);
                }
                
                TimeLeft = timeLeft;
                DefuseLeft = defuseLeft;

                string siteName = bombSite == 1 ? "B" : "A";
                
                BombPlantedText = $"Bomb planted at {siteName}";
                TimerText = $"Time: {timeLeft:0.00} s";
                
                if (beingDefused)
                {
                   if (defuseLeft > 0)
                       DefuseText = $"Defusing: {defuseLeft:0.00} s";
                   else
                       DefuseText = "Defusing...";
                }
                else
                {
                    DefuseText = "";
                }
            }
            else
            {
                if (_wasBombPlanted)
                {
                    if (_bombDisappearedTick == 0)
                        _bombDisappearedTick = Environment.TickCount64;
                    
                    // Keep the state for 2.5 seconds after bomb disappears (exploded or round end)
                    // If round restarts very quickly, this might cause issues, but 2.5s should be safe for flickers.
                    if (Environment.TickCount64 - _bombDisappearedTick > 2500)
                    {
                        _wasBombPlanted = false;
                        _bombDisappearedTick = 0;
                        TimeLeft = 0;
                        DefuseLeft = 0;
                        BombPlantedText = "";
                        TimerText = "";
                        DefuseText = "";
                    }
                }
                else
                {
                    TimeLeft = 0;
                    DefuseLeft = 0;
                    BombPlantedText = "";
                    TimerText = "";
                    DefuseText = "";
                }
            }
        }
    }
}