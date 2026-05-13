using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace ServerGuardMod.Common.Systems
{
    public class LoginUI : ModSystem
    {
        private enum ActiveField { None, Username, Password }
        private ActiveField _activeField = ActiveField.None;

        public override void PostUpdateEverything()
        {
            if (Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient) return;
            if (!ClientLoginSystem.ShowingLogin && !ClientLoginSystem.ShowingRegister) return;

            Main.LocalPlayer.frozen     = true;
            Main.LocalPlayer.noBuilding = true;
        }

        public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
        {
            if (Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient) return;
            if (!ClientLoginSystem.ShowingLogin && !ClientLoginSystem.ShowingRegister) return;

            int idx = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer(
                "ServerGuard: Login Screen",
                () => { DrawLoginScreen(Main.spriteBatch); return true; },
                InterfaceScaleType.UI
            ));
        }

        private void DrawLoginScreen(SpriteBatch sb)
        {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;

            // Dark overlay
            sb.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 180));

            int bw = 400, bh = 320;
            int bx = (sw - bw) / 2;
            int by = (sh - bh) / 2;

            // Panel background
            sb.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(bx, by, bw, bh), new Color(20, 20, 50, 230));
            DrawBorder(sb, bx, by, bw, bh, Color.Gold);

            // Title
            string title = ClientLoginSystem.ShowingRegister
                ? "[ ServerGuard ] Register"
                : "[ ServerGuard ] Login";
            DrawCenteredText(sb, title, bx + bw / 2, by + 20, Color.Gold, 1.1f);

            // Username
            DrawCenteredText(sb, "Username:", bx + bw / 2, by + 70, Color.White);
            DrawInputField(sb, bx + 50, by + 90, bw - 100, 30,
                ClientLoginSystem.InputUsername, _activeField == ActiveField.Username);

            // Password
            DrawCenteredText(sb, "Password:", bx + bw / 2, by + 140, Color.White);
            string masked = new string('*', ClientLoginSystem.InputPassword.Length);
            DrawInputField(sb, bx + 50, by + 160, bw - 100, 30,
                masked, _activeField == ActiveField.Password);

            // Status
            Color sc = ClientLoginSystem.StatusMessage.StartsWith("Error") ? Color.Red : Color.Yellow;
            DrawCenteredText(sb, ClientLoginSystem.StatusMessage, bx + bw / 2, by + 205, sc, 0.85f);

            // Login / Register button
            string btnLabel = ClientLoginSystem.ShowingRegister ? "Create Account" : "Login";
            bool loginClicked = DrawButton(sb, bx + 50, by + 235, 130, 35, btnLabel, Color.DarkGreen);
            if (loginClicked)
            {
                if (ClientLoginSystem.ShowingRegister)
                    ClientLoginSystem.SendRegister(ClientLoginSystem.InputUsername, ClientLoginSystem.InputPassword);
                else
                    ClientLoginSystem.SendLogin(ClientLoginSystem.InputUsername, ClientLoginSystem.InputPassword);
            }

            // Toggle button
            string toggleLabel = ClientLoginSystem.ShowingRegister ? "Have account" : "New account";
            bool toggleClicked = DrawButton(sb, bx + bw - 180, by + 235, 130, 35, toggleLabel, Color.DarkBlue);
            if (toggleClicked)
            {
                ClientLoginSystem.ShowingRegister = !ClientLoginSystem.ShowingRegister;
                ClientLoginSystem.StatusMessage   = "";
            }

            DrawCenteredText(sb, "Click a field then type", bx + bw / 2, by + 285, Color.Gray, 0.75f);

            HandleInput(bx + 50, by + 90,  bw - 100, 30,
                        bx + 50, by + 160, bw - 100, 30);
        }

        private void DrawInputField(SpriteBatch sb, int x, int y, int w, int h, string text, bool active)
        {
            Color bg     = active ? new Color(40, 40, 80, 230) : new Color(30, 30, 60, 200);
            Color border = active ? Color.Cyan : Color.Gray;
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, w, h), bg);
            DrawBorder(sb, x, y, w, h, border);
            string display = text + (active && (Main.GameUpdateCount % 60 < 30) ? "|" : "");
            Utils.DrawBorderStringFourWay(sb, FontAssets.MouseText.Value, display,
                x + 8, y + 7, Color.White, Color.Black, Vector2.Zero, 0.9f);
        }

        private bool DrawButton(SpriteBatch sb, int x, int y, int w, int h, string text, Color color)
        {
            var mp    = Main.MouseScreen;
            bool over = mp.X >= x && mp.X <= x + w && mp.Y >= y && mp.Y <= y + h;
            Color c   = over ? color * 1.5f : color;
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, w, h), c);
            DrawBorder(sb, x, y, w, h, over ? Color.White : Color.Gray);
            float tx = x + w / 2f - FontAssets.MouseText.Value.MeasureString(text).X * 0.45f;
            float ty = y + h / 2f - 8;
            Utils.DrawBorderStringFourWay(sb, FontAssets.MouseText.Value, text,
                tx, ty, Color.White, Color.Black, Vector2.Zero, 0.9f);
            return over && Main.mouseLeft && Main.mouseLeftRelease;
        }

        private void DrawBorder(SpriteBatch sb, int x, int y, int w, int h, Color c)
        {
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, w, 2), c);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y + h - 2, w, 2), c);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, 2, h), c);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x + w - 2, y, 2, h), c);
        }

        private void DrawCenteredText(SpriteBatch sb, string text, int cx, int y, Color color, float scale = 1f)
        {
            var size = FontAssets.MouseText.Value.MeasureString(text) * scale;
            Utils.DrawBorderStringFourWay(sb, FontAssets.MouseText.Value, text,
                cx - size.X / 2f, y, color, Color.Black, Vector2.Zero, scale);
        }

        private void HandleInput(int ux, int uy, int uw, int uh,
                                  int px, int py, int pw, int ph)
        {
            var mouse = Main.MouseScreen;

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                if (mouse.X >= ux && mouse.X <= ux + uw && mouse.Y >= uy && mouse.Y <= uy + uh)
                    _activeField = ActiveField.Username;
                else if (mouse.X >= px && mouse.X <= px + pw && mouse.Y >= py && mouse.Y <= py + ph)
                    _activeField = ActiveField.Password;
                else
                    _activeField = ActiveField.None;
            }

            if (_activeField == ActiveField.None) return;

            Main.blockInput = true;

            foreach (char c in Main.GetInputText(""))
            {
                if (_activeField == ActiveField.Username && ClientLoginSystem.InputUsername.Length < 30)
                    ClientLoginSystem.InputUsername += c;
                else if (_activeField == ActiveField.Password && ClientLoginSystem.InputPassword.Length < 30)
                    ClientLoginSystem.InputPassword += c;
            }

            if (Main.inputText.IsKeyDown(Keys.Back))
            {
                if (_activeField == ActiveField.Username && ClientLoginSystem.InputUsername.Length > 0)
                    ClientLoginSystem.InputUsername = ClientLoginSystem.InputUsername[..^1];
                else if (_activeField == ActiveField.Password && ClientLoginSystem.InputPassword.Length > 0)
                    ClientLoginSystem.InputPassword = ClientLoginSystem.InputPassword[..^1];
            }

            if (Main.inputText.IsKeyDown(Keys.Enter))
            {
                if (ClientLoginSystem.ShowingRegister)
                    ClientLoginSystem.SendRegister(ClientLoginSystem.InputUsername, ClientLoginSystem.InputPassword);
                else
                    ClientLoginSystem.SendLogin(ClientLoginSystem.InputUsername, ClientLoginSystem.InputPassword);
            }
        }
    }
}
