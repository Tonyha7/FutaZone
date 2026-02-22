using System.Collections.Generic;
using System.Numerics;

namespace FutaZone
{
    public class Entity
    {
        public List<Vector3> bones { get; set; } = new List<Vector3>();
        public List<Vector2> bones2d { get; set; } = new List<Vector2>();
        public Vector3 position { get; set; }
        public Vector3 viewOffset { get; set; }
        public Vector2 position2D { get; set; }
        public Vector2 viewPosition2D { get; set; }
        public float distance { get; set; }
        public int ping { get; set; }
        public int velocity { get; set; }
        public Vector3 velocityVec { get; set; }
        public Vector2 viewAngles { get; set; }
        public int team { get; set; }

        public int health { get; set; }
        public int maxHealth { get; set; }

        public string name { get; set; } = string.Empty;
        public bool isLocalPlayer { get; set; }
    }

    public enum BoneIds
    {
        Waist = 0,
        Neck = 5, 
        Head = 6,
        ShoulderL = 8,
        ForeL = 9,
        HandL = 11,
        ShoulderR = 13,
        ForeR = 14,
        HandR = 16,
        KneeL = 23,
        feetL = 24,
        KneeRight = 26,
        feetR = 27
    }
}
