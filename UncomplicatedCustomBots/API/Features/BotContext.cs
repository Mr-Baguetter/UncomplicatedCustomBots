using LabApi.Features.Wrappers;
using System.Collections.Generic;
using System.Linq;
using UncomplicatedCustomBots.API.Struct;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features
{
    public enum SensedEventType
    {
        Gunshot,
        DoorOpen,
        DoorClose,
        Tesla,
        Grenade,
        Footstep,
        UsingItem,
        Speaking,
    }

    public class BotContext
    {
        private const int MaxAttackers = 8;
        private const int MaxSensedEvents = 16;
        private const float SensedEventLifetime = 15f;
        private const float WallAttenuationFactor = 0.4f;

        private static readonly LayerMask _hearingObstacleMask = LayerMask.GetMask("Default", "InvisibleCollider", "Door", "Fence");

        public Player? Target { get; set; }

        public Vector3? LastKnownTargetPosition { get; set; }

        public float LastKnownTargetTime { get; set; }

        public Player? LastAttacker { get; set; }

        public float LastAttackedTime { get; set; }

        public List<AttackerRecord> Attackers { get; } = [];

        public List<SensedEvent> SensedEvents { get; } = [];

        public void RecordAttacker(Player attacker, float damage)
        {
            if (attacker == null)
                return;

            Attackers.RemoveAll(a => a.Attacker == attacker);
            Attackers.Add(new AttackerRecord { Attacker = attacker, Damage = damage, Time = Time.time });

            if (Attackers.Count > MaxAttackers)
                Attackers.RemoveAt(0);

            LastAttacker = attacker;
            LastAttackedTime = Time.time;
        }

        public Player? PeekRecentAttacker(float withinSeconds)
        {
            float now = Time.time;
            Player? best = null;
            float bestTime = float.MinValue;
            for (int i = 0; i < Attackers.Count; i++)
            {
                AttackerRecord a = Attackers[i];
                if (a.Attacker == null || now - a.Time > withinSeconds)
                    continue;
                    
                if (a.Time > bestTime)
                {
                    bestTime = a.Time;
                    best = a.Attacker;
                }
            }
            return best;
        }

        public Player? GetRecentAttacker(float withinSeconds)
        {
            float now = Time.time;
            Attackers.RemoveAll(a => a.Attacker == null || now - a.Time > withinSeconds);

            Player? best = null;
            float bestTime = float.MinValue;
            for (int i = 0; i < Attackers.Count; i++)
            {
                if (Attackers[i].Time > bestTime)
                {
                    bestTime = Attackers[i].Time;
                    best = Attackers[i].Attacker;
                }
            }

            return best;
        }

        public void AddSensedEvent(SensedEventType type, Vector3 position, byte priority, float distance)
        {
            SensedEvents.Add(new SensedEvent
            {
                Type = type,
                Position = position,
                Time = Time.time,
                Priority = priority,
                Distance = distance,
            });

            SensedEvents.RemoveAll(e => Time.time - e.Time > SensedEventLifetime);

            if (SensedEvents.Count > MaxSensedEvents)
                SensedEvents.RemoveAt(0);
        }

        public static bool HasSoundLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 origin = from + Vector3.up * 1.5f;
            Vector3 target = to + Vector3.up * 1.0f;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;

            if (distance < 0.1f)
                return true;

            return !Physics.Raycast(origin, direction.normalized, distance, _hearingObstacleMask);
        }

        public float AttenuateHearing(float baseRange, Vector3 listenerPosition, Vector3 soundPosition)
        {
            if (HasSoundLineOfSight(listenerPosition, soundPosition))
                return baseRange;

            return baseRange * WallAttenuationFactor;
        }

        public bool HasHeardGunshotNear(Vector3 position, float withinSeconds, float angularTolerance)
        {
            float now = Time.time;
            for (int i = 0; i < SensedEvents.Count; i++)
            {
                SensedEvent e = SensedEvents[i];
                if (e.Type != SensedEventType.Gunshot || now - e.Time > withinSeconds)
                    continue;

                float dist = Vector3.Distance(e.Position, position);
                if (dist < angularTolerance)
                    return true;
            }

            return false;
        }

        public bool HasHeardSpeakingNear(Vector3 position, float withinSeconds, float distanceTolerance)
        {
            float now = Time.time;
            for (int i = 0; i < SensedEvents.Count; i++)
            {
                SensedEvent e = SensedEvents[i];
                if (e.Type != SensedEventType.Speaking || now - e.Time > withinSeconds)
                    continue;

                float dist = Vector3.Distance(e.Position, position);
                if (dist < distanceTolerance)
                    return true;
            }

            return false;
        }

        public bool HasHeardEventNear(SensedEventType type, Vector3 position, float withinSeconds, float distanceTolerance)
        {
            float now = Time.time;
            for (int i = 0; i < SensedEvents.Count; i++)
            {
                SensedEvent e = SensedEvents[i];
                if (e.Type != type || now - e.Time > withinSeconds)
                    continue;

                float dist = Vector3.Distance(e.Position, position);
                if (dist < distanceTolerance)
                    return true;
            }

            return false;
        }

        public SensedEvent? GetHighestPriorityEvent(SensedEventType type, float withinSeconds)
        {
            float now = Time.time;
            SensedEvent? best = null;

            for (int i = 0; i < SensedEvents.Count; i++)
            {
                SensedEvent e = SensedEvents[i];
                if (e.Type != type || now - e.Time > withinSeconds)
                    continue;

                if (best == null || e.Priority > best.Value.Priority || (e.Priority == best.Value.Priority && e.Time > best.Value.Time))
                    best = e;
            }

            return best;
        }

        public void RememberLastKnownTarget(Player? target)
        {
            if (target == null)
                return;

            LastKnownTargetPosition = target.Position;
            LastKnownTargetTime = Time.time;
        }

        public void ClearMemory()
        {
            Target = null;
            LastKnownTargetPosition = null;
            LastKnownTargetTime = 0f;
            LastAttacker = null;
            LastAttackedTime = 0f;
            Attackers.Clear();
            SensedEvents.Clear();
        }
    }
}