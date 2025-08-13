
using GameCore;
using HarmonyLib;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Firearms.Modules.Misc;
using Mirror;
using RelativePositioning;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UncomplicatedCustomBots.API.Managers;
using UnityEngine;

namespace UncomplicatedCustomBots.Harmony.Patches
{
    [HarmonyPatch]
    public class DummyRevolverTranspilerPatch
    {
        [HarmonyPatch(typeof(DoubleActionModule), nameof(DoubleActionModule.FireLive))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FireLiveTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            ConstructorInfo shotBacktrackDataNetworkReaderCtor = typeof(ShotBacktrackData).GetConstructor(new[] { typeof(NetworkReader) });
            ConstructorInfo shotBacktrackDataFirearmCtor = typeof(ShotBacktrackData).GetConstructor(new[] { typeof(Firearm) });

            if (shotBacktrackDataNetworkReaderCtor == null || shotBacktrackDataFirearmCtor == null)
            {
                LogManager.Error("Could not find ShotBacktrackData constructors for transpiler patch");
                return codes;
            }

            Label skipNullCheckLabel = generator.DefineLabel();
            Label continueLabel = generator.DefineLabel();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj && codes[i].operand is ConstructorInfo ctor && ctor == shotBacktrackDataNetworkReaderCtor)
                {
                    LogManager.Debug("Found ShotBacktrackData(NetworkReader) constructor call, injecting null check");

                    List<CodeInstruction> injectedInstructions =
                    [
                        // Duplicate the NetworkReader on the stack so we can check it
                        new CodeInstruction(OpCodes.Dup),

                        // Check if its null
                        new CodeInstruction(OpCodes.Brtrue_S, skipNullCheckLabel),

                        // If null: pop the null NetworkReader from stack
                        new CodeInstruction(OpCodes.Pop),

                        // Load 'this' (the DoubleActionModule instance) onto stack  
                        new CodeInstruction(OpCodes.Ldarg_0),

                        // Get the Firearm property
                        new CodeInstruction(OpCodes.Call, typeof(FirearmSubcomponentBase).GetMethod("get_Firearm")),

                        // Call ShotBacktrackData constructor with Firearm instead
                        new CodeInstruction(OpCodes.Newobj, shotBacktrackDataFirearmCtor),

                        // Jump to continue normal execution
                        new CodeInstruction(OpCodes.Br_S, continueLabel),

                        // Label for when NetworkReader is not null - continue with original constructor
                        new CodeInstruction(OpCodes.Nop) { labels = [skipNullCheckLabel] }
                    ];

                    codes.InsertRange(i, injectedInstructions);

                    if (i + injectedInstructions.Count + 1 < codes.Count)
                        codes[i + injectedInstructions.Count + 1].labels.Add(continueLabel);

                    LogManager.Debug("Successfully injected null check for ShotBacktrackData constructor");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ShotBacktrackData), MethodType.Constructor, new Type[] { typeof(NetworkReader) })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ShotBacktrackDataConstructorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            Label skipLabel = generator.DefineLabel();

            List<CodeInstruction> nullCheckInstructions =
            [
                // Load the NetworkReader parameter (arg.1, since arg.0 is 'this')
                new CodeInstruction(OpCodes.Ldarg_1),
            
                // Check if it's not null
                new CodeInstruction(OpCodes.Brtrue_S, skipLabel),
            
                // If null, just return (this will create a default-initialized ShotBacktrackData)
                new CodeInstruction(OpCodes.Ret),
            
                // Label for normal execution when NetworkReader is not null
                new CodeInstruction(OpCodes.Nop) { labels = [skipLabel] }
            ];

            codes.InsertRange(0, nullCheckInstructions);

            LogManager.Debug("Added null check to ShotBacktrackData constructor");
            return codes;
        }

        // Additional safety net for RelativePosition constructor  
        [HarmonyPatch(typeof(RelativePosition), MethodType.Constructor, new Type[] { typeof(NetworkReader) })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RelativePositionConstructorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            Label skipLabel = generator.DefineLabel();

            List<CodeInstruction> nullCheckInstructions =
            [
                // Load the NetworkReader parameter
                new CodeInstruction(OpCodes.Ldarg_1),
            
                // Check if it's not null
                new CodeInstruction(OpCodes.Brtrue_S, skipLabel),
            
                // If null, return early (creates default RelativePosition)
                new CodeInstruction(OpCodes.Ret),
            
                // Label for normal execution
                new CodeInstruction(OpCodes.Nop) { labels = [skipLabel] }
            ];

            codes.InsertRange(0, nullCheckInstructions);

            LogManager.Debug("Added null check to RelativePosition constructor");
            return codes;
        }
    }
}