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
using System.Linq;
// Containers and consumption related components
using ProjectM.Shared;

namespace PvPKit
{
    // Reference classes for component matching only, not actual component declarations
    public class PotionItemDataRef 
    {
        public PrefabGUID EmptyContainerPrefabGUID;
    }

    public class SpawnEmptyContainerOnConsumeRef
    {
        public PrefabGUID EmptyContainerPrefab;
    }

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInDependency("gg.deca.Bloodstone")]
    public class Plugin : BasePlugin
    {
        public static ManualLogSource? Logger;
        public static Plugin? Instance;
        private static bool _hasPrefabsBeenModified = false;
        
        // Potions that need to be infinite
        private static readonly HashSet<int> potionItems = new HashSet<int>
        {
            -1568756102, // Physical Power Potion
            1510182325,  // Spell Power Potion
            -2102469163, // Vampiric Brew
            800879747,   // Blood Rose Brew
            -1885959251  // Vermin Salve
        };

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
            
            // Schedule delayed attempts to modify prefabs - try every 10 seconds instead of 5
            System.Threading.Tasks.Task.Run(async () => {
                for (int i = 0; i < 10; i++) {
                    await System.Threading.Tasks.Task.Delay(10000); // Wait longer between attempts
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

                Logger?.LogInfo("Starting minimal prefab modifications for infinite consumables...");
                bool anyModified = false;
                
                // ONLY modify ItemData.RemoveOnConsume - nothing else
                foreach (var potionHash in potionItems)
                {
                    try
                    {
                        var prefabGUID = new PrefabGUID(potionHash);
                        Logger?.LogInfo($"Looking for potion with hash {potionHash}");
                        
                        var prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                        if (prefabCollectionSystem == null)
                        {
                            Logger?.LogError("PrefabCollectionSystem not found!");
                            continue;
                        }
                        
                        if (!prefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(prefabGUID))
                        {
                            Logger?.LogWarning($"Could not find entity for potion {potionHash} in prefab map");
                            continue;
                        }
                        
                        var prefabEntity = prefabCollectionSystem._PrefabGuidToEntityMap[prefabGUID];
                        
                        if (prefabEntity.Equals(Entity.Null))
                        {
                            Logger?.LogWarning($"Entity is null for potion {potionHash}");
                            continue;
                        }
                        
                        if (!world.EntityManager.HasComponent<ItemData>(prefabEntity))
                        {
                            Logger?.LogWarning($"Entity does not have ItemData component for potion {potionHash}");
                            continue;
                        }
                        
                        var itemData = world.EntityManager.GetComponentData<ItemData>(prefabEntity);
                        if (itemData.RemoveOnConsume)
                        {
                            Logger?.LogInfo($"Making potion {potionHash} infinite (setting RemoveOnConsume=false)");
                            itemData.RemoveOnConsume = false;
                            world.EntityManager.SetComponentData(prefabEntity, itemData);
                            anyModified = true;
                        }
                        else
                        {
                            Logger?.LogInfo($"Potion {potionHash} is already infinite (RemoveOnConsume=false)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError($"Error processing potion {potionHash}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                
                if (anyModified)
                {
                    Logger?.LogInfo("Successfully modified prefabs to have infinite consumables!");
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

