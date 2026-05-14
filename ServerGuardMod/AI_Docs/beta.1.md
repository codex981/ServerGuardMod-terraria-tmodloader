# ServerGuard Mod - Beta 1 Changelog

## Overview
This document serves as a persistent context record for future AI agents working on this mod.
In this version (Beta 1), several critical anti-cheat and quality-of-life bugs were fixed to make the mod fully playable and secure on a live server.

## Changes Made in Beta 1:
1. **Total Freeze (Ghost Mode) for Unregistered Players:**
   - Modified `SGPlayer.SetControls()` to set all control flags (Left, Right, Jump, UseItem, Inv, etc.) to `false`.
   - Modified `SGPlayer.CanUseItem()` to return `false`, completely preventing interaction.
   - Fixed `FreeDodge` so unregistered players take absolutely 0 damage, resolving an issue where the client-side check failed.

2. **Inventory Wipe Fix (Starter Items Preservation):**
   - Removed the forced inventory wipe (`new Item()`) inside `SGPlayer.BlockPlayer()`.
   - Updated `ClientLoginSystem.OnLoginSuccess` to verify if the incoming database inventory is completely empty (all `0` IDs). If it is, the client skips wiping the local inventory, allowing new players to keep their starter items given by the server/mods.

3. **Heuristic Anti-Cheat (Cheat Engine / Memory Editing Protection):**
   - Implemented `CheckMemoryEditing()` in `SGPlayer.cs`.
   - **Logic**: Tracks the `Total Value` of all items in the player's inventory in copper coins.
   - **Trigger**: If the total value spikes by more than 50 Gold Coins (`5_000_000` copper) in a single second, AND the player is NOT interacting with a chest (`chest == -1`) or a vendor/NPC (`talkNPC == -1`), it flags the player for memory editing.
   - **Result**: The player is instantly kicked with a "Cheat Detected" message to prevent them from distributing injected items. Legitimate shop purchases and chest looting bypass this check because the game registers the interaction.

4. **In-Game Inspect UI (`/sg showme <player>`):**
   - Created `InspectUI.cs`, a custom `ModSystem` that draws an interface layer.
   - Allows an admin to view a target player's full 50-slot inventory, coins, and ammo slots in real-time.
   - Also calculates and displays the estimated total value of the inventory in Platinum, Gold, Silver, and Copper.

5. **God Mode Fix (`/sg god`):**
   - Changed `CmdGod` to toggle a custom `IsGodMode` property in `SGPlayer` instead of the vanilla `player.immune`.
   - `FreeDodge` now checks `IsGodMode` to grant permanent and perfect damage immunity.

6. **Admin Commands Enhancements:**
   - Improved `CmdBan` and `CmdKick` to locate online players by their active character names, even if they haven't registered an account yet, allowing immediate removal of malicious actors.
   - Added `account` as an alias for `accounts`.

## Future AI Instructions:
- **Versioning**: When making new changes, increment the version in `build.txt` (e.g., to `1.0.2` and update `displayName` to `Beta 2`), and create a new `beta.2.md` file in this directory detailing the changes.
- **Anti-Cheat**: If the value-based anti-cheat produces false positives, adjust the `5_000_000` copper threshold or add checks for other legitimate value-gaining actions (like completing quests).
- **UI**: `InspectUI` uses `LegacyGameInterfaceLayer`. If tModLoader updates deprecate this, migrate it to `UIState`.
