using System.Collections.Generic;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp173;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class Scp173StareMonitor : MonoBehaviour
    {
        private Bot _bot = null!;
        private readonly Dictionary<int, float> _stareDurations = [];
        private const float TriggerTime = 1f;
        private const float CheckInterval = 0.05f;
        private float _checkTimer = 0f;

        private Scp173Role _scp173Role = null!;
        private Scp173ObserversTracker _tracker = null!;

        public void Initialize(Bot bot)
        {
            _bot = bot;
        }

        private void Update()
        {
            if (_bot == null || _bot.Player == null)
                return;

            if (_bot.Player.Role != RoleTypeId.Scp173)
            {
                if (_stareDurations.Count > 0)
                    _stareDurations.Clear();

                _tracker = null!;
                _scp173Role = null!;
                return;
            }

            if (_bot.State is Scp173State)
            {
                if (_stareDurations.Count > 0)
                    _stareDurations.Clear();

                return;
            }

            _checkTimer += Time.deltaTime;
            if (_checkTimer < CheckInterval)
                return;

            _checkTimer = 0f;

            Scp173Role? currentRole = _bot.Player.RoleBase as Scp173Role;
            if (currentRole == null)
            {
                _stareDurations.Clear();
                return;
            }

            if (_scp173Role != currentRole || _tracker == null)
            {
                _scp173Role = currentRole;
                if (!_scp173Role.SubroutineModule.TryGetSubroutine(out _tracker) || _tracker == null)
                {
                    _stareDurations.Clear();
                    return;
                }
            }

            HashSet<int> currentlyObserving = [];

            foreach (Player p in Player.List)
            {
                if (p == null || p.ReferenceHub == null)
                    continue;

                if (p == _bot.Player)
                    continue;

                if (!p.IsAlive || p.Role == RoleTypeId.Spectator || p.Role == RoleTypeId.Destroyed)
                    continue;

                if (_tracker.IsObservedBy(p.ReferenceHub))
                {
                    int id = p.PlayerId;
                    currentlyObserving.Add(id);

                    if (!_stareDurations.TryGetValue(id, out float dur))
                        dur = 0f;

                    dur += CheckInterval;
                    _stareDurations[id] = dur;

                    if (dur >= TriggerTime)
                    {
                        LogManager.Debug($"Scp173StareMonitor: {_bot.Player.DisplayName} was stared at by {p.DisplayName} for {dur:F2}s -> forcing Scp173State");
                        _stareDurations.Clear();
                        _bot.ChangeState(new Scp173State(_bot));
                        return;
                    }
                }
            }

            List<int> toRemove = null!;
            foreach (int id in _stareDurations.Keys)
            {
                if (!currentlyObserving.Contains(id))
                {
                    toRemove ??= [];
                    toRemove.Add(id);
                }
            }

            if (toRemove != null)
            {
                foreach (int id in toRemove)
                    _stareDurations.Remove(id);
            }

            if (_stareDurations.Count == 0 && _tracker.IsObserved)
            {
                const int overallKey = -1;
                if (!_stareDurations.TryGetValue(overallKey, out float overall))
                    overall = 0f;

                overall += CheckInterval;
                _stareDurations[overallKey] = overall;
                if (overall >= TriggerTime)
                {
                    LogManager.Debug($"Scp173StareMonitor: {_bot.Player.DisplayName} was observed (overall) for {overall:F2}s -> forcing Scp173State");
                    _stareDurations.Clear();
                    _bot.ChangeState(new Scp173State(_bot));
                    return;
                }
            }
            else if (_stareDurations.ContainsKey(-1) && !_tracker.IsObserved)
            {
                _stareDurations.Remove(-1);
            }
        }
    }
}
