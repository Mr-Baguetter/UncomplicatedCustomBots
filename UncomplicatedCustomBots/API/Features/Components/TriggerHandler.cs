using System;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Pickups;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class TriggerHandler : MonoBehaviour
    {
        /// <summary>
        /// Gets the collider associated with the trigger.
        /// </summary>
        public Collider? Collider { get; internal set; }

        public virtual void OnPlayerEntered(Player player) { }
        public virtual void OnPlayerExited(Player player) { }
        public virtual void OnDoorEntered(DoorVariant door) { }
        public virtual void OnDoorExited(DoorVariant door) { }
        public virtual void OnPickupEntered(Pickup pickup) { }
        public virtual void OnPickupExited(Pickup pickup) { }

        private void OnTriggerEnter(Collider other)
        {
            if (Player.TryGet(other.gameObject, out var player))
            {
                OnPlayerEntered(player);
                return;
            }

            if (other.gameObject.TryGetComponent<ItemPickupBase>(out var pickupbase) && Pickup.TryGet(pickupbase.Info.Serial, out var pickup))
            {
                OnPickupEntered(pickup);
                return;
            }

            if (other.gameObject.GetComponentInParent<DoorVariant>() is { } door)
            {
                OnDoorEntered(door);
                return;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (Player.TryGet(other.gameObject, out var player))
            {
                OnPlayerExited(player);
                return;
            }

            if (other.gameObject.TryGetComponent<ItemPickupBase>(out var pickupbase) && Pickup.TryGet(pickupbase.Info.Serial, out var pickup))
            {
                OnPickupEntered(pickup);
                return;
            }

            if (other.gameObject.GetComponentInParent<DoorVariant>() is { } door)
            {
                OnDoorEntered(door);
                return;
            }
        }
    }
}