using UnityEngine;

namespace UncomplicatedCustomBots.API.YamlObjects
{
    public class CustomNavBlocker
    {
        public string ObjectName { get; set; } = string.Empty;

        // Optional explicit primitive definition - if Position/Scale are provided, a PrimitiveObjectToy is spawned at that transform instead of searching for a matching collider
        public Vector3? Position { get; set; }
        public Vector3? Scale { get; set; }
        public Vector3? RotationEuler { get; set; }
        public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.Cube;
        public bool Visible { get; set; } = false;
        public Color? Color { get; set; }
    }
}