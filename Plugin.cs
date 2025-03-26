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
        private Harmony? _harmony;
        public static ManualLogSource? Logger;
        public static Plugin? Instance;
        private static bool _hasPrefabsBeenModified = false;

        public override void Load()
        {
            Instance = this;
            Logger = Log;
            
            // Patch everything
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            // Initialize database and config
            MainConfig.Initialize();
            DB.LoadData();
            
            // Register commands
            CommandRegistry.RegisterAll();
            
            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");
            
            // Try to directly modify prefabs on load
            // This is a backup approach in case the world system doesn't work
            Log.LogInfo("⭐ Trying to modify prefabs immediately from Plugin.Load ⭐");
            
            try 
            {
                ModifyPrefabs();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to modify prefabs during load: {ex.Message}");
                Log.LogInfo("Will try again using the system approach...");
            }
            
            // Add a system for server startup to modify prefabs properly
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                // Add a system for server startup
                Log.LogInfo("Creating DirectModificationSystem for world: " + world.Name);
                world.CreateSystem<DirectModificationSystem>();
                
                // Add our system to intercept empty container creation
                Log.LogInfo("Creating EmptyContainerInterceptSystem");
                world.CreateSystem<EmptyContainerInterceptSystem>();
            }
            else
            {
                Log.LogWarning("DefaultGameObjectInjectionWorld is null at load time!");
            }
        }

        public override bool Unload()
        {
            CommandRegistry.UnregisterAssembly();
            _harmony?.UnpatchSelf();
            return true;
        }

        public static void ModifyPrefabs()
        {
            var world = GetWorld("Server");
            if (world == null)
            {
                Logger?.LogError("Cannot modify prefabs - Server world is null!");
                return;
            }

            Logger?.LogInfo("Starting prefab modifications for infinite items...");
            
            int modifiedItemsCount = 0;
            int failedItemsCount = 0;

            // Define items to make infinite
            List<(PrefabGUID guid, string name)> itemsToMakeInfinite = new List<(PrefabGUID, string)>
            {     
                (new PrefabGUID(-1461326411), "Siege Golem Stone"),        // Item_Building_Siege_Golem_T02
                (new PrefabGUID(-1568756102), "Physical Power Potion"),    // Item_Consumable_PhysicalPowerPotion_T02
                (new PrefabGUID(1510182325), "Spell Power Potion"),        // Item_Consumable_SpellPowerPotion_T02
                (new PrefabGUID(-2102469163), "Vampiric Brew"),           // Item_Consumable_SpellLeechPotion_T01
                (new PrefabGUID(800879747), "Blood Rose Brew"),           // Item_Consumable_HealingPotion_T01
                (new PrefabGUID(-1885959251), "Vermin Salve")             // Item_Consumable_Salve_Vermin
            };

            // Try to get the EntityManager and PrefabCollectionSystem
            var entityManager = world.EntityManager;
            var prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (prefabCollectionSystem == null)
            {
                Logger?.LogError("PrefabCollectionSystem is null! Cannot continue with prefab modifications.");
                return;
            }

            // Log all items in the itemsToMakeInfinite list
            Logger?.LogInfo("Items that will be made infinite:");
            foreach (var item in itemsToMakeInfinite)
            {
                Logger?.LogInfo($"- {item.name} (GUID: {item.guid})");
            }

            // Process each item
            foreach (var itemInfo in itemsToMakeInfinite)
            {
                try
                {
                    var itemGuid = itemInfo.guid;
                    var itemName = itemInfo.name;

                    // Try to get the prefab entity
                    if (prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(itemGuid, out var prefabEntity))
                    {
                        // First ensure the item doesn't get consumed
                        if (entityManager.HasComponent<ItemData>(prefabEntity))
                        {
                            var itemData = entityManager.GetComponentData<ItemData>(prefabEntity);
                            itemData.RemoveOnConsume = false;
                            entityManager.SetComponentData(prefabEntity, itemData);
                            Logger?.LogInfo($"✅ Made {itemName} infinite");
                            modifiedItemsCount++;
                        }

                        // Disable empty container generation
                        DisableEmptyContainer(entityManager, prefabEntity);
                    }
                    else
                    {
                        failedItemsCount++;
                        Logger?.LogError($"Could not find entity for {itemName} (GUID: {itemGuid})");
                    }
                }
                catch (Exception ex)
                {
                    failedItemsCount++;
                    Logger?.LogError($"Error processing {itemInfo.name} (GUID: {itemInfo.guid}): {ex.Message}");
                }
            }

            // Clear the drop table data to prevent empty containers
            Logger?.LogInfo("🍶 Clearing drop table data to prevent empty containers...");
            
            List<(PrefabGUID guid, string name)> dropTableDataToClear = new List<(PrefabGUID, string)>
            {
                (new PrefabGUID(-437611596), "DT_Shared_Consume_GlassPotion_01"),  // Empty Glass Bottle
                (new PrefabGUID(-810738866), "DT_Shared_Consume_WaterSkin_01")     // Empty Waterskin
            };
            
            foreach (var dropTableInfo in dropTableDataToClear)
            {
                try
                {
                    if (prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(dropTableInfo.guid, out var dropTableEntity))
                    {
                        // Try to clear the drop table data
                        if (entityManager.HasComponent<DropTableDataBuffer>(dropTableEntity))
                        {
                            var dropTableData = entityManager.GetBuffer<DropTableDataBuffer>(dropTableEntity);
                            dropTableData.Clear();
                            Logger?.LogInfo($"✅ Cleared drop table data for {dropTableInfo.name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Error clearing drop table data for {dropTableInfo.name}: {ex.Message}");
                }
            }

            // Remove consume on cast from abilities
            try
            {
                HashSet<PrefabGUID> consumableAbilitiesToModify = new HashSet<PrefabGUID>
                {
                    new PrefabGUID(-1568756102), // Physical Power Potion
                    new PrefabGUID(1510182325),  // Spell Power Potion
                    new PrefabGUID(-2102469163), // Vampiric Brew
                    new PrefabGUID(800879747),   // Blood Rose Brew
                    new PrefabGUID(-1885959251)  // Vermin Salve
                };
                
                var entities = GetPrefabEntitiesByComponentTypes<AbilityGroupConsumeItemOnCast>();
                int abilitiesModified = 0;
                
                foreach (var entity in entities)
                {
                    if (entityManager.HasComponent<PrefabGUID>(entity))
                    {
                        var prefabGuid = entityManager.GetComponentData<PrefabGUID>(entity);
                        
                        if (consumableAbilitiesToModify.Contains(prefabGuid))
                        {
                            Logger?.LogInfo($"Removing AbilityGroupConsumeItemOnCast from ability with GUID: {prefabGuid}");
                            
                            if (entityManager.HasComponent<AbilityGroupConsumeItemOnCast>(entity))
                            {
                                entityManager.RemoveComponent<AbilityGroupConsumeItemOnCast>(entity);
                                abilitiesModified++;
                                Logger?.LogInfo($"✅ Successfully removed AbilityGroupConsumeItemOnCast!");
                            }
                        }
                    }
                }
                
                if (entities.IsCreated)
                {
                    entities.Dispose();
                }
                
                Logger?.LogInfo($"Modified {abilitiesModified} abilities to not consume items");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error modifying abilities: {ex.Message}");
            }

            // Handle success or failure
            if (modifiedItemsCount > 0)
            {
                Logger?.LogInfo($"✅ Successfully modified {modifiedItemsCount} items to be infinite");
            }
            if (failedItemsCount > 0)
            {
                Logger?.LogError($"❌ Failed to modify {failedItemsCount} items");
            }
        }

        private static Entity GetPrefabEntityByPrefabGUID(PrefabGUID prefabGUID)
        {
            var world = GetWorld("Server");
            if (world == null) return Entity.Null;
            
            var prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(prefabGUID, out var entity))
            {
                return entity;
            }
            return Entity.Null;
        }

        public static NativeArray<Entity> GetPrefabEntitiesByComponentTypes<T1>()
        {
            var world = GetWorld("Server");
            if (world == null) return new NativeArray<Entity>(0, Allocator.Temp);
            
            EntityQueryOptions options = EntityQueryOptions.IncludePrefab;

            EntityQueryDesc queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    new ComponentType(Il2CppType.Of<Prefab>(), ComponentType.AccessMode.ReadWrite),
                    new ComponentType(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite)
                },
                Options = options
            };

            var query = world.EntityManager.CreateEntityQuery(queryDesc);
            var entities = query.ToEntityArray(Allocator.Temp);
            query.Dispose();
            return entities;
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

        // // Uncomment for example commmand or delete

        // /// <summary> 
        // /// Example VCF command that demonstrated default values and primitive types
        // /// Visit https://github.com/decaprime/VampireCommandFramework for more info 
        // /// </summary>
        // /// <remarks>
        // /// How you could call this command from chat:
        // ///
        // /// .startingkit-example "some quoted string" 1 1.5
        // /// .startingkit-example boop 21232
        // /// .startingkit-example boop-boop
        // ///</remarks>
        // [Command("startingkit-example", description: "Example command from startingkit", adminOnly: true)]
        // public void ExampleCommand(ICommandContext ctx, string someString, int num = 5, float num2 = 1.5f)
        // { 
        //     ctx.Reply($"You passed in {someString} and {num} and {num2}");
        // }

        // Add a new method to force modify items after the player gets them
        public static void ForceItemsInfinite(List<PrefabGUID> itemGuids)
        {
            try
            {
                var world = GetWorld("Server");
                if (world == null)
                {
                    Logger?.LogError("Cannot make items infinite - Server world is null!");
                    return;
                }
                
                var entityManager = world.EntityManager;
                var prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabCollectionSystem == null)
                {
                    Logger?.LogError("PrefabCollectionSystem is null in ForceItemsInfinite!");
                    return;
                }
                
                Logger?.LogInfo($"ForceItemsInfinite: Trying to make {itemGuids.Count} items infinite at runtime");
                
                foreach (var itemGuid in itemGuids)
                {
                    try
                    {
                        // Find all entities with this prefab GUID in the player inventory
                        var query = world.EntityManager.CreateEntityQuery(
                            ComponentType.ReadWrite<ItemData>(),
                            ComponentType.ReadOnly<PrefabGUID>());
                        
                        var entities = query.ToEntityArray(Allocator.Temp);
                        
                        foreach (var entity in entities)
                        {
                            if (entityManager.HasComponent<PrefabGUID>(entity))
                            {
                                var guid = entityManager.GetComponentData<PrefabGUID>(entity);
                                if (guid.GuidHash == itemGuid.GuidHash)
                                {
                                    // Found matching item, make it infinite
                                    if (entityManager.HasComponent<ItemData>(entity))
                                    {
                                        var itemData = entityManager.GetComponentData<ItemData>(entity);
                                        itemData.RemoveOnConsume = false;
                                        entityManager.SetComponentData<ItemData>(entity, itemData);
                                        Logger?.LogInfo($"📝 Made individual item entity infinite for GUID: {itemGuid}");
                                    }
                                    
                                    // Also try to disable the empty container if the item has one
                                    DisableEmptyContainer(entityManager, entity);
                                }
                            }
                        }
                        
                        if (entities.IsCreated)
                        {
                            entities.Dispose();
                        }
                        
                        // Also try to modify the prefab to affect future items
                        if (prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(itemGuid, out var prefabEntity))
                        {
                            if (entityManager.HasComponent<ItemData>(prefabEntity))
                            {
                                var itemData = entityManager.GetComponentData<ItemData>(prefabEntity);
                                itemData.RemoveOnConsume = false;
                                entityManager.SetComponentData<ItemData>(prefabEntity, itemData);
                                Logger?.LogInfo($"✏️ Modified prefab for item GUID: {itemGuid}");
                            }
                            
                            // Also try to disable the empty container in the prefab
                            DisableEmptyContainer(entityManager, prefabEntity);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError($"Error making item {itemGuid} infinite: {ex.Message}");
                    }
                }
                
                // Also clear the drop tables directly to prevent empty containers
                List<(PrefabGUID guid, string name)> dropTableDataToClear = new List<(PrefabGUID, string)>
                {
                    (new PrefabGUID(-437611596), "DT_Shared_Consume_GlassPotion_01"),  // Empty Glass Bottle
                    (new PrefabGUID(-810738866), "DT_Shared_Consume_WaterSkin_01")     // Empty Waterskin
                };
                
                foreach (var dropTableInfo in dropTableDataToClear)
                {
                    try 
                    {
                        var guid = dropTableInfo.guid;
                        var name = dropTableInfo.name;
                        
                        Logger?.LogInfo($"Runtime - Looking for drop table data: {name} (GUID: {guid})");
                        
                        bool foundEntity = prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(guid, out var dropTableEntity);
                        if (foundEntity && dropTableEntity != Entity.Null)
                        {
                            Logger?.LogInfo($"Runtime - Found drop table entity: {name}");
                            
                            // Check if it has DropTableDataBuffer component
                            if (entityManager.HasComponent<DropTableDataBuffer>(dropTableEntity))
                            {
                                // Get and clear the buffer
                                var buffer = entityManager.GetBuffer<DropTableDataBuffer>(dropTableEntity);
                                Logger?.LogInfo($"Runtime - Found drop table buffer with {buffer.Length} entries");
                                buffer.Clear();
                                Logger?.LogInfo($"✅ Runtime - Successfully cleared drop table buffer for {name}!");
                            }
                            else
                            {
                                Logger?.LogWarning($"Runtime - Entity {name} doesn't have DropTableDataBuffer component");
                            }
                        }
                        else
                        {
                            Logger?.LogWarning($"Runtime - Could not find drop table entity for {name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError($"Runtime - Error clearing drop table data: {ex.Message}");
                    }
                }
                
                // And try to remove the consume on cast
                try
                {
                    HashSet<PrefabGUID> consumableAbilitiesToModify = new HashSet<PrefabGUID>
                    {
                        new PrefabGUID(-1568756102), // Physical Power Potion
                        new PrefabGUID(1510182325),  // Spell Power Potion
                        new PrefabGUID(-2102469163), // Vampiric Brew
                        new PrefabGUID(800879747),   // Blood Rose Brew
                        new PrefabGUID(-1885959251)  // Vermin Salve
                    };
                    
                    var entities = GetPrefabEntitiesByComponentTypes<AbilityGroupConsumeItemOnCast>();
                    int abilitiesModified = 0;
                    
                    foreach (var entity in entities)
                    {
                        if (entityManager.HasComponent<PrefabGUID>(entity))
                        {
                            var prefabGuid = entityManager.GetComponentData<PrefabGUID>(entity);
                            
                            if (consumableAbilitiesToModify.Contains(prefabGuid))
                            {
                                Logger?.LogInfo($"Runtime - Removing AbilityGroupConsumeItemOnCast from ability with GUID: {prefabGuid}");
                                
                                if (entityManager.HasComponent<AbilityGroupConsumeItemOnCast>(entity))
                                {
                                    entityManager.RemoveComponent<AbilityGroupConsumeItemOnCast>(entity);
                                    abilitiesModified++;
                                    Logger?.LogInfo($"✅ Runtime - Successfully removed AbilityGroupConsumeItemOnCast!");
                                }
                            }
                        }
                    }
                    
                    if (entities.IsCreated)
                    {
                        entities.Dispose();
                    }
                    
                    Logger?.LogInfo($"Runtime - Modified {abilitiesModified} abilities to not consume items");
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Runtime - Error modifying abilities: {ex.Message}");
                }
                
                Logger?.LogInfo("Finished ForceItemsInfinite processing");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error in ForceItemsInfinite: {ex.Message}");
            }
        }
        
        // Helper method to disable the empty container component on an item
        public static void DisableEmptyContainer(EntityManager entityManager, Entity entity)
        {
            try
            {
                // First ensure the item doesn't get consumed 
                if (entityManager.HasComponent<ItemData>(entity))
                {
                    var itemData = entityManager.GetComponentData<ItemData>(entity);
                    itemData.RemoveOnConsume = false;
                    entityManager.SetComponentData(entity, itemData);
                    Logger?.LogInfo($"Made item entity infinite: {entity}");
                }
                
                // Look for EmptyContainerAfterUse components to modify
                try
                {
                    var componentTypes = entityManager.GetComponentTypes(entity);
                    
                    foreach (var componentType in componentTypes)
                    {
                        string typeName = componentType.ToString();
                        
                        // Look for the EmptyContainerAfterUse component
                        if (typeName.Contains("Empty") && typeName.Contains("Container"))
                        {
                            Logger?.LogInfo($"Found EmptyContainerAfterUse component on entity {entity}");
                            
                            // Try to remove the component completely
                            try
                            {
                                // Try to remove the component, which is most effective
                                entityManager.RemoveComponent(entity, componentType);
                                Logger?.LogInfo($"✅ Successfully removed EmptyContainerAfterUse component from entity {entity}");
                            }
                            catch (Exception ex)
                            {
                                Logger?.LogWarning($"Couldn't remove component: {ex.Message}. Will try clearing drop table data instead.");
                            }
                        }
                        
                        // Also look for DropTableData component - this is what creates the empty containers
                        if (typeName.Contains("DropTableData") || typeName.Contains("DropTable"))
                        {
                            Logger?.LogInfo($"Found DropTableData component on entity {entity}");
                            
                            try
                            {
                                // Try to clear the drop table data
                                if (typeName.Contains("Buffer"))
                                {
                                    var buffer = entityManager.GetBuffer<DropTableDataBuffer>(entity);
                                    buffer.Clear();
                                    Logger?.LogInfo($"✅ Cleared DropTableDataBuffer on entity {entity}");
                                }
                                else
                                {
                                    // Try to remove the component if it's not a buffer
                                    entityManager.RemoveComponent(entity, componentType);
                                    Logger?.LogInfo($"✅ Removed DropTableData component from entity {entity}");
                                }
                            }
                            catch (Exception innerEx)
                            {
                                Logger?.LogError($"Error modifying component data: {innerEx.Message}");
                            }
                        }
                    }
                    
                    componentTypes.Dispose();
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Error checking components: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error disabling empty container: {ex.Message}");
            }
        }

        private void OnGameInitialized(World world)
        {
            try
            {
                Logger?.LogInfo("Game initialized - Setting up EmptyContainerInterceptSystem");
                
                // Add our systems
                if (world.Name == "Server")
                {
                    if (world.GetExistingSystemManaged<EmptyContainerInterceptSystem>() == null)
                    {
                        Logger?.LogInfo("Creating EmptyContainerInterceptSystem for server world");
                        world.CreateSystem<EmptyContainerInterceptSystem>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error in OnGameInitialized: {ex.Message}");
            }
        }
    }

    internal class DirectModificationSystem : SystemBase
    {
        private bool _initialized = false;
        private float _timer = 0f;
        private int _attemptCount = 0;
        private const int MAX_ATTEMPTS = 5;
        private bool _loggedCreation = false;

        public override void OnCreate()
        {
            base.OnCreate();
            Plugin.Logger?.LogInfo("🟢 DirectModificationSystem created!");
            _loggedCreation = true;
        }

        public override void OnUpdate()
        {
            // Log on the first update call to confirm the system is running
            if (!_loggedCreation)
            {
                Plugin.Logger?.LogInfo("🟢 DirectModificationSystem OnUpdate running for the first time!");
                _loggedCreation = true;
            }

            if (_initialized && _attemptCount >= MAX_ATTEMPTS) return;

            _timer += Time.DeltaTime;
            
            // Check more frequently - first check after 3 seconds, then every 5 seconds
            if (_timer < 3f && _attemptCount == 0) return;
            if (_timer < 5f && _attemptCount > 0) return;
            
            _timer = 0f;
            _attemptCount++;
            
            try
            {
                var serverWorld = GetServerWorld();
                if (serverWorld != null)
                {
                    Plugin.Logger?.LogInfo($"⚡ ATTEMPT #{_attemptCount}: MODIFYING PREFABS FROM SYSTEM ⚡");
                    Plugin.ModifyPrefabs();
                    
                    // Consider it done after MAX_ATTEMPTS regardless of success
                    if (_attemptCount >= MAX_ATTEMPTS)
                    {
                        _initialized = true;
                        Plugin.Logger?.LogInfo($"⚠️ Reached max attempts ({MAX_ATTEMPTS}). Assuming prefabs are modified.");
                    }
                }
                else
                {
                    // Log all available worlds for debugging
                    string worldsInfo = "Available worlds: ";
                    try 
                    {
                        foreach (var world in World.All)
                        {
                            worldsInfo += world.Name + ", ";
                        }
                    }
                    catch (Exception ex)
                    {
                        worldsInfo += "Error listing worlds: " + ex.Message;
                    }
                    
                    Plugin.Logger?.LogWarning($"Server world not found on attempt #{_attemptCount}. {worldsInfo} Will retry in 5 seconds.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error modifying prefabs (attempt #{_attemptCount}): {ex.Message}");
            }
        }

        private World GetServerWorld()
        {
            try
            {
                foreach (var world in World.All)
                {
                    if (world != null && world.Name == "Server")
                    {
                        return world;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error getting server world: {ex.Message}");
                return null;
            }
        }
    }

    // System to intercept and prevent empty containers from appearing
    internal class EmptyContainerInterceptSystem : SystemBase
    {
        private EntityQuery _emptyContainerQuery;
        private EntityQuery _consumableQuery;
        private List<PrefabGUID> _emptyContainerGuids = new List<PrefabGUID>();
        private List<PrefabGUID> _consumableGuids = new List<PrefabGUID>();
        private float _timer = 0f;
        private const float CHECK_INTERVAL = 1.0f; // Check every second
        
        public override void OnCreate()
        {
            base.OnCreate();
            Plugin.Logger?.LogInfo("🍶 EmptyContainerInterceptSystem created!");
            
            // Initialize the lists of GUIDs to monitor
            _emptyContainerGuids.Add(new PrefabGUID(-437611596)); // Empty Glass Bottle
            _emptyContainerGuids.Add(new PrefabGUID(-810738866)); // Empty Waterskin
            
            _consumableGuids.Add(new PrefabGUID(-1568756102)); // Physical Power Potion T02
            _consumableGuids.Add(new PrefabGUID(1510182325));  // Spell Power Potion T02
            _consumableGuids.Add(new PrefabGUID(-2102469163)); // Vampiric Brew
            _consumableGuids.Add(new PrefabGUID(800879747));   // Blood Rose Brew
            _consumableGuids.Add(new PrefabGUID(-1885959251)); // Vermin Salve
            
            // Create queries for monitoring
            _emptyContainerQuery = GetEntityQuery(
                ComponentType.ReadWrite<ItemData>(),
                ComponentType.ReadOnly<PrefabGUID>());
                
            _consumableQuery = GetEntityQuery(
                ComponentType.ReadWrite<ItemData>(),
                ComponentType.ReadOnly<PrefabGUID>());
            
            Plugin.Logger?.LogInfo("EmptyContainerInterceptSystem initialized and ready to monitor empty bottles");
        }
        
        public override void OnUpdate()
        {
            try
            {
                // Only check periodically to reduce performance impact
                _timer += Time.DeltaTime;
                if (_timer < CHECK_INTERVAL) return;
                _timer = 0f;
                
                // Ensure our pvpkit consumables always stay infinite
                EnsureConsumablesInfinite();
                
                // Look for and delete empty containers
                DeleteEmptyContainers();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error in EmptyContainerInterceptSystem.OnUpdate: {ex.Message}");
            }
        }
        
        private void EnsureConsumablesInfinite()
        {
            try
            {
                // Get all entities that match consumables
                var entities = _consumableQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                int modifiedCount = 0;
                
                foreach (var entity in entities)
                {
                    if (EntityManager.HasComponent<PrefabGUID>(entity))
                    {
                        var guid = EntityManager.GetComponentData<PrefabGUID>(entity);
                        
                        // Check if this is one of our monitored consumables
                        bool isMonitored = false;
                        foreach (var consumableGuid in _consumableGuids)
                        {
                            if (guid.GuidHash == consumableGuid.GuidHash)
                            {
                                isMonitored = true;
                                break;
                            }
                        }
                        
                        if (isMonitored && EntityManager.HasComponent<ItemData>(entity))
                        {
                            var itemData = EntityManager.GetComponentData<ItemData>(entity);
                            if (itemData.RemoveOnConsume)
                            {
                                // Make the item infinite
                                itemData.RemoveOnConsume = false;
                                EntityManager.SetComponentData(entity, itemData);
                                modifiedCount++;
                            }
                        }
                    }
                }
                
                if (modifiedCount > 0)
                {
                    Plugin.Logger?.LogInfo($"Made {modifiedCount} consumable items infinite in this update");
                }
                
                if (entities.IsCreated)
                {
                    entities.Dispose();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error ensuring consumables are infinite: {ex.Message}");
            }
        }
        
        private void DeleteEmptyContainers()
        {
            try
            {
                // Get all entities that match empty containers
                var entities = _emptyContainerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                int deletedCount = 0;
                
                foreach (var entity in entities)
                {
                    if (EntityManager.HasComponent<PrefabGUID>(entity))
                    {
                        var guid = EntityManager.GetComponentData<PrefabGUID>(entity);
                        
                        // Check if this is an empty container
                        foreach (var emptyGuid in _emptyContainerGuids)
                        {
                            if (guid.GuidHash == emptyGuid.GuidHash)
                            {
                                // Found an empty container - try to delete it
                                Plugin.Logger?.LogInfo($"Found empty container entity: {entity} with GUID: {guid}");
                                deletedCount++;
                                
                                try
                                {
                                    // Check if this is a prefab, in which case we just want to modify it
                                    if (EntityManager.HasComponent<Prefab>(entity))
                                    {
                                        Plugin.Logger?.LogInfo("This is a prefab entity, not deleting it.");
                                        continue;
                                    }
                                    
                                    // Try to destroy the entity directly if it's not a prefab
                                    Plugin.Logger?.LogInfo($"Attempting to destroy entity {entity}");
                                    EntityManager.DestroyEntity(entity);
                                    Plugin.Logger?.LogInfo($"🍾 Successfully removed empty container!");
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Logger?.LogError($"Error removing empty container: {ex.Message}");
                                }
                                
                                break;
                            }
                        }
                    }
                }
                
                if (deletedCount > 0)
                {
                    Plugin.Logger?.LogInfo($"Found {deletedCount} empty containers in this update");
                }
                
                if (entities.IsCreated)
                {
                    entities.Dispose();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error deleting empty containers: {ex.Message}");
            }
        }
    }
}
