using System.Collections.Generic;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Pickups;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.API.Extensions;
using UncomplicatedCustomBots.API.Features.States;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class BotRadius : TriggerHandler
    {
        public const float DefaultRadius = 5f;

        public SphereCollider? SphereCollider { get; set; }
        public Bot? Bot { get; set; }
        public PrimitiveObjectToy? Visualizer { get; set; }
        public readonly List<DoorVariant> DoorsInRange = [];
        public readonly List<Pickup> PickupsInRange = [];
        private readonly HashSet<DoorVariant> _doorsSet = [];
        private readonly HashSet<Pickup> _pickupsSet = [];

        private readonly Collider[] _overlapBuffer = new Collider[64];

        public void Init(Bot bot)
        {
            Bot = bot;

            SphereCollider ??= gameObject.AddComponent<SphereCollider>();
            SphereCollider.isTrigger = true;
            SphereCollider.radius = DefaultRadius;
            // SphereCollider.gameObject.layer = 16; // InvisibleCollider

            Collider = SphereCollider;

            if (TryGetComponent<BoxCollider>(out var box))
                Destroy(box);
        }

        private float _overlapTimer = 0f;
        private const float OverlapInterval = 0.2f;
        private static readonly int _overlapMask = LayerMask.GetMask("Default", "Door");
        private bool _staggerInitialized = false;

        private void Update()
        {
            if (!_staggerInitialized)
            {
                _staggerInitialized = true;
                if (Bot != null)
                    _overlapTimer = Bot.Player.PlayerId % 5 * 0.04f;
            }

            _overlapTimer += Time.deltaTime;
            if (_overlapTimer < OverlapInterval)
                return;
                
            _overlapTimer = 0f;

            float radius = SphereCollider != null ? SphereCollider.radius : DefaultRadius;

            DoorsInRange.Clear();
            PickupsInRange.Clear();
            _doorsSet.Clear();
            _pickupsSet.Clear();

            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapBuffer, _overlapMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlapBuffer[i];
                if (collider == null || collider.transform.root == transform.root)
                    continue;

                if (collider.GetComponentInParent<DoorVariant>() is { } door && _doorsSet.Add(door))
                    DoorsInRange.Add(door);

                if (collider.TryGetComponent<ItemPickupBase>(out var pickupBase) && Pickup.TryGet(pickupBase.Info.Serial, out var pickup) && _pickupsSet.Add(pickup))
                    PickupsInRange.Add(pickup);
            }
        }

        public override void OnDoorEntered(DoorVariant door)
        {
            if (_doorsSet.Add(door))
                DoorsInRange.Add(door);
        }

        public override void OnDoorExited(DoorVariant door)
        {
            if (_doorsSet.Remove(door))
                DoorsInRange.Remove(door);
        }

        public override void OnPickupEntered(Pickup pickup)
        {
            if (_pickupsSet.Add(pickup))
                PickupsInRange.Add(pickup);
        }

        public override void OnPickupExited(Pickup pickup)
        {
            if (_pickupsSet.Remove(pickup))
                PickupsInRange.Remove(pickup);
        }

        public override void OnPlayerEntered(Player player)
        {
            if (Bot == null)
                return;

            if (Bot.State == null || Bot.GetState() == typeof(CombatState))
                return;

            if (Targeting.IsValidTarget(Bot, player))
            {
                CombatState state = new(Bot)
                {
                    Target = player
                };

                Bot.ChangeState(state);
            }
        }

        public void VisualizeCollider()
        {
            if (Visualizer == null || Visualizer.IsDestroyed)
            {
                Visualizer = PrimitiveObjectToy.Create(transform);
                Visualizer.Type = PrimitiveType.Sphere;
                Visualizer.SyncInterval = 0; 
                Visualizer.Flags |= AdminToys.PrimitiveFlags.Visible;
                Visualizer.Flags &= ~AdminToys.PrimitiveFlags.Collidable;
                Visualizer.Color = new Color(0.000f, 0.502f, 1.000f, 0.544f);
            }
        }

        public void DestroyVisualizer()
            => Visualizer?.Destroy();
    }
}