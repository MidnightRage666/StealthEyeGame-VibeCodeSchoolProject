using System;
using System.Numerics;
using System.Windows.Forms;
using StealthEyeGame.Core;
using StealthEyeGame.Rendering;

namespace StealthEyeGame
{
    /// <summary>
    /// Host-Fenster des Spiels.
    /// Das Spiel verwendet intern immer 1920x1080.
    /// Das Fenster läuft randlos im Vollbild und wird automatisch
    /// an die Auflösung des Monitors angepasst.
    /// </summary>
    public class MainForm : Form
    {
        private readonly GameManager _gameManager = new();
        private readonly Renderer _renderer = new();
        private readonly System.Windows.Forms.Timer _loopTimer;

        // Mausposition in der INTERNEN 1920x1080-Spielwelt.
        private Vector2 _mouseFieldPos = Vector2.Zero;

        private DateTime _lastTick = DateTime.UtcNow;

        public MainForm()
        {
            Text = "Augen im Dunkeln - Stealth";

            // ==========================================
            // INTERNES SPIELFORMAT
            // ==========================================
            // Das Spiel zeichnet intern immer in 1920x1080.
            // Die tatsächliche Monitorauflösung wird nur
            // für die Darstellung verwendet.

            ClientSize = new System.Drawing.Size(
                GameConstants.WindowWidth,
                GameConstants.WindowHeight);

            // ==========================================
            // VOLLBILD
            // ==========================================

            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;

            // Wichtig für saubere Pixel-/Koordinatenberechnung.
            AutoScaleMode = AutoScaleMode.None;

            StartPosition = FormStartPosition.CenterScreen;

            DoubleBuffered = true;
            BackColor = System.Drawing.Color.Black;

            // ==========================================
            // EINGABE
            // ==========================================

            MouseMove += OnMouseMove;
            MouseClick += OnMouseClick;
            Paint += OnPaint;

            KeyPreview = true;
            KeyDown += OnKeyDown;

            // ==========================================
            // GAME LOOP
            // ==========================================

            _loopTimer =
                new System.Windows.Forms.Timer
                {
                    Interval = GameConstants.TimerIntervalMs
                };

            _loopTimer.Tick += OnTick;
            _loopTimer.Start();
        }

        // =========================================================
        // MOUSE
        // =========================================================

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            _mouseFieldPos =
                _renderer.ScreenToField(
                    e.X,
                    e.Y,
                    ClientSize.Width,
                    ClientSize.Height);
        }

        // =========================================================
        // TASTATUREINGABEN
        // =========================================================

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (_gameManager.State == GameState.Playing ||
                    _gameManager.State == GameState.Paused)
                {
                    _gameManager.TogglePause();
                }

                return;
            }

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

            if (e.KeyCode == Keys.ShiftKey &&
                _gameManager.State == GameState.Playing)
            {
                _gameManager.TryDash(_mouseFieldPos);
            }

            if (e.KeyCode == Keys.Space &&
                _gameManager.State == GameState.Playing)
            {
                _gameManager.ToggleMovementPause();
            }
        }

        // =========================================================
        // MAUSKLICKS
        // =========================================================

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            // Bildschirmkoordinaten -> interne Spielkoordinaten
            Vector2 gameMouse =
                _renderer.ScreenToVirtual(
                    e.X,
                    e.Y,
                    ClientSize.Width,
                    ClientSize.Height);

            System.Drawing.Point gamePoint =
                new System.Drawing.Point(
                    (int)gameMouse.X,
                    (int)gameMouse.Y);

            switch (_gameManager.State)
            {
                case GameState.MainMenu:

                    if (_renderer.MainMenuStartButtonRect.Contains(gamePoint))
                    {
                        _gameManager.ContinueGame();
                    }
                    else if (_renderer.MainMenuNewGameButtonRect.Contains(gamePoint))
                    {
                        _gameManager.OpenNewGameConfirmation();
                    }
                    else if (_renderer.MainMenuLoadButtonRect.Contains(gamePoint))
                    {
                        _gameManager.OpenLoadMenu();
                    }
                    else if (_renderer.MainMenuExitButtonRect.Contains(gamePoint))
                    {
                        Application.Exit();
                    }

                    break;

                case GameState.NewGameConfirmation:

                    if (_renderer.NewGameConfirmButtonRect.Contains(gamePoint))
                    {
                        _gameManager.ConfirmNewGame();
                    }
                    else if (_renderer.NewGameCancelButtonRect.Contains(gamePoint))
                    {
                        _gameManager.CancelNewGame();
                    }

                    break;

                case GameState.LoadMenu:

                    if (_renderer.LoadSlot1ButtonRect.Contains(gamePoint))
                    {
                        _gameManager.LoadGame(1);
                    }
                    else if (_renderer.LoadSlot2ButtonRect.Contains(gamePoint))
                    {
                        _gameManager.LoadGame(2);
                    }
                    else if (_renderer.LoadSlot3ButtonRect.Contains(gamePoint))
                    {
                        _gameManager.LoadGame(3);
                    }
                    else if (_renderer.LoadBackButtonRect.Contains(gamePoint))
                    {
                        _gameManager.GoToMainMenu();
                    }

                    break;

                case GameState.GameOver:

                    if (_renderer.GameOverShopButtonRect.Contains(gamePoint))
                    {
                        _gameManager.OpenShop();
                    }
                    else if (_renderer.GameOverRestartButtonRect.Contains(gamePoint))
                    {
                        _gameManager.RestartAfterGameOver();
                    }

                    break;

                case GameState.Shop:

                    foreach (var (itemType, rect) in _renderer.ShopBuyButtonRects)
                    {
                        if (rect.Contains(gamePoint))
                        {
                            _gameManager.BuyItem(itemType);
                            return;
                        }
                    }

                    if (_renderer.ShopContinueButtonRect.Contains(gamePoint))
                    {
                        _gameManager.RestartAfterGameOver();
                    }

                    break;

                case GameState.Playing:

                    if (_renderer.DynamiteButtonRect.Contains(gamePoint))
                    {
                        _gameManager.ToggleDynamitePlacementMode();
                    }
                    else if (_gameManager.IsPlacingDynamite &&
                             gamePoint.Y > GameConstants.TopBarHeight)
                    {
                        _gameManager.TryPlaceDynamiteAt(_mouseFieldPos);
                    }

                    break;

                case GameState.Paused:

                    if (_renderer.PauseResumeButtonRect.Contains(gamePoint))
                    {
                        _gameManager.TogglePause();
                    }
                    else if (_renderer.PauseSaveButtonRect.Contains(gamePoint))
                    {
                        _gameManager.OpenSaveMenu();
                    }
                    else if (_renderer.PauseMainMenuButtonRect.Contains(gamePoint))
                    {
                        _gameManager.GoToMainMenu();
                    }
                    else if (_renderer.PauseExitButtonRect.Contains(gamePoint))
                    {
                        Application.Exit();
                    }

                    break;

                case GameState.SaveMenu:

                    if (_renderer.SaveSlot1ButtonRect.Contains(gamePoint))
                    {
                        _gameManager.SaveGame(1);
                        _gameManager.CloseSaveMenu();
                    }
                    else if (_renderer.SaveSlot2ButtonRect.Contains(gamePoint))
                    {
                        _gameManager.SaveGame(2);
                        _gameManager.CloseSaveMenu();
                    }
                    else if (_renderer.SaveSlot3ButtonRect.Contains(gamePoint))
                    {
                        _gameManager.SaveGame(3);
                        _gameManager.CloseSaveMenu();
                    }
                    else if (_renderer.SaveBackButtonRect.Contains(gamePoint))
                    {
                        _gameManager.CloseSaveMenu();
                    }

                    break;
            }
        }

        // =========================================================
        // GAME LOOP
        // =========================================================

        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;

            float dt =
                (float)(now - _lastTick).TotalSeconds;

            _lastTick = now;

            dt = Math.Min(dt, 0.05f);

            _gameManager.Update(
                dt,
                _mouseFieldPos);

            Invalidate();
        }

        // =========================================================
        // RENDERING
        // =========================================================

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            _renderer.Draw(
                e.Graphics,
                _gameManager,
                new System.Drawing.PointF(
                    _mouseFieldPos.X,
                    _mouseFieldPos.Y),
                ClientSize.Width,
                ClientSize.Height);
        }
    }
}