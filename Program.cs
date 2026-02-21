using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Swed64;

namespace FutaZone
{
    class Program
    {
        static async Task Main(string[] args)
        {
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
                        entity.distance = Vector3.Distance(entity.position, localPlayer.position);
                        entity.bones = Calculate.ReadBones(boneMatrix, swed);
                        entity.bones2d = Calculate.ReadBones2D(entity.bones, viewMatrix, screenSize);
                        entity.isLocalPlayer = (currentPawn == localPlayerPawn);

                        entities.Add(entity);
                    }
                    
                    //update renderer entities
                    // run aimbot processing (if enabled)
                    Aimbot.Instance.Process(localPlayer, entities, screenSize);

                    BombTimer.Update(swed, client);
                    renderer.UpdateLocalPlayer(localPlayer);
                    renderer.UpdateEntities(entities);
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Make sure CS2 is running.");
                Environment.Exit(0);
            }
        }
    }
}