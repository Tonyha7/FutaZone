using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace FutaZone
{
    public class SoundEffect
    {
        public Vector3 origin;
        public double spawnTime;
        public Entity entity;
    }

    public static class SoundESP
    {
        public static float MaxDistance = 2000.0f;
        public static float EffectSpeed = 200.0f;
        public static float MaxRadius = 60.0f;
        public static float MinMovementSpeed = 15.0f;
        public static double MinSpawnInterval = 0.2f;

        private static List<SoundEffect> soundEffects = new List<SoundEffect>();
        private static Dictionary<long, float> lastSoundTimes = new Dictionary<long, float>();
        private static Dictionary<long, double> lastSpawnTimes = new Dictionary<long, double>();

        public static void RenderSound(Vector3 origin, float radius, Vector4 color, float[] viewMatrix, Vector2 screenSize, ImDrawListPtr drawList)
        {
            const float PI = 3.14159265358979323846f;
            const float STEP = PI * 2.0f / 60;
            
            List<Vector2> points = new List<Vector2>();
            
            for (float angle = 0.0f; angle <= PI * 2.0f; angle += STEP)
            {
                Vector3 worldPoint = origin + new Vector3((float)Math.Sin(angle) * radius, (float)Math.Cos(angle) * radius, 0.0f);
                Vector2 screenPoint = Calculate.WorldToScreen(viewMatrix, worldPoint, screenSize);
                
                if (screenPoint.X != -99 && screenPoint.Y != -99)
                {
                    points.Add(screenPoint);
                }
            }

            if (points.Count > 1)
            {
                for (int i = 0; i < points.Count - 1; i++)
                {
                    drawList.AddLine(points[i], points[i + 1], ImGui.ColorConvertFloat4ToU32(color), 1.0f);
                }
                // Close the circle if we have enough points
                if (points.Count > 50)
                {
                    drawList.AddLine(points[points.Count - 1], points[0], ImGui.ColorConvertFloat4ToU32(color), 1.0f);
                }
            }
        }

        public static void ProcessSound(Entity entity, Entity localEntity)
        {
            if (Vector3.Distance(entity.position, localEntity.position) > MaxDistance)
                return;

            float currentSoundTime = entity.emitSoundTime;
            bool Jumped = (entity.flags & (1 << 0)) == 0; // ON_GROUND flag is 1<<0

            if (!lastSoundTimes.ContainsKey(entity.controllerAddress))
            {
                lastSoundTimes[entity.controllerAddress] = currentSoundTime;
                return;
            }
            if (lastSoundTimes[entity.controllerAddress] == currentSoundTime)
                return;

            if (entity.velocity < MinMovementSpeed && !Jumped)
            {
                lastSoundTimes[entity.controllerAddress] = currentSoundTime;
                return;
            }

            lastSoundTimes[entity.controllerAddress] = currentSoundTime;

            double currentTime = ImGui.GetTime();
            if (!lastSpawnTimes.ContainsKey(entity.controllerAddress))
            {
                lastSpawnTimes[entity.controllerAddress] = 0;
            }

            if (currentTime - lastSpawnTimes[entity.controllerAddress] < MinSpawnInterval)
                return;

            lastSpawnTimes[entity.controllerAddress] = currentTime;

            bool updated = false;
            foreach (var soundEffect in soundEffects)
            {
                if (soundEffect.entity.controllerAddress == entity.controllerAddress)
                {
                    soundEffect.origin = entity.position;
                    soundEffect.spawnTime = currentTime;
                    soundEffect.entity = entity;
                    updated = true;
                    break;
                }
            }

            if (!updated)
            {
                soundEffects.Add(new SoundEffect
                {
                    origin = entity.position,
                    spawnTime = currentTime,
                    entity = entity
                });
            }
        }

        public static void Render(float[] viewMatrix, Vector2 screenSize, ImDrawListPtr drawList, Vector4 baseColor)
        {
            if (soundEffects.Count == 0)
                return;

            double currentTime = ImGui.GetTime();
            float duration = MaxRadius / EffectSpeed;

            for (int i = soundEffects.Count - 1; i >= 0; i--)
            {
                var soundEffect = soundEffects[i];

                float elapsed = (float)(currentTime - soundEffect.spawnTime);
                
                if (elapsed > duration)
                {
                    soundEffects.RemoveAt(i);
                    continue;
                }

                float progress = Math.Clamp(elapsed / duration, 0.0f, 1.0f);
                float startRadius = MaxRadius * 0.1f;
                float radius = startRadius + (MaxRadius - startRadius) * progress;
                
                Vector4 color = baseColor;
                color.W *= (1.0f - progress);

                RenderSound(soundEffect.origin, radius, color, viewMatrix, screenSize, drawList);
            }
        }
    }
}