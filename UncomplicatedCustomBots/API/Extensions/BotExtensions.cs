using System;
using System.Collections.Generic;
using System.Linq;
using InventorySystem.Items.Autosync;
using PlayerRoles;
using PlayerRoles.Subroutines;
using UncomplicatedCustomBots.API.Features;
using UncomplicatedCustomBots.API.Features.Components;
using UncomplicatedCustomBots.API.Features.States;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class BotExtensions
    {
        public static void AddNavigation(this Bot bot)
        {
            if (!bot.Player.GameObject!.TryGetComponent<Navigation>(out var nav))
                bot.Player.GameObject!.AddComponent<Navigation>();
        }

        public static bool TryAddNavigation(this Bot bot, out Navigation navigation)
        {
            if (!bot.Player.GameObject!.TryGetComponent(out navigation))
                navigation = bot.Player.GameObject!.AddComponent<Navigation>();

            return navigation != null;
        }

        public static bool TryGetNavigation(this Bot bot, out Navigation navigation) => navigation = bot.Player.GameObject!.GetComponent<Navigation>();

        public static void StartNavigation(this Bot bot, float speed = Navigation.DefaultSpeed, bool patrol = false, bool debug = false, bool enableVariation = true, float variationRadius = 2.5f)
        {
            if (!bot.Player.GameObject!.TryGetComponent<Navigation>(out var nav))
                LogManager.Warn($"{bot.Player.DisplayName} - {bot.Player.PlayerId} Dosent have the Navigation component!");

            nav.Init(speed, patrol, debug, enableVariation, variationRadius);
            bot.ChangeState(new WalkingState(bot));
        }

        public static void StartNavigating(this Bot bot, float speed = Navigation.DefaultSpeed, bool patrol = false, bool debug = false, bool enableVariation = true, float variationRadius = 2.5f)
        {
            TryAddNavigation(bot, out Navigation nav);
            nav.Init(speed, patrol, debug, enableVariation, variationRadius);
            bot.ChangeState(new WalkingState(bot));
        }

        public static bool HasNavigation(this Bot bot) => bot.Player.GameObject!.TryGetComponent<Navigation>(out var nav) && nav != null;

        public static bool IsInState<T>(this Bot bot) where T : State => bot.State is T;

        public static Type GetState(this Bot bot) => bot.State.GetType();

        public static void ChangeStateIfNot<T>(this Bot bot, State newState) where T : State
        {
            if (!bot.IsInState<T>())
                bot.ChangeState(newState);
        }

        public static bool CanStart(this Bot bot) => bot.Player.Role != RoleTypeId.Spectator && bot.Player.Role != RoleTypeId.Destroyed;

        public static IEnumerable<Bot> GetBotsInRole(this IEnumerable<Bot> bots, RoleTypeId role) => bots.Where(b => b.Player.Role == role);

        public static IEnumerable<Bot> GetBotsInState<T>(this IEnumerable<Bot> bots) where T : State => bots.Where(b => b.IsInState<T>());

        public static bool TryGet(this Bot bot, out Bot bot1)
        {
            bot1 = Get(bot);
            return bot1 != null;
        }

        public static Bot Get(this Bot bot) => bot;

        public static bool TryRunItemAction<T>(T item, ActionName actionName, bool isClick) where T : AutosyncItem
        {
            if (item == null)
                return false;
                
            if (!item.IsEmulatedDummy)
                return false;

            item.DummyEmulator.AddEntry(actionName, isClick);
            return true;
        }

        public static bool TryStopItemAction<T>(T item, ActionName actionName) where T : AutosyncItem
        {
            if (item == null)
                return false;

            if (!item.IsEmulatedDummy)
                return false;

            item.DummyEmulator.RemoveEntry(actionName);
            return true;
        }

        public static bool TryRunRoleAction<T>(T subroutine, ActionName actionName, bool isClick) where T : SubroutineBase
        {
            if (subroutine == null)
                return false;

            if (!subroutine.Role.IsEmulatedDummy)
                return false;

            subroutine.DummyEmulator.AddEntry(actionName, isClick);
            return true;
        }

        public static bool TryStopRoleAction<T>(T subroutine, ActionName actionName) where T : SubroutineBase
        {
            if (subroutine == null)
                return false;
                
            if (!subroutine.Role.IsEmulatedDummy)
                return false;

            subroutine.DummyEmulator.RemoveEntry(actionName);
            return true;
        }
    }
}