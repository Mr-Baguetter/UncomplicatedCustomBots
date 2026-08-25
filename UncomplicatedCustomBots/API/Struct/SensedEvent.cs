using UncomplicatedCustomBots.API.Features;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Struct
{
    public struct SensedEvent
    {
        public SensedEventType Type;
        public Vector3 Position;
        public float Time;
        public byte Priority;
        public float Distance;

        public static class Priorities
        {
            public const byte Grenade = 100;
            public const byte Gunshot = 80;
            public const byte Tesla = 70;
            public const byte Speaking = 50;
            public const byte DoorOpen = 40;
            public const byte DoorClose = 30;
            public const byte UsingItem = 20;
            public const byte Footstep = 10;

            public static byte ForType(SensedEventType type) => type switch
            {
                SensedEventType.Grenade => Grenade,
                SensedEventType.Gunshot => Gunshot,
                SensedEventType.Tesla => Tesla,
                SensedEventType.Speaking => Speaking,
                SensedEventType.DoorOpen => DoorOpen,
                SensedEventType.DoorClose => DoorClose,
                SensedEventType.UsingItem => UsingItem,
                SensedEventType.Footstep => Footstep,
                _ => 0,
            };
        }
    }
}