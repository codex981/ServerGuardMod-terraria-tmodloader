# ServerGuard Mod - Beta 4 Changelog

## Overview
Beta 4 moves ServerGuard toward strict server-authoritative character data. The server no longer treats a player's local inventory as truth during registration, and saved item data now uses tModLoader `ItemIO` data so modded items can keep their custom state.

## Changes Made in Beta 4
1. **Strict Server-Side Character Registration**
   - New accounts now start from a server-created profile instead of importing the client character inventory.
   - Default starter tools are created on the server: Copper Shortsword, Copper Pickaxe, and Copper Axe.
   - This prevents edited local characters from becoming trusted data at account creation.

2. **Mod-Compatible Item Persistence**
   - `AccountDatabase` now stores full ItemIO base64 data for inventory, armor, dyes, misc equips, misc dyes, and all four banks.
   - Legacy `InventoryIDs`, `InventoryStacks`, and `InventoryPrefix` remain for migrating old accounts.
   - Login and sync packets now transmit the full saved item data instead of only `type/stack/prefix`.

3. **Server Join Initialization Fix**
   - `SGPlayer` now initializes login blocking from server-side `PreUpdate`, not only `OnEnterWorld`.
   - Login UI packets are resent every 10 seconds while the player is not logged in.

4. **Inventory Stack Tracker**
   - Added a trusted 59-slot inventory snapshot on the server.
   - Detects sudden stack/type/value gains without gameplay context.
   - Allows normal contexts such as nearby world item pickups, chest/NPC interaction, item use, and plausible crafting/material swaps.
   - On violation, the server restores the saved account state and syncs it back to the client.

5. **ServerGuard Packet Guard**
   - ServerGuard mod packets are now rate-limited and blocked before login unless they are login/register requests.
   - Note: vanilla packet interception still needs a deeper tModLoader hook if future releases require full packet-level filtering outside ServerGuard's own mod packets.

6. **Build Project Cleanup**
   - Updated the project to target `net8.0`.
   - Imported local `tMLMod.targets` for correct tModLoader references.
   - Excluded the bundled local `tModLoader/` folder from compilation and mod packaging to avoid huge `.tmod` builds.

## Known Release Blockers
- Need dedicated multiplayer tests for Magic Storage, shops, chests, crafting, boss drops, and modded reward UIs.
- Need a final decision on whether strict new accounts should start with only vanilla tools or a configurable server starter kit.
- Full vanilla packet filtering is not implemented yet; current protection covers ServerGuard mod packets and server-side state rollback.

## Next AI Instructions
- If Beta 4 passes basic login/save tests, create Beta 5 for compatibility testing with Magic Storage and other item-heavy mods.
- Do not re-enable first-login inventory importing unless explicitly requested; it breaks the server-authoritative security model.
