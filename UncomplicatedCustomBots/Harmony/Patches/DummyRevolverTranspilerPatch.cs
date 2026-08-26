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

// Fixes dummies being unable to fire revolvers.
namespace UncomplicatedCustomBots.Harmony.Patches
{
    [HarmonyPatch]
    public class DummyRevolverTranspilerPatch
    {
        [HarmonyPatch(typeof(DoubleActionModule), nameof(DoubleActionModule.FireLive))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FireLiveTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = new(instructions);

            ConstructorInfo shotBacktrackDataNetworkReaderCtor = typeof(ShotBacktrackData).GetConstructor([typeof(NetworkReader)]);
            ConstructorInfo shotBacktrackDataFirearmCtor = typeof(ShotBacktrackData).GetConstructor([typeof(Firearm)]);
            
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
                        
                        // Check if it's null
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
                        
                        // Label for when NetworkReader is not null continue with original constructor
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
        
        [HarmonyPatch(typeof(ShotBacktrackData), MethodType.Constructor, [typeof(NetworkReader)])]
        [HarmonyTranspiler] 
        public static IEnumerable<CodeInstruction> ShotBacktrackDataConstructorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = new(instructions);
            
            Label skipLabel = generator.DefineLabel();
            
            List<CodeInstruction> nullCheckInstructions =
            [
                // Load the NetworkReader parameter
                new CodeInstruction(OpCodes.Ldarg_1),
                
                // Check if it's not null
                new CodeInstruction(OpCodes.Brtrue_S, skipLabel),
                
                // If null return (this will create a default initialized ShotBacktrackData)
                new CodeInstruction(OpCodes.Ret),
                
                // Label for normal execution when NetworkReader is not null
                new CodeInstruction(OpCodes.Nop) { labels = [skipLabel] }
            ];

            codes.InsertRange(0, nullCheckInstructions);
            
            LogManager.Debug("Added null check to ShotBacktrackData constructor");
            return codes;
        }

        [HarmonyPatch(typeof(RelativePosition), MethodType.Constructor, [typeof(NetworkReader)])]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RelativePositionConstructorTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = new(instructions);

            Label skipLabel = generator.DefineLabel();

            List<CodeInstruction> nullCheckInstructions =
            [
                // Load the NetworkReader parameter
                new CodeInstruction(OpCodes.Ldarg_1),
                
                // Check if it's not null
                new CodeInstruction(OpCodes.Brtrue_S, skipLabel),
                
                // If null return (creates default RelativePosition)
                new CodeInstruction(OpCodes.Ret),
                
                // Label for normal execution
                new CodeInstruction(OpCodes.Nop) { labels = [skipLabel] }
            ];

            codes.InsertRange(0, nullCheckInstructions);

            LogManager.Debug("Added null check to RelativePosition constructor");
            return codes;
        }

        [HarmonyPatch(typeof(DoubleActionModule), "Fire")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FireTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = new(instructions);

            FieldInfo triggerPullField = AccessTools.Field(typeof(DoubleActionModule), "_triggerPull");
            FieldInfo serverCooldownField = AccessTools.Field(typeof(DoubleActionModule), "_serverShotCooldown");
            Type triggerPullType = AccessTools.Inner(typeof(DoubleActionModule), "TriggerPull");
            MethodInfo resetMethod = AccessTools.Method(triggerPullType, "Reset");
            MethodInfo triggerMethod = AccessTools.Method(typeof(FullAutoRateLimiter), "Trigger");

            if (triggerPullField == null || resetMethod == null || triggerMethod == null || serverCooldownField == null)
            {
                LogManager.Error("Could not find fields/methods for DoubleActionModule.Fire fix — skipping patch");
                return codes;
            }

            bool patched = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Callvirt || codes[i].operand is not MethodInfo mi || mi != triggerMethod)
                    continue;

                bool isServerTrigger = false;
                for (int j = Math.Max(0, i - 5); j < i; j++)
                {
                    if (codes[j].opcode == OpCodes.Ldfld && codes[j].operand is FieldInfo fi && fi == serverCooldownField)
                    {
                        isServerTrigger = true;
                        break;
                    }
                }

                if (!isServerTrigger)
                    continue;

                List<CodeInstruction> injected =
                [
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, triggerPullField),
                    new CodeInstruction(OpCodes.Callvirt, resetMethod),
                ];

                codes.InsertRange(i + 1, injected);
                LogManager.Debug("Injected _triggerPull.Reset() after _serverShotCooldown.Trigger in DoubleActionModule.Fire");
                patched = true;
                break;
            }

            if (!patched)
                LogManager.Error("Failed to locate _serverShotCooldown.Trigger in DoubleActionModule.Fire — revolver click fix not applied");

            return codes;
        }
    }
}