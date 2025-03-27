using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PvPKit.Configs;
using PvPKit.Database;
using VampireCommandFramework;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM.Gameplay.Systems;
using ProjectM.Gameplay;
using Stunlock.Core;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using System;

namespace PvPKit
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInDependency("gg.deca.Bloodstone")]
    public class Plugin : BasePlugin
    {
        public static ManualLogSource? Logger;
        public static Plugin? Instance;
        private static bool _hasPrefabsBeenModified = false;

        public override void Load()
        {
            Instance = this;
            Logger = Log;
            
            // Initialize database and config
            MainConfig.Initialize();
            DB.LoadData();
            
            // Register commands
            CommandRegistry.RegisterAll();
            
            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");
            
            // Try directly modifying prefabs on startup
            try {
                ModifyPrefabs();
            } catch (Exception ex) {
                Logger?.LogInfo($"Initial prefab modification failed: {ex.Message}");
            }
            
            // Schedule delayed attempts to modify prefabs
            System.Threading.Tasks.Task.Run(async () => {
                for (int i = 0; i < 10; i++) {
                    await System.Threading.Tasks.Task.Delay(5000);
                    try {
                        Logger?.LogInfo($"Attempt #{i+1} to modify prefabs");
                        ModifyPrefabs();
                        if (_hasPrefabsBeenModified) {
                            Logger?.LogInfo("Prefabs successfully modified!");
                            break;
                        }
                    } catch (Exception ex) {
                        Logger?.LogInfo($"Attempt #{i+1} failed: {ex.Message}");
                    }
                }
            });
        }

        public override bool Unload()
        {
            CommandRegistry.UnregisterAssembly();
            return true;
        }

        private static void ModifyPrefabs()
        {
            try
            {
                var world = GetWorld("Server");
                if (world == null)
                {
                    Logger?.LogError("Cannot modify prefabs - Server world is null!");
                    return;
                }

                Logger?.LogInfo("Starting prefab modifications for infinite items...");
                bool anyModified = false;

                // Define items to make infinite
                int[] itemsToMakeInfinite = new int[]
                {
                    -1568756102, // Physical Power Potion
                    1510182325,  // Spell Power Potion
                    -2102469163, // Vampiric Brew
                    800879747,   // Blood Rose Brew
                    -1885959251  // Vermin Salve
                };

                // Process each item
                Logger?.LogInfo("Modifying consumable items to be infinite...");
                foreach (var itemHash in itemsToMakeInfinite)
                {
                    var prefabGUID = new PrefabGUID(itemHash);
                    var prefabEntity = GetPrefabEntityByPrefabGUID(world, prefabGUID);
                    if (!prefabEntity.Equals(Entity.Null))
                    {
                        if (world.EntityManager.HasComponent<ItemData>(prefabEntity))
                        {
                            var itemData = world.EntityManager.GetComponentData<ItemData>(prefabEntity);
                            if (itemData.RemoveOnConsume)
                            {
                                Logger?.LogInfo($"Making item {itemHash} infinite");
                                itemData.RemoveOnConsume = false;
                                world.EntityManager.SetComponentData(prefabEntity, itemData);
                                anyModified = true;
                            }
                            else
                            {
                                Logger?.LogInfo($"Item {itemHash} is already infinite");
                            }
                        }
                        else
                        {
                            Logger?.LogWarning($"Item {itemHash} doesn't have ItemData component");
                        }
                    }
                    else
                    {
                        Logger?.LogWarning($"Couldn't find entity for item hash {itemHash}");
                    }
                }

                // Clear the drop table data to prevent empty containers
                Logger?.LogInfo("Clearing drop tables for empty containers...");
                int[] dropTableDataToClear = new int[]
                {
                    -437611596, // Empty Glass Bottle
                    -810738866  // Empty Waterskin
                };

                foreach (var dropTableHash in dropTableDataToClear)
                {
                    var prefabGUID = new PrefabGUID(dropTableHash);
                    var prefabEntity = GetPrefabEntityByPrefabGUID(world, prefabGUID);
                    if (!prefabEntity.Equals(Entity.Null))
                    {
                        if (world.EntityManager.HasComponent<DropTableDataBuffer>(prefabEntity))
                        {
                            var buffer = world.EntityManager.GetBuffer<DropTableDataBuffer>(prefabEntity);
                            if (buffer.Length > 0)
                            {
                                Logger?.LogInfo($"Clearing drop table for {dropTableHash}");
                                buffer.Clear();
                                anyModified = true;
                            }
                            else
                            {
                                Logger?.LogInfo($"Drop table for {dropTableHash} is already empty");
                            }
                        }
                        else
                        {
                            Logger?.LogWarning($"Item {dropTableHash} doesn't have DropTableDataBuffer component");
                        }
                    }
                    else
                    {
                        Logger?.LogWarning($"Couldn't find entity for drop table hash {dropTableHash}");
                    }
                }

                // Remove consume components from abilities
                Logger?.LogInfo("Removing consume components from abilities...");
                int[] abilityHashes = new int[]
                {
                    -1885959251, // AB_Consumable_Salve_Vermin_AbilityGroup
                    -1661839227, // AB_Consumable_HealingPotion_T01_AbilityGroup
                    -2102469163, // AB_Consumable_SpellLeechPotion_T01_AbilityGroup
                    -1568756102, // AB_Consumable_PhysicalPowerPotion_T02_AbilityGroup
                    1510182325   // AB_Consumable_SpellPowerPotion_T02_AbilityGroup
                };

                int modifiedCount = 0;
                var prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabCollectionSystem != null)
                {
                    var prefabMap = prefabCollectionSystem._PrefabGuidToEntityMap;
                    Logger?.LogInfo("Checking prefab collection system");
                    
                    // Process abilities
                    foreach (var abilityHash in abilityHashes)
                    {
                        var prefabGUID = new PrefabGUID(abilityHash);
                        if (prefabMap.TryGetValue(prefabGUID, out var entity))
                        {
                            Logger?.LogInfo($"Found ability with hash: {abilityHash}");
                            
                            if (world.EntityManager.HasComponent<AbilityGroupConsumeItemOnCast>(entity))
                            {
                                Logger?.LogInfo($"Removing AbilityGroupConsumeItemOnCast from {abilityHash}");
                                world.EntityManager.RemoveComponent<AbilityGroupConsumeItemOnCast>(entity);
                                modifiedCount++;
                                anyModified = true;
                            }
                            else
                            {
                                Logger?.LogInfo($"Ability {abilityHash} already has no consume component");
                            }
                        }
                        else
                        {
                            Logger?.LogWarning($"Ability hash {abilityHash} not found in prefab map");
                        }
                    }
                }
                else
                {
                    Logger?.LogWarning("PrefabCollectionSystem is not initialized yet");
                }
                
                Logger?.LogInfo($"Modified {modifiedCount} abilities");
                
                if (anyModified)
                {
                    Logger?.LogInfo("Successfully modified prefabs!");
                    _hasPrefabsBeenModified = true;
                }
                else
                {
                    Logger?.LogInfo("No prefabs needed modification or prefabs weren't ready yet");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error modifying prefabs: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static Entity GetPrefabEntityByPrefabGUID(World world, PrefabGUID prefabGUID)
        {
            if (world == null) return Entity.Null;
            
            var prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (prefabCollectionSystem != null && 
                prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(prefabGUID, out var entity))
            {
                return entity;
            }
            return Entity.Null;
        }

        private static World? GetWorld(string name)
        {
            foreach (var world in World.All)
            {
                if (world.Name == name)
                {
                    return world;
                }
            }
            return null;
        }
    }
}
