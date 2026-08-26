using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LabApi.Features.Wrappers;
using UncomplicatedCustomBots.Events.Handlers;

namespace UncomplicatedCustomBots.Harmony.Patches.Events
{
    [HarmonyPatch(typeof(NetworkManagerUtils.Dummies.DummyUtils), nameof(NetworkManagerUtils.Dummies.DummyUtils.SpawnDummy))]
    public static class DummyEventTranspilerPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = new(instructions);

            int spawnEventIndex = -1;
            int spawnedEventIndex = -1;

            for (int i = 0; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo mi && mi.Name == "set_MyNick")
                {
                    spawnEventIndex = i + 1;
                    break;
                }
            }

            for (int i = codes.Count - 1; i >= 0; i--)
            {
                if (codes[i].opcode == OpCodes.Ret && i > 0)
                {
                    spawnedEventIndex = i;
                    break;
                }
            }

            if (spawnEventIndex == -1 || spawnedEventIndex == -1)
                return codes;

            Label continueLabel = generator.DefineLabel();

            ConstructorInfo spawningCtor = typeof(DummySpawningEventArgs).GetConstructor([typeof(ReferenceHub), typeof(bool)]) ?? throw new InvalidOperationException("DummySpawningEventArgs ctor not found");
            MethodInfo onSpawning = typeof(Dummy).GetMethod("OnDummySpawning", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException("OnDummySpawning not found");
            ConstructorInfo spawnedCtor = typeof(DummySpawnedEventArgs).GetConstructor([typeof(ReferenceHub)]) ?? throw new InvalidOperationException("DummySpawnedEventArgs ctor not found");
            MethodInfo onSpawned = typeof(Dummy).GetMethod("OnDummySpawned", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException("OnDummySpawned not found");

            List<CodeInstruction> spawningEventInstructions =
            [
                // Load ReferenceHub from local.1
                new CodeInstruction(OpCodes.Ldloc_1),

                // true (isAllowed)
                new CodeInstruction(OpCodes.Ldc_I4_1),

                // new DummySpawningEventArgs(ReferenceHub, true)
                new CodeInstruction(OpCodes.Newobj, spawningCtor),

                // duplicate for event call
                new CodeInstruction(OpCodes.Dup),

                // call Dummy.OnDummySpawning(...)
                new CodeInstruction(OpCodes.Call, onSpawning),

                // get IsAllowed
                new CodeInstruction(OpCodes.Callvirt, typeof(DummySpawningEventArgs).GetProperty("IsAllowed")!.GetGetMethod()),

                // branch if true
                new CodeInstruction(OpCodes.Brtrue, continueLabel),

                // else return null
                new CodeInstruction(OpCodes.Ldnull),
                new CodeInstruction(OpCodes.Ret),

                // continue label
                new CodeInstruction(OpCodes.Nop) { labels = { continueLabel } }
            ];
            
            List<CodeInstruction> spawnedEventInstructions =
            [
                // Load ReferenceHub from local.1
                new CodeInstruction(OpCodes.Ldloc_1),

                // new DummySpawnedEventArgs(ReferenceHub)
                new CodeInstruction(OpCodes.Newobj, spawnedCtor),
                
                // call Dummy.OnDummySpawned(...)
                new CodeInstruction(OpCodes.Call, onSpawned)
            ];

            codes.InsertRange(spawnEventIndex, spawningEventInstructions);
            spawnedEventIndex += spawningEventInstructions.Count;
            codes.InsertRange(spawnedEventIndex, spawnedEventInstructions);

            return codes;
        }
    }
}