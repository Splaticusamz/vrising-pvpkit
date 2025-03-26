using PvPKit.Database;
using PvPKit.Utils;
using System.Collections.Generic;
using VampireCommandFramework;
using Stunlock.Core;
using Unity.Entities;
using System;
using ProjectM;
using System.Linq;

namespace PvPKit.Commands
{
    internal class KitCommands
    {
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

        [Command("pvpkit rogue", description: "Give Dracula Rogue armor set to the player.", adminOnly: false)]
        public static void KitRogueCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var added = new List<string>();
                    
                    // Add and equip the Rogue Set
                    Plugin.Logger.LogInfo("Adding and equipping Rogue armor set");
                    var rogueSet = new Dictionary<string, PrefabGUID>
                    {
                        { "Item_Boots_T09_Dracula_Rogue", new PrefabGUID(1855323424) },
                        { "Item_Chest_T09_Dracula_Rogue", new PrefabGUID(933057100) },
                        { "Item_Gloves_T09_Dracula_Rogue", new PrefabGUID(-1826382550) },
                        { "Item_Legs_T09_Dracula_Rogue", new PrefabGUID(-345596442) }
                    };
                    
                    foreach (var item in rogueSet)
                    {
                        var result = Helper.AddItemToInventory(playerEntity, item.Value, 1);
                        if (result != Entity.Null)
                        {
                            added.Add(item.Key);
                            // Try to equip the item
                            Helper.TryEquipItem(playerEntity, item.Value, result);
                            Plugin.Logger.LogInfo($"Successfully added and equipped {item.Key}");
                        }
                        else
                        {
                            Plugin.Logger.LogWarning($"Failed to add {item.Key}");
                        }
                    }
                    
                    // Final response to the player
                    if (added.Count > 0)
                    {
                        ctx.Reply($"<color=#ffffffff>Added Dracula Rogue set to your inventory.</color>");
                    }
                    else
                    {
                        ctx.Reply($"<color=#ff0000>Failed to add Rogue set. Please use .dumpitems for debug info.</color>");
                    }
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

        [Command("pvpkit warrior", description: "Give Dracula Warrior armor set to the player.", adminOnly: false)]
        public static void KitWarriorCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var added = new List<string>();
                    
                    // Add and equip the Warrior Set
                    Plugin.Logger.LogInfo("Adding and equipping Warrior armor set");
                    var warriorSet = new Dictionary<string, PrefabGUID>
                    {
                        { "Item_Boots_T09_Dracula_Warrior", new PrefabGUID(-382349289) },
                        { "Item_Chest_T09_Dracula_Warrior", new PrefabGUID(1392314162) },
                        { "Item_Gloves_T09_Dracula_Warrior", new PrefabGUID(1982551454) },
                        { "Item_Legs_T09_Dracula_Warrior", new PrefabGUID(205207385) }
                    };
                    
                    foreach (var item in warriorSet)
                    {
                        var result = Helper.AddItemToInventory(playerEntity, item.Value, 1);
                        if (result != Entity.Null)
                        {
                            added.Add(item.Key);
                            // Try to equip the item
                            Helper.TryEquipItem(playerEntity, item.Value, result);
                            Plugin.Logger.LogInfo($"Successfully added and equipped {item.Key}");
                        }
                        else
                        {
                            Plugin.Logger.LogWarning($"Failed to add {item.Key}");
                        }
                    }
                    
                    // Final response to the player
                    if (added.Count > 0)
                    {
                        ctx.Reply($"<color=#ffffffff>Added Dracula Warrior set to your inventory.</color>");
                    }
                    else
                    {
                        ctx.Reply($"<color=#ff0000>Failed to add Warrior set. Please use .dumpitems for debug info.</color>");
                    }
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
        
        [Command("pvpkit sorcerer", description: "Give Dracula Scholar armor set to the player.", adminOnly: false)]
        public static void KitSorcererCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var added = new List<string>();
                    
                    // Add and equip the Scholar Set (Sorcerer)
                    Plugin.Logger.LogInfo("Adding and equipping Scholar armor set");
                    var scholarSet = new Dictionary<string, PrefabGUID>
                    {
                        { "Item_Boots_T09_Dracula_Scholar", new PrefabGUID(1531721602) },
                        { "Item_Chest_T09_Dracula_Scholar", new PrefabGUID(114259912) },
                        { "Item_Gloves_T09_Dracula_Scholar", new PrefabGUID(-1899539896) },
                        { "Item_Legs_T09_Dracula_Scholar", new PrefabGUID(1592149279) }
                    };
                    
                    foreach (var item in scholarSet)
                    {
                        var result = Helper.AddItemToInventory(playerEntity, item.Value, 1);
                        if (result != Entity.Null)
                        {
                            added.Add(item.Key);
                            // Try to equip the item
                            Helper.TryEquipItem(playerEntity, item.Value, result);
                            Plugin.Logger.LogInfo($"Successfully added and equipped {item.Key}");
                        }
                        else
                        {
                            Plugin.Logger.LogWarning($"Failed to add {item.Key}");
                        }
                    }
                    
                    // Final response to the player
                    if (added.Count > 0)
                    {
                        ctx.Reply($"<color=#ffffffff>Added Dracula Sorcerer set to your inventory.</color>");
                    }
                    else
                    {
                        ctx.Reply($"<color=#ff0000>Failed to add Sorcerer set. Please use .dumpitems for debug info.</color>");
                    }
                }
                catch (System.Exception ex)
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
        
        [Command("pvpkit brute", description: "Give Dracula Brute armor set to the player.", adminOnly: false)]
        public static void KitBruteCommand(ChatCommandContext ctx)
        {
            if (DB.EnabledKitCommand)
            {
                try
                {
                    var playerEntity = ctx.Event.SenderCharacterEntity;
                    var added = new List<string>();
                    
                    // Add and equip the Brute Set
                    Plugin.Logger.LogInfo("Adding and equipping Brute armor set");
                    var bruteSet = new Dictionary<string, PrefabGUID>
                    {
                        { "Item_Boots_T09_Dracula_Brute", new PrefabGUID(1646489863) },
                        { "Item_Chest_T09_Dracula_Brute", new PrefabGUID(1033753207) },
                        { "Item_Gloves_T09_Dracula_Brute", new PrefabGUID(1039083725) },
                        { "Item_Legs_T09_Dracula_Brute", new PrefabGUID(993033515) }
                    };
                    
                    foreach (var item in bruteSet)
                    {
                        var result = Helper.AddItemToInventory(playerEntity, item.Value, 1);
                        if (result != Entity.Null)
                        {
                            added.Add(item.Key);
                            // Try to equip the item
                            Helper.TryEquipItem(playerEntity, item.Value, result);
                            Plugin.Logger.LogInfo($"Successfully added and equipped {item.Key}");
                        }
                        else
                        {
                            Plugin.Logger.LogWarning($"Failed to add {item.Key}");
                        }
                    }
                    
                    // Final response to the player
                    if (added.Count > 0)
                    {
                        ctx.Reply($"<color=#ffffffff>Added Dracula Brute set to your inventory.</color>");
                    }
                    else
                    {
                        ctx.Reply($"<color=#ff0000>Failed to add Brute set. Please use .dumpitems for debug info.</color>");
                    }
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

