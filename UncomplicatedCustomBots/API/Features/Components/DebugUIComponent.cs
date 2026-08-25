using System.Text;
using System.Linq;
using LabApi.Features.Wrappers;
using UnityEngine;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Enums;
using PlayerRoles;
using MapGeneration;
using UncomplicatedCustomBots.API.Features.States;
using System.Collections.Generic;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class DebugUIComponent : MonoBehaviour
    {
        private StringBuilder Text = null!;
        private Player Player = null!;
        private readonly int layerMask = ~LayerMask.GetMask("Hitbox");

        public DebugUISections ActiveSections = DebugUISections.All;

        public void Initialize(Player player)
        {
            Text = new();
            Player = player;
        }

        public void SetSectionActive(DebugUISections section, bool enable)
        {
            if (enable)
            {
                ActiveSections |= section;
            }
            else
                ActiveSections &= ~section;
        }

        private float _updateTimer = 0f;
        private const float UpdateInterval = 0.5f;

        public void Update()
        {
            if (Player == null)
                return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UpdateInterval)
                return;
            _updateTimer = 0f;

            Text.Clear();
            Text.AppendLine("<mark=#000000AA>");
            Text.AppendLine($"<pos=-10em><size=20><b><color=blue>{Plugin.Instance.Name}</color> <color=yellow>v{Plugin.Instance.Version}</color> <color=red>Debug Menu</color></b></size>");
            Text.AppendLine($"<pos=-10em><size=18><i>{System.DateTime.Now:hh:mm:ss tt}</i></size>");

            if (ActiveSections.HasFlag(DebugUISections.RaycastInfo) && Physics.Raycast(Player.Camera.position, Player.Camera.forward, out RaycastHit hitInfo, 100f, layerMask))
            {
                GameObject hitObject = hitInfo.transform.gameObject;

                Text.AppendLine($"<pos=-10em><size=14><b><color=yellow>Raycast Info</color></b></size>");
                Text.AppendLine($"<pos=-10em><size=10><b>Position:</b> {hitInfo.point}");
                Text.AppendLine($"<pos=-10em><size=10><b>Distance:</b> {hitInfo.distance:F2}m");
                Text.AppendLine($"<pos=-10em><size=10><b>Normal:</b> {hitInfo.normal}");
                Text.AppendLine($"<pos=-10em><size=10><b>Object Name:</b> {hitInfo.transform.name}");
                Text.AppendLine($"<pos=-10em><size=10><b>Collider Name:</b> {hitInfo.collider.name}");
                Text.AppendLine($"<pos=-10em><size=10><b>Object Layer:</b> {LayerMask.LayerToName(hitObject.layer)}");
                Text.AppendLine($"<pos=-10em><size=10><b>Instance ID:</b> {hitObject.GetInstanceID()}");

                Component[] components = hitObject.GetComponents<Component>();
                List<string> names = components.Select(c => c.GetType().Name).ToList();
                int wrapAfter = 4;
                var lines = names.Select((name, index) => new { name, index }).GroupBy(x => x.index / wrapAfter).Select(g => string.Join(", ", g.Select(x => x.name)));

                string componentNames = string.Join("\n", lines);
                Text.AppendLine($"<pos=-10em><size=10><b>Components:</b>\n{componentNames}");

                Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.PlayerInfo))
            {
                if (Player.Room != null && Player.CachedRoom != null)
                {
                    Text.AppendLine($"<pos=-10em><size=14><b><color=blue>Player Info</color></b></size>");
                    Text.AppendLine($"<pos=-10em><size=10><b>Role:</b> {Player.Role}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Position:</b> {Player.Position}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Relative Position:</b> {Player.Room.LocalPosition(Player.Position)}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Rotation:</b> {Player.Rotation.eulerAngles}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Cached Room Name:</b> {Player.CachedRoom.Name}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Current Room Name:</b> {Player.Room}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Current Room GameObject Name:</b> {Player.Room.GameObject.name}");
                    Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                    Text.AppendLine();
                }
                else
                {
                    Text.AppendLine($"<pos=-10em><size=14><b><color=blue>Player Info</color></b></size>");
                    Text.AppendLine($"<pos=-10em><size=10><b>Role:</b> {Player.Role}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Position:</b> {Player.Position}");
                    Text.AppendLine($"<pos=-10em><size=10><b>Rotation:</b> {Player.Rotation.eulerAngles}");
                    Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                    Text.AppendLine();
                }
            }

            if (ActiveSections.HasFlag(DebugUISections.ServerInfo))
            {
                Text.AppendLine($"<pos=-10em><size=14><b><color=red>Server Info</color></b></size>");
                Text.AppendLine($"<pos=-10em><size=10><b>Max TPS:</b> {Server.MaxTps}");
                Text.AppendLine($"<pos=-10em><size=10><b>Current TPS:</b> {Server.Tps}");
                Text.AppendLine($"<pos=-10em><size=10><b>FriendlyFire:</b> {Server.FriendlyFire}");
                Text.AppendLine($"<pos=-10em><size=10><b>Max Players:</b> {Server.MaxPlayers}");
                Text.AppendLine($"<pos=-10em><size=10><b>Player Count:</b> {Server.PlayerCount}");
                Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.RoundInfo))
            {
                Text.AppendLine($"<pos=-10em><size=14><b><color=orange>Round Info</color></b></size>");
                Text.AppendLine($"<pos=-10em><size=10><b>Can Round End:</b> {Round.CanRoundEnd}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Deaths:</b> {Round.TotalDeaths}");
                Text.AppendLine($"<pos=-10em><size=10><b>Round Duration:</b> {Round.Duration}");
                Text.AppendLine($"<pos=-10em><size=10><b>Round Locked:</b> {Round.IsLocked}");
                Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.RoleInfo))
            {
                var snapshot = Player.ReadyList.ToArray();
                int dead=0, classD=0, sci=0, guard=0, mtf=0, chaos=0, scp=0, flam=0;
                foreach (var p in snapshot)
                {
                    if (p.Team == Team.Dead)
                        dead++;
                    if (p.Role == RoleTypeId.ClassD)
                        classD++;
                    if (p.Role == RoleTypeId.Scientist)
                        sci++;
                    if (p.Role == RoleTypeId.FacilityGuard)
                        guard++;
                    if (p.Role == RoleTypeId.NtfCaptain || p.Role == RoleTypeId.NtfPrivate || p.Role == RoleTypeId.NtfSergeant || p.Role == RoleTypeId.NtfSpecialist)
                        mtf++;
                    if (p.Role == RoleTypeId.ChaosConscript || p.Role == RoleTypeId.ChaosMarauder || p.Role == RoleTypeId.ChaosRepressor || p.Role == RoleTypeId.ChaosRifleman)
                        chaos++;
                    if (p.Team == Team.SCPs)
                        scp++;
                    if (p.Team == Team.Flamingos)
                        flam++;
                }
                Text.AppendLine($"<pos=-10em><size=14><b><color=red>Role Info</color></b></size>");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Players:</b> {snapshot.Length}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Dead:</b> {dead}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total ClassDs:</b> {classD}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Scientists:</b> {sci}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Facility Guards:</b> {guard}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total MTF:</b> {mtf}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Chaos:</b> {chaos}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total SCPs:</b> {scp}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Flamingos:</b> {flam}");
                Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.ZoneInfo))
            {
                var snapshot = Player.ReadyList.ToArray();
                int lcz=0,hcz=0,ez=0,surf=0;
                foreach (var p in snapshot)
                {
                    if (p.Zone == FacilityZone.LightContainment)
                        lcz++;
                    else if (p.Zone == FacilityZone.HeavyContainment) hcz++;
                    else if (p.Zone == FacilityZone.Entrance) ez++;
                    else if (p.Zone == FacilityZone.Surface) surf++;
                }
                Text.AppendLine($"<pos=-10em><size=14><b><color=Green>Zone Info</color></b></size>");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Players in Light:</b> {lcz}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Players in Heavy:</b> {hcz}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Players in Entrance:</b> {ez}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Players on Surface:</b> {surf}");
                Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Text.AppendLine();
            }

            if (ActiveSections.HasFlag(DebugUISections.BotInfo))
            {
                Bot[] bots = Bot.SnapshotBotList();
                int nav=0, walk=0, combat=0, flee=0;
                foreach (var b in bots)
                {
                    if (b.HasNavigation())
                        nav++;
                    if (b.State is WalkingState)
                        walk++;
                    else if (b.State is CombatState) combat++;
                    else if (b.State is FleeState) flee++;
                }
                Text.AppendLine($"<pos=-10em><size=14><b><color=blue>Bot Info</color></b></size>");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Bots:</b> {bots.Length}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Bots with Navigation Component:</b> {nav}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Bots in WalkingState:</b> {walk}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Bots in CombatState:</b> {combat}");
                Text.AppendLine($"<pos=-10em><size=10><b>Total Bots in FleeState:</b> {flee}");
                Text.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Text.AppendLine();
            }
            Text.AppendLine("</mark>");
            Player.SendHint($"<align=left>{Text}");
        }

        public void Destroy()
        {
            Text = null!;
            Player = null!;
        }
    }
}