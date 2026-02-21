using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickableTransparentOverlay;
using ImGuiNET;

namespace FutaZone
{
    public class Renderer : Overlay
    {
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        //renderer variables
        public Vector2 screenSize;
        //entities copies
        public ConcurrentQueue<Entity> entities = new ConcurrentQueue<Entity>();
        private Entity localPlayer = new Entity();
        private readonly object entityLock = new object();

        //GUI elements
        private bool enableESP = true;
        private bool enableLines = false;
        private bool enableBombTimer = true;
        private bool enableAimbot = false;
        private bool enableTriggerBot = false;
        private bool showTeammates = false; // Default to not showing teammates
        private Vector4 enemyColor = new Vector4(0.6f, 0.827f, 0.0f, 1.0f); // Lime green for enemy 

        private Vector4 teamColor = new Vector4(1.0f, 0.6f, 0.75f, 1.0f); // Sakura pink for team
        private Vector4 bonesColor = new Vector4(0.5f, 0.0f, 0.5f, 1.0f); // Deep purple for bones

        float boneThickness = 2.0f;

        private enum EspMode { Team = 0, Ffa = 1 }
        private EspMode espMode = EspMode.Team;

        //draw list
        ImDrawListPtr drawList;

        // UI visibility
        private bool showUI = true;
        private bool insKeyPressed = false;
        private bool styleInitialized = false;

        private void InitializeStyle()
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            
            style.WindowRounding = 8.0f;
            style.FrameRounding = 4.0f;
            style.GrabRounding = 4.0f;
            
            // Transparent light pink background
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(1.0f, 0.85f, 0.9f, 0.85f); 
            // Popup background (for combos, etc.) - Light pink
            style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(1.0f, 0.9f, 0.95f, 0.95f);
            
            // Sakura color for title and headers
            style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(1.0f, 0.6f, 0.75f, 0.9f); 
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(1.0f, 0.5f, 0.7f, 1.0f);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(1.0f, 0.6f, 0.75f, 0.7f);
            
            style.Colors[(int)ImGuiCol.Header] = new Vector4(1.0f, 0.65f, 0.8f, 0.8f);
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(1.0f, 0.55f, 0.75f, 0.9f);
            style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(1.0f, 0.45f, 0.7f, 1.0f);
            
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(1.0f, 0.95f, 0.95f, 0.7f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(1.0f, 0.85f, 0.9f, 0.8f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(1.0f, 0.75f, 0.85f, 0.9f);
            
            style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(0.9f, 0.3f, 0.5f, 1.0f);
            style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.9f, 0.4f, 0.6f, 1.0f);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.9f, 0.3f, 0.5f, 1.0f);
            
            style.Colors[(int)ImGuiCol.Button] = new Vector4(1.0f, 0.65f, 0.8f, 0.8f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(1.0f, 0.55f, 0.75f, 0.9f);
            style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(1.0f, 0.45f, 0.7f, 1.0f);
            
            style.Colors[(int)ImGuiCol.Text] = new Vector4(0.3f, 0.1f, 0.2f, 1.0f); // Dark pinkish text
        }

        public Renderer()
        {
            // Enable VSync
            VSync = true;
            
            // Auto-detect screen resolution
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            screenSize = new Vector2(screenWidth, screenHeight);

            // Load Chinese font
            ReplaceFont(@"c:\windows\fonts\msyh.ttc", 18, FontGlyphRangeType.ChineseFull);
        }

        protected override void Render()
        {
            if (!styleInitialized)
            {
                InitializeStyle();
                styleInitialized = true;
            }

            // Check for INS key to toggle UI
            bool vsync = VSync;
            bool currentInsState = (Aimbot.GetAsyncKeyState(0x2D) & 0x8000) != 0; // 0x2D is VK_INSERT
            if (currentInsState && !insKeyPressed)
            {
                showUI = !showUI;
            }
            insKeyPressed = currentInsState;

            if (showUI)
            {
                ImGui.Begin("FutaZone");

                // ESP feature group
                if (ImGui.CollapsingHeader("ESP (透视)", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Checkbox("Enable ESP (开启透视)", ref enableESP);
                    if (enableESP)
                    {
                        ImGui.Checkbox("Enable Lines (开启射线)", ref enableLines);
                        ImGui.Checkbox("Show Teammates (显示队友)", ref showTeammates);
                        // ESP settings shown when feature expanded
                        ImGui.Text("Mode (模式):");
                        ImGui.SameLine();
                        // Team or FFA mode toggle
                        bool isTeam = espMode == EspMode.Team;
                        if (ImGui.RadioButton("Team (团队)", isTeam)) espMode = EspMode.Team;
                        ImGui.SameLine();
                        bool isFfa = espMode == EspMode.Ffa;
                        if (ImGui.RadioButton("FFA (死斗)", isFfa)) espMode = EspMode.Ffa;

                        // color pickers
                        ImGui.ColorEdit4("Team Color (队伍颜色)", ref teamColor);
                        ImGui.ColorEdit4("Enemy Color (敌人颜色)", ref enemyColor);
                        ImGui.ColorEdit4("Bones Color (骨骼颜色)", ref bonesColor);
                    }
                }

                // Aimbot feature group
                if (ImGui.CollapsingHeader("Aimbot (自瞄)", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Checkbox("Enable Aimbot (开启自瞄)", ref enableAimbot);
                    Aimbot.Instance.Enabled = enableAimbot;
                    if (enableAimbot)
                    {
                        // simple aimbot settings
                        float fov = Aimbot.Instance.FOV;
                        if (ImGui.SliderFloat("FOV (范围)", ref fov, 32f, 800f)) Aimbot.Instance.FOV = fov;
                        
                        float smoothness = Aimbot.Instance.Smoothness;
                        if (ImGui.SliderFloat("Smoothness (平滑度)", ref smoothness, 0.1f, 50.0f)) Aimbot.Instance.Smoothness = smoothness;
                        
                        bool aimAtTeammates = Aimbot.Instance.AimAtTeammates;
                        if (ImGui.Checkbox("Aim at Teammates (瞄准队友)", ref aimAtTeammates)) Aimbot.Instance.AimAtTeammates = aimAtTeammates;

                        // Keybind selection
                        int currentKey = Aimbot.Instance.AimKey;
                        string[] keyNames = { "LBUTTON (左键)", "RBUTTON (右键)", "MBUTTON (中键)", "XBUTTON1 (下侧键)", "XBUTTON2 (上侧键)", "SHIFT", "ALT", "CTRL" };
                        int[] keyCodes = { 0x01, 0x02, 0x04, 0x05, 0x06, 0x10, 0x12, 0x11 };
                        
                        int selectedIndex = Array.IndexOf(keyCodes, currentKey);
                        if (selectedIndex == -1) selectedIndex = 5; // Default to SHIFT

                        if (ImGui.Combo("Aim Key (自瞄按键)", ref selectedIndex, keyNames, keyNames.Length))
                        {
                            Aimbot.Instance.AimKey = keyCodes[selectedIndex];
                        }

                        // Bone selection
                        int currentBoneIndex = Aimbot.Instance.TargetBoneIndex;
                        string[] boneNames = { "Waist (腰部)", "Neck (颈部)", "Head (头部)", "ShoulderL (左肩)", "ForeL (左前臂)", "HandL (左手)", "ShoulderR (右肩)", "ForeR (右前臂)", "HandR (右手)", "KneeL (左膝)", "FeetL (左脚)", "KneeR (右膝)", "FeetR (右脚)" };
                        
                        if (ImGui.Combo("Aim Bone (自瞄部位)", ref currentBoneIndex, boneNames, boneNames.Length))
                        {
                            Aimbot.Instance.TargetBoneIndex = currentBoneIndex;
                        }

                        ImGui.Text($"Hold {keyNames[selectedIndex]} to activate aimbot pulse ({Aimbot.Instance.FOV} px)");
                    }
                }

                // TriggerBot feature group
                if (ImGui.CollapsingHeader("TriggerBot (自动开火)", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Checkbox("Enable TriggerBot (开启自动开火)", ref enableTriggerBot);
                    TriggerBot.Instance.Enabled = enableTriggerBot;
                    if (enableTriggerBot)
                    {
                        int delay = TriggerBot.Instance.DelayMs;
                        if (ImGui.SliderInt("Delay (延迟 ms)", ref delay, 0, 100)) TriggerBot.Instance.DelayMs = delay;

                        float maxVelocity = TriggerBot.Instance.MaxVelocityThreshold;
                        if (ImGui.SliderFloat("Max Velocity Z (最大Z轴速度)", ref maxVelocity, 0f, 50f)) TriggerBot.Instance.MaxVelocityThreshold = maxVelocity;

                        bool triggerOnTeammates = TriggerBot.Instance.TriggerOnTeammates;
                        if (ImGui.Checkbox("Trigger on Teammates (对队友开火)", ref triggerOnTeammates)) TriggerBot.Instance.TriggerOnTeammates = triggerOnTeammates;

                        // Keybind selection
                        int currentKey = TriggerBot.Instance.TriggerKey;
                        string[] keyNames = { "LBUTTON (左键)", "RBUTTON (右键)", "MBUTTON (中键)", "XBUTTON1 (下侧键)", "XBUTTON2 (上侧键)", "SHIFT", "ALT", "CTRL" };
                        int[] keyCodes = { 0x01, 0x02, 0x04, 0x05, 0x06, 0x10, 0x12, 0x11 };
                        
                        int selectedIndex = Array.IndexOf(keyCodes, currentKey);
                        if (selectedIndex == -1) selectedIndex = 6; // Default to ALT

                        if (ImGui.Combo("Trigger Key (开火按键)", ref selectedIndex, keyNames, keyNames.Length))
                        {
                            TriggerBot.Instance.TriggerKey = keyCodes[selectedIndex];
                        }

                        ImGui.Text($"Hold {keyNames[selectedIndex]} to activate triggerbot");
                    }
                }
                
                ImGui.Checkbox("Enable Bomb Timer (C4计时器)", ref enableBombTimer);
                if (ImGui.Checkbox("Enable VSync (垂直同步)", ref vsync)) VSync = vsync;
                ImGui.Text("Press INS to show/hide menu (按INS显示/隐藏菜单)");
                ImGui.End(); // End "FutaZone ESP"
            }

            //draw overlay
            DrawOverlay(screenSize);
            drawList = ImGui.GetWindowDrawList();

            // Draw aimbot range circle around the crosshair when aimbot is enabled
            if (Aimbot.Instance.Enabled)
            {
                Vector2 screenCenter = new Vector2(screenSize.X / 2f, screenSize.Y / 2f);
                uint circleColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.6f, 0.75f, 0.6f)); // Sakura pink
                float visualRadius = Aimbot.Instance.FOV * 0.5f;
                drawList.AddCircle(screenCenter, visualRadius, circleColor, 32, 2.0f);
            }

            if (enableESP)
            {
                lock (entityLock)
                {
                    foreach (var entity in entities)
                    {
                        if (entity.isLocalPlayer) continue;

                        if (entity.position == Vector3.Zero) continue;

                        // Skip teammates if showTeammates is false and we are in Team mode
                        if (!showTeammates && espMode == EspMode.Team && entity.team == localPlayer.team) continue;

                        // Filter out huge entities (likely non-players or glitched models in casual mode)
                        // Standard distance waist-head is ~30 units. We allow up to 60 as a generous limit.
                        if (entity.bones != null && entity.bones.Count > 6)
                        {
                            float height3D = Vector3.Distance(entity.bones[0], entity.bones[6]);
                            if (height3D > 60.0f) continue;
                        }

                        if (enableLines && entity.position2D != new Vector2(-99, -99)) DrawLine(entity);

                        if (EntityOnScreen(entity))
                        {
                            // If player is an observer (Team 1), only draw head circle and skip other visuals
                            if (entity.team == 1)
                            {
                                if (entity.bones2d != null && entity.bones2d.Count > 2)
                                {
                                    uint headColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // White for observer
                                    drawList.AddCircle(entity.bones2d[2], 5.0f, headColor);
                                }
                                continue;
                            }

                            DrawHealthBar(entity);
                            DrawBox(entity);
                            DrawBones(entity);
                        }
                    }
                }
            }

            if (enableBombTimer && BombTimer.IsBombPlanted)
            {
                // C4 Timer Progress Bar
                // Assume 40 seconds max duration for C4
                float maxTime = 40.0f; 
                float timeLeft = BombTimer.TimeLeft;
                float progress = Math.Clamp(timeLeft / maxTime, 0.0f, 1.0f);
                
                // Bar dimensions - Full left side column
                float barWidth = 25.0f;
                float barMaxHeight = screenSize.Y;
                float startX = 0.0f;
                float startY = 0.0f;
                
                // Background bar (dark gray/black)
                drawList.AddRectFilled(new Vector2(startX, startY), new Vector2(startX + barWidth, startY + barMaxHeight), ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.6f)));
                
                // Progress bar (Green)
                // "Shrink from bottom" -> Means the bar should be anchored at Top, and its bottom edge moves up as time decreases.
                // At 100%, bottom is Y=ScreenHeight. At 0%, bottom is Y=0.
                
                Vector4 barColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // Green
                float currentHeight = barMaxHeight * progress;
                drawList.AddRectFilled(new Vector2(startX, startY), new Vector2(startX + barWidth, currentHeight), ImGui.ColorConvertFloat4ToU32(barColor));

                // Text Info at Top Left
                string text = BombTimer.BombPlantedText;
                if (!string.IsNullOrEmpty(BombTimer.TimerText)) text += $"\n{BombTimer.TimerText}";
                if (!string.IsNullOrEmpty(BombTimer.DefuseText)) text += $"\n{BombTimer.DefuseText}";
                
                // Draw pink text to the right of the bar at the top
                // Sakura Pink: (1.0f, 0.6f, 0.75f, 1.0f)
                uint col = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.6f, 0.75f, 1.0f));
                drawList.AddText(new Vector2(startX + barWidth + 10, 10), col, text);
            }

            // End the overlay and the main GUI window (end inner overlay first)
            ImGui.End(); // End "Overlay"
        }

        bool EntityOnScreen(Entity entity)
        {
            return entity.position2D.X >= 0 && entity.position2D.X <= screenSize.X && entity.position2D.Y >= 0 && entity.position2D.Y <= screenSize.Y;
        }

        private void DrawBox(Entity entity)
        {
            float entityHight = entity.position2D.Y - entity.viewPosition2D.Y;
            Vector2 recTop = new Vector2(entity.viewPosition2D.X - entityHight / 3, entity.viewPosition2D.Y);
            Vector2 rectBottom = new Vector2(entity.position2D.X + entityHight / 3, entity.position2D.Y);
            Vector4 boxColor;
            if (espMode == EspMode.Ffa)
                boxColor = enemyColor;
            else
                boxColor = localPlayer.team == entity.team ? teamColor : enemyColor;

            uint col = ImGui.ColorConvertFloat4ToU32(boxColor);
            drawList.AddRect(recTop, rectBottom, col);

            // Draw player name above the box if available
            if (!string.IsNullOrEmpty(entity.name))
            {
                // Sanitize name: cut at null terminator and remove control characters
                string displayName = entity.name;
                int nullIdx = displayName.IndexOf('\0');
                if (nullIdx >= 0) displayName = displayName.Substring(0, nullIdx);

                var sb = new System.Text.StringBuilder(displayName.Length);
                foreach (char c in displayName)
                {
                    if (!char.IsControl(c)) sb.Append(c);
                }
                displayName = sb.ToString();

                // limit length to avoid overflow in UI
                if (displayName.Length > 32) displayName = displayName.Substring(0, 32);

                float textOffset = 14.0f;
                float textX = recTop.X;
                float textY = recTop.Y - textOffset;
                if (textY < 0) textY = recTop.Y + 2.0f; // if would go off-screen, draw inside box
                drawList.AddText(new Vector2(textX, textY), col, displayName);
            }
        }

        private void DrawHealthBar(Entity entity)
        {
            // Draw a vertical health bar to the left of the entity box
            float healthPercent = entity.maxHealth > 0 ? (float)entity.health / entity.maxHealth : 0f;

            // Calculate entity box left X using the same logic as DrawBox
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;
            float boxLeftX = entity.viewPosition2D.X - entityHeight / 3f;

            // Bar dimensions
            float barWidth = 6.0f;
            float gap = 4.0f; // gap between box and bar
            float barX1 = boxLeftX - gap - barWidth; // left
            float barX2 = boxLeftX - gap; // right

            Vector2 barTop = new Vector2(barX1, entity.viewPosition2D.Y);
            Vector2 barBottom = new Vector2(barX2, entity.position2D.Y);

            // Background
            drawList.AddRectFilled(barTop, barBottom, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)));

            // Filled portion (fill from bottom up)
            float totalHeight = MathF.Max(1.0f, barBottom.Y - barTop.Y);
            float filledHeight = totalHeight * Math.Clamp(healthPercent, 0f, 1f);
            float filledTopY = barBottom.Y - filledHeight;
            Vector4 healthColor;
            if (espMode == EspMode.Ffa)
                healthColor = enemyColor;
            else
                healthColor = localPlayer.team == entity.team ? teamColor : enemyColor;
            drawList.AddRectFilled(new Vector2(barX1, filledTopY), barBottom, ImGui.ColorConvertFloat4ToU32(healthColor));
        }

        public void DrawBones(Entity entity)
        {
            if (entity.bones2d == null || entity.bones2d.Count < 13) return;

            uint col = ImGui.ColorConvertFloat4ToU32(bonesColor);
            float thickness = boneThickness / Math.Max(1.0f, entity.distance);
            
            drawList.AddLine(entity.bones2d[1], entity.bones2d[2], col, boneThickness);
            drawList.AddLine(entity.bones2d[1], entity.bones2d[3], col, boneThickness);
            drawList.AddLine(entity.bones2d[1], entity.bones2d[6], col, boneThickness);
            drawList.AddLine(entity.bones2d[3], entity.bones2d[4], col, boneThickness);
            drawList.AddLine(entity.bones2d[6], entity.bones2d[7], col, boneThickness);
            drawList.AddLine(entity.bones2d[4], entity.bones2d[5], col, boneThickness);
            drawList.AddLine(entity.bones2d[7], entity.bones2d[8], col, boneThickness);
            drawList.AddLine(entity.bones2d[1], entity.bones2d[0], col, boneThickness);
            drawList.AddLine(entity.bones2d[0], entity.bones2d[9], col, boneThickness);
            drawList.AddLine(entity.bones2d[0], entity.bones2d[11], col, boneThickness);
            drawList.AddLine(entity.bones2d[9], entity.bones2d[10], col, boneThickness);
            drawList.AddLine(entity.bones2d[11], entity.bones2d[12], col, boneThickness);
            drawList.AddCircle(entity.bones2d[2], 3 + boneThickness, col);
        }

        private void DrawLine(Entity entity)
        {
            Vector4 lineColor;
            if (espMode == EspMode.Ffa)
                lineColor = enemyColor;
            else
                lineColor = localPlayer.team == entity.team ? teamColor : enemyColor;
            drawList.AddLine(new Vector2(screenSize.X / 2, screenSize.Y), entity.position2D, ImGui.ColorConvertFloat4ToU32(lineColor), 1.0f);
        }

        public void UpdateEntities(IEnumerable<Entity> newEntities)
        {
            lock (entityLock)
            {
                entities = new ConcurrentQueue<Entity>(newEntities);
            }
        }

        public void UpdateLocalPlayer(Entity newEntity)
        {
            lock (entityLock)
            {
                localPlayer = newEntity;
            }
        }

        void DrawOverlay(Vector2 screenSize)
        {
            ImGui.SetNextWindowSize(screenSize);
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("FUTAZONE-OVERLAY", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        }
    }
}
