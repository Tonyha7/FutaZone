using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Swed64;

namespace FutaZone
{
    class Program
    {
        [DllImport("user32.dll")]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        public static extern uint timeEndPeriod(uint uPeriod);

        static async Task Main(string[] args)
        {
            timeBeginPeriod(1); // Set timer resolution to 1ms
            Console.WriteLine("Initializing FutaZone...");
            
            // Fetch offsets from URL
            await Offsets.InitializeAsync();

            try
            {
                Process[] cs2Processes = Process.GetProcessesByName("cs2");
                if (cs2Processes.Length == 0)
                {
                    Console.WriteLine("CS2 process not found. Please start the game first.");
                    return;
                }
                Process cs2Process = cs2Processes[0];

                Swed swed = new Swed("cs2");
                IntPtr client = swed.GetModuleBase("client.dll");
                Console.WriteLine($"client.dll base: 0x{client:X}");

                //init renderer
                Renderer renderer = new Renderer();
                Thread renderThread = new Thread(() => renderer.Start().Wait());
                renderThread.Start();

                //get screen size
                Vector2 screenSize = renderer.screenSize;

                List<Entity> entities = new List<Entity>();
                Entity localPlayer = new Entity();

                //esp logic
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Get refresh rate
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm);
                int targetFps = dm.dmDisplayFrequency;
                if (targetFps == 0) targetFps = 144; // Fallback

                Console.WriteLine($"Target FPS set to screen refresh rate: {targetFps}");

                while (true)
                {
                    if (cs2Process.HasExited)
                    {
                        Console.WriteLine("CS2 process ended. Exiting FutaZone...");
                        Environment.Exit(0);
                    }

                    entities.Clear();
                    IntPtr entityList = swed.ReadPointer(client, Offsets.dwEntityList);

                    // make entry
                    IntPtr listEntry = swed.ReadPointer(entityList, 0x10);
                    
                    //get localplayer
                    IntPtr localPlayerPawn = swed.ReadPointer(client, Offsets.dwLocalPlayerPawn);
                    float[] viewMatrix = swed.ReadMatrix(client + Offsets.dwViewMatrix);
                    localPlayer.team = swed.ReadInt(localPlayerPawn, Offsets.m_iTeamNum);
                    localPlayer.position = swed.ReadVec(localPlayerPawn, Offsets.m_vOldOrigin);
                    Vector3 velocity = swed.ReadVec(localPlayerPawn, Offsets.m_vecVelocity);
                    localPlayer.velocityVec = velocity;
                    localPlayer.velocity = (int)Math.Round(new Vector2(velocity.X, velocity.Y).Length());
                    
                    Vector3 viewAngles = swed.ReadVec(client, Offsets.dwViewAngles);
                    localPlayer.viewAngles = new Vector2(viewAngles.X, viewAngles.Y);

                    // Update local player name and ping
                    IntPtr localPlayerController = swed.ReadPointer(client, Offsets.dwEntityList); // We need to find local player controller, but typically it is at a known index or we iterate like below.
                    // Actually, local player controller is usually at index 1 or we can get it from dwLocalPlayerController if it exists, 
                    // or we have to find the controller that controls the local pawn.
                    // For simplicity, let's find it in the loop or assume it's the first one if not readily available as a direct pointer offset.
                    // However, in the loop below we iterate all controllers. Let's just capture local player data inside the loop when we find it.
                    
                    // We need to reset local player name/ping in case we don't find it
                    // localPlayer.name = "Unknown"; // Keep previous value or reset
                    // localPlayer.ping = 0;


                    for (int i = 0; i < 64; i++)
                    {
                        IntPtr currentController = swed.ReadPointer(listEntry, i * 0x70);
                        if (currentController == IntPtr.Zero) continue;

                        //get pawn handle
                        int pawnHandle = swed.ReadInt(currentController, Offsets.m_hPlayerPawn);
                        if (pawnHandle == 0) continue;

                        //get current pawn 
                        IntPtr listEntry2 = swed.ReadPointer(entityList, 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
                        if (listEntry2 == IntPtr.Zero) continue;
                        
                        //get current pawn
                        IntPtr currentPawn = swed.ReadPointer(listEntry2, 0x70 * (pawnHandle & 0x1FF));
                        if (currentPawn == IntPtr.Zero) continue;
                        
                        //check if is alive
                        int lifeState = swed.ReadInt(currentPawn, Offsets.m_lifeState);
                        if (lifeState != 256) continue;

                        IntPtr gameSceneNode = swed.ReadPointer(currentPawn, Offsets.m_pGameSceneNode);
                        IntPtr boneMatrix = swed.ReadPointer(gameSceneNode, Offsets.m_modelState + 0x80);
                        
                        //populate entity data
                        Entity entity = new Entity();
                        entity.team = swed.ReadInt(currentPawn + Offsets.m_iTeamNum);
                        entity.position = swed.ReadVec(currentPawn + Offsets.m_vOldOrigin);
                        entity.viewOffset = swed.ReadVec(currentPawn + Offsets.m_vecViewOffset);
                        entity.position2D = Calculate.WorldToScreen(viewMatrix, entity.position, screenSize);
                        entity.health = swed.ReadInt(currentPawn + Offsets.m_iHealth);
                        entity.maxHealth = swed.ReadInt(currentPawn + Offsets.m_iMaxHealth);
                        entity.viewPosition2D = Calculate.WorldToScreen(viewMatrix, Vector3.Add(entity.position, entity.viewOffset), screenSize);
                        entity.name = swed.ReadString(currentController, Offsets.m_iszPlayerName, 16);
                        entity.ping = swed.ReadInt(currentController, Offsets.m_iPing);
                        entity.distance = Vector3.Distance(entity.position, localPlayer.position);
                        entity.bones = Calculate.ReadBones(boneMatrix, swed);
                        entity.bones2d = Calculate.ReadBones2D(entity.bones, viewMatrix, screenSize);
                        entity.isLocalPlayer = (currentPawn == localPlayerPawn);
                        entity.emitSoundTime = swed.ReadFloat(currentPawn, Offsets.m_flEmitSoundTime);
                        entity.flags = (uint)swed.ReadInt(currentPawn, Offsets.m_fFlags);
                        entity.controllerAddress = currentController.ToInt64();
                        Vector3 entVelocity = swed.ReadVec(currentPawn, Offsets.m_vecVelocity);
                        entity.velocityVec = entVelocity;
                        entity.velocity = (int)Math.Round(new Vector2(entVelocity.X, entVelocity.Y).Length());

                        if (entity.isLocalPlayer)
                        {
                            localPlayer.name = entity.name;
                            localPlayer.ping = entity.ping;
                        }

                        entities.Add(entity);
                    }
                    
                    //update renderer entities
                    // run aimbot processing (if enabled)
                    Aimbot.Instance.Process(localPlayer, entities, screenSize);
                    
                    // run triggerbot processing (if enabled)
                    TriggerBot.Instance.Process(swed, client, localPlayer);

                    // run autostop processing (if enabled)
                    AutoStop.Instance.Process(localPlayer);

                    BombTimer.Update(swed, client);
                    renderer.UpdateLocalPlayer(localPlayer);
                    renderer.UpdateEntities(entities);
                    renderer.UpdateViewMatrix(viewMatrix);
                    
                    long targetFrameTime = 1000 / targetFps;
                    long frameTime = stopwatch.ElapsedMilliseconds;
                    
                    int sleepTime = (int)(targetFrameTime - frameTime);
                    if (sleepTime > 0)
                    {
                        Thread.Sleep(sleepTime);
                    }
                    stopwatch.Restart();
                }
            }
            catch (Exception ex)
            {
                timeEndPeriod(1); // Reset timer resolution
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Make sure CS2 is running.");
                Environment.Exit(0);
            }
        }
    }
}