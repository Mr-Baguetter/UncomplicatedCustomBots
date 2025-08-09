using System.Text;
using System.Linq;
using LabApi.Features.Wrappers;
using UnityEngine;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Enums;
using PlayerRoles;
using MapGeneration;
using UncomplicatedCustomBots.API.Features.States;
using MEC;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class DebugUIComponent : MonoBehaviour
    {
        private StringBuilder Text;
        private Player Player;
        private int layerMask = ~LayerMask.GetMask("Hitbox");

        public DebugUISections ActiveSections = DebugUISections.All;

        public void Initialize(Player player)
        {
            Text = new();
            Player = player;
        }

        public void SetSectionActive(DebugUISections section, bool enable)
        {
            if (enable)
                ActiveSections |= section;
            else
                ActiveSections &= ~section;
        }

        public void Update()
        {
            if (Player == null)
                return;

            Text.Clear();
            Text.AppendLine($"<size=20><b><color=blue>{Plugin.Instance.Name}</color> <color=yellow>v{Plugin.Instance.Version}</color> <color=red>Debug Menu</color></b></size>");
            Text.AppendLine($"<size=18><i>{System.DateTime.Now:hh:mm:ss tt}</i></size>");

            if (ActiveSections.HasFlag(DebugUISections.RaycastInfo) && Physics.Raycast(Player.Camera.position, Player.Camera.forward, out RaycastHit hitInfo, 100f, layerMask))
            {
                GameObject hitObject = hitInfo.transform.gameObject;

                Text.AppendLine($"<size=14><b><color=yellow>Raycast Info</color></b></size>");
                Text.AppendLine($"<size=10><b>Position:</b> {hitInfo.point}");
                Text.AppendLine($"<size=10><b>Distance:</b> {hitInfo.distance:F2}m");
                Text.AppendLine($"<size=10><b>Normal:</b> {hitInfo.normal}");
                Text.AppendLine($"<size=10><b>Object Name:</b> {hitInfo.transform.name}");
                Text.AppendLine($"<size=10><b>Collider Name:</b> {hitInfo.collider.name}");
                Text.AppendLine($"<size=10><b>Object Layer:</b> {LayerMask.LayerToName(hitObject.layer)}");
                Text.AppendLine($"<size=10><b>Instance ID:</b> {hitObject.GetInstanceID()}");

                Component[] components = hitObject.GetComponents<Component>();
                var names = components.Select(c => c.GetType().Name).ToList();
                int wrapAfter = 4;
                var lines = names.Select((name, index) => new { name, index }).GroupBy(x => x.index / wrapAfter).Select(g => string.Join(", ", g.Select(x => x.name)));

                string componentNames = string.Join("\n", lines);
                Text.AppendLine($"<size=10><b>Components:</b>\n{componentNames}");

                Text.AppendLine("<color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.PlayerInfo))
            {
                Text.AppendLine($"<size=14><b><color=blue>Player Info</color></b></size>");
                Text.AppendLine($"<size=10><b>Role:</b> {Player.Role}");
                Text.AppendLine($"<size=10><b>Position:</b> {Player.Position}");
                Text.AppendLine($"<size=10><b>Relative Position:</b> {Player.Room.LocalPosition(Player.Position)}");
                Text.AppendLine($"<size=10><b>Rotation:</b> {Player.Rotation.eulerAngles}");
                Text.AppendLine($"<size=10><b>Cached Room Name:</b> {Player.CachedRoom.Name}");
                Text.AppendLine($"<size=10><b>Current Room Name:</b> {Player.Room}");
                Text.AppendLine($"<size=10><b>Current Room GameObject Name:</b> {Player.Room.GameObject.name}");
                Text.AppendLine("<color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.ServerInfo))
            {
                Text.AppendLine($"<size=14><b><color=red>Server Info</color></b></size>");
                Text.AppendLine($"<size=10><b>Max TPS:</b> {Server.MaxTps}");
                Text.AppendLine($"<size=10><b>Current TPS:</b> {Server.Tps}");
                Text.AppendLine($"<size=10><b>FriendlyFire:</b> {Server.FriendlyFire}");
                Text.AppendLine($"<size=10><b>Max Players:</b> {Server.MaxPlayers}");
                Text.AppendLine($"<size=10><b>Player Count:</b> {Server.PlayerCount}");
                Text.AppendLine("<color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.RoundInfo))
            {
                Text.AppendLine($"<size=14><b><color=orange>Round Info</color></b></size>");
                Text.AppendLine($"<size=10><b>Can Round End:</b> {Round.CanRoundEnd}");
                Text.AppendLine($"<size=10><b>Total Deaths:</b> {Round.TotalDeaths}");
                Text.AppendLine($"<size=10><b>Round Duration:</b> {Round.Duration}");
                Text.AppendLine($"<size=10><b>Round Locked:</b> {Round.IsLocked}");
                Text.AppendLine("<color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.RoleInfo))
            {
                Text.AppendLine($"<size=14><b><color=red>Role Info</color></b></size>");
                Text.AppendLine($"<size=10><b>Total Dead:</b> {Player.ReadyList.Where(p => p.Team == Team.Dead).Count()}");
                Text.AppendLine($"<size=10><b>Total ClassDs:</b> {Player.ReadyList.Where(p => p.Role == RoleTypeId.ClassD).Count()}");
                Text.AppendLine($"<size=10><b>Total Scientists:</b> {Player.ReadyList.Where(p => p.Role == RoleTypeId.Scientist).Count()}");
                Text.AppendLine($"<size=10><b>Total Facility Guards:</b> {Player.ReadyList.Where(p => p.Role == RoleTypeId.FacilityGuard).Count()}");
                Text.AppendLine($"<size=10><b>Total MTF:</b> {Player.ReadyList.Where(p => p.Role == RoleTypeId.NtfCaptain || p.Role == RoleTypeId.NtfPrivate || p.Role == RoleTypeId.NtfSergeant || p.Role == RoleTypeId.NtfSpecialist).Count()}");
                Text.AppendLine($"<size=10><b>Total Chaos:</b> {Player.ReadyList.Where(p => p.Role == RoleTypeId.ChaosConscript || p.Role == RoleTypeId.ChaosMarauder || p.Role == RoleTypeId.ChaosRepressor || p.Role == RoleTypeId.ChaosRifleman).Count()}");
                Text.AppendLine($"<size=10><b>Total SCPs:</b> {Player.ReadyList.Where(p => p.Team == Team.SCPs).Count()}");
                Text.AppendLine($"<size=10><b>Total Flamingos:</b> {Player.ReadyList.Where(p => p.Team == Team.Flamingos).Count()}");
                Text.AppendLine("<color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.ZoneInfo))
            {
                Text.AppendLine($"<size=14><b><color=Green>Zone Info</color></b></size>");
                Text.AppendLine($"<size=10><b>Total Players in Light:</b> {Player.ReadyList.Where(p => p.Zone == FacilityZone.LightContainment).Count()}");
                Text.AppendLine($"<size=10><b>Total Players in Heavy:</b> {Player.ReadyList.Where(p => p.Zone == FacilityZone.HeavyContainment).Count()}");
                Text.AppendLine($"<size=10><b>Total Players in Entrance:</b> {Player.ReadyList.Where(p => p.Zone == FacilityZone.Entrance).Count()}");
                Text.AppendLine($"<size=10><b>Total Players on Surface:</b> {Player.ReadyList.Where(p => p.Zone == FacilityZone.Surface).Count()}");
                Text.AppendLine("<color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.BotInfo))
            {
                Text.AppendLine($"<size=14><b><color=blue>Bot Info</color></b></size>");
                Text.AppendLine($"<size=10><b>Total Bots:</b> {Bot.Bots.Count()}");
                Text.AppendLine($"<size=10><b>Total Bots with Navigation Component:</b> {Bot.Bots.Where(b => b.HasNavigation()).Count()}");
                Text.AppendLine($"<size=10><b>Total Bots in WalkingState:</b> {Bot.Bots.Where(b => b.State is WalkingState).Count()}");
                Text.AppendLine($"<size=10><b>Total Bots in CombatState:</b> {Bot.Bots.Where(b => b.State is CombatState).Count()}");
                Text.AppendLine($"<size=10><b>Total Bots in FleeState:</b> {Bot.Bots.Where(b => b.State is FleeState).Count()}");
                Text.AppendLine("<color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            Player.SendHint($"<pos=-85em><align=left>{Text}</align></pos>");
        }

        public void Destroy()
        {
            Text = null;
            Player = null;
        }
    }
}