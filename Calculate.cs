using System;
using System.Collections.Generic;
using System.Numerics;
using Swed64;

namespace FutaZone
{
    public static class Calculate
    {
        public static Vector2 WorldToScreen(float[] matrix, Vector3 position, Vector2 screenSize)
        {
            //calculate screenwidth
            float screenWidth = (matrix[12] * position.X) + (matrix[13] * position.Y) + (matrix[14] * position.Z) + matrix[15];

            //if entity is in front of us
            if (screenWidth > 0.001f)
            {
                //calculate screen X and Y
                float screenX = (matrix[0] * position.X) + (matrix[1] * position.Y) + (matrix[2] * position.Z) + matrix[3];
                float screenY = (matrix[4] * position.X) + (matrix[5] * position.Y) + (matrix[6] * position.Z) + matrix[7];

                //perform perspective division
                float X = (screenSize.X / 2) + (screenSize.X / 2) * (screenX / screenWidth);
                float Y = (screenSize.Y / 2) - (screenSize.Y / 2) * (screenY / screenWidth);

                return new Vector2(X, Y);
            }
            else
            {
                //enemy is behind us, return offscreen coordinates
                return new Vector2(-99, -99);
            }
        }

        public static List<Vector3> ReadBones(IntPtr boneAddress, Swed swed) 
        {
            byte[] boneBytes = swed.ReadBytes(boneAddress, 27 * 32 + 16); //get max bone count (27) * size of each bone (32 bytes) + 16 bytes for the header
            List<Vector3> bones = new List<Vector3>();
            foreach (var boneId in Enum.GetValues(typeof(BoneIds))) 
            {
                float x = BitConverter.ToSingle(boneBytes, (int)boneId * 32 + 0);
                float y = BitConverter.ToSingle(boneBytes, (int)boneId * 32 + 4);
                float z = BitConverter.ToSingle(boneBytes, (int)boneId * 32 + 8);
                Vector3 currentBone = new Vector3(x, y, z);
                bones.Add(currentBone);
            }
            return bones;
        }

        public static List<Vector2> ReadBones2D(List<Vector3> bones, float[] viewMatrix, Vector2 screenSize) 
        {
            List<Vector2> bones2d = new List<Vector2>();
            foreach (var bone in bones) 
            {
                bones2d.Add(WorldToScreen(viewMatrix, bone, screenSize));
            }
            return bones2d;
        }
    }
}
