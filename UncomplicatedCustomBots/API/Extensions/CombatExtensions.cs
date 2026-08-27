using System;
using System.Collections.Generic;
using Interactables.Interobjects.DoorUtils;
using LabApi.Features.Wrappers;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using UncomplicatedCustomBots.API.Features;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class CombatExtensions
    {
        public static readonly CachedLayerMask CombatHitregMask = new("InvisibleCollider", "Default", "Hitbox", "Glass", "CCTV", "Door");
        private static readonly LayerMask ObstacleMask = LayerMask.GetMask("Default", "InvisibleCollider", "Door", "Fence");
        private const float BodyRadius = 0.3f;
        private static readonly RaycastHit[] _raycastBuffer = new RaycastHit[16];
        private static readonly IComparer<RaycastHit> _hitDistanceComparer = new HitDistanceComparer();

        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        public static bool IsValidCombatTarget(this Bot bot, Player? target)
        {
            if (target == null)
                return false;

            if (!Features.Targeting.IsValidTarget(bot, target))
                return false;

            if (!target.IsAlive)
                return false;

            return Vector3.Distance(bot.Player.Position, target.Position) <= 30f;
        }

        public static bool IsValidPosition(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
                return hit.distance < 3f;

            return false;
        }

        public static bool HasLineOfSight(this Bot bot, Player? target, int layerMask, bool allowTargetHit = true)
        {
            if (target == null)
                return false;

            Vector3 botPosition = bot.Player.Position + Vector3.up * 1.5f;
            Vector3 targetPosition = target.Position + Vector3.up * 1.0f;
            Vector3 direction = (targetPosition - botPosition).normalized;
            float distance = Vector3.Distance(botPosition, targetPosition);

            if (Physics.Raycast(botPosition, direction, out RaycastHit hit, distance, layerMask))
            {
                if (allowTargetHit)
                    return hit.transform.root == target.ReferenceHub.transform.root || Vector3.Distance(hit.point, targetPosition) < 0.5f;

                return false;
            }

            return true;
        }

        public static bool HasLineOfSightWithDoors(this Bot bot, Player? target)
        {
            if (target == null)
                return false;

            Vector3 botPosition = bot.Player.Position + Vector3.up * 1.5f;
            Vector3 targetPosition = target.Position + Vector3.up * 1.5f;
            Vector3 direction = (targetPosition - botPosition).normalized;
            float distance = Vector3.Distance(botPosition, targetPosition);

            int hitCount = Physics.RaycastNonAlloc(botPosition, direction, _raycastBuffer, distance, CombatHitregMask);
            if (hitCount == 0)
                return true;

            Array.Sort(_raycastBuffer, 0, hitCount, _hitDistanceComparer);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastBuffer[i];
                if (hit.transform.root == target.ReferenceHub.transform.root)
                    return true;

                if (IsDoorOpen(hit.collider.transform))
                    continue;

                return false;
            }

            return true;
        }

        private static bool IsDoorOpen(Transform t)
        {
            if (t == null)
                return false;

            DoorVariant? door = t.GetComponentInParent<DoorVariant>();
            if (door != null)
            {
                if (door.IsConsideredOpen())
                    return true;

                if (door.GetExactState() > 0.1f)
                    return true;

                return false;
            }

            return false;
        }

        public static void LookAt(this Bot bot, Vector3 target)
        {
            if (bot.Player.RoleBase is not IFpcRole fpcRole)
                return;

            Vector3 direction = target - bot.Player.Position;
            if (direction.sqrMagnitude < 0.01f)
                return;

            fpcRole.FpcModule.MouseLook.LookAtDirection(direction.normalized);
        }

        public static void MoveToOptimalDistance(this Bot bot, Player? target, float optimalDistance, float tooCloseDistance, float speed)
        {
            if (target == null || bot.Player.RoleBase is not IFpcRole fpcRole)
                return;

            Vector3 botPosition = bot.Player.Position;
            Vector3 targetPosition = target.Position;

            Vector3 direction = targetPosition - botPosition;
            float distance = direction.magnitude;

            if (distance < 0.01f)
                return;

            direction.Normalize();

            bot.LookAt(targetPosition);

            Vector3 moveDirection = Vector3.zero;

            if (distance > optimalDistance)
            {
                moveDirection = direction;
            }
            else if (distance < tooCloseDistance)
                moveDirection = -direction;

            if (moveDirection == Vector3.zero)
                return;

            moveDirection.y = 0;
            if (moveDirection.sqrMagnitude < 0.01f)
                return;

            moveDirection.Normalize();
            Vector3 newPosition = botPosition + moveDirection * speed * Time.deltaTime;

            newPosition = ResolveCombatCollision(botPosition, newPosition);

            if (IsValidPosition(newPosition))
                fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
        }

        public static void MoveTowards(this Bot bot, Vector3 target, float speed)
        {
            if (bot.Player.RoleBase is not IFpcRole fpcRole)
                return;

            Vector3 botPosition = bot.Player.Position;
            Vector3 direction = target - botPosition;
            direction.y = 0;

            if (direction.sqrMagnitude < 0.01f)
                return;

            direction.Normalize();

            Vector3 newPosition = botPosition + direction * speed * Time.deltaTime;

            newPosition = ResolveCombatCollision(botPosition, newPosition);

            if (newPosition.y > botPosition.y - 2f && newPosition.y < botPosition.y + 2f && IsValidPosition(newPosition))
                fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
        }

        private static Vector3 ResolveCombatCollision(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.001f)
                return to;

            Vector3 direction = delta / distance;
            Vector3 bottom = from + Vector3.up * 0.05f;
            Vector3 top = from + Vector3.up * 0.55f;

            if (Physics.CapsuleCast(bottom, top, BodyRadius, direction, out RaycastHit hit, distance, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null)
                {
                    DoorVariant door = hit.collider.GetComponentInParent<DoorVariant>();
                    if (door != null && door.IsConsideredOpen())
                        return to;
                }

                float safeDistance = Mathf.Max(0f, hit.distance - 0.03f);
                Vector3 blocked = from + direction * safeDistance;

                Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
                slide.y = 0f;
                if (slide.sqrMagnitude > 0.01f)
                {
                    slide.Normalize();
                    blocked += slide * Mathf.Min(0.25f, distance - safeDistance);
                }

                return blocked;
            }

            return to;
        }
    }
}