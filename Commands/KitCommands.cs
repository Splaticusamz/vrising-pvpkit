using PvPKit.Database;
using PvPKit.Utils;
using System.Collections.Generic;
using VampireCommandFramework;
using Stunlock.Core;
using Unity.Entities;
using System;
using ProjectM;
using System.Linq;
using ProjectM.Network;

namespace PvPKit.Commands
{
    internal class KitCommands
    {
        // Cooldown tracking
        private static Dictionary<ulong, DateTime> kitCooldowns = new Dictionary<ulong, DateTime>();
        private static Dictionary<ulong, string> playerKits = new Dictionary<ulong, string>();
        private const int KIT_COOLDOWN_SECONDS = 1; // Reduced for testing

        // Helper to get player platform ID
        private static ulong GetPlayerPlatformId(Entity playerEntity)
        {
            try
            {
                var userData = Helper.EntityManager.GetComponentData<ProjectM.Network.User>(
                    Helper.EntityManager.GetComponentData<ProjectM.PlayerCharacter>(playerEntity).UserEntity
                );
                return userData.PlatformId;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error getting player platform ID: {ex.Message}");
                return 0;
            }
        }

        // Helper to reset kit tracking for a player
        private static void ResetKitTracking(ulong platformId)
        {
            if (playerKits.ContainsKey(platformId))
            {
                playerKits.Remove(platformId);
                Plugin.Logger.LogInfo($"Reset kit tracking for player {platformId}");
            }
        }

        // Helper to unequip all armor
        private static void UnequipArmor(Entity playerEntity)
        {
            try
            {
                // Unequip all equipment slots that might have armor
                Plugin.Logger.LogInfo("Unequipping all armor pieces");
                
                // Unequip in this order: Chest, Legs, Gloves, Boots
                Helper.EquipEquipment(playerEntity, 3); // Chest
                Helper.EquipEquipment(playerEntity, 4); // Legs
                Helper.EquipEquipment(playerEntity, 6); // Gloves
                Helper.EquipEquipment(playerEntity, 5); // Boots
                
                Plugin.Logger.LogInfo("All armor pieces unequipped");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error unequipping armor: {ex.Message}");
            }
        }

        // Get inventory index for a specific item entity
        private static int GetInventoryIndex(Entity characterEntity, Entity itemEntity)
        {
            try
            {
                // Get inventory buffer directly from character entity
                if (Helper.EntityManager.HasComponent<InventoryBuffer>(characterEntity))
                {
                    var inventoryBuffer = Helper.EntityManager.GetBuffer<InventoryBuffer>(characterEntity);
                    
                    // Walk the buffer to find the index where our item is
                    for (var i = 0; i < inventoryBuffer.Length; i++)
                    {
                        Entity bufferItemEntity = inventoryBuffer[i].ItemEntity._Entity;
                        if (bufferItemEntity == itemEntity)
                        {
                            return i;
                        }
                    }
                }
                return -1;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error getting inventory index: {ex.Message}");
                return -1;
            }
        }

        // Directly equip an item using its entity reference
        private static void EquipItem(Entity characterEntity, Entity itemEntity)
        {
            try
            {
                // Find slot index for the item
                int slot = GetInventoryIndex(characterEntity, itemEntity);

                if (slot == -1)
                {
                    Plugin.Logger.LogError("Couldn't find slot index for item to equip.");
                    return;
                }

                // Create entity for equip event
                Entity equipEntity = Helper.EntityManager.CreateEntity();
                
                // Get player character and user entity
                PlayerCharacter playerChar = Helper.EntityManager.GetComponentData<PlayerCharacter>(characterEntity);
                Entity userEntity = playerChar.UserEntity;
                
                // Add equip event component
                Helper.EntityManager.AddComponentData(equipEntity, new EquipItemEvent
                {
                    SlotIndex = slot,
                    IsCosmetic = false
                });
                
                // Add character info
                Helper.EntityManager.AddComponentData(equipEntity, new FromCharacter
                {
                    Character = characterEntity,
                    User = userEntity
                });
                
                Plugin.Logger.LogInfo($"Equipped item at slot {slot}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error equipping item: {ex.Message}");
            }
        }
        
        // Add and equip items in a single operation
        private static void AddAndEquipItems(Entity characterEntity, Dictionary<PrefabGUID, int> items)
        {
            foreach (var item in items)
            {
                var itemGuid = item.Key;
                var amount = item.Value;

                try
                {
                    // Add item to inventory
                    var response = Helper.serverGameManager.TryAddInventoryItem(characterEntity, itemGuid, amount);

                    if (response.Success)
                    {
                        Plugin.Logger.LogInfo($"Item {itemGuid.GuidHash} added successfully. Equipping...");
                        // Equip item if successfully added
                        EquipItem(characterEntity, response.NewEntity);
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Failed to add item with GUID {itemGuid}: {response.Result}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error adding/equipping item {itemGuid}: {ex.Message}");
                }
            }
        }
        
        // Remove existing kit items from inventory
        private static void RemoveKitItemsFromInventory(Entity playerEntity)
        {
            try
            {
                // First unequip all armor pieces
                UnequipArmor(playerEntity);
                
                // These are the item prefixes we're looking for in all kits
                var kitItemPrefixes = new[]
                {
                    "Item_Boots_T09_Dracula_",
                    "Item_Chest_T09_Dracula_",
                    "Item_Gloves_T09_Dracula_",
                    "Item_Legs_T09_Dracula_"
                };
                
                // Only remove if we have access to the inventory buffer
                if (Helper.EntityManager.HasComponent<InventoryBuffer>(playerEntity))
                {
                    var prefabSystem = Helper.Server.GetExistingSystemManaged<PrefabCollectionSystem>();
                    var inventoryBuffer = Helper.EntityManager.GetBuffer<InventoryBuffer>(playerEntity);
                    
                    // Log kit item removal attempt
                    Plugin.Logger.LogInfo($"Checking inventory for kit items to remove. Buffer length: {inventoryBuffer.Length}");
                    
                    // Store items to remove (can't modify while iterating)
                    var itemsToRemove = new List<(Entity, string)>();
                    
                    // Check each inventory item
                    for (int i = 0; i < inventoryBuffer.Length; i++)
                    {
                        var itemEntity = inventoryBuffer[i].ItemEntity._Entity;
                        
                        // Skip if null/invalid entity
                        if (itemEntity == Entity.Null || !Helper.EntityManager.Exists(itemEntity))
                            continue;
                            
                        // Get the prefab component to find out what item this is
                        if (Helper.EntityManager.HasComponent<PrefabGUID>(itemEntity))
                        {
                            var prefabGUID = Helper.EntityManager.GetComponentData<PrefabGUID>(itemEntity);
                            
                            // Try to get the name of this prefab
                            if (prefabSystem.PrefabGuidToNameDictionary.TryGetValue(prefabGUID, out string itemName))
                            {
                                // Check if it's a kit item
                                foreach (var prefix in kitItemPrefixes)
                                {
                                    if (itemName.StartsWith(prefix))
                                    {
                                        // We found a kit item, add it to removal list
                                        itemsToRemove.Add((itemEntity, itemName));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    
                    // Now remove all identified kit items
                    foreach (var item in itemsToRemove)
                    {
                        Helper.EntityManager.DestroyEntity(item.Item1);
                        Plugin.Logger.LogInfo($"Removed kit item: {item.Item2}");
                    }
                    
                    Plugin.Logger.LogInfo($"Removed {itemsToRemove.Count} kit items from inventory");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error removing kit items: {ex.Message}");
            }
        }
        
        // Helper to remove existing kit items
        private static void RemoveExistingKitItems(Entity playerEntity)
        {
            // Remove kit items from inventory
            RemoveKitItemsFromInventory(playerEntity);
        }

        [Command("dumpitems", description: "Dumps item list to log", adminOnly: true)]
        public static void DumpItemsCommand(ChatCommandContext ctx)
        {
            try
            {
                var prefabSystem = Helper.Server.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabSystem != null)
                {
                    Plugin.Logger.LogInfo("========== DUMPING ALL ITEMS ==========");
                    var count = 0;
                    foreach (var entry in prefabSystem.PrefabGuidToNameDictionary)
                    {
                        if (entry.Value.StartsWith("Item_"))
                        {
                            Plugin.Logger.LogInfo($"{entry.Value} -> {entry.Key}");
                            count++;
                        }
                    }
                    
                    Plugin.Logger.LogInfo($"========== DUMPED {count} ITEMS ==========");
                    ctx.Reply($"<color=#00ff00>Dumped {count} items to the log file.</color>");
                }
                else
                {
                    ctx.Reply("<color=#ff0000>Failed to get prefab system.</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error dumping items: {ex.Message}");
                ctx.Reply($"<color=#ff0000>Error: {ex.Message}</color>");
            }
        }

        [Command("pk", description: "Shows available kit options.", adminOnly: false)]
        public static void ShowKitOptionsCommand(ChatCommandContext ctx)
        {
            ctx.Reply("<color=#ffffffff>Available kits: .pk warrior, .pk rogue, .pk brute, .pk sorcerer</color>");
            ctx.Reply("<color=#ffffffff>For general weapons and equipment: .pvpkit</color>");
        }

        [Command("pvpkit", description: "Give basic weapons, consumables, and equipment to the player.", adminOnly: false)]
        public static void KitCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var added = new List<string>();
                    
                    // 1. FIRST: Add and equip the backpack
                    Plugin.Logger.LogInfo("Step 1: Adding and equipping backpack");
                    var backpackGuid = new PrefabGUID(-181179773); // Item_NewBag_T06
                    var backpackEntity = Helper.AddItemToInventory(playerEntity, backpackGuid, 1);
                    
                    if (backpackEntity != Entity.Null)
                    {
                        added.Add("Item_NewBag_T06");
                        // Try to equip the backpack immediately
                        Helper.TryEquipItem(playerEntity, backpackGuid, backpackEntity);
                        Plugin.Logger.LogInfo("Successfully added and equipped backpack");
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("Failed to add backpack");
                    }
                    
                    // 2. SECOND: Add all weapons
                    Plugin.Logger.LogInfo("Step 2: Adding all weapons");
                    var weapons = new Dictionary<string, PrefabGUID>
                    {
                        { "Item_Weapon_Whip_Unique_T08_Variation01", new PrefabGUID(-671246832) },
                        { "Item_Weapon_Pistols_Unique_T08_Variation01", new PrefabGUID(1759077469) },
                        { "Item_Weapon_Reaper_Unique_T08_Variation01", new PrefabGUID(-859437190) },
                        { "Item_Weapon_Slashers_Unique_T08_Variation01", new PrefabGUID(-2068145306) },
                        { "Item_Weapon_Slashers_Unique_T08_Variation02", new PrefabGUID(1570363331) },
                        { "Item_Weapon_Spear_Unique_T08_Variation01", new PrefabGUID(-1674680373) },
                        { "Item_Weapon_Sword_Unique_T08_Variation01", new PrefabGUID(2106567892) },
                        { "Item_Weapon_Axe_Unique_T08_Variation01", new PrefabGUID(1239564213) },
                        { "Item_Weapon_Crossbow_Unique_T08_Variation01", new PrefabGUID(-1401104184) },
                        { "Item_Weapon_GreatSword_Unique_T08_Variation01", new PrefabGUID(820408138) },
                        { "Item_Weapon_Longbow_Unique_T08_Variation01", new PrefabGUID(-557203874) },
                        { "Item_Weapon_Mace_Unique_T08_Variation01", new PrefabGUID(675187526) }
                    };
                    
                    foreach (var weapon in weapons)
                    {
                        try
                        {
                            var result = Helper.AddItemToInventory(playerEntity, weapon.Value, 1);
                            if (result != Entity.Null)
                            {
                                added.Add(weapon.Key);
                                Plugin.Logger.LogInfo($"Successfully added {weapon.Key}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning($"Error adding {weapon.Key}: {ex.Message}");
                        }
                    }
                    
                    // 3. THIRD: Add all amulets
                    Plugin.Logger.LogInfo("Step 3: Adding all amulets");
                    var amulets = new Dictionary<string, PrefabGUID>
                    {
                        { "Item_MagicSource_General_T08_Illusion", new PrefabGUID(-1306155896) },
                        { "Item_MagicSource_General_T08_Frost", new PrefabGUID(1380368392) },
                        { "Item_MagicSource_General_T08_Storm", new PrefabGUID(-296161379) },
                        { "Item_MagicSource_General_T08_Blood", new PrefabGUID(-104934480) },
                        { "Item_MagicSource_General_T08_Unholy", new PrefabGUID(-1004351840) }
                    };
                    
                    foreach (var amulet in amulets)
                    {
                        try
                        {
                            var result = Helper.AddItemToInventory(playerEntity, amulet.Value, 1);
                            if (result != Entity.Null)
                            {
                                added.Add(amulet.Key);
                                Plugin.Logger.LogInfo($"Successfully added {amulet.Key}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning($"Error adding {amulet.Key}: {ex.Message}");
                        }
                    }
                    
                    // 4. FOURTH: Add cloak
                    Plugin.Logger.LogInfo("Step 4: Adding cloak");
                    var cloakGuid = new PrefabGUID(584164197); // Item_Cloak_T03_Royal
                    var cloakEntity = Helper.AddItemToInventory(playerEntity, cloakGuid, 1);
                    
                    if (cloakEntity != Entity.Null)
                    {
                        added.Add("Item_Cloak_T03_Royal");
                        Plugin.Logger.LogInfo("Successfully added cloak");
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("Failed to add cloak");
                    }
                    
                    // 5. FIFTH: Add consumables (1 of each)
                    Plugin.Logger.LogInfo("Step 5: Adding consumables");
                    var consumables = new Dictionary<string, PrefabGUID>
                    {
                        { "Item_Consumable_SpellLeechPotion_T01", new PrefabGUID(-2102469163) }, // Vampiric Brew
                        { "Item_Consumable_HealingPotion_T01", new PrefabGUID(800879747) },   // Blood Rose Brew
                        { "Item_Consumable_Salve_Vermin", new PrefabGUID(-1885959251) },     // Vermin Salve
                        { "Item_Consumable_PhysicalPowerPotion_T02", new PrefabGUID(-1568756102) }, // Physical Power Potion
                        { "Item_Consumable_SpellPowerPotion_T02", new PrefabGUID(1510182325) }   // Spell Power Potion
                    };
                    
                    foreach (var consumable in consumables)
                    {
                        try
                        {
                            var result = Helper.AddItemToInventory(playerEntity, consumable.Value, 1); // Add 1 of each
                            if (result != Entity.Null)
                            {
                                added.Add(consumable.Key);
                                Plugin.Logger.LogInfo($"Successfully added {consumable.Key}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning($"Error adding {consumable.Key}: {ex.Message}");
                        }
                    }
                    
                    // 6. SIXTH: Add siege golem stone
                    Plugin.Logger.LogInfo("Step 6: Adding siege golem stone");
                    var golemGuid = new PrefabGUID(-1461326411); // Item_Building_Siege_Golem_T02
                    var golemEntity = Helper.AddItemToInventory(playerEntity, golemGuid, 1);
                    
                    if (golemEntity != Entity.Null)
                    {
                        added.Add("Item_Building_Siege_Golem_T02");
                        Plugin.Logger.LogInfo("Successfully added siege golem stone");
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("Failed to add siege golem stone");
                    }
                    
                    // Final response to the player
                    if (added.Count > 0)
                    {
                        ctx.Reply($"<color=#ffffffff>Added {added.Count} items to your inventory.</color>");
                        Plugin.Logger.LogInfo($"Total items added: {added.Count}");
                    }
                    else
                    {
                        ctx.Reply($"<color=#ff0000>Failed to add any items. Please use .dumpitems for debug info.</color>");
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"Error giving kit: {ex.Message}");
                    ctx.Reply($"<color=#ff0000>Error giving kit: {ex.Message}</color>");
                }
            }
            else
            {
                ctx.Reply($"Command disabled by admins.");
            }
        }

        [Command("pk rogue", description: "Give Dracula Rogue armor set to the player.", adminOnly: false)]
        public static void KitRogueCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var platformId = GetPlayerPlatformId(playerEntity);
                    
                    // Check if player has this kit already
                    if (playerKits.TryGetValue(platformId, out string currentKit) && currentKit == "rogue")
                    {
                        ctx.Reply($"<color=#ffffffff>You already have the Rogue set equipped.</color>");
                        return;
                    }
                    
                    // Remove existing kit items first
                    RemoveExistingKitItems(playerEntity);
                    
                    // Add and equip the Rogue Set
                    Plugin.Logger.LogInfo("Adding and equipping Rogue armor set");
                    var rogueSet = new Dictionary<PrefabGUID, int>
                    {
                        { new PrefabGUID(1855323424), 1 },  // Item_Boots_T09_Dracula_Rogue
                        { new PrefabGUID(933057100), 1 },   // Item_Chest_T09_Dracula_Rogue
                        { new PrefabGUID(-1826382550), 1 }, // Item_Gloves_T09_Dracula_Rogue
                        { new PrefabGUID(-345596442), 1 }   // Item_Legs_T09_Dracula_Rogue
                    };
                    
                    // Add and equip all items in one operation
                    AddAndEquipItems(playerEntity, rogueSet);
                    
                    // Update player kit tracking
                    playerKits[platformId] = "rogue";
                    ctx.Reply($"<color=#ffffffff>Added Dracula Rogue set to your inventory and equipped it.</color>");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"Error giving Rogue kit: {ex.Message}");
                    ctx.Reply($"<color=#ff0000>Error giving Rogue kit: {ex.Message}</color>");
                }
            }
            else
            {
                ctx.Reply($"Command disabled by admins.");
            }
        }

        [Command("pk warrior", description: "Give Dracula Warrior armor set to the player.", adminOnly: false)]
        public static void KitWarriorCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var platformId = GetPlayerPlatformId(playerEntity);
                    
                    // Check if player has this kit already
                    if (playerKits.TryGetValue(platformId, out string currentKit) && currentKit == "warrior")
                    {
                        ctx.Reply($"<color=#ffffffff>You already have the Warrior set equipped.</color>");
                        return;
                    }
                    
                    // Remove existing kit items first
                    RemoveExistingKitItems(playerEntity);
                    
                    // Add and equip the Warrior Set
                    Plugin.Logger.LogInfo("Adding and equipping Warrior armor set");
                    var warriorSet = new Dictionary<PrefabGUID, int>
                    {
                        { new PrefabGUID(-382349289), 1 },  // Item_Boots_T09_Dracula_Warrior
                        { new PrefabGUID(1392314162), 1 },  // Item_Chest_T09_Dracula_Warrior
                        { new PrefabGUID(1982551454), 1 },  // Item_Gloves_T09_Dracula_Warrior
                        { new PrefabGUID(205207385), 1 }    // Item_Legs_T09_Dracula_Warrior
                    };
                    
                    // Add and equip all items in one operation
                    AddAndEquipItems(playerEntity, warriorSet);
                    
                    // Update player kit tracking
                    playerKits[platformId] = "warrior";
                    ctx.Reply($"<color=#ffffffff>Added Dracula Warrior set to your inventory and equipped it.</color>");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"Error giving Warrior kit: {ex.Message}");
                    ctx.Reply($"<color=#ff0000>Error giving Warrior kit: {ex.Message}</color>");
                }
            }
            else
            {
                ctx.Reply($"Command disabled by admins.");
            }
        }
        
        [Command("pk sorcerer", description: "Give Dracula Sorcerer armor set to the player.", adminOnly: false)]
        public static void KitSorcererCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var platformId = GetPlayerPlatformId(playerEntity);
                    
                    // Check if player has this kit already
                    if (playerKits.TryGetValue(platformId, out string currentKit) && currentKit == "sorcerer")
                    {
                        ctx.Reply($"<color=#ffffffff>You already have the Sorcerer set equipped.</color>");
                        return;
                    }
                    
                    // Remove existing kit items first
                    RemoveExistingKitItems(playerEntity);
                    
                    // Add and equip the Scholar Set (Sorcerer)
                    Plugin.Logger.LogInfo("Adding and equipping Scholar armor set");
                    var scholarSet = new Dictionary<PrefabGUID, int>
                    {
                        { new PrefabGUID(1531721602), 1 },  // Item_Boots_T09_Dracula_Scholar
                        { new PrefabGUID(114259912), 1 },   // Item_Chest_T09_Dracula_Scholar
                        { new PrefabGUID(-1899539896), 1 }, // Item_Gloves_T09_Dracula_Scholar
                        { new PrefabGUID(1592149279), 1 }   // Item_Legs_T09_Dracula_Scholar
                    };
                    
                    // Add and equip all items in one operation
                    AddAndEquipItems(playerEntity, scholarSet);
                    
                    // Update player kit tracking
                    playerKits[platformId] = "sorcerer";
                    ctx.Reply($"<color=#ffffffff>Added Dracula Sorcerer set to your inventory and equipped it.</color>");
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error giving Sorcerer kit: {ex.Message}");
                    ctx.Reply($"<color=#ff0000>Error giving Sorcerer kit: {ex.Message}</color>");
                }
            }
            else
            {
                ctx.Reply($"Command disabled by admins.");
            }
        }
        
        [Command("pk brute", description: "Give Dracula Brute armor set to the player.", adminOnly: false)]
        public static void KitBruteCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var platformId = GetPlayerPlatformId(playerEntity);
                    
                    // Check if player has this kit already
                    if (playerKits.TryGetValue(platformId, out string currentKit) && currentKit == "brute")
                    {
                        ctx.Reply($"<color=#ffffffff>You already have the Brute set equipped.</color>");
                        return;
                    }
                    
                    // Remove existing kit items first
                    RemoveExistingKitItems(playerEntity);
                    
                    // Add and equip the Brute Set
                    Plugin.Logger.LogInfo("Adding and equipping Brute armor set");
                    var bruteSet = new Dictionary<PrefabGUID, int>
                    {
                        { new PrefabGUID(1646489863), 1 }, // Item_Boots_T09_Dracula_Brute
                        { new PrefabGUID(1033753207), 1 }, // Item_Chest_T09_Dracula_Brute 
                        { new PrefabGUID(1039083725), 1 }, // Item_Gloves_T09_Dracula_Brute
                        { new PrefabGUID(993033515), 1 }   // Item_Legs_T09_Dracula_Brute
                    };
                    
                    // Add and equip all items in one operation
                    AddAndEquipItems(playerEntity, bruteSet);
                    
                    // Update player kit tracking
                    playerKits[platformId] = "brute";
                    ctx.Reply($"<color=#ffffffff>Added Dracula Brute set to your inventory and equipped it.</color>");
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError($"Error giving Brute kit: {ex.Message}");
                    ctx.Reply($"<color=#ff0000>Error giving Brute kit: {ex.Message}</color>");
                }
            }
            else
            {
                ctx.Reply($"Command disabled by admins.");
            }
        }
    }
}

