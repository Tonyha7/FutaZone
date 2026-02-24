using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FutaZone
{
    public static class Offsets
    {
        // global offsets
        public static int dwEntityList;
        public static int dwViewMatrix;
        public static int dwLocalPlayerPawn;
        public static int dwViewAngles;
        // Bomb Timer related
        public static int dwGlobalVars;
        public static int dwPlantedC4;

        // client.dll offsets
        public static int m_vOldOrigin;
        public static int m_iTeamNum;
        public static int m_lifeState;
        public static int m_hPlayerPawn;
        public static int m_vecViewOffset;
        public static int m_iHealth;
        public static int m_iMaxHealth;
        public static int m_iszPlayerName;
        public static int m_modelState;
        public static int m_pGameSceneNode;
        public static int m_iIDEntIndex;
        public static int m_vecAbsVelocity;
        public static int m_fFlags;
        public static int m_iPing;
        public static int m_vecVelocity;
        public static int m_flEmitSoundTime;
        public static int m_pBulletServices;
        public static int m_totalHitsOnServer;
        public static int m_entitySpottedState;
        public static int m_bSpotted;
        public static int m_flFlashDuration;
        // Player State fields
        public static int m_bIsBuyMenuOpen;
        public static int m_bIsScoped;
        public static int m_bIsDefusing;
        public static int m_bIsGrabbingHostage;
        public static int m_bPawnHasDefuser;
        // Bomb Timer related fields
        public static int m_flDefuseCountDown;
        public static int m_flC4Blow;
        public static int m_bBeingDefused;
        public static int m_nBombSite;
        public static int m_bBombDefused;

        public static async Task InitializeAsync()
        {
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            async Task<string> FetchJsonWithFallbackAsync(string primaryUrl, string fallbackUrl)
            {
                try
                {
                    Console.WriteLine($"Attempting to fetch from: {primaryUrl}");
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var response = await client.GetAsync(primaryUrl, cts.Token);
                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync();

                    // Validate JSON completeness
                    try 
                    {
                        using (JsonDocument.Parse(content)) { }
                    }
                    catch (JsonException)
                    {
                        throw new Exception("Get JSON is incomplete or invalid.");
                    }

                    return content;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Primary fetch failed ({ex.Message}). Falling back to: {fallbackUrl}");
                    return await client.GetStringAsync(fallbackUrl);
                }
            }
            
            try
            {
                // Fetch offsets from URL directly
                Console.WriteLine("Fetching offsets.json...");
                string offsetsJson = await FetchJsonWithFallbackAsync(
                    "https://ob.tonyha7.com/offsets.json",
                    "https://raw.githubusercontent.com/a2x/cs2-dumper/refs/heads/main/output/offsets.json"
                );
                
                using JsonDocument offsetsDoc = JsonDocument.Parse(offsetsJson);
                var clientDllOffsets = offsetsDoc.RootElement.GetProperty("client.dll");
                
                dwEntityList = clientDllOffsets.GetProperty("dwEntityList").GetInt32();
                dwViewMatrix = clientDllOffsets.GetProperty("dwViewMatrix").GetInt32();
                dwLocalPlayerPawn = clientDllOffsets.GetProperty("dwLocalPlayerPawn").GetInt32();
                dwViewAngles = clientDllOffsets.GetProperty("dwViewAngles").GetInt32();
                dwGlobalVars = clientDllOffsets.GetProperty("dwGlobalVars").GetInt32();
                dwPlantedC4 = clientDllOffsets.GetProperty("dwPlantedC4").GetInt32();

                Console.WriteLine("Fetching client_dll.json...");
                string clientDllJson = await FetchJsonWithFallbackAsync(
                    "https://ob.tonyha7.com/client_dll.json",
                    "https://raw.githubusercontent.com/a2x/cs2-dumper/refs/heads/main/output/client_dll.json"
                );
                
                using JsonDocument clientDllDoc = JsonDocument.Parse(clientDllJson);
                var classes = clientDllDoc.RootElement.GetProperty("client.dll").GetProperty("classes");

                // Helper function to find field offset
                int GetFieldOffset(string className, string fieldName)
                {
                    return classes.GetProperty(className).GetProperty("fields").GetProperty(fieldName).GetInt32();
                }

                m_vOldOrigin = GetFieldOffset("C_BasePlayerPawn", "m_vOldOrigin");
                m_iTeamNum = GetFieldOffset("C_BaseEntity", "m_iTeamNum");
                m_lifeState = GetFieldOffset("C_BaseEntity", "m_lifeState");
                m_hPlayerPawn = GetFieldOffset("CCSPlayerController", "m_hPlayerPawn");
                m_vecViewOffset = GetFieldOffset("C_BaseModelEntity", "m_vecViewOffset");
                m_iHealth = GetFieldOffset("C_BaseEntity", "m_iHealth");
                m_iMaxHealth = GetFieldOffset("C_BaseEntity", "m_iMaxHealth");
                m_iszPlayerName = GetFieldOffset("CBasePlayerController", "m_iszPlayerName");
                m_modelState = GetFieldOffset("CSkeletonInstance", "m_modelState");
                m_pGameSceneNode = GetFieldOffset("C_BaseEntity", "m_pGameSceneNode");
                m_iIDEntIndex = GetFieldOffset("C_CSPlayerPawn", "m_iIDEntIndex");
                m_vecAbsVelocity = GetFieldOffset("C_BaseEntity", "m_vecAbsVelocity");
                m_fFlags = GetFieldOffset("C_BaseEntity", "m_fFlags");
                m_iPing = GetFieldOffset("CCSPlayerController", "m_iPing");
                m_vecVelocity = GetFieldOffset("C_BaseEntity", "m_vecVelocity");
                m_flEmitSoundTime = GetFieldOffset("C_CSPlayerPawn", "m_flEmitSoundTime");
                m_pBulletServices = GetFieldOffset("C_CSPlayerPawn", "m_pBulletServices");
                m_totalHitsOnServer = GetFieldOffset("CCSPlayer_BulletServices", "m_totalHitsOnServer");
                m_entitySpottedState = GetFieldOffset("C_CSPlayerPawn", "m_entitySpottedState");
                m_bSpotted = GetFieldOffset("EntitySpottedState_t", "m_bSpotted");
                m_flFlashDuration = GetFieldOffset("C_CSPlayerPawnBase", "m_flFlashDuration");
                
                // Player State fields
                m_bIsBuyMenuOpen = GetFieldOffset("C_CSPlayerPawn", "m_bIsBuyMenuOpen");
                m_bIsScoped = GetFieldOffset("C_CSPlayerPawn", "m_bIsScoped");
                m_bIsDefusing = GetFieldOffset("C_CSPlayerPawn", "m_bIsDefusing");
                m_bIsGrabbingHostage = GetFieldOffset("C_CSPlayerPawn", "m_bIsGrabbingHostage");
                m_bPawnHasDefuser = GetFieldOffset("CCSPlayerController", "m_bPawnHasDefuser");

                // C_PlantedC4 fields
                m_flDefuseCountDown = GetFieldOffset("C_PlantedC4", "m_flDefuseCountDown");
                m_flC4Blow = GetFieldOffset("C_PlantedC4", "m_flC4Blow");
                m_bBeingDefused = GetFieldOffset("C_PlantedC4", "m_bBeingDefused");
                m_nBombSite = GetFieldOffset("C_PlantedC4", "m_nBombSite");
                m_bBombDefused = GetFieldOffset("C_PlantedC4", "m_bBombDefused");

                Console.WriteLine("Offsets loaded successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load offsets: {ex.Message}");
                throw;
            }
        }
    }
}
