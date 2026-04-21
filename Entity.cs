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

        public float emitSoundTime { get; set; }
        public uint flags { get; set; }
        public long controllerAddress { get; set; }

        public string name { get; set; } = string.Empty;
        public bool isLocalPlayer { get; set; }
        public bool isSpotted { get; set; }
        public float flashDuration { get; set; }

        // Player State
        public bool isBuyMenuOpen { get; set; }
        public bool isScoped { get; set; }
        public bool isDefusing { get; set; }
        public bool isGrabbingHostage { get; set; }
        public bool hasDefuser { get; set; }
    }

    public enum BoneIds
    {
        Waist = 1,
        Neck = 6, 
        Head = 7,
        ShoulderL = 9,
        ForeL = 10,
        HandL = 11,
        ShoulderR = 13,
        ForeR = 14,
        HandR = 15,
        KneeL = 18,
        feetL = 19,
        KneeRight = 21,
        feetR = 22
    }
}
