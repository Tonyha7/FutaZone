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

        public static async Task InitializeAsync()
        {
            using HttpClient client = new HttpClient();
            
            try
            {
                Console.WriteLine("Fetching offsets.json...");
                string offsetsJson = await client.GetStringAsync("https://ob.tonyha7.com/offsets.json");
                using JsonDocument offsetsDoc = JsonDocument.Parse(offsetsJson);
                var clientDllOffsets = offsetsDoc.RootElement.GetProperty("client.dll");
                
                dwEntityList = clientDllOffsets.GetProperty("dwEntityList").GetInt32();
                dwViewMatrix = clientDllOffsets.GetProperty("dwViewMatrix").GetInt32();
                dwLocalPlayerPawn = clientDllOffsets.GetProperty("dwLocalPlayerPawn").GetInt32();

                Console.WriteLine("Fetching client_dll.json...");
                string clientDllJson = await client.GetStringAsync("https://ob.tonyha7.com/client_dll.json");
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
