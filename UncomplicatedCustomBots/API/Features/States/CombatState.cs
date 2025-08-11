using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Modules;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using System.Linq;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;
using InventorySystem.Items.ThrowableProjectiles;
using InventorySystem.Items.Usables;
using InventorySystem.Items.Keycards;
using MEC;
using CustomPlayerEffects;
using static InventorySystem.Items.ThrowableProjectiles.ThrowableNetworkHandler;
using Mirror;
using RemoteAdmin;
using UncomplicatedCustomBots.API.Extensions;

namespace UncomplicatedCustomBots.API.Features.States
{
    public class CombatState : State
    {
        private Player _target;
        private float _fireRate = 0.2f;
        private float _fireTimer = 0f;
        private float _optimalDistance = 15f;
        public static readonly CachedLayerMask HitregMask = new("InvisibleCollider", "Default", "Hitbox", "Glass", "CCTV");
        private float _tooCloseDistance = 7f;
        private bool _isReloading = false;
        private float _combatSpeed = 13.5f;
        private float _targetCheckTimer = 0f;
        private const float TARGET_CHECK_INTERVAL = 0.5f;
        private float _stateChangeTimer = 0f;
        private const float MIN_STATE_DURATION = 1f;
        private float _noTargetSightTimer = 0f;
        private const float MAX_NO_SIGHT_TIME = 5f;

        public CombatState(Bot bot) : base(bot) { }

        public override void Enter()
        {
            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
                nav.StopNavigation();
            
            _stateChangeTimer = 0f;
            _noTargetSightTimer = 0f;
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
                return;
            }

            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= TARGET_CHECK_INTERVAL || _target == null)
            {
                _target = Targeting.GetTarget(Bot.Player);
                _targetCheckTimer = 0f;
            }

            if (_target != null && HasLineOfSight())
            {
                _noTargetSightTimer = 0f;
            }
            else
            {
                _noTargetSightTimer += Time.deltaTime;
                if (_noTargetSightTimer >= MAX_NO_SIGHT_TIME)
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

            if (Bot.Player.Health < 50)
            {
                UseMedicalItem();
                return;
            }

            if (CheckAndReload())
                return;

            if (!SwitchToBestWeapon())
            {
                if (_target != null && _stateChangeTimer > MIN_STATE_DURATION)
                {
                    Bot.ChangeState(new FleeState(Bot, _target));
                    return;
                }
            }

            HandleCombatMovement();
            HandleCombatShooting();
        }

        private bool ShouldExitCombat()
        {
            if (_target == null || !_target.IsAlive || _target.Role == RoleTypeId.Spectator)
                return true;

            if (_target.Faction == Bot.Player.Faction || _target.IsDisarmed)
                return true;

            if (_target.Role == RoleTypeId.Tutorial && !Plugin.Instance.Config.AttackTutorials)
                return true;

            if (Bot.Player.Role == RoleTypeId.ClassD && _target.Role == RoleTypeId.Scientist)
                return true;

            float distance = Vector3.Distance(Bot.Player.Position, _target.Position);
            if (distance > 30f)
                return true;

            return false;
        }

        private void HandleCombatMovement()
        {
            if (_target == null || !(Bot.Player.RoleBase is IFpcRole fpcRole))
                return;

            Vector3 targetPosition = _target.Position;
            Vector3 botPosition = Bot.Player.Position;
            Vector3 direction = (targetPosition - botPosition).normalized;
            float distance = Vector3.Distance(botPosition, targetPosition);

            Vector3 moveDirection = Vector3.zero;

            if (distance > _optimalDistance)
            {
                moveDirection = direction;
            }
            else if (distance < _tooCloseDistance)
            {
                moveDirection = -direction;
            }
            else
            {
                Vector3 strafeDirection = Vector3.Cross(Vector3.up, direction);
                if (Random.value > 0.5f)
                    strafeDirection = -strafeDirection;
                moveDirection = strafeDirection;
            }

            if (moveDirection != Vector3.zero)
            {
                Vector3 newPosition = botPosition + moveDirection * _combatSpeed * Time.deltaTime;
                
                if (IsValidPosition(newPosition))
                {
                    fpcRole.FpcModule.Motor.ReceivedPosition = new RelativePosition(newPosition);
                }
            }

            fpcRole.FpcModule.MouseLook.LookAtDirection(direction);
        }

        private bool IsValidPosition(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
                return hit.distance < 3f;

            return false;
        }

        private void HandleCombatShooting()
        {
            if (_target == null || !HasLineOfSight() || Bot.Player.HasEffect<Flashed>())
                return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                if (Bot.Player.CurrentItem is FirearmItem currentFirearm)
                {
                    if (currentFirearm.Base.TryGetModule<IAmmoContainerModule>(out var ammoContainer)  && ammoContainer.AmmoStored > 0)
                    {
                        SilentCommandSender silentSender = new();
                        Server.RunCommand($"/dummy action {Bot.Player.PlayerId} {currentFirearm.Type}_(ANY) Shoot->Click", silentSender);
                        _fireTimer = _fireRate;
                    }
                }
                else if (Bot.Player.CurrentItem is LabApi.Features.Wrappers.ThrowableItem throwableItem)
                {
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
        }

        private bool SwitchToBestWeapon()
        {
            Item bestWeapon = Bot.Player.Items.Where(item => item is FirearmItem || item is LabApi.Features.Wrappers.ThrowableItem).OrderByDescending(item =>
            {
                if (item is FirearmItem firearm)
                {
                    if (firearm.Base.TryGetModule<IAmmoContainerModule>(out var ammoContainer) && ammoContainer.AmmoStored > 0)
                        return 100;
                    
                    if (Bot.Player.Ammo.TryGetValue(firearm.AmmoType, out var ammoCount) && ammoCount > 0)
                        return 50;
                    
                    return 10;
                }
                
                if (item is LabApi.Features.Wrappers.ThrowableItem)
                    return 30;
                
                return 0;
            })
            .FirstOrDefault();

            if (bestWeapon != null && Bot.Player.CurrentItem != bestWeapon)
            {
                Bot.Player.CurrentItem = bestWeapon;
                return true;
            }

            return bestWeapon != null;
        }

        private void UseMedicalItem()
        {
            LabApi.Features.Wrappers.UsableItem medicalItem = Bot.Player.Items.OfType<LabApi.Features.Wrappers.UsableItem>().FirstOrDefault(item => item.Type == ItemType.Medkit ||  item.Type == ItemType.Adrenaline ||  item.Type == ItemType.Painkillers);

            if (medicalItem != null)
            {
                Bot.Player.CurrentItem = medicalItem;
                try
                {
                    if (!medicalItem.IsUsing)
                    {
                        medicalItem.Base.ServerOnUsingCompleted();
                        typeof(UsableItemsController).InvokeStaticEvent("ServerOnUsingCompleted", [medicalItem.CurrentOwner.ReferenceHub, medicalItem.Base]);
                    }
                }
                catch (System.Exception ex)
                {
                    LogManager.Error($"Error using medical item: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Checks if the bot has a clear line of sight to its target.
        /// </summary>
        /// <returns>True if there are no obstructions, otherwise false.</returns>
        private bool HasLineOfSight()
        {
            if (_target == null)
                return false;

            Vector3 botPosition = Bot.Player.Position + Vector3.up * 1.5f;
            Vector3 targetPosition = _target.Position + Vector3.up * 1.5f;
            Vector3 direction = (targetPosition - botPosition).normalized;
            float distance = Vector3.Distance(botPosition, targetPosition);

            if (Physics.Raycast(botPosition, direction, out RaycastHit hit, distance, HitregMask))
            {
                if (hit.transform.root == _target.ReferenceHub.transform.root)
                    return true;

                return false;
            }

            return true;
        }

        private bool CheckAndReload()
        {
            if (!(Bot.Player.CurrentItem is FirearmItem firearm))
                return false;

            if (!firearm.Base.TryGetModule<IReloaderModule>(out var reloadModule) || !firearm.Base.TryGetModule<IAmmoContainerModule>(out var ammoContainer))
                return false;

            if (ammoContainer.AmmoStored == 0)
            {
                if (Bot.Player.Ammo.TryGetValue(firearm.AmmoType, out var availableAmmo) && availableAmmo > 0)
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
            if (Bot.Player.GameObject.TryGetComponent<Navigation>(out var nav))
                nav.enabled = true;
        }
    }
}