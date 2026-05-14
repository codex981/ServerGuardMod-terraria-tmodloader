# ServerGuard Mod - Beta 2 Changelog

## Overview
This document serves as a persistent context record for future AI agents working on this mod.
In this version (Beta 2), we implemented architectural fixes for character data wipes, improved the anti-cheat heuristic logic to detect stack injections, and enhanced the freezing mechanism.

## Changes Made in Beta 2:
1. **High-Level Character Wipe Fix:**
   - **Issue**: `AccountDatabase.CreateAccount` was hardcoding `100 HP`, `20 Mana`, and `0` item IDs for every new account, which destroyed the progress of pre-existing powerful characters upon registration.
   - **Fix**: Modified `CreateAccount` to capture a real-time snapshot of `player.statLifeMax`, `player.statManaMax`, and iterate through the actual `player.inventory` to save their exact state upon registration.

2. **Absolute Freezing (Ghost Mode Enhancement):**
   - **Issue**: Players could still be attacked by monsters, and could use quick-keys for mounts/hooks. The ESC menu was completely disabled due to `controlInv = false`, preventing them from saving and exiting.
   - **Fix**: 
     - Re-enabled `Player.controlInv` so the ESC menu works.
     - Overrode `CanBeHitByNPC` and `CanBeHitByProjectile` to return `false` for unregistered/frozen players, making them truly invincible and ignored by AI.
     - Added `ModifyHitByNPC` safety measure (`modifiers.SetMaxDamage(0)`).
     - Explicitly disabled `controlQuickHeal` and `controlQuickMana`.

3. **Advanced Heuristic Anti-Cheat (Stack Tracker Radar):**
   - **Issue**: The previous value-based anti-cheat failed to catch memory editing if the user injected low-value items (like Dirt Blocks) because the total inventory value didn't spike significantly.
   - **Fix**: 
     - Built a `Stack Tracker` in `SGPlayer.CheckMemoryEditing()`. It maintains an array of all `59` inventory slot stacks.
     - If any single item's stack increases by more than `5` instantly, the system runs three checks:
       1. Is the player interacting with a chest? (`Player.chest != -1`)
       2. Is the player interacting with an NPC/Shop? (`Player.talkNPC != -1`)
       3. Was there an active item on the ground near the player in the last tick? (`_itemNearLastTick`)
     - If all three checks are `false`, the game knows it is physically impossible for the stack to grow naturally, proving the use of Cheat Engine or Memory Injection. The player is instantly kicked.

4. **Arabic / Unicode Name Prevention:**
   - **Issue**: Players with Arabic Steam names had their names corrupted in chat.
   - **Fix**: Added a Regex validation (`^[a-zA-Z0-9_]+$`) in `PacketHandler.HandleRegister`. It rejects registration and warns the user to use English letters and numbers only.

5. **Admin Commands Improvements:**
   - **Pagination**: The `/sg accounts` command output was too long, causing the game chat to drop the message. It has been refactored to support pagination (e.g., `/sg accounts 1`, `/sg accounts 2`), displaying 10 accounts per page.
   - **Alias**: Added `/sg inv` as a direct alias to the `/sg showme` command.

## Future AI Instructions:
- **Versioning**: For future fixes, update `build.txt` to `1.0.3` (Beta 3), and create a `beta.3.md` file.
- **Heuristic Tweaks**: The stack tracker is very powerful but may need tuning if players find legitimate ways to increase stacks by >5 without chests, NPCs, or ground drops (e.g., Quest Rewards or special modded UI items). Keep an eye on `_itemNearLastTick` performance.
