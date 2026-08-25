using System.Collections.Generic;
using System.ComponentModel;
using MapGeneration;
using UnityEngine;
#if EXILED
using Exiled.API.Interfaces;
#endif

namespace UncomplicatedCustomBots
{
#if EXILED
    public class Config : IConfig
#else
    public class Config
#endif
    {
#if EXILED
        public bool IsEnabled { get; set; }
#endif

        [Description("Enable debug messages in the console.")]
        public bool Debug { get; set; }

        [Description("Interval in seconds between debug log flushes. Debug messages are buffered and printed in batches at this rate.")]
        public float DebugBatchInterval { get; set; } = 2f;
        public bool AllowPreReleases { get; set; }

        [Description("Enable credit tags for all UCS developers in the game.")]
        public bool EnableCreditTags { get; set; } = true;

        [Description("Allow bots to spawn as SCP entities.")]
        public bool AllowScps { get; set; }

        [Description("Display silent logs (typically internal logs) in the console.")]
        public bool ShowSilentLogs { get; set; }

        [Description("Enable bots to attack Tutorial players.")]
        public bool AttackTutorials { get; set; }

        [Description("Maximum number of bots to spawn at round start. Values above 10 may cause performance issues.")]
        public int MaxBots { get; set; } = 10;

        [Description("Number of bots per MTF squad. Must be divisible by 2, maximum 4. Squads will group up and navigate together.")]
        public int MtfSquadSize { get; set; } = 2;

        [Description("Number of bots per Chaos squad. Must be divisible by 2, maximum 4. Squads will group up and navigate together.")]
        public int ChaosSquadSize { get; set; } = 2;

        [Description("Number of bots per Guard squad. Must be divisible by 2, maximum 4. Squads will group up and navigate together.")]
        public int GuardSquadSize { get; set; } = 2;

        [Description("Maximum distance between squad members before they regroup. Higher values allow more spread.")]
        public float SquadRegroupDistance { get; set; } = 20f;

        [Description("Maximum number of human players before bot spawning is disabled.")]
        public int MaxPlayers { get; set; } = 5;

        [Description("Per zone minimum and maximum height (Y) that a generated waypoint may be set to. Each entry is a Vector2 where X is the minimum height and Y is the maximum height. Any waypoint produced by the navmesh that falls outside its zone's band is clamped to it. Zones without an entry are left unclamped.")]
        public Dictionary<FacilityZone, Vector2> WaypointHeightLimits { get; set; } = new()
        {
            [FacilityZone.LightContainment] = new Vector2(98, 104f),
            [FacilityZone.HeavyContainment] = new Vector2(-102f, -85f),
            [FacilityZone.Entrance] = new Vector2(-102f, -95f),
            [FacilityZone.Surface] = new Vector2(285, 340f)
        };

        [Description("Maximum distance in meters between two consecutive waypoints. Longer path segments are split into evenly spaced waypoints so bots steer more precisely. Set to 0 or below to disable the limit. Capped to 2.5m to prevent wall penetration.")]
        public float MaxWaypointDistance { get; set; } = 1f;

        [Description("Automatically replace bots with human players when new players join the server.")]
        public bool NewPlayersReplaceBots { get; set; } = true;

        [Description("GitHub personal access token for the updater feature. Leave empty to not use a token.")]
        public string GithubToken { get; set; } = string.Empty;

        [Description("List of room GameObject names where bots are prohibited from tracking to.")]
        public List<string> BlacklistedRooms { get; set; } =
        [
            "HCZ_ServerRoom(Clone)",
            "HCZ_Crossroom_Water(Clone)",
            "HCZ_TArmory(Clone)",
            "HCZ_Straight_PipeRoom(Clone)",
            "PocketWorld(Clone)"
        ];

        [Description("List of item types that bots are allowed to pick up and use. Default includes all available items.")]
        public List<ItemType> AllowedPickupItems { get; set; } =
        [
            ItemType.KeycardJanitor,
            ItemType.KeycardScientist,
            ItemType.KeycardResearchCoordinator,
            ItemType.KeycardZoneManager,
            ItemType.KeycardGuard,
            ItemType.KeycardMTFPrivate,
            ItemType.KeycardContainmentEngineer,
            ItemType.KeycardMTFOperative,
            ItemType.KeycardMTFCaptain,
            ItemType.KeycardFacilityManager,
            ItemType.KeycardChaosInsurgency,
            ItemType.KeycardO5,
            ItemType.Radio,
            ItemType.GunCOM15,
            ItemType.Medkit,
            ItemType.Flashlight,
            ItemType.SCP500,
            ItemType.SCP207,
            ItemType.Ammo12gauge,
            ItemType.GunE11SR,
            ItemType.GunCrossvec,
            ItemType.Ammo556x45,
            ItemType.GunFSP9,
            ItemType.GunLogicer,
            ItemType.GrenadeHE,
            ItemType.GrenadeFlash,
            ItemType.Ammo44cal,
            ItemType.Ammo762x39,
            ItemType.Ammo9x19,
            ItemType.GunCOM18,
            ItemType.SCP018,
            ItemType.SCP268,
            ItemType.Adrenaline,
            ItemType.Painkillers,
            ItemType.Coin,
            ItemType.ArmorLight,
            ItemType.ArmorCombat,
            ItemType.ArmorHeavy,
            ItemType.GunRevolver,
            ItemType.GunAK,
            ItemType.GunShotgun,
            ItemType.SCP330,
            ItemType.SCP2176,
            ItemType.SCP244a,
            ItemType.SCP244b,
            ItemType.SCP1853,
            ItemType.ParticleDisruptor,
            ItemType.GunCom45,
            ItemType.SCP1576,
            ItemType.Jailbird,
            ItemType.AntiSCP207,
            ItemType.GunFRMG0,
            ItemType.GunA7,
            ItemType.Lantern,
            ItemType.SCP1344,
            ItemType.Snowball,
            ItemType.Coal,
            ItemType.SpecialCoal,
            ItemType.SCP1507Tape,
            ItemType.DebugRagdollMover,
            ItemType.SurfaceAccessPass,
            ItemType.GunSCP127,
            ItemType.KeycardCustomTaskForce,
            ItemType.KeycardCustomSite02,
            ItemType.KeycardCustomManagement,
            ItemType.KeycardCustomMetalCase
        ];

        [Description("NavMeshAgent avoidance quality for bot steering. Higher quality uses more CPU but avoids collisions better. Applies to unlimited bot counts.")]
        public string NavMeshAvoidanceQuality { get; set; } = "Medium";

        [Description("Maximum concurrent async NavMesh path calculations. Lower keeps main thread smoother at high bot counts.")]
        public int PathQueueConcurrency { get; set; } = 2;

        [Description("Pool of names that will be randomly assigned to spawned bots.")]
        public List<string> Names { get; set; } =
        [
            "John",
            "David",
            "Mr. Baguetter",
            "Sarah",
            "Marcus",
            "Elena",
            "Dr. Thompson",
            "Jake",
            "Amelia",
            "Professor Chen",
            "Lucas",
            "Maya",
            "Captain Rodriguez",
            "Oliver",
            "Zoe",
            "Agent Smith",
            "Isabella",
            "Derek",
            "Luna",
            "Commander Hayes",
            "Ethan",
            "Aria",
            "Specialist Johnson",
            "Nathan",
            "Chloe",
            "Director Kim",
            "Alex",
            "Sophia",
            "Sergeant Miller",
            "Ryan",
            "Ava",
            "Dr. Patel",
            "Caleb",
            "Lily",
            "Engineer Davis",
            "Noah",
            "Grace",
            "Officer Wilson",
            "Liam",
            "Emma",
            "Technician Brown",
            "Mason",
            "Mia",
            "Researcher Garcia",
            "Logan",
            "Harper",
            "Administrator Lee",
            "Jackson",
            "Dexter Morgan",
        ];
    }
}