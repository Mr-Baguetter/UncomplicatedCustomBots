using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using PlayerRoles.FirstPersonControl;
using UncomplicatedCustomBots.Events.Handlers;

namespace UncomplicatedCustomBots.Harmony.Patches.Events
{
    [HarmonyPatch(typeof(FirstPersonMovementModule), nameof(FirstPersonMovementModule.UpdateMovement))]
    public static class PlayerMovedTranspilerPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = new(instructions);

            int onPlayerMoveIndex = -1;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo mi && mi.Name == "OnPlayerMove")
                {
                    onPlayerMoveIndex = i;
                    break;
                }
            }

            if (onPlayerMoveIndex == -1)
                return codes;

            Label skipLabel = generator.DefineLabel();

            MethodInfo getMotor = typeof(FirstPersonMovementModule).GetProperty("Motor")!.GetGetMethod() ?? throw new InvalidOperationException("FirstPersonMovementModule.Motor getter not found");
            MethodInfo getMovementDetected = typeof(FpcMotor).GetProperty("MovementDetected")!.GetGetMethod() ?? throw new InvalidOperationException("FpcMotor.MovementDetected getter not found");
            MethodInfo onPlayerMoved = typeof(PlayerMoved).GetMethod("OnPlayerMoved", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException("OnPlayerMoved not found");

            List<CodeInstruction> movedEventInstructions =
            [
                // Load the module instance
                new CodeInstruction(OpCodes.Ldarg_0),

                // Get the module's motor
                new CodeInstruction(OpCodes.Callvirt, getMotor),

                // Get whether the player actually moved this frame
                new CodeInstruction(OpCodes.Callvirt, getMovementDetected),

                // Skip the event if the player did not move
                new CodeInstruction(OpCodes.Brfalse_S, skipLabel),

                // Load the module instance again
                new CodeInstruction(OpCodes.Ldarg_0),

                // Raise the event
                new CodeInstruction(OpCodes.Call, onPlayerMoved),

                // Continue normal execution
                new CodeInstruction(OpCodes.Nop) { labels = [skipLabel] }
            ];

            codes.InsertRange(onPlayerMoveIndex + 1, movedEventInstructions);

            return codes;
        }
    }
}