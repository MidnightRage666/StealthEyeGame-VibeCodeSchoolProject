using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using StealthEyeGame.Core;
using StealthEyeGame.Entities;
using StealthEyeGame.Levels;
using StealthEyeGame.Systems;

namespace StealthEyeGame.Rendering
{
    /// <summary>
    /// Reine Darstellungsschicht - liest nur aus dem GameManager, verändert nie
    /// den Spielzustand. Zeichnet Wände (inkl. angeknackster), Augen samt
    /// zustandsabhängiger Darstellung und Sichtkegel, Spielerpunkt, Dynamit,
    /// Explosionen, UI-Leiste sowie Game-Over- und Shop-Bildschirme.
    /// </summary>
    public class Renderer
    {
        private static readonly Color BackgroundColor = Color.FromArgb(255, 14, 14, 18);
        private static readonly Color WallColor = Color.FromArgb(255, 210, 210, 220);
        private static readonly Color WallShadow = Color.FromArgb(255, 150, 150, 165);
        private static readonly Color CrackedWallColor = Color.FromArgb(255, 175, 150, 110);
        private static readonly Color CrackedWallCrackColor = Color.FromArgb(255, 80, 55, 30);
        private static readonly Color EyeShellColor = Color.FromArgb(255, 235, 235, 240);
        private static readonly Color PupilColor = Color.FromArgb(255, 10, 10, 12);
        private static readonly Color PlayerGlowColor = Color.FromArgb(255, 120, 230, 255);
        private static readonly Color ExitGlowColor = Color.FromArgb(255, 255, 210, 80);
        private static readonly Color DynamiteColor = Color.FromArgb(255, 200, 60, 40);
        private static readonly Color ExplosionColor = Color.FromArgb(255, 255, 160, 40);
        private static readonly Color PanelColor = Color.FromArgb(255, 28, 28, 35);
        private static readonly Color ButtonColor = Color.FromArgb(255, 60, 130, 220);
        private static readonly Color ButtonDisabledColor = Color.FromArgb(255, 70, 70, 78);

        // Klick-Flächen, die vom MainForm für Hit-Tests abgefragt werden.
        public Rectangle GameOverShopButtonRect { get; private set; }
        public Rectangle GameOverRestartButtonRect { get; private set; }
        public Rectangle DynamiteButtonRect { get; private set; }
        public Rectangle ShopContinueButtonRect { get; private set; }
        public List<(ShopItemType ItemType, Rectangle Rect)> ShopBuyButtonRects { get; } = new();

        public void Draw(Graphics g, GameManager gm, PointF mouseFieldPos)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackgroundColor);

            if (gm.State == GameState.Shop)
            {
                DrawShopScreen(g, gm);
                return;
            }

            var state = g.Save();
            g.TranslateTransform(0, GameConstants.TopBarHeight);

            DrawExit(g, gm.CurrentLevel);
            DrawWalls(g, gm.CurrentLevel);
            DrawEyes(g, gm.CurrentLevel);
            DrawDynamite(g, gm);
            DrawExplosions(g, gm);
            DrawPlayer(g, gm.Player);

            if (gm.IsPlacingDynamite)
            {
                DrawPlacementPreview(g, mouseFieldPos);
            }

            g.Restore(state);

            DrawTopBar(g, gm);

            if (gm.State == GameState.GameOver)
            {
                DrawGameOverOverlay(g, gm);
            }
            else if (gm.State == GameState.LevelTransition)
            {
                DrawTransitionOverlay(g, gm);
            }
        }

        // ------------------------------------------------------------------
        // Spielfeld
        // ------------------------------------------------------------------

        private void DrawWalls(Graphics g, Level level)
        {
            using var solidFill = new SolidBrush(WallColor);
            using var solidEdge = new Pen(WallShadow, 1.5f);
            using var crackedFill = new SolidBrush(CrackedWallColor);
            using var crackedEdge = new Pen(CrackedWallCrackColor, 1.5f);
            using var crackPen = new Pen(CrackedWallCrackColor, 2f);

            foreach (var (rect, type) in level.EnumerateWallsForRender())
            {
                if (type == WallType.Solid)
                {
                    g.FillRectangle(solidFill, rect);
                    g.DrawRectangle(solidEdge, rect.X, rect.Y, rect.Width, rect.Height);
                }
                else // Cracked
                {
                    g.FillRectangle(crackedFill, rect);
                    g.DrawRectangle(crackedEdge, rect.X, rect.Y, rect.Width, rect.Height);
                    // Zwei diagonale "Sprünge", damit angeknackste Wände sofort erkennbar sind.
                    g.DrawLine(crackPen, rect.X + rect.Width * 0.2f, rect.Y, rect.X + rect.Width * 0.55f, rect.Y + rect.Height * 0.5f);
                    g.DrawLine(crackPen, rect.X + rect.Width * 0.55f, rect.Y + rect.Height * 0.5f, rect.X + rect.Width * 0.3f, rect.Y + rect.Height);
                    g.DrawLine(crackPen, rect.X + rect.Width * 0.55f, rect.Y + rect.Height * 0.5f, rect.X + rect.Width * 0.9f, rect.Y + rect.Height * 0.65f);
                }
            }
        }

        private void DrawExit(Graphics g, Level level)
        {
            var r = level.ExitRect;
            using var glow = new PathGradientBrush(new[]
            {
                new PointF(r.X - 14, r.Y - 14), new PointF(r.X + r.Width + 14, r.Y - 14),
                new PointF(r.X + r.Width + 14, r.Y + r.Height + 14), new PointF(r.X - 14, r.Y + r.Height + 14)
            })
            {
                CenterColor = Color.FromArgb(160, ExitGlowColor),
                SurroundColors = new[] { Color.FromArgb(0, ExitGlowColor) }
            };
            g.FillEllipse(glow, r.X - 14, r.Y - 14, r.Width + 28, r.Height + 28);

            using var brush = new SolidBrush(ExitGlowColor);
            g.FillRectangle(brush, r);
            using var pen = new Pen(Color.White, 1.5f);
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        }

        private void DrawPlayer(Graphics g, Player player)
        {
            float glowRadius = player.Radius * 4f;
            using var glow = new GraphicsPath();
            glow.AddEllipse(player.Position.X - glowRadius, player.Position.Y - glowRadius, glowRadius * 2, glowRadius * 2);
            using var glowBrush = new PathGradientBrush(glow)
            {
                CenterColor = Color.FromArgb(140, PlayerGlowColor),
                SurroundColors = new[] { Color.FromArgb(0, PlayerGlowColor) }
            };
            g.FillPath(glowBrush, glow);

            using var coreBrush = new SolidBrush(player.IsSpottedThisFrame ? Color.FromArgb(255, 255, 120, 120) : PlayerGlowColor);
            float r = player.Radius;
            g.FillEllipse(coreBrush, player.Position.X - r, player.Position.Y - r, r * 2, r * 2);
        }

        private void DrawDynamite(Graphics g, GameManager gm)
        {
            foreach (var dyn in gm.PlacedDynamite)
            {
                float blink = 0.5f + 0.5f * MathF.Sin(dyn.FuseProgress * MathF.PI * 10f);
                using var brush = new SolidBrush(Color.FromArgb((int)(140 + 100 * blink), DynamiteColor));
                float radius = 8f;
                g.FillEllipse(brush, dyn.Position.X - radius, dyn.Position.Y - radius, radius * 2, radius * 2);
                using var pen = new Pen(Color.White, 1.2f);
                g.DrawEllipse(pen, dyn.Position.X - radius, dyn.Position.Y - radius, radius * 2, radius * 2);

                int secondsLeft = Math.Max(1, (int)MathF.Ceiling(dyn.FuseTimeRemaining));
                using var font = new Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.White);
                var text = secondsLeft.ToString();
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, textBrush, dyn.Position.X - size.Width / 2f, dyn.Position.Y - size.Height / 2f - 14f);
            }
        }

        private void DrawExplosions(Graphics g, GameManager gm)
        {
            foreach (var explosion in gm.ActiveExplosions)
            {
                float t = explosion.Progress; // 0..1
                float radius = explosion.Radius * (0.3f + 0.7f * t);
                int alpha = (int)(200 * (1f - t));
                using var brush = new SolidBrush(Color.FromArgb(Math.Max(0, alpha), ExplosionColor));
                g.FillEllipse(brush, explosion.Position.X - radius, explosion.Position.Y - radius, radius * 2, radius * 2);
                using var pen = new Pen(Color.FromArgb(Math.Max(0, alpha), Color.White), 2f);
                g.DrawEllipse(pen, explosion.Position.X - radius, explosion.Position.Y - radius, radius * 2, radius * 2);
            }
        }

        private void DrawPlacementPreview(Graphics g, PointF mouseFieldPos)
        {
            using var pen = new Pen(Color.FromArgb(200, DynamiteColor), 1.5f) { DashStyle = DashStyle.Dash };
            float radius = GameConstants.ExplosionBaseRadius;
            g.DrawEllipse(pen, mouseFieldPos.X - radius, mouseFieldPos.Y - radius, radius * 2, radius * 2);
            using var dotBrush = new SolidBrush(DynamiteColor);
            g.FillEllipse(dotBrush, mouseFieldPos.X - 6, mouseFieldPos.Y - 6, 12, 12);
        }

        // ------------------------------------------------------------------
        // Augen
        // ------------------------------------------------------------------

        private void DrawEyes(Graphics g, Level level)
        {
            foreach (var eye in level.Eyes)
            {
                DrawVisionCone(g, level, eye);
            }
            foreach (var eye in level.Eyes)
            {
                DrawEyeShape(g, eye);
            }
        }

        private static (Color Iris, Color Cone) ColorsForState(EyeState state)
        {
            return state switch
            {
                EyeState.Idle => (Color.FromArgb(255, 70, 160, 210), Color.FromArgb(65, 90, 170, 220)),
                EyeState.Investigation => (Color.FromArgb(255, 235, 165, 55), Color.FromArgb(85, 235, 165, 55)),
                EyeState.Alert => (Color.FromArgb(255, 220, 45, 45), Color.FromArgb(100, 220, 50, 50)),
                EyeState.Searching => (Color.FromArgb(255, 235, 210, 60), Color.FromArgb(90, 235, 210, 60)),
                EyeState.Returning => (Color.FromArgb(255, 130, 150, 160), Color.FromArgb(60, 130, 150, 160)),
                _ => (Color.FromArgb(255, 70, 160, 210), Color.FromArgb(65, 90, 170, 220))
            };
        }

        private void DrawVisionCone(Graphics g, Level level, Eye eye)
        {
            int rays = GameConstants.VisionRayCount;
            var points = new PointF[rays + 2];
            points[0] = new PointF(eye.CurrentPosition.X, eye.CurrentPosition.Y);

            float start = eye.GazeAngle - eye.VisionHalfAngle;
            float step = (eye.VisionHalfAngle * 2f) / rays;

            for (int i = 0; i <= rays; i++)
            {
                float angle = start + step * i;
                float dist = level.RaycastDistance(eye.CurrentPosition, angle, eye.VisionRange);
                points[i + 1] = new PointF(
                    eye.CurrentPosition.X + MathF.Cos(angle) * dist,
                    eye.CurrentPosition.Y + MathF.Sin(angle) * dist);
            }

            var (_, coneColor) = ColorsForState(eye.State);
            using var brush = new SolidBrush(coneColor);
            g.FillPolygon(brush, points);
        }

        private void DrawEyeShape(Graphics g, Eye eye)
        {
            const float eyeWidth = 30f;
            const float eyeHeight = 18f;
            float x = eye.CurrentPosition.X;
            float y = eye.CurrentPosition.Y;

            var (irisColor, _) = ColorsForState(eye.State);

            // Leichtes Leuchten im Alert-Zustand, damit er sofort ins Auge fällt (Wortspiel beabsichtigt).
            if (eye.State == EyeState.Alert)
            {
                using var glowBrush = new SolidBrush(Color.FromArgb(70, irisColor));
                g.FillEllipse(glowBrush, x - eyeWidth, y - eyeHeight, eyeWidth * 2, eyeHeight * 2);
            }

            using var shellBrush = new SolidBrush(EyeShellColor);
            using var outline = new Pen(Color.FromArgb(255, 40, 40, 45), 2f);
            var eyeRect = new RectangleF(x - eyeWidth / 2, y - eyeHeight / 2, eyeWidth, eyeHeight);
            g.FillEllipse(shellBrush, eyeRect);
            g.DrawEllipse(outline, eyeRect);

            float irisRadius = 6.5f;
            float pupilOffsetRange = 5.5f;
            float ox = MathF.Cos(eye.GazeAngle) * pupilOffsetRange;
            float oy = MathF.Sin(eye.GazeAngle) * pupilOffsetRange * 0.55f;

            using var irisBrush = new SolidBrush(irisColor);
            g.FillEllipse(irisBrush, x + ox - irisRadius, y + oy - irisRadius, irisRadius * 2, irisRadius * 2);

            using var pupilBrush = new SolidBrush(PupilColor);
            float pupilRadius = 3f;
            g.FillEllipse(pupilBrush, x + ox - pupilRadius, y + oy - pupilRadius, pupilRadius * 2, pupilRadius * 2);
        }

        // ------------------------------------------------------------------
        // UI: Top-Leiste
        // ------------------------------------------------------------------

        private void DrawTopBar(Graphics g, GameManager gm)
        {
            using var barBrush = new SolidBrush(Color.FromArgb(255, 24, 24, 30));
            g.FillRectangle(barBrush, 0, 0, GameConstants.CanvasWidth, GameConstants.TopBarHeight);

            float barW = 170f, barH = 16f, barX = 12f, barY = (GameConstants.TopBarHeight - barH) / 2f;
            using var hpBg = new SolidBrush(Color.FromArgb(255, 55, 55, 60));
            g.FillRectangle(hpBg, barX, barY, barW, barH);

            float hpRatio = Math.Max(0, gm.Player.HP / gm.Player.MaxHP);
            Color hpColor = hpRatio > 0.5f ? Color.FromArgb(255, 90, 200, 110)
                            : hpRatio > 0.25f ? Color.FromArgb(255, 230, 190, 60)
                            : Color.FromArgb(255, 220, 70, 70);
            using var hpFg = new SolidBrush(hpColor);
            g.FillRectangle(hpFg, barX, barY, barW * hpRatio, barH);
            using var hpOutline = new Pen(Color.FromArgb(255, 200, 200, 200), 1f);
            g.DrawRectangle(hpOutline, barX, barY, barW, barH);

            using var font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString($"HP {gm.Player.HP:0}/{gm.Player.MaxHP:0}", font, textBrush, barX + 4, barY - 1, StringFormat.GenericDefault);

            float cursorX = barX + barW + 14f;

            string levelText = $"Level {gm.LevelNumber}";
            g.DrawString(levelText, font, textBrush, cursorX, barY - 1);
            cursorX += g.MeasureString(levelText, font).Width + 16f;

            using var coinBrush = new SolidBrush(Color.FromArgb(255, 255, 210, 80));
            string coinText = $"Coins: {gm.Progress.Coins}";
            g.DrawString(coinText, font, coinBrush, cursorX, barY - 1);
            cursorX += g.MeasureString(coinText, font).Width + 16f;

            // Dynamit-Button (Klickfläche für Platzier-Modus)
            string dynText = $"Dynamit x{gm.Progress.DynamiteOwned}";
            var dynSize = g.MeasureString(dynText, font);
            var dynRect = new Rectangle((int)cursorX, (int)(barY - 4), (int)dynSize.Width + 16, (int)barH + 8);
            DynamiteButtonRect = dynRect;

            bool dynAvailable = gm.Progress.DynamiteOwned > 0;
            Color dynBg = gm.IsPlacingDynamite ? Color.FromArgb(255, 200, 80, 40)
                         : dynAvailable ? Color.FromArgb(255, 70, 70, 85) : ButtonDisabledColor;
            using var dynBrush = new SolidBrush(dynBg);
            g.FillRectangle(dynBrush, dynRect);
            using var dynPen = new Pen(Color.FromArgb(255, 200, 200, 200), 1f);
            g.DrawRectangle(dynPen, dynRect);
            g.DrawString(dynText, font, textBrush, dynRect.X + 8, dynRect.Y + 4);

            // Status rechts
            string status = gm.PlayerIsSpotted ? "STATUS: ENTDECKT!" : "STATUS: VERSTECKT";
            using var statusBrush = new SolidBrush(gm.PlayerIsSpotted ? Color.FromArgb(255, 255, 90, 90) : Color.FromArgb(255, 140, 220, 150));
            var statusSize = g.MeasureString(status, font);
            g.DrawString(status, font, statusBrush, GameConstants.CanvasWidth - statusSize.Width - 14, barY - 1);
        }

        // ------------------------------------------------------------------
        // Overlays
        // ------------------------------------------------------------------

        private void DrawGameOverOverlay(Graphics g, GameManager gm)
        {
            using var overlay = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
            g.FillRectangle(overlay, 0, 0, GameConstants.CanvasWidth, GameConstants.WindowHeight);

            float cx = GameConstants.CanvasWidth / 2f;
            float cy = GameConstants.WindowHeight / 2f;

            using var titleFont = new Font("Segoe UI", 32f, System.Drawing.FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.FromArgb(255, 230, 60, 60));
            string title = "GAME OVER";
            var titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, titleBrush, cx - titleSize.Width / 2f, cy - 130);

            using var subFont = new Font("Segoe UI", 12.5f);
            using var subBrush = new SolidBrush(Color.White);
            string sub1 = $"Erreicht: Level {gm.LevelNumber}";
            string sub2 = $"Coins verdient: {gm.RunCoinsEarned}      Gesamt-Coins: {gm.Progress.Coins}";
            var s1 = g.MeasureString(sub1, subFont);
            var s2 = g.MeasureString(sub2, subFont);
            g.DrawString(sub1, subFont, subBrush, cx - s1.Width / 2f, cy - 78);
            g.DrawString(sub2, subFont, subBrush, cx - s2.Width / 2f, cy - 54);

            GameOverShopButtonRect = new Rectangle((int)(cx - 190), (int)cy, 170, 46);
            GameOverRestartButtonRect = new Rectangle((int)(cx + 20), (int)cy, 170, 46);

            DrawButton(g, GameOverShopButtonRect, "SHOP", true);
            DrawButton(g, GameOverRestartButtonRect, "NEUER RUN", true);
        }

        private void DrawTransitionOverlay(Graphics g, GameManager gm)
        {
            using var overlay = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
            g.FillRectangle(overlay, 0, 0, GameConstants.CanvasWidth, GameConstants.WindowHeight);

            using var font = new Font("Segoe UI", 20f, System.Drawing.FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(255, 255, 210, 80));
            string text = $"Level {gm.LevelNumber} geschafft! +{GameConstants.CoinsPerLevelComplete} Coins";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush,
                GameConstants.CanvasWidth / 2f - size.Width / 2f,
                GameConstants.WindowHeight / 2f - size.Height / 2f);
        }

        // ------------------------------------------------------------------
        // Shop
        // ------------------------------------------------------------------

        private void DrawShopScreen(Graphics g, GameManager gm)
        {
            ShopBuyButtonRects.Clear();

            using var panelBrush = new SolidBrush(PanelColor);
            var panelRect = new Rectangle(GameConstants.CanvasWidth / 2 - 260, 40, 520, GameConstants.WindowHeight - 80);
            g.FillRectangle(panelBrush, panelRect);
            using var panelPen = new Pen(Color.FromArgb(255, 90, 90, 100), 2f);
            g.DrawRectangle(panelPen, panelRect);

            using var titleFont = new Font("Segoe UI", 22f, System.Drawing.FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.White);
            string title = "SHOP";
            var titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, titleBrush, panelRect.X + panelRect.Width / 2f - titleSize.Width / 2f, panelRect.Y + 16);

            using var coinFont = new Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold);
            using var coinBrush = new SolidBrush(Color.FromArgb(255, 255, 210, 80));
            string coinText = $"Coins: {gm.Progress.Coins}";
            var coinSize = g.MeasureString(coinText, coinFont);
            g.DrawString(coinText, coinFont, coinBrush, panelRect.X + panelRect.Width / 2f - coinSize.Width / 2f, panelRect.Y + 54);

            float itemY = panelRect.Y + 96;
            const float itemHeight = 78f;
            using var nameFont = new Font("Segoe UI", 13f, System.Drawing.FontStyle.Bold);
            using var descFont = new Font("Segoe UI", 9.5f);
            using var textBrush = new SolidBrush(Color.White);
            using var descBrush = new SolidBrush(Color.FromArgb(255, 190, 190, 200));
            using var ownedBrush = new SolidBrush(Color.FromArgb(255, 140, 220, 150));

            foreach (var item in ShopCatalog.Items)
            {
                var rowRect = new RectangleF(panelRect.X + 16, itemY, panelRect.Width - 32, itemHeight - 10);
                using var rowBrush = new SolidBrush(Color.FromArgb(255, 38, 38, 46));
                g.FillRectangle(rowBrush, rowRect);

                g.DrawString(item.Name, nameFont, textBrush, rowRect.X + 12, rowRect.Y + 8);
                g.DrawString(item.Description, descFont, descBrush, rowRect.X + 12, rowRect.Y + 30);
                g.DrawString(item.GetOwnedLabel(gm.Progress), descFont, ownedBrush, rowRect.X + 12, rowRect.Y + 48);

                bool canBuy = item.CanPurchase(gm.Progress);
                var buttonRect = new Rectangle((int)(rowRect.Right - 118), (int)(rowRect.Y + rowRect.Height / 2 - 18), 106, 36);
                ShopBuyButtonRects.Add((item.ItemType, buttonRect));

                string label = $"{item.GetPrice(gm.Progress)} Coins";
                DrawButton(g, buttonRect, label, canBuy);

                itemY += itemHeight;
            }

            ShopContinueButtonRect = new Rectangle(panelRect.X + panelRect.Width / 2 - 90, panelRect.Bottom - 60, 180, 42);
            DrawButton(g, ShopContinueButtonRect, "WEITER", true);
        }

        private void DrawButton(Graphics g, Rectangle rect, string label, bool enabled)
        {
            using var brush = new SolidBrush(enabled ? ButtonColor : ButtonDisabledColor);
            g.FillRectangle(brush, rect);
            using var pen = new Pen(Color.White, 1.2f);
            g.DrawRectangle(pen, rect);

            using var font = new Font("Segoe UI", 10.5f, System.Drawing.FontStyle.Bold);
            using var textBrush = new SolidBrush(enabled ? Color.White : Color.FromArgb(255, 170, 170, 175));
            var size = g.MeasureString(label, font);
            g.DrawString(label, font, textBrush,
                rect.X + rect.Width / 2f - size.Width / 2f,
                rect.Y + rect.Height / 2f - size.Height / 2f);
        }
    }
}
