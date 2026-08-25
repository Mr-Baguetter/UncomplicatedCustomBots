using System.Text;
using System.Linq;
using LabApi.Features.Wrappers;
using UnityEngine;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Features.States;
using System.Collections.Generic;
using LabApi.Features.Extensions;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class BotDebugUIComponent : MonoBehaviour
    {
        private StringBuilder Builder = null!;
        private Player Player = null!;
        private Bot Bot = null!;

        public BotDebugSections ActiveSections = BotDebugSections.None;

        public void Initialize(Player player, Bot bot)
        {
            Builder = new();
            Player = player;
            Bot = bot;
        }

        public void SetSectionActive(BotDebugSections section, bool enable)
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
        private Navigation? _cachedNav;

        public void Update()
        {
            if (Player == null)
                return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UpdateInterval)
                return;
            _updateTimer = 0f;

            _cachedNav ??= Bot.Player.GameObject!.GetComponent<Navigation>();

            Builder.Clear();

            if (ActiveSections.HasFlag(BotDebugSections.ComponentInfo))
            {
                Builder.AppendLine($"<pos=-10em><size=14><b><color=blue>Bot Components</color></b></size>");
                Component[] components = Bot.Player.GameObject!.GetComponents<Component>();
                List<string> names = components.Select(c => c.GetType().Name).ToList();
                int wrapAfter = 4;
                IEnumerable<string> lines = names.Select((name, index) => new { name, index }).GroupBy(x => x.index / wrapAfter).Select(g => string.Join(", ", g.Select(x => x.name)));

                string componentNames = string.Join("\n", lines);
                Builder.AppendLine($"<pos=-10em><size=10><b>Components:</b>\n{componentNames}");
                Builder.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Builder.AppendLine();
            }

            if (ActiveSections.HasFlag(BotDebugSections.PlayerInfo))
            {
                Builder.AppendLine($"<pos=-10em><size=14><b><color=red>Bot Info</color></b></size>");
                Builder.AppendLine($"<pos=-10em><size=10><b>Bot Name:</b> {Bot.Player.DisplayName}");
                Builder.AppendLine($"<pos=-10em><size=10><b>Bot Position:</b> {Bot.Player.Position}");
                if (Bot.Player.Room != null && Bot.Player.CachedRoom != null)
                {
                    Builder.AppendLine($"<pos=-10em><size=10><b>Relative Position In Room:</b> {Bot.Player.Room.LocalPosition(Bot.Player.Position)}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Relative Position In Chached Room:</b> {Bot.Player.CachedRoom.LocalPosition(Bot.Player.Position)}");
                }
                Builder.AppendLine($"<pos=-10em><size=10><b>Bot Rotation:</b> {Bot.Player.Rotation.eulerAngles}");

                if (Bot.Player.Room != null && Bot.Player.CachedRoom != null)
                {
                    Builder.AppendLine($"<pos=-10em><size=10><b>Bot Room:</b> {Bot.Player.Room}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Bot Chached Room:</b> {Bot.Player.CachedRoom}");
                }
                else
                {
                    Builder.AppendLine($"<pos=-10em><size=10><b>Bot Room:</b> null");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Bot Chached Room:</b> null");
                }

                Builder.AppendLine($"<pos=-10em><size=10><b>Bot Health:</b> {Bot.Player.Health}/{Bot.Player.MaxHealth}");
                Builder.AppendLine($"<pos=-10em><size=10><b>Bot Faction:</b> {Bot.Player.Faction}");
                Builder.AppendLine($"<pos=-10em><size=10><b>Bot Team:</b> {Bot.Player.Team}");
                Builder.AppendLine($"<pos=-10em><size=10><b>Bot Role:</b> {Bot.Player.Role.GetFullName()}");
                Builder.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Builder.AppendLine();
            }
            if (ActiveSections.HasFlag(BotDebugSections.StateInfo))
            {
                switch (Bot.State)
                {
                    case WalkingState:
                        Builder.AppendLine("<pos=-10em>In WalkingState: true");
                        break;

                    case CombatState:
                        Builder.AppendLine("<pos=-10em>In CombatState: true");
                        break;

                    case FleeState:
                        Builder.AppendLine("<pos=-10em>In FleeState: true");
                        break;

                    case Scp0492State:
                        Builder.AppendLine("<pos=-10em>In Scp0492State: true");
                        break;

                    case Scp049State:
                        Builder.AppendLine("<pos=-10em>In Scp049State: true");
                        break;

                    case Scp106State:
                        Builder.AppendLine("<pos=-10em>In Scp106State: true");
                        break;

                    case Scp173State:
                        Builder.AppendLine("<pos=-10em>In Scp173State: true");
                        break;

                    case Scp939State:
                        Builder.AppendLine("<pos=-10em>In Scp939State: true");
                        break;

                    case Scp3114State:
                        Builder.AppendLine("<pos=-10em>In Scp3114State: true");
                        break;

                    default:
                        Builder.AppendLine($"<pos=-10em>{Bot.GetState().Name}");
                        break;
                }

                Builder.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Builder.AppendLine();
            }

            if (ActiveSections.HasFlag(BotDebugSections.NavigationInfo))
            {
                Builder.AppendLine($"<pos=-10em><size=14><b><color=green>Navigation Info</color></b></size>");

                if (_cachedNav != null && Bot.HasNavigation())
                {
                    Navigation nav = _cachedNav;
                    string waypointInfo = nav.CurrentPath.Count > 0 && nav.CurrentWaypointIndex < nav.CurrentPath.Count ? nav.CurrentPath[nav.CurrentWaypointIndex].ToString() : "none";
                    Builder.AppendLine($"<pos=-10em><size=10><b>Enabled:</b> {nav.enabled}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Speed:</b> {nav._speed}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Current Waypoint:</b> {waypointInfo}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Waypoint:</b> {nav.CurrentWaypointIndex}/{nav.CurrentPath.Count}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Target Room:</b> {nav.CurrentTarget}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Waiting For Door:</b> {nav.IsWaitingForDoor}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Waiting For Elevator:</b> {nav.IsWaitingToEnterElevator}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Walking Into Elevator:</b> {nav.IsWalkingIntoElevator}");
                    Builder.AppendLine($"<pos=-10em><size=10><b>Inside Elevator:</b> {nav.IsInsideElevatorChamber}");
                    
                }
                else
                    Builder.AppendLine($"<pos=-10em><size=10><b>{Bot.Player.DisplayName} - {Bot.Player.PlayerId} Dosent have the Navigation Component!</b>");

                Builder.AppendLine("<pos=-10em><color=grey>--------------------------</color>");
                Builder.AppendLine();
            }

            Player.SendHint($"<align=right>{Builder}</align>");
        }

        public void Destroy()
        {
            Builder = null!;
            Player = null!;
            Bot = null!;
        }
    }
}