using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using PvPKit.Database;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;
using ProjectM.CastleBuilding;

namespace PvPKit.Utils
{
    internal class Helper
    {
        private static World? _serverWorld;
        public static EntityManager EntityManager => Server.EntityManager;
        public static GameDataSystem gameData => Server.GetExistingSystemManaged<GameDataSystem>();
        public static PrefabCollectionSystem PrefabCollection => Server.GetExistingSystemManaged<PrefabCollectionSystem>();
        public static ServerGameManager serverGameManager = Server.GetExistingSystemManaged<ServerScriptMapper>()._ServerGameManager;
        public static Il2CppSystem.Collections.Generic.Dictionary<string, PrefabGUID> NameToGuid => PrefabCollection.NameToPrefabGuidDictionary;
        
        // Method to safely try to equip an item
        public static void TryEquipItem(Entity playerEntity, PrefabGUID itemGuid, Entity itemEntity)
        {
            try
            {
                // Create an equip event entity for the specific item
                var entity = EntityManager.CreateEntity(ComponentType.ReadWrite<FromCharacter>(), ComponentType.ReadWrite<EquipItemEvent>());
                
                // Get player character and user entity
                PlayerCharacter playerchar = EntityManager.GetComponentData<PlayerCharacter>(playerEntity);
                Entity userEntity = playerchar.UserEntity;
                
                // Set the character/user data
                EntityManager.SetComponentData<FromCharacter>(entity, new() { User = userEntity, Character = playerEntity });
                
                // Get the inventory slot where the item is located
                int inventorySlot = -1;
                if (EntityManager.HasComponent<InventoryBuffer>(playerEntity))
                {
                    var inventoryBuffer = EntityManager.GetBuffer<InventoryBuffer>(playerEntity);
                    for (int i = 0; i < inventoryBuffer.Length; i++)
                    {
                        if (inventoryBuffer[i].ItemEntity._Entity == itemEntity)
                        {
                            inventorySlot = i;
                            break;
                        }
                    }
                }
                
                // If we found the item's inventory slot, try to equip it
                if (inventorySlot >= 0)
                {
                    // Set the equip data for this slot
                    EntityManager.SetComponentData<EquipItemEvent>(entity, new() { SlotIndex = inventorySlot });
                    Plugin.Logger.LogInfo($"Sent equip command for item in inventory slot {inventorySlot}");
                }
                else
                {
                    Plugin.Logger.LogWarning($"Could not find item in inventory to equip");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"Error equipping item: {ex.Message}");
            }
        }
        
        // Method to add item directly by GUID
        public static Entity AddItemToInventoryByGuid(Entity recipient, PrefabGUID guid, int amount)
        {
            try
            {
                // Check if the GUID is valid
                if (guid.GuidHash == 0)
                {
                    Plugin.Logger.LogWarning($"Invalid GUID: {guid}");
                    return Entity.Null;
                }
                
                // Try to add the item
                var inventoryResponse = serverGameManager.TryAddInventoryItem(recipient, guid, amount);
                
                // Check if the item was successfully added
                if (inventoryResponse.Success)
                {
                    Plugin.Logger.LogInfo($"Successfully added item with GUID {guid}");
                    return inventoryResponse.NewEntity;
                }
                else
                {
                    Plugin.Logger.LogWarning($"Failed to add item with GUID {guid}: {inventoryResponse.Result}");
                    return Entity.Null;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Exception adding item with GUID {guid}: {e.Message}");
                return Entity.Null;
            }
        }

        public static void GiveStartKit(ChatCommandContext ctx, List<RecordKit> kit)
        {
            int successCount = 0;
            List<string> failedItems = new List<string>();
            
            foreach (var item in kit)
            {
                try
                {
                    if (!NameToGuid.ContainsKey(item.Name))
                    {
                        // Item name doesn't exist in the game
                        failedItems.Add(item.Name);
                        Plugin.Logger.LogWarning($"Item not found: {item.Name}");
                        continue;
                    }
                    
                    var itemEntity = Helper.AddItemToInventory(ctx.Event.SenderCharacterEntity, NameToGuid[item.Name], item.Amount);
                    if (itemEntity != Entity.Null)
                    {
                        // Try to equip the item
                        try
                        {
                            TryEquipItem(ctx.Event.SenderCharacterEntity, NameToGuid[item.Name], itemEntity);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning($"Failed to equip item {item.Name}: {ex.Message}");
                            // Still count it as a success since the item was added to inventory
                            successCount++;
                        }
                    }
                    else
                    {
                        failedItems.Add(item.Name);
                    }
                }
                catch (Exception ex)
                {
                    failedItems.Add(item.Name);
                    Plugin.Logger.LogWarning($"Error adding item {item.Name}: {ex.Message}");
                }
            }
            
            if (failedItems.Count > 0)
            {
                Plugin.Logger.LogWarning($"Failed to add {failedItems.Count} items: {string.Join(", ", failedItems)}");
            }
            
            Plugin.Logger.LogInfo($"Successfully added {successCount} of {kit.Count} items");
        }

        public static Entity AddItemToInventory(Entity recipient, PrefabGUID guid, int amount)
        {
            try
            {
                // Check if the GUID is valid
                if (guid.GuidHash == 0)
                {
                    Plugin.Logger.LogWarning($"Invalid GUID: {guid}");
                    return Entity.Null;
                }
                
                // Try to add the item
                var inventoryResponse = serverGameManager.TryAddInventoryItem(recipient, guid, amount);
                
                // Check if the item was successfully added
                if (inventoryResponse.Success)
                {
                    return inventoryResponse.NewEntity;
                }
                else
                {
                    Plugin.Logger.LogWarning($"Failed to add item with GUID {guid}: {inventoryResponse.Result}");
                    return Entity.Null;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Logger.LogError($"Exception adding item: {e.Message}");
                return Entity.Null;
            }
        }
        
        public static void EquipEquipment(Entity player, int slot)
        {
            try
            {
                var entity = Helper.Server.EntityManager.CreateEntity(ComponentType.ReadWrite<FromCharacter>(), ComponentType.ReadWrite<EquipItemEvent>());
                PlayerCharacter playerchar = Helper.Server.EntityManager.GetComponentData<PlayerCharacter>(player);
                Entity userEntity = playerchar.UserEntity;
                Helper.Server.EntityManager.SetComponentData<FromCharacter>(entity, new() { User = userEntity, Character = player });
                Helper.Server.EntityManager.SetComponentData<EquipItemEvent>(entity, new() { SlotIndex = slot });
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error equipping item in slot {slot}: {ex.Message}");
            }
        }

        public static World Server
        {
            get
            {
                if (_serverWorld != null) return _serverWorld;

                _serverWorld = GetWorld("Server")
                    ?? throw new System.Exception("There is no Server world (yet). Did you install a server mod on the client?");
                return _serverWorld;
            }
        }

        public static bool IsServer => Application.productName == "VRisingServer";

        private static World GetWorld(string name)
        {
            foreach (var world in World.s_AllWorlds)
            {
                if (world.Name == name)
                {
                    return world;
                }
            }

            return null;
        }

        // Helper method to try adding an item by name
        public static bool TryAddItemByName(Entity playerEntity, string itemName, int amount)
        {
            try
            {
                // Try to find the prefab GUID from the name
                if (NameToGuid.TryGetValue(itemName, out var prefabGuid))
                {
                    Plugin.Logger.LogInfo($"Found item {itemName} with GUID {prefabGuid}");
                    
                    // Add the item
                    var result = AddItemToInventory(playerEntity, prefabGuid, amount);
                    return result != Entity.Null;
                }
                
                // Try alternative prefab lookup - strict version
                var prefabLookupSystem = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabLookupSystem != null)
                {
                    // Avoid recipe items, only look for actual items
                    foreach (var entry in prefabLookupSystem.NameToPrefabGuidDictionary)
                    {
                        // Skip any recipe entries
                        if (entry.Key.StartsWith("Recipe_"))
                            continue;
                            
                        // Only match if it's an item and contains the exact term we want
                        if (entry.Key.StartsWith("Item_") && 
                            (entry.Key.Contains("_Dracula_") || // Match Dracula items specifically
                             entry.Key.EndsWith("_Dracula") ||
                             entry.Key.Contains("Dracula")))
                        {
                            Plugin.Logger.LogInfo($"Found Dracula item: {entry.Key} with GUID {entry.Value}");
                            // Check if we've already added this item to avoid duplicates
                            var result = AddItemToInventory(playerEntity, entry.Value, amount);
                            if (result != Entity.Null)
                            {
                                return true;
                            }
                        }
                    }
                }
                
                // For the fallback T09 items, use hardcoded GUIDs instead of searching
                if (itemName.Contains("Item_Armor_") && itemName.Contains("_T09"))
                {
                    // Try giving some high-tier items directly by GUID
                    if (itemName.Contains("Chest"))
                        return AddItemToInventory(playerEntity, new PrefabGUID(1626067133), 1) != Entity.Null;
                    if (itemName.Contains("Legs"))
                        return AddItemToInventory(playerEntity, new PrefabGUID(871588325), 1) != Entity.Null;
                    if (itemName.Contains("Gloves"))
                        return AddItemToInventory(playerEntity, new PrefabGUID(-2097658343), 1) != Entity.Null;
                    if (itemName.Contains("Boots"))
                        return AddItemToInventory(playerEntity, new PrefabGUID(1851183595), 1) != Entity.Null;
                }
                
                // If we get here, we couldn't find the item
                Plugin.Logger.LogWarning($"Could not find item with name {itemName}");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error in TryAddItemByName for {itemName}: {ex.Message}");
                return false;
            }
        }

        // Special method to add dracula items by prefix with duplicate checking
        public static bool TryAddDraculaItemsByPrefix(Entity playerEntity, string prefix, HashSet<string> alreadyAdded)
        {
            bool anySuccess = false;
            var prefabLookupSystem = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (prefabLookupSystem == null)
                return false;
                
            // Look for items with this prefix that contain Dracula
            foreach (var entry in prefabLookupSystem.NameToPrefabGuidDictionary)
            {
                // Skip recipes and non-item entries
                if (entry.Key.StartsWith("Recipe_") || !entry.Key.StartsWith("Item_"))
                    continue;
                    
                // Match only items with the prefix and "Dracula" in the name
                if (entry.Key.StartsWith(prefix) && entry.Key.Contains("Dracula"))
                {
                    // Check if we've already added something with a similar name
                    bool isDuplicate = false;
                    foreach (var added in alreadyAdded)
                    {
                        if (entry.Key.Contains(added) || added.Contains(entry.Key))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    
                    if (!isDuplicate)
                    {
                        Plugin.Logger.LogInfo($"Found Dracula item by prefix: {entry.Key} with GUID {entry.Value}");
                        var result = AddItemToInventory(playerEntity, entry.Value, 1);
                        if (result != Entity.Null)
                        {
                            // Add to tracking set to avoid duplicates
                            alreadyAdded.Add(entry.Key);
                            anySuccess = true;
                            
                            // Try to equip the item
                            TryEquipItem(playerEntity, entry.Value, result);
                        }
                    }
                }
            }
            
            return anySuccess;
        }

        /// <summary>
        /// Force equips an item to a specific inventory slot, bypassing normal checks
        /// </summary>
        public static void ForceEquipItemToInventorySlot(Entity playerEntity, Entity itemEntity, int slotIndex)
        {
            try
            {
                if (playerEntity == Entity.Null || itemEntity == Entity.Null)
                {
                    Plugin.Logger.LogWarning($"Cannot equip: player or item is null");
                    return;
                }

                Plugin.Logger.LogInfo($"Attempting to directly equip item to slot {slotIndex}");
                
                // Create and dispatch a direct equip event
                var entity = Server.EntityManager.CreateEntity(
                    ComponentType.ReadWrite<FromCharacter>(), 
                    ComponentType.ReadWrite<EquipItemEvent>()
                );
                
                var playerChar = Server.EntityManager.GetComponentData<PlayerCharacter>(playerEntity);
                Entity userEntity = playerChar.UserEntity;
                
                // Set the source character
                Server.EntityManager.SetComponentData<FromCharacter>(entity, new FromCharacter { 
                    User = userEntity, 
                    Character = playerEntity 
                });
                
                // Set the equip event with target slot - only SlotIndex is available in EquipItemEvent
                Server.EntityManager.SetComponentData<EquipItemEvent>(entity, new EquipItemEvent { 
                    SlotIndex = slotIndex
                });
                
                Plugin.Logger.LogInfo($"Force equip event dispatched for slot {slotIndex}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error force equipping item: {ex.Message}");
            }
        }
    }
}
