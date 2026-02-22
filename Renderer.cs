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
        public float[] viewMatrix = new float[16];
        //entities copies
        public ConcurrentQueue<Entity> entities = new ConcurrentQueue<Entity>();
        private Entity localPlayer = new Entity();
        private readonly object entityLock = new object();

        //GUI elements
        private bool enableESP = true;
        private bool enableLines = false;
        private bool enableBombTimer = true;
        private bool enableWatermark = true;
        private bool enableAimbot = false;
        private bool showAimTarget = false;
        private bool enableTriggerBot = false;
        private bool enableAutoStop = false;
        private bool enableSoundESP = false;
        private bool showTeammates = false; // Default to not showing teammates
        private Vector4 enemyColor = new Vector4(1.0f, 0.6f, 0.75f, 1.0f); // Sakura pink for enemy 

        private Vector4 teamColor = new Vector4(0.6f, 0.827f, 0.0f, 1.0f); // Lime green for team
        private Vector4 bonesColor = new Vector4(0.5f, 0.0f, 0.5f, 1.0f); // Deep purple for bones
        private Vector4 soundESPColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f); // Red for SoundESP

        float boneThickness = 2.0f;

        private enum EspMode { Team = 0, Ffa = 1 }
        private EspMode espMode = EspMode.Team;

        private enum EspStyle { FullBox = 0, CornerBox = 1, NoBox = 2, Circle3D = 3, Star3D = 4 }
        private EspStyle espStyle = EspStyle.FullBox;

        //draw list
        ImDrawListPtr drawList;

        // UI visibility
        private bool showUI = true;
        private bool insKeyPressed = false;
        private bool styleInitialized = false;

        // Watermark caching
        private string watermarkName = "";
        private int watermarkPing = 0;
        private int watermarkVelocity = 0;
        private float lastWatermarkUpdate = 0f;

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
                    ImGui.Checkbox("Enable SoundESP (声音透视)", ref enableSoundESP);
                    
                    if (enableESP || enableSoundESP)
                    {
                        if (enableESP)
                        {
                            // ESP Style Selection
                            string[] styleNames = { "Full Box (全框)", "Corner Box (四角)", "No Box (无框)", "3D Circle (立体圆环)", "3D Star (立体五角星)" };
                            int styleIndex = (int)espStyle;
                            if (ImGui.Combo("ESP Style (透视样式)", ref styleIndex, styleNames, styleNames.Length))
                            {
                                espStyle = (EspStyle)styleIndex;
                            }

                            ImGui.Checkbox("Enable Lines (开启射线)", ref enableLines);
                        }
                        
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

                        if (enableESP)
                        {
                            // color pickers
                            ImGui.ColorEdit4("Team Color (队伍颜色)", ref teamColor);
                            ImGui.ColorEdit4("Enemy Color (敌人颜色)", ref enemyColor);
                            ImGui.ColorEdit4("Bones Color (骨骼颜色)", ref bonesColor);
                        }
                        
                        if (enableSoundESP)
                        {
                            ImGui.ColorEdit4("SoundESP Color (声音透视颜色)", ref soundESPColor);
                        }
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

                        ImGui.Checkbox("Show Aim Target (显示瞄准点)", ref showAimTarget);

                        // Aim Mode Selection
                        Aimbot.AimMode currentMode = Aimbot.Instance.Mode;
                        string[] modeNames = { "Linear (线性)", "Fast->Slow (先快后慢)", "Slow->Fast (先慢后快)", "Overshoot (过顶回拉)", "Random (随机)" };
                        int modeIndex = (int)currentMode;
                        if (ImGui.Combo("Aim Mode (模式)", ref modeIndex, modeNames, modeNames.Length))
                        {
                            Aimbot.Instance.Mode = (Aimbot.AimMode)modeIndex;
                        }

                        // Randomness Settings (Duration control)
                        if (modeIndex != 0) // If not Linear
                        {
                            bool randSpeed = Aimbot.Instance.RandomizeSpeed;
                            if (ImGui.Checkbox("Randomize Duration (随机时长)", ref randSpeed)) Aimbot.Instance.RandomizeSpeed = randSpeed;

                            int duration = Aimbot.Instance.SpeedChangeDuration;
                            if (ImGui.SliderInt("Switch Duration (切换时长 ms)", ref duration, 100, 2000)) Aimbot.Instance.SpeedChangeDuration = duration;
                        
                            // Overshoot specific settings
                            // Show if Overshoot (3) or Random (4) is selected
                            if (modeIndex == 3 || modeIndex == 4)
                            {
                                float overshoot = Aimbot.Instance.OvershootScale;
                                if (ImGui.SliderFloat("Overshoot Scale (过顶倍率)", ref overshoot, 1.0f, 3.0f)) Aimbot.Instance.OvershootScale = overshoot;
                            }
                        }

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

                // AutoStop feature group
                if (ImGui.CollapsingHeader("AutoStop (自动急停)", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Checkbox("Enable AutoStop (开启自动急停)", ref enableAutoStop);
                    AutoStop.Instance.Enabled = enableAutoStop;
                    if (enableAutoStop)
                    {
                        float trigger = AutoStop.Instance.TriggerThreshold;
                        float stop = AutoStop.Instance.StopThreshold;
                        
                        if (ImGui.SliderFloat("Trigger Speed (触发速度)", ref trigger, 10f, 250f)) AutoStop.Instance.TriggerThreshold = trigger;
                        if (ImGui.SliderFloat("Stop Speed (停止速度)", ref stop, 0f, 100f)) AutoStop.Instance.StopThreshold = stop;
                    }
                }
                
                ImGui.Checkbox("Enable Bomb Timer (C4计时器)", ref enableBombTimer);
                ImGui.Checkbox("Enable Watermark (水印)", ref enableWatermark);
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

                if (showAimTarget && Aimbot.Instance.TargetPosition.HasValue)
                {
                    uint targetColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.0f, 0.0f, 1.0f)); // Red
                    drawList.AddCircleFilled(Aimbot.Instance.TargetPosition.Value, 4.0f, targetColor, 16);
                }
            }

            if (enableESP || enableSoundESP)
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

                        if (enableSoundESP)
                        {
                            SoundESP.ProcessSound(entity, localPlayer);
                        }

                        if (enableESP)
                        {
                            if (enableLines && entity.position2D != new Vector2(-99, -99)) DrawLine(entity);

                            if (EntityOnScreen(entity))
                            {
                                // If player is an observer (Team 1), only draw head circle and skip other visuals
                                if (entity.team == 1 || entity.team == 0)
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
                
                if (enableSoundESP)
                {
                    SoundESP.Render(viewMatrix, screenSize, drawList, soundESPColor);
                }
            }

            if (enableWatermark)
            {
                DrawWatermark();
            }

            if (enableBombTimer && BombTimer.IsBombPlanted)
            {
                DrawBombTimer();
            }
        }

        private void DrawBombTimer()
        {
            // Set default position to top-left
            ImGui.SetNextWindowPos(new Vector2(20, 20), ImGuiCond.FirstUseEver);
            
            // Sakura background for the window
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1.0f, 0.75f, 0.8f, 0.6f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1.0f, 0.6f, 0.75f, 1.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5.0f);
            
            // Allow dragging from body
            ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = false;

            if (ImGui.Begin("BombTimer", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
            {
                // C4 Timer Logic
                float maxTime = 40.0f; 
                float timeLeft = BombTimer.TimeLeft;
                float progress = Math.Clamp(timeLeft / maxTime, 0.0f, 1.0f);

                // Draw Text
                string text = BombTimer.BombPlantedText;
                if (!string.IsNullOrEmpty(BombTimer.TimerText)) text += $"\n{BombTimer.TimerText}";
                if (!string.IsNullOrEmpty(BombTimer.DefuseText)) text += $"\n{BombTimer.DefuseText}";

                // Draw text (Dark pink/purple for readability on light pink background)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.0f, 0.5f, 1.0f));
                ImGui.Text(text);
                ImGui.PopStyleColor();

                // Draw Progress Bar
                // Custom drawing for the bar to match previous style (vertical or horizontal? 
                // Previous was vertical full screen height. Now it's a window. Let's make it a horizontal bar inside the window for better UX in a floating window)
                
                Vector2 barSize = new Vector2(200, 15);
                
                // Get cursor for custom drawing
                Vector2 p = ImGui.GetCursorScreenPos();
                ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
                
                // Background (Darker Pink)
                windowDrawList.AddRectFilled(p, new Vector2(p.X + barSize.X, p.Y + barSize.Y), ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.5f, 0.6f, 0.6f)));
                
                // Foreground (Green)
                float currentWidth = barSize.X * progress;
                windowDrawList.AddRectFilled(p, new Vector2(p.X + currentWidth, p.Y + barSize.Y), ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 1.0f, 0.0f, 1.0f)));
                
                // Advance cursor so window resizes correctly
                ImGui.Dummy(barSize);
            }
            ImGui.End();
            
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
        }

        private void DrawWatermark()
        {
            // Set default position to top-right
            ImGui.SetNextWindowPos(new Vector2(screenSize.X - 300, 20), ImGuiCond.FirstUseEver);
            
            // Set window styling for watermark
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(1.0f, 0.75f, 0.8f, 0.6f)); // Sakura background
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1.0f, 0.6f, 0.75f, 1.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5.0f);
            
            // 默认允许拖动无标题栏窗口
            ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = false;

            // Update watermark data every frame (real-time)
            watermarkName = string.IsNullOrEmpty(localPlayer.name) ? "" : localPlayer.name;
            watermarkPing = localPlayer.ping;
            watermarkVelocity = localPlayer.velocity;

            if (ImGui.Begin("Watermark", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
            {
                // Gradient Text for "FUTAZONE"
                string title = "FUTAZONE";
                float time = (float)ImGui.GetTime();
                
                for (int i = 0; i < title.Length; i++)
                {
                    float r = (float)Math.Sin(time + i * 0.1f) * 0.5f + 0.5f;
                    float g = (float)Math.Sin(time + i * 0.1f + 2.0f) * 0.5f + 0.5f;
                    float b = (float)Math.Sin(time + i * 0.1f + 4.0f) * 0.5f + 0.5f;
                    
                    ImGui.TextColored(new Vector4(r, g, b, 1.0f), title[i].ToString());
                    ImGui.SameLine(0, 0);
                }
                
                ImGui.SameLine();
                ImGui.Text(" | ");
                ImGui.SameLine();
                
                // Player Name
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), watermarkName);
                
                ImGui.SameLine();
                ImGui.Text(" | ");
                ImGui.SameLine();
                
                // Ping
                Vector4 pingColor;
                if (watermarkPing <= 20)
                {
                    pingColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // Green
                }
                else if (watermarkPing >= 80)
                {
                    pingColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f); // Red
                }
                else
                {
                    // Gradient from Green (0,1,0) to Red (1,0,0)
                    float t = (watermarkPing - 20.0f) / (80.0f - 20.0f);
                    pingColor = new Vector4(t, 1.0f - t, 0.0f, 1.0f);
                }
                
                ImGui.TextColored(pingColor, $"{watermarkPing} ms");

                ImGui.SameLine();
                ImGui.Text(" | ");
                ImGui.SameLine();

                // Velocity
                // Gradient based on speed (0-250)
                // 0-100: White to Yellow
                // 100-250: Yellow to Red
                // Or simply keep it white or sakura? Let's use a dynamic color for fun.
                // Assuming max running speed with knife is 250.
                
                Vector4 velColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // Default White
                if (watermarkVelocity > 0)
                {
                   float t = Math.Clamp(watermarkVelocity / 250.0f, 0.0f, 1.0f);
                   // White (1,1,1) -> Sakura Pink (1, 0.6, 0.75) -> Deep Purple (0.5, 0, 0.5)
                   // Let's just do White -> Green for now or similar to ping but inverted?
                   // User didn't specify color, let's use a nice Cyan to Purple gradient
                   
                   velColor = new Vector4(1.0f - t * 0.5f, 1.0f - t, 1.0f - t, 1.0f); // White -> RED
                }
                
                ImGui.TextColored(velColor, $"{watermarkVelocity} u/s");
            }
            ImGui.End();
            
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
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
            
            if (espStyle == EspStyle.FullBox)
            {
                drawList.AddRect(recTop, rectBottom, col);
            }
            else if (espStyle == EspStyle.CornerBox)
            {
                float lineW = (rectBottom.X - recTop.X) / 4;
                float lineH = (rectBottom.Y - recTop.Y) / 4;
                
                // Top left
                drawList.AddLine(new Vector2(recTop.X, recTop.Y), new Vector2(recTop.X + lineW, recTop.Y), col);
                drawList.AddLine(new Vector2(recTop.X, recTop.Y), new Vector2(recTop.X, recTop.Y + lineH), col);
                
                // Top right
                drawList.AddLine(new Vector2(rectBottom.X, recTop.Y), new Vector2(rectBottom.X - lineW, recTop.Y), col);
                drawList.AddLine(new Vector2(rectBottom.X, recTop.Y), new Vector2(rectBottom.X, recTop.Y + lineH), col);
                
                // Bottom left
                drawList.AddLine(new Vector2(recTop.X, rectBottom.Y), new Vector2(recTop.X + lineW, rectBottom.Y), col);
                drawList.AddLine(new Vector2(recTop.X, rectBottom.Y), new Vector2(recTop.X, rectBottom.Y - lineH), col);
                
                // Bottom right
                drawList.AddLine(new Vector2(rectBottom.X, rectBottom.Y), new Vector2(rectBottom.X - lineW, rectBottom.Y), col);
                drawList.AddLine(new Vector2(rectBottom.X, rectBottom.Y), new Vector2(rectBottom.X, rectBottom.Y - lineH), col);
            }
            else if (espStyle == EspStyle.Circle3D)
            {
                int segments = 32;
                float radius = 30.0f; // Adjust radius as needed
                Vector2[] points = new Vector2[segments];
                bool allOnScreen = true;

                for (int i = 0; i < segments; i++)
                {
                    float angle = (float)(i * 2 * Math.PI / segments);
                    Vector3 point3D = new Vector3(
                        entity.position.X + radius * (float)Math.Cos(angle),
                        entity.position.Y + radius * (float)Math.Sin(angle),
                        entity.position.Z
                    );

                    Vector2 point2D = Calculate.WorldToScreen(viewMatrix, point3D, screenSize);
                    if (point2D.X == -99 && point2D.Y == -99)
                    {
                        allOnScreen = false;
                        break;
                    }
                    points[i] = point2D;
                }

                if (allOnScreen)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        Vector2 p1 = points[i];
                        Vector2 p2 = points[(i + 1) % segments];
                        drawList.AddLine(p1, p2, col, 2.0f);
                    }
                }
            }
            else if (espStyle == EspStyle.Star3D)
            {
                float radius = 30.0f; // Adjust radius as needed
                int starPoints = 5;
                Vector2[] starVertices = new Vector2[starPoints];
                bool starOnScreen = true;
                
                // Calculate star vertices
                for (int i = 0; i < starPoints; i++)
                {
                    // Start from top (PI/2) and go clockwise
                    float angle = (float)(Math.PI / 2 + i * 4 * Math.PI / starPoints);
                    Vector3 point3D = new Vector3(
                        entity.position.X + radius * (float)Math.Cos(angle),
                        entity.position.Y + radius * (float)Math.Sin(angle),
                        entity.position.Z
                    );

                    Vector2 point2D = Calculate.WorldToScreen(viewMatrix, point3D, screenSize);
                    if (point2D.X == -99 && point2D.Y == -99)
                    {
                        starOnScreen = false;
                        break;
                    }
                    starVertices[i] = point2D;
                }

                if (starOnScreen)
                {
                    for (int i = 0; i < starPoints; i++)
                    {
                        Vector2 p1 = starVertices[i];
                        Vector2 p2 = starVertices[(i + 1) % starPoints];
                        drawList.AddLine(p1, p2, col, 2.0f);
                    }
                }
            }

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

        public void UpdateViewMatrix(float[] newViewMatrix)
        {
            viewMatrix = newViewMatrix;
        }

        void DrawOverlay(Vector2 screenSize)
        {
            ImGui.SetNextWindowSize(screenSize);
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("FUTAZONE-OVERLAY", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        }
    }
}
