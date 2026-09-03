using System;
using System.Numerics;
using System.Windows.Forms;
using StealthEyeGame.Core;
using StealthEyeGame.Rendering;

namespace StealthEyeGame
{
    /// <summary>
    /// Host-Fenster des Spiels. Verantwortlich für: Game-Loop-Timer, Erfassen der
    /// Mausposition, Weiterreichen an den GameManager, Neuzeichnen sowie sämtliche
    /// Maus-Klick-Interaktionen (Dynamit platzieren, Game-Over-Buttons, Shop).
    /// Enthält selbst keine Spiellogik.
    /// </summary>
    public class MainForm : Form
    {
        private readonly GameManager _gameManager = new();
        private readonly Renderer _renderer = new();
        private readonly System.Windows.Forms.Timer _loopTimer;

        private Vector2 _mouseFieldPos = Vector2.Zero;
        private DateTime _lastTick = DateTime.UtcNow;

        public MainForm()
        {
            Text = "Augen im Dunkeln - Stealth";
            ClientSize = new System.Drawing.Size(GameConstants.WindowWidth, GameConstants.WindowHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = System.Drawing.Color.Black;

            // Der Mauszeiger bleibt bewusst sichtbar (Cursor.Hide() wird NICHT aufgerufen) -
            // der Spielerpunkt wird zusätzlich neben dem OS-Cursor gerendert.
            MouseMove += OnMouseMove;
            MouseClick += OnMouseClick;
            Paint += OnPaint;

            // NEU: Tastatureingaben des Fensters empfangen
            KeyPreview = true;
            KeyDown += OnKeyDown;

            _loopTimer = new System.Windows.Forms.Timer { Interval = GameConstants.TimerIntervalMs };
            _loopTimer.Tick += OnTick;
            _loopTimer.Start();
        }

        // NEU: E-Taste toggelt ausschließlich den Dynamit-Platzierungsmodus
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.E &&
                _gameManager.State == GameState.Playing)
            {
                _gameManager.ToggleDynamitePlacementMode();
            }

            if (e.KeyCode == Keys.R &&
                _gameManager.State == GameState.Playing)
            {
                _gameManager.UseMedkit();
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            _mouseFieldPos = new Vector2(e.X, e.Y - GameConstants.TopBarHeight);
        }

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            switch (_gameManager.State)
            {
                case GameState.GameOver:
                    if (_renderer.GameOverShopButtonRect.Contains(e.Location))
                    {
                        _gameManager.OpenShop();
                    }
                    else if (_renderer.GameOverRestartButtonRect.Contains(e.Location))
                    {
                        _gameManager.RestartAfterGameOver();
                    }
                    break;

                case GameState.Shop:
                    foreach (var (itemType, rect) in _renderer.ShopBuyButtonRects)
                    {
                        if (rect.Contains(e.Location))
                        {
                            _gameManager.BuyItem(itemType);
                            return;
                        }
                    }
                    if (_renderer.ShopContinueButtonRect.Contains(e.Location))
                    {
                        _gameManager.RestartAfterGameOver();
                    }
                    break;

                case GameState.Playing:
                    if (_renderer.DynamiteButtonRect.Contains(e.Location))
                    {
                        _gameManager.ToggleDynamitePlacementMode();
                    }
                    else if (_gameManager.IsPlacingDynamite && e.Y > GameConstants.TopBarHeight)
                    {
                        _gameManager.TryPlaceDynamiteAt(_mouseFieldPos);
                    }
                    break;
            }
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            float dt = (float)(now - _lastTick).TotalSeconds;
            _lastTick = now;

            dt = Math.Min(dt, 0.05f);

            _gameManager.Update(dt, _mouseFieldPos);
            Invalidate();
        }

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            _renderer.Draw(e.Graphics, _gameManager, new System.Drawing.PointF(_mouseFieldPos.X, _mouseFieldPos.Y));
        }
    }
}