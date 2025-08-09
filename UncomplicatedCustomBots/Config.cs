using System.Collections.Generic;
#if EXILED
using Exiled.API.Interfaces;
#endif

namespace UncomplicatedCustomBots
{
#if LABAPI
    public class Config
    {
        public bool Debug { get; set; }

        public bool EnableCreditTags { get; set; } = true;

        public bool AllowScps { get; set; }

        public bool ShowSilentLogs { get; set; }

        public bool AttackTutorials { get; set; }

        public float MaxBots { get; set; } = 10;

        public float MaxPlayers { get; set; } = 5;

        public bool NewPlayersReplaceBots { get; set; } = true;

        public string GithubToken { get; set; } 

        public List<string> BlacklistedRooms { get; set; } = new List<string>
        {
            "HCZ_ServerRoom(Clone)",
            "HCZ_Crossroom_Water(Clone)",
            "HCZ_TArmory(Clone)",
            "HCZ_Straight_PipeRoom(Clone)",
            "PocketWorld(Clone)"
        };

        public List<ItemType> AllowedPickupItems { get; set; } = new List<ItemType>
        {
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
        };

        public List<string> Names { get; set; } =
        [
            "John",
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
#elif EXILED
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; }

        public bool Debug { get; set; }

        public bool EnableCreditTags { get; set; } = true;

        public bool AllowScps { get; set; }

        public bool ShowSilentLogs { get; set; }

        public float MaxBots { get; set; } = 10;

        public float MaxPlayers { get; set; } = 5;

        public bool NewPlayersReplaceBots { get; set; } = true;

        public string GithubToken { get; set; }

        public List<string> BlacklistedRooms { get; set; } = new List<string>
        {
            "HCZ_ServerRoom(Clone)",
            "HCZ_Crossroom_Water(Clone)",
            "HCZ_TArmory(Clone)",
            "HCZ_Straight_PipeRoom(Clone)",
            "PocketWorld(Clone)"
        };

        public List<ItemType> AllowedPickupItems { get; set; } = new List<ItemType>
        {
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
            ItemType.MicroHID,
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
        };

        public List<string> Names { get; set; } =
        [
            "John",
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
        ];
    }
#endif
}