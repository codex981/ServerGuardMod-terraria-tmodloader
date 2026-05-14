using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using System.Collections.Generic;

namespace ServerGuardMod.Common.Systems
{
    public class InspectUI : ModSystem
    {
        public static Player InspectTarget { get; set; } = null;

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;

            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "ServerGuard: Inspect UI",
                    delegate
                    {
                        if (InspectTarget != null && InspectTarget.active)
                        {
                            DrawInspectUI(Main.spriteBatch);
                        }
                        else
                        {
                            InspectTarget = null; // Target left or disconnected
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }

        private void DrawInspectUI(SpriteBatch spriteBatch)
        {
            int screenWidth = Main.screenWidth;
            int screenHeight = Main.screenHeight;

            int panelWidth = 850;
            int panelHeight = 400;
            int panelX = (screenWidth - panelWidth) / 2;
            int panelY = (screenHeight - panelHeight) / 2;

            // Draw Background Panel
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(20, 20, 40, 240));
            DrawBorder(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.Cyan);

            // Draw Title & Stats
            string title = $"Inspecting: {InspectTarget.name}";
            var font = FontAssets.MouseText.Value;
            Vector2 titleSize = font.MeasureString(title);
            Utils.DrawBorderString(spriteBatch, title, new Vector2(panelX + panelWidth / 2f - titleSize.X / 2f, panelY + 10), Color.Yellow);

            string stats = $"HP: {InspectTarget.statLife}/{InspectTarget.statLifeMax2}  |  Mana: {InspectTarget.statMana}/{InspectTarget.statManaMax2}";
            Utils.DrawBorderString(spriteBatch, stats, new Vector2(panelX + 20, panelY + 15), Color.Red);

            // Draw Close Button (Top Right)
            string closeText = "[X]";
            Vector2 closePos = new Vector2(panelX + panelWidth - 30, panelY + 10);
            Color closeColor = Color.Red;
            if (Main.MouseScreen.X > closePos.X && Main.MouseScreen.X < closePos.X + 20 &&
                Main.MouseScreen.Y > closePos.Y && Main.MouseScreen.Y < closePos.Y + 20)
            {
                closeColor = Color.White;
                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    InspectTarget = null;
                    return;
                }
            }
            Utils.DrawBorderString(spriteBatch, closeText, closePos, closeColor);

            // ----------------------------------------------------
            // Main Inventory (10 cols x 5 rows)
            // ----------------------------------------------------
            int startX = panelX + 20;
            int startY = panelY + 50;
            int slotSize = 46;
            int padding = 4;

            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    int index = row * 10 + col;
                    DrawItemSlot(spriteBatch, InspectTarget.inventory[index], startX + col * (slotSize + padding), startY + row * (slotSize + padding));
                }
            }

            // ----------------------------------------------------
            // Coins & Ammo (Cols 10 and 11)
            // ----------------------------------------------------
            int extraX = startX + 10 * (slotSize + padding) + 10;
            Utils.DrawBorderString(spriteBatch, "Coins", new Vector2(extraX, startY - 20), Color.Gold, 0.8f);
            for (int i = 0; i < 4; i++) DrawItemSlot(spriteBatch, InspectTarget.inventory[50 + i], extraX, startY + i * (slotSize + padding));

            int ammoX = extraX + slotSize + padding;
            Utils.DrawBorderString(spriteBatch, "Ammo", new Vector2(ammoX, startY - 20), Color.LightGray, 0.8f);
            for (int i = 0; i < 4; i++) DrawItemSlot(spriteBatch, InspectTarget.inventory[54 + i], ammoX, startY + i * (slotSize + padding));

            // ----------------------------------------------------
            // Armor & Accessories (10 slots) - Col 12
            // ----------------------------------------------------
            int armorX = ammoX + slotSize + padding + 15;
            Utils.DrawBorderString(spriteBatch, "Equips", new Vector2(armorX, startY - 20), Color.LightCyan, 0.8f);
            for (int i = 0; i < 10; i++)
            {
                // To fit 10 slots vertically, we need a smaller Y step or two columns. Let's do 2 columns: Armor and Vanity.
                DrawItemSlot(spriteBatch, InspectTarget.armor[i], armorX, startY + i * 32); // Slightly overlapping to fit vertically
            }

            // Vanity (10 slots) - Col 13
            int vanityX = armorX + 40;
            for (int i = 0; i < 10; i++)
            {
                DrawItemSlot(spriteBatch, InspectTarget.armor[i + 10], vanityX, startY + i * 32);
            }

            // Dyes (10 slots) - Col 14
            int dyeX = vanityX + 40;
            Utils.DrawBorderString(spriteBatch, "Dyes", new Vector2(dyeX, startY - 20), Color.Violet, 0.8f);
            for (int i = 0; i < 10; i++)
            {
                DrawItemSlot(spriteBatch, InspectTarget.dye[i], dyeX, startY + i * 32);
            }

            // ----------------------------------------------------
            // Misc Equips (5 slots) - Col 15
            // (Pet, Light Pet, Minecart, Mount, Hook)
            // ----------------------------------------------------
            int miscX = dyeX + slotSize + padding + 15;
            Utils.DrawBorderString(spriteBatch, "Misc", new Vector2(miscX, startY - 20), Color.Orange, 0.8f);
            for (int i = 0; i < 5; i++)
            {
                DrawItemSlot(spriteBatch, InspectTarget.miscEquips[i], miscX, startY + i * (slotSize + padding));
            }

            // Total Value Info
            long totalValue = 0;
            for(int i = 0; i < 58; i++) {
                if (InspectTarget.inventory[i].type > 0)
                    totalValue += (long)InspectTarget.inventory[i].value * InspectTarget.inventory[i].stack;
            }
            
            int p = (int)(totalValue / 1000000);
            int g = (int)((totalValue % 1000000) / 10000);
            int s = (int)((totalValue % 10000) / 100);
            int c = (int)(totalValue % 100);
            
            string valStr = $"Total Est. Value (Inventory): {p}P {g}G {s}S {c}C";
            Utils.DrawBorderString(spriteBatch, valStr, new Vector2(panelX + 20, panelY + panelHeight - 30), Color.Lime);
        }

        private void DrawItemSlot(SpriteBatch sb, Item item, int x, int y)
        {
            // Draw slot background
            sb.Draw(TextureAssets.InventoryBack.Value, new Vector2(x, y), null, new Color(100, 100, 100, 180), 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

            if (item != null && item.type > 0)
            {
                Main.inventoryScale = 0.85f; // Scale it down a bit to fit
                ItemSlot.Draw(sb, ref item, ItemSlot.Context.InventoryItem, new Vector2(x, y));

                // Hover tooltip
                if (Main.MouseScreen.X >= x && Main.MouseScreen.X <= x + 40 &&
                    Main.MouseScreen.Y >= y && Main.MouseScreen.Y <= y + 40)
                {
                    Main.HoverItem = item.Clone();
                    Main.hoverItemName = item.Name;
                }
            }
        }

        private void DrawBorder(SpriteBatch sb, int x, int y, int w, int h, Color c)
        {
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, w, 2), c);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y + h - 2, w, 2), c);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, 2, h), c);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x + w - 2, y, 2, h), c);
        }
    }
}
