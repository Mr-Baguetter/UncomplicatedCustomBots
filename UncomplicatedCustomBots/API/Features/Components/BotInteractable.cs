using CommandSystem.Commands.RemoteAdmin.Dummies;
using InventorySystem.Searching;
using LabApi.Features.Wrappers;
using UnityEngine;
using static AdminToys.InvisibleInteractableToy;

namespace UncomplicatedCustomBots.API.Features.Components
{
    public class BotInteractable : MonoBehaviour
    {
        public InteractableToy? Interactable { get; set; }
        public Bot? Bot { get; set; }
        public Player? Following { get; set; }
        public PlayerFollower? PlayerFollower { get; set; }
        public Navigation? Navigation { get; set; }

        public void Init(Bot bot)
        {
            Bot = bot;

            Interactable = InteractableToy.Create(Bot.Player.GameObject!.transform);
            Interactable.Shape = ColliderShape.Capsule;
            Interactable.InteractionDuration = 0.5f;
            Interactable.OnSearched += OnSearched;

            if (!bot.Player.GameObject!.TryGetComponent<PlayerFollower>(out var follower))
                follower = bot.Player.GameObject!.AddComponent<PlayerFollower>();

            PlayerFollower = follower;

            if (bot.Player.GameObject!.TryGetComponent<Navigation>(out var nav))
                Navigation = nav;
        }

        private void OnDestroy()
        {
            Interactable?.Destroy();
            PlayerFollower?.enabled = false;
        }

        private void OnSearched(Player player)
        {
            if (Bot?.Player.Faction != player.Faction)
                return;

            Navigation?.StopNavigation();
            Navigation?.enabled = false;
            PlayerFollower?.enabled = true;
            PlayerFollower?.Init(player.ReferenceHub);
            Following = player;
        }
    }
}