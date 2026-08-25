using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabApi.Features.Extensions;
using UncomplicatedCustomBots.API.Enums;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class BotDebugComponent : MonoBehaviour
    {
        private Bot _bot = null!;
        private StringBuilder builder = null!;
        public BotDebugSections ActiveSections = BotDebugSections.None;
        private readonly float _logCooldown = 1f;
        private float _nextLogTime = 0f;
        private bool Stopped = false;
        public bool Hidden = false;

        public void Init(Bot bot)
        {
            _bot = bot;
            builder = new();
        }

        private void Update()
        {
            if (Stopped)
                return;
                
            if (Time.time < _nextLogTime)
                return;

            _nextLogTime = Time.time + _logCooldown;

            builder.Clear();

            if (ActiveSections.HasFlag(BotDebugSections.PlayerInfo))
            {
                builder.AppendLine($"Name: {_bot.Player.DisplayName}");
                builder.AppendLine($"Position: {_bot.Player.Position}");
                builder.AppendLine($"Rotation: {_bot.Player.Rotation}");

                if (_bot.Player.Room != null && _bot.Player.CachedRoom != null)
                {
                    builder.AppendLine($"Current Room: {_bot.Player.Room}");
                    builder.AppendLine($"Current Chached Room: {_bot.Player.CachedRoom}");
                }
                else
                {
                    builder.AppendLine($"Current Room: null");
                    builder.AppendLine($"Current Chached Room: null");
                }

                builder.AppendLine($"Health: {_bot.Player.Health}/{_bot.Player.MaxHealth}");
                builder.AppendLine($"Faction: {_bot.Player.Faction}");
                builder.AppendLine($"Team: {_bot.Player.Team}");
                builder.AppendLine($"Role: {_bot.Player.Role.GetFullName()}");
            }

            if (ActiveSections.HasFlag(BotDebugSections.ComponentInfo))
            {
                Component[] components = _bot.Player.GameObject!.GetComponents<Component>();
                List<string> names = components.Select(c => c.GetType().Name).ToList();
                int wrapAfter = 4;
                IEnumerable<string> lines = names.Select((name, index) => new { name, index }).GroupBy(x => x.index / wrapAfter).Select(g => string.Join(", ", g.Select(x => x.name)));

                string componentNames = string.Join("\n", lines);
                builder.AppendLine($"Components:\n{componentNames}");
            }

            if (ActiveSections.HasFlag(BotDebugSections.StateInfo))
            {
                switch (_bot.State)
                {
                    case WalkingState:
                        builder.AppendLine("In WalkingState: true");
                        break;

                    case CombatState:
                        builder.AppendLine("In CombatState: true");
                        break;

                    case FleeState:
                        builder.AppendLine("In FleeState: true");
                        break;

                    case Scp0492State:
                        builder.AppendLine("In Scp0492State: true");
                        break;

                    case Scp049State:
                        builder.AppendLine("In Scp049State: true");
                        break;

                    case Scp106State:
                        builder.AppendLine("In Scp106State: true");
                        break;

                    case Scp173State:
                        builder.AppendLine("In Scp173State: true");
                        break;

                    case Scp939State:
                        builder.AppendLine("In Scp939State: true");
                        break;

                    case Scp3114State:
                        builder.AppendLine("In Scp3114State: true");
                        break;

                    default:
                        break;
                }
            }

            if (Hidden)
            {
                if (!ActiveSections.HasFlag(BotDebugSections.None))
                {
                    LogManager.Silent(builder.ToString());
                }
                else
                    Destroy(this);
            }
            else
            {
                if (!ActiveSections.HasFlag(BotDebugSections.None))
                {
                    LogManager.Info(builder.ToString());
                }
                else
                    Destroy(this);
            }
        }

        public void Stop() => Stopped = true;
        public void Resume() => Stopped = false;
        public void Hide() => Hidden = true;
        public void Show() => Hidden = false;
    }
}
