using System;
using System.Collections.Generic;
using CommandSystem.Commands.RemoteAdmin.Dummies;
using CustomPlayerEffects;
using InventorySystem.Items;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Usables;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles.FirstPersonControl;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using static InventorySystem.Items.ThrowableProjectiles.ThrowableNetworkHandler;
using UsableItem = LabApi.Features.Wrappers.UsableItem;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class CombatState : State
    {
        public Player Target = null!;

        private Navigation _navigator = null!;

        private PlayerFollower? _follower;

        private const float FireRate = 0.2f;
        private const int BurstSize = 4;
        private const float BurstCooldown = 0.7f;
        private const float OptimalDistance = 15f;
        private const float TooCloseDistance = 7f;
        private const float EffectiveRange = 35f;
        private const float CombatSpeed = 13.5f;
        private const float TargetCheckInterval = 0.5f;
        private const float MinStateDuration = 1f;
        private const float MaxNoSightTime = 5f;
        private const float HealHealthThreshold = 50f;
        private const float SafeHealWindow = 2f;
        private const float DodgeFlipInterval = 1.2f;
        private const float ThrowRange = 12f;
        private const float CombatPathRecalcInterval = 0.5f;

        private float _fireTimer = 0f;
        private int _burstRoundsFired = 0;
        private float _burstCooldownTimer = 0f;
        private float _targetCheckTimer = 0f;
        private float _stateChangeTimer = 0f;
        private float _noTargetSightTimer = 0f;
        private bool _isReloading = false;
        private float _dodgeDirection = 1f;
        private float _dodgeFlipTimer = 0f;
        private float _combatPathTimer = 0f;
        private bool _isThrowingGrenade = false;
        private CoroutineHandle _throwCoroutineHandle;

        public CombatState(Bot bot) : base(bot) { }

        public override void Enter()
        {
            Target ??= Bot.Context.Target ?? null!;

            Navigation? cached = Bot.CachedNavigation;
            if (cached == null)
            {
                cached = Bot.Player.GameObject!.AddComponent<Navigation>();
                Bot.SetCachedNavigation(cached);
            }

            _navigator = cached;
            _navigator.enabled = true;

            if (Bot.Player.RoleBase is IFpcRole fpc)
            {
                float chaseSpeed = CombatSpeed;

                if (fpc.FpcModule.SprintSpeed > chaseSpeed)
                    chaseSpeed = fpc.FpcModule.SprintSpeed;
                    
                chaseSpeed = Mathf.Clamp(chaseSpeed, 6f, 10f);

                if (chaseSpeed < fpc.FpcModule.WalkSpeed * 1.6f)
                    chaseSpeed = fpc.FpcModule.WalkSpeed * 1.6f;
                    
                _navigator.Init(speed: chaseSpeed, enablePatrol: false);
            }
            else
                _navigator.Init(speed: CombatSpeed, enablePatrol: false);

            _navigator.StopNavigation();

            if (Bot.Player.GameObject!.TryGetComponent<PlayerFollower>(out _follower) && _follower.enabled)
                _follower.enabled = false;

            _stateChangeTimer = 0f;
            _noTargetSightTimer = 0f;
            _combatPathTimer = 0f;
            _isThrowingGrenade = false;
            if (_throwCoroutineHandle.IsRunning)
            {
                Timing.KillCoroutines(_throwCoroutineHandle);
            }
        }

        public override void Update()
        {
            _stateChangeTimer += Time.deltaTime;

            if (_isReloading)
            {
                if (Bot.Player.CurrentItem is FirearmItem firearm && !IsActuallyReloading(firearm))
                {
                    _isReloading = false;
                }
                else
                {
                    if (Target != null)
                        HandleCombatMovement();
                        
                    return;
                }
            }

            if (IsUsingMedicalItem())
            {
                if (Target != null)
                    HandleCombatMovement();

                return;
            }

            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= TargetCheckInterval || Target == null)
            {
                Target = Targeting.GetTarget(Bot, Target)!;
                _targetCheckTimer = 0f;
            }

            if (Target != null && Bot.HasLineOfSightWithDoors(Target))
            {
                _noTargetSightTimer = 0f;
            }
            else
            {
                _noTargetSightTimer += Time.deltaTime;
                if (_noTargetSightTimer >= MaxNoSightTime)
                {
                    Bot.ChangeState(new WalkingState(Bot));
                    return;
                }
            }

            if (ShouldExitCombat())
            {
                if (_follower != null)
                {
                    _follower.enabled = true;
                    return;
                }

                Bot.ChangeState(new WalkingState(Bot));
                return;
            }

            if (Bot.Player.Health < HealHealthThreshold && CanHealSafely())
                UseMedicalItem();

            if (CheckAndReload())
                return;

            if (!_isThrowingGrenade && Bot.Player.Health > HealHealthThreshold)
            {
                if (!SwitchToBestWeapon())
                {
                    if (Target != null && _stateChangeTimer > MinStateDuration)
                    {
                        Bot.ChangeState(new FleeState(Bot, Target));
                        return;
                    }
                }
            }

            HandleCombatMovement();
            HandleCombatShooting();
        }

        private bool IsUsingMedicalItem()
        {
            if (Bot.Player.CurrentItem is not UsableItem usable)
                return false;

            return usable.IsUsing;
        }

        private bool ShouldExitCombat() => !Bot.IsValidCombatTarget(Target);

        private bool CanHealSafely() => Bot.Context.GetRecentAttacker(SafeHealWindow) == null;

        private void HandleCombatMovement()
        {
            if (Target == null || Bot.Player.RoleBase is not IFpcRole fpcRole)
                return;

            Vector3 targetPosition = Target.Position;
            Vector3 botPosition = Bot.Player.Position;
            Vector3 direction = (targetPosition - botPosition).normalized;
            float sq = (botPosition - targetPosition).sqrMagnitude;

            if (sq > OptimalDistance * OptimalDistance)
            {
                HandleChaseMovement(targetPosition);
            }
            else if (sq < TooCloseDistance * TooCloseDistance)
            {
                StopChaseMovement();

                Vector3 retreatTarget = _navigator.ProjectToNavMesh(botPosition - direction * 3f);
                _navigator.MoveTowards(retreatTarget, CombatSpeed, lookAtTarget: false);
            }
            else
            {
                StopChaseMovement();

                _dodgeFlipTimer -= Time.deltaTime;
                if (_dodgeFlipTimer <= 0f)
                {
                    _dodgeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                    _dodgeFlipTimer = DodgeFlipInterval;
                }

                Vector3 strafeDirection = Vector3.Cross(Vector3.up, direction) * _dodgeDirection;

                Player? recentAttacker = Bot.Context.GetRecentAttacker(3f);
                Vector3 moveDirection;
                if (recentAttacker != null && recentAttacker != Target)
                {
                    Vector3 awayFromAttacker = (botPosition - recentAttacker.Position).normalized;
                    moveDirection = (strafeDirection + awayFromAttacker * 0.5f).normalized;
                }
                else
                    moveDirection = strafeDirection;

                Vector3 strafeTarget = _navigator.ProjectToNavMesh(botPosition + moveDirection * 3f);
                _navigator.MoveTowards(strafeTarget, CombatSpeed, lookAtTarget: false);
            }

            fpcRole.FpcModule.MouseLook.LookAtDirection(direction, 0.7f);
        }

        private void HandleChaseMovement(Vector3 targetPosition)
        {
            _combatPathTimer -= Time.deltaTime;
            if (_combatPathTimer <= 0f)
            {
                Vector3 projected = _navigator.ProjectToNavMesh(targetPosition);
                if (!_navigator.NavigateToWorldPosition(projected))
                {
                    if (Plugin.Instance.Config.Debug)
                    {
                        LogManager.Debug($"{Bot.Player.Nickname} failed to build a navmesh chase path to {targetPosition}, moving directly.");
                    }
                }

                _combatPathTimer = CombatPathRecalcInterval;
            }

            if (_navigator.IsNavigating)
                return;

            _navigator.MoveTowards(_navigator.ProjectToNavMesh(targetPosition), CombatSpeed, lookAtTarget: false);
        }

        private void StopChaseMovement()
        {
            _combatPathTimer = 0f;

            if (_navigator.IsNavigating)
                _navigator.StopNavigation();
        }

        private void HandleCombatShooting()
        {
            if (Target == null || !Bot.HasLineOfSightWithDoors(Target) || Bot.Player.HasEffect<Flashed>())
                return;

            float sq = (Bot.Player.Position - Target.Position).sqrMagnitude;

            if (Bot.Player.CurrentItem is FirearmItem currentFirearm)
            {
                if (sq > EffectiveRange * EffectiveRange)
                    return;

                if (_burstCooldownTimer > 0f)
                {
                    _burstCooldownTimer -= Time.deltaTime;
                    return;
                }

                if (currentFirearm.Base.TryGetModule<IAmmoContainerModule>(out var ammoContainer) && ammoContainer.AmmoStored > 0)
                {
                    _fireTimer -= Time.deltaTime;
                    if (_fireTimer <= 0f)
                    {
                        BotExtensions.TryRunItemAction(currentFirearm.Base, ActionName.Shoot, true);
                        _fireTimer = FireRate;
                        _burstRoundsFired++;

                        if (_burstRoundsFired >= BurstSize)
                        {
                            _burstRoundsFired = 0;
                            _burstCooldownTimer = BurstCooldown;
                        }
                    }
                }
            }
            else if (Bot.Player.CurrentItem is ThrowableItem throwableItem)
            {
                if (sq > ThrowRange * ThrowRange)
                    return;

                if (_isThrowingGrenade)
                    return;

                TryBeginThrowableThrow(throwableItem);
            }
        }

        private bool TryBeginThrowableThrow(ThrowableItem throwableItem)
        {
            if (throwableItem == null)
                return false;

            if (_isThrowingGrenade)
                return false;

            if (throwableItem.Base == null)
                return false;

            if (throwableItem.Base.PendingRemoval)
                return false;

            if (!throwableItem.Base.AllowHolster)
                return false;

            if (throwableItem.Base.CancelStopwatch != null && throwableItem.Base.CancelStopwatch.IsRunning)
                return false;

            if (throwableItem.Base.ThrowStopwatch != null && throwableItem.Base.ThrowStopwatch.IsRunning)
                return false;


            ReferenceHub ownerHub = throwableItem.Base.Owner;
            if (ownerHub.HasBlock(BlockedInteraction.ItemPrimaryAction))
                return false;

            float speedMultiplier = 1f;
            if (throwableItem.Base.ItemTypeId.TryGetSpeedMultiplier(ownerHub, out float mult) && mult > 0f)
                speedMultiplier = mult;

            if (speedMultiplier <= 0f)
                speedMultiplier = 1f;

            try
            {
                throwableItem.Base.ServerProcessInitiation();
            }
            catch (Exception ex)
            {
                LogManager.Error($"Error initiating throwable throw: {ex.Message}");
                return false;
            }

            if (throwableItem.Base.ThrowStopwatch == null || !throwableItem.Base.ThrowStopwatch.IsRunning)
                return false;

            _isThrowingGrenade = true;

            float baseTime = throwableItem.Base.ThrowingAnimTime;
            if (baseTime <= 0f)
                baseTime = 0.6f;

            float delay = baseTime * 0.8f / speedMultiplier;
            delay += 0.05f;
            if (delay < 0.05f)
                delay = 0.05f;

            if (delay > 2f)
                delay = 2f;

            if (_throwCoroutineHandle.IsRunning)
                Timing.KillCoroutines(_throwCoroutineHandle);

            _throwCoroutineHandle = Timing.RunCoroutine(ThrowGrenadeRoutine(throwableItem, delay));
            return true;
        }

        private IEnumerator<float> ThrowGrenadeRoutine(ThrowableItem throwableItem, float delay)
        {
            float elapsed = 0f;

            while (elapsed < delay)
            {
                if (Target != null && Bot.Player.RoleBase is IFpcRole fpcRole)
                {
                    Vector3 direction = Target.Position + Vector3.up * 1f - Bot.Player.Position;
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        direction.Normalize();
                        fpcRole.FpcModule.MouseLook.LookAtDirection(direction);
                    }
                }

                if (Bot.Player.CurrentItem != throwableItem)
                {
                    _isThrowingGrenade = false;
                    yield break;
                }

                if (throwableItem.Base == null || throwableItem.Base.PendingRemoval)
                {
                    _isThrowingGrenade = false;
                    yield break;
                }

                if (Target == null || !Bot.IsValidCombatTarget(Target))
                {
                    try
                    {
                        if (throwableItem.Base.CancelStopwatch != null && !throwableItem.Base.CancelStopwatch.IsRunning)
                        {
                            throwableItem.Base.ServerProcessCancellation();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"Error cancelling throwable throw: {ex.Message}");
                    }

                    _isThrowingGrenade = false;
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return Timing.WaitForOneFrame;
            }

            if (Bot.Player.CurrentItem != throwableItem)
            {
                _isThrowingGrenade = false;
                yield break;
            }

            if (throwableItem.Base == null || throwableItem.Base.PendingRemoval)
            {
                _isThrowingGrenade = false;
                yield break;
            }

            if (Target != null)
            {
                float dist = Vector3.Distance(Bot.Player.Position, Target.Position);
                if (dist > ThrowRange + 3f || !Bot.HasLineOfSightWithDoors(Target))
                {
                    try
                    {
                        if (throwableItem.Base.CancelStopwatch != null && !throwableItem.Base.CancelStopwatch.IsRunning)
                            throwableItem.Base.ServerProcessCancellation();
                    }
                    catch (Exception ex)
                    {
                        LogManager.Error($"Error cancelling throwable throw (LOS): {ex.Message}");
                    }

                    _isThrowingGrenade = false;
                    yield break;
                }
            }

            if (throwableItem.Base.ThrowStopwatch == null || !throwableItem.Base.ThrowStopwatch.IsRunning)
            {
                _isThrowingGrenade = false;
                yield break;
            }

            try
            {
                ReferenceHub hub = Bot.Player.ReferenceHub;
                if (hub == null || hub.PlayerCameraReference == null)
                {
                    _isThrowingGrenade = false;
                    yield break;
                }

                Vector3 camPos = hub.PlayerCameraReference.position;
                Quaternion camRot = hub.PlayerCameraReference.rotation;

                if (Target != null)
                {
                    Vector3 aimPoint = Target.Position + Vector3.up * 1f;
                    Vector3 aimDir = aimPoint - camPos;
                    if (aimDir.sqrMagnitude > 0.01f)
                    {
                        aimDir.Normalize();
                        if (Bot.Player.RoleBase is IFpcRole fpcLook)
                        {
                            fpcLook.FpcModule.MouseLook.LookAtDirection(aimDir);
                            camRot = hub.PlayerCameraReference.rotation;
                        }
                    }
                }

                Vector3 limitedVel = GetLimitedVelocity(Bot.Player.Velocity);
                throwableItem.Base.ServerProcessThrowConfirmation(true, camPos, camRot, limitedVel);
            }
            catch (Exception ex)
            {
                LogManager.Error($"Error confirming throwable throw: {ex.Message}");
            }
            finally
            {
                _isThrowingGrenade = false;
            }
        }

        private bool SwitchToBestWeapon()
        {
            Item? bestWeapon = null;
            int bestScore = int.MinValue;

            foreach (Item item in Bot.Player.Items)
            {
                int score = 0;
                if (item is FirearmItem firearm)
                {
                    if (firearm.Base.TryGetModule<IAmmoContainerModule>(out var ammoContainer) && ammoContainer.AmmoStored > 0)
                    {
                        score = 100;
                    }
                    else if (Bot.Player.Ammo.TryGetValue(firearm.AmmoType, out var ammoCount) && ammoCount > 0)
                    {
                        score = 50;
                    }
                    else
                        score = 10;
                }
                else if (item is ThrowableItem)
                {
                    score = 30;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestWeapon = item;
                }
            }

            if (bestWeapon != null && Bot.Player.CurrentItem != bestWeapon)
            {
                Bot.Player.CurrentItem = bestWeapon;
                return true;
            }

            return bestWeapon != null;
        }

        private void UseMedicalItem()
        {
            UsableItem? medicalItem = null;
            foreach (Item item in Bot.Player.Items)
            {
                if (item is UsableItem usable && (item.Type == ItemType.Medkit || item.Type == ItemType.Adrenaline || item.Type == ItemType.Painkillers))
                {
                    medicalItem = usable;
                    break;
                }
            }

            if (medicalItem != null)
            {
                Bot.Player.CurrentItem = medicalItem;
                try
                {
                    if (!medicalItem.IsUsing)
                    {
                        UsableItemsController.ServerEmulateMessage(medicalItem.Serial, StatusMessage.StatusType.Start);
                        medicalItem.IsUsing = true;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error($"Error using medical item: {ex.Message}");
                }
            }
        }

        private bool CheckAndReload()
        {
            if (Bot.Player.CurrentItem is not FirearmItem firearm)
                return false;

            if (!firearm.Base.TryGetModule<IReloaderModule>(out var reloadModule) || !firearm.Base.TryGetModule<IAmmoContainerModule>(out var ammoContainer))
                return false;

            if (ammoContainer.AmmoStored == 0)
            {
                if (Bot.Player.Ammo.TryGetValue(firearm.AmmoType, out ushort availableAmmo) && availableAmmo > 0)
                {
                    if (reloadModule is AnimatorReloaderModuleBase reloadAnimator)
                    {
                        try
                        {
                            reloadAnimator.ServerTryReload();
                            _isReloading = true;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"Reloading weapon: {ex.Message}");
                        }
                    }
                }
            }

            return false;
        }

        private bool IsActuallyReloading(FirearmItem firearm)
        {
            if (firearm?.Base?.TryGetModule<IReloaderModule>(out var reloadModule) == true)
                return reloadModule.IsReloading;

            return false;
        }

        public override void Exit()
        {
            if (_throwCoroutineHandle.IsRunning)
                Timing.KillCoroutines(_throwCoroutineHandle);

            _isThrowingGrenade = false;
            _navigator?.enabled = true;
        }
    }
}