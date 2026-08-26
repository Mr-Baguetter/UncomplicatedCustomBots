using CustomPlayerEffects;
using InventorySystem.Items.Firearms.Modules;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles.FirstPersonControl;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using Utils.Networking;
using static InventorySystem.Items.ThrowableProjectiles.ThrowableNetworkHandler;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class CombatState : State
    {
        public Player Target = null!;

        private Navigation _navigator = null!;

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

        public CombatState(Bot bot) : base(bot) { }

        public override void Enter()
        {
            Target ??= Bot.Context.Target ?? null!;

            _navigator = Bot.Player.GameObject!.GetComponent<Navigation>() ?? Bot.Player.GameObject!.AddComponent<Navigation>();
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

            _stateChangeTimer = 0f;
            _noTargetSightTimer = 0f;
            _combatPathTimer = 0f;
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
                Bot.ChangeState(new WalkingState(Bot));
                return;
            }

            if (Bot.Player.Health < HealHealthThreshold && CanHealSafely())
                UseMedicalItem();

            if (CheckAndReload())
                return;

            if (Bot.Player.Health > HealHealthThreshold)
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
            direction.Normalize();
            float distance = Vector3.Distance(botPosition, targetPosition);

            if (distance > OptimalDistance)
            {
                HandleChaseMovement(targetPosition);
            }
            else if (distance < TooCloseDistance)
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
                    _dodgeDirection = Random.value > 0.5f ? 1f : -1f;
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
                    LogManager.Debug($"{Bot.Player.Nickname} failed to build a navmesh chase path to {targetPosition}, moving directly.");
                }
                else
                    LogManager.Debug($"{Bot.Player.Nickname} rebuilt chase path to {projected}");

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

            float distance = Vector3.Distance(Bot.Player.Position, Target.Position);

            if (Bot.Player.CurrentItem is FirearmItem currentFirearm)
            {
                if (distance > EffectiveRange)
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
                if (distance > ThrowRange)
                    return;

                try
                {
                    throwableItem.Base.ServerThrow(throwableItem.FullThrowStartVelocity, throwableItem.FullThrowUpwardsFactor, throwableItem.FullThrowStartTorque, GetLimitedVelocity(throwableItem.CurrentOwner?.Velocity ?? Vector3.one));
                }
                catch (System.Exception ex)
                {
                    LogManager.Error($"Error throwing item: {ex.Message}");
                }
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
                        medicalItem.IsUsing = true;
                        new InventorySystem.Items.Usables.StatusMessage(InventorySystem.Items.Usables.StatusMessage.StatusType.Start, medicalItem.Serial).SendToAuthenticated();
                    }
                }
                catch (System.Exception ex)
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
                        catch (System.Exception ex)
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
            _navigator?.enabled = true;
        }
    }
}