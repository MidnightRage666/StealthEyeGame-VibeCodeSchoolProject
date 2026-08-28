using System;
using System.Numerics;
using StealthEyeGame.Entities;
using StealthEyeGame.Levels;
using StealthEyeGame.Systems;

namespace StealthEyeGame.Core
{
    public enum GameState
    {
        Playing,
        LevelTransition,
        GameOver,
        Shop
    }

    /// <summary>
    /// Zentrale Spiellogik: hält Spieler, aktuelles Level, Dynamit/Explosionen und
    /// den dauerhaften Fortschritt (Coins/Upgrades). Verarbeitet Bewegung, Kollision,
    /// Augen-Update, Schaden/Slow, Levelwechsel, Game-Over und den Shop.
    /// Enthält absichtlich keinerlei Rendering- oder WinForms-Code, damit
    /// Spiellogik und Darstellung sauber getrennt bleiben.
    /// </summary>
    public class GameManager
    {
        public Player Player { get; private set; } = null!;
        public Level CurrentLevel { get; private set; } = null!;
        public int LevelNumber { get; private set; } = 1;
        public GameState State { get; private set; } = GameState.Playing;

        /// <summary>Dauerhafter Fortschritt (Coins, Inventar, Upgrades) - überlebt Runs.</summary>
        public PersistentProgress Progress { get; } = new PersistentProgress();

        /// <summary>Wie viele Coins in diesem konkreten Run bereits verdient wurden (nur für die Anzeige).</summary>
        public int RunCoinsEarned { get; private set; }

        public System.Collections.Generic.List<Dynamite> PlacedDynamite { get; } = new();
        public System.Collections.Generic.List<Explosion> ActiveExplosions { get; } = new();

        /// <summary>true, während der Spieler eine Position für Dynamit auswählt (Platzier-Modus).</summary>
        public bool IsPlacingDynamite { get; private set; }

        public bool PlayerIsSpotted { get; private set; }

        private float _totalTime;
        private float _transitionTimer;
        private const float TransitionDuration = 0.7f;

        private readonly Random _rng = new Random();

        public GameManager()
        {
            StartNewGame();
        }

        /// <summary>Startet einen komplett neuen Run bei Level 1. Coins/Inventar/Upgrades bleiben erhalten.</summary>
        public void StartNewGame()
        {
            LevelNumber = 1;
            _totalTime = 0f;
            RunCoinsEarned = 0;
            State = GameState.Playing;
            IsPlacingDynamite = false;
            PlacedDynamite.Clear();
            ActiveExplosions.Clear();

            CurrentLevel = LevelGenerator.Generate(LevelNumber, _rng);
            float maxHp = GameConstants.PlayerBaseMaxHP + Progress.BonusMaxHP;
            Player = new Player(CurrentLevel.PlayerStart, maxHp);
        }

        private void LoadLevel(int number)
        {
            LevelNumber = number;
            CurrentLevel = LevelGenerator.Generate(LevelNumber, _rng);
            Player.MoveToNewLevel(CurrentLevel.PlayerStart);
        }

        // ------------------------------------------------------------------
        // Game-Over / Shop / Neustart
        // ------------------------------------------------------------------

        /// <summary>Vom Game-Over-Bildschirm: öffnet den Shop, ohne den Run-Zustand zu verändern.</summary>
        public void OpenShop() => State = GameState.Shop;

        /// <summary>Kauft ein Item aus dem Katalog, falls genug Coins vorhanden und das Item verfügbar ist.</summary>
        public void BuyItem(ShopItemType itemType)
        {
            foreach (var item in ShopCatalog.Items)
            {
                if (item.ItemType != itemType) continue;
                if (item.CanPurchase(Progress))
                {
                    item.Purchase(Progress);
                }
                return;
            }
        }

        /// <summary>Beendet Shop/Game-Over und beginnt einen neuen Run (Coins/Inventar bleiben erhalten).</summary>
        public void RestartAfterGameOver() => StartNewGame();

        // ------------------------------------------------------------------
        // Dynamit
        // ------------------------------------------------------------------

        public void ToggleDynamitePlacementMode()
        {
            if (State != GameState.Playing) return;
            if (Progress.DynamiteOwned <= 0) { IsPlacingDynamite = false; return; }
            IsPlacingDynamite = !IsPlacingDynamite;
        }

        /// <summary>Platziert Dynamit an der gegebenen Feldposition, falls im Platzier-Modus und Dynamit vorhanden.</summary>
        public bool TryPlaceDynamiteAt(Vector2 fieldPosition)
        {
            if (!IsPlacingDynamite || State != GameState.Playing) return false;
            if (Progress.DynamiteOwned <= 0) { IsPlacingDynamite = false; return false; }
            if (fieldPosition.X < 0 || fieldPosition.Y < 0 ||
                fieldPosition.X > GameConstants.CanvasWidth || fieldPosition.Y > GameConstants.CanvasHeight)
                return false;

            Progress.DynamiteOwned -= 1;
            PlacedDynamite.Add(new Dynamite(fieldPosition, GameConstants.DynamiteFuseSeconds));
            IsPlacingDynamite = false;
            return true;
        }

        // ------------------------------------------------------------------
        // Noise-System
        // ------------------------------------------------------------------

        /// <summary>
        /// Benachrichtigt alle Augen im Radius über ein Geräusch. Zentrale Stelle für
        /// jede aktuelle und zukünftige Geräuschquelle (Explosionen, später z. B. Noise Maker).
        /// </summary>
        public void EmitNoise(NoiseEvent noise)
        {
            foreach (var eye in CurrentLevel.Eyes)
            {
                if (eye.IsDestroyed) continue;
                float dist = Vector2.Distance(eye.HomePosition, noise.Position);
                // Auch Augen, die sich gerade woanders befinden (z. B. schon unterwegs),
                // sollen reagieren können - Distanz wird daher zur aktuellen Position geprüft.
                float distCurrent = Vector2.Distance(eye.CurrentPosition, noise.Position);
                if (MathF.Min(dist, distCurrent) <= noise.Radius)
                {
                    eye.NotifyNoise(noise.Position);
                }
            }
        }

        // ------------------------------------------------------------------
        // Update
        // ------------------------------------------------------------------

        /// <summary>
        /// Ein Simulationsschritt. mouseFieldPos ist die aktuelle Mausposition,
        /// bereits in Spielfeld-Koordinaten (Canvas, ohne UI-Leiste) umgerechnet.
        /// </summary>
        public void Update(float dt, Vector2 mouseFieldPos)
        {
            switch (State)
            {
                case GameState.Playing:
                    UpdatePlaying(dt, mouseFieldPos);
                    break;

                case GameState.LevelTransition:
                    _transitionTimer += dt;
                    if (_transitionTimer >= TransitionDuration)
                    {
                        _transitionTimer = 0f;
                        LoadLevel(LevelNumber + 1);
                        State = GameState.Playing;
                    }
                    break;

                case GameState.GameOver:
                case GameState.Shop:
                    // Wartet auf Eingabe des Spielers (Buttons/Klicks werden von außen verarbeitet).
                    break;
            }
        }

        private void UpdatePlaying(float dt, Vector2 mouseFieldPos)
        {
            _totalTime += dt;

            MovePlayerTowards(mouseFieldPos, dt);
            UpdateDynamiteAndExplosions(dt);

            float bestDamage = 0f;
            float bestSlow = 1f;
            bool spotted = false;

            foreach (var eye in CurrentLevel.Eyes)
            {
                eye.Update(dt, Player.Position, CurrentLevel.CollidesWithWall, CurrentLevel.HasWallBetween);
                if (eye.DetectedThisFrame)
                {
                    spotted = true;
                    if (eye.DamagePerSecond > bestDamage) bestDamage = eye.DamagePerSecond;
                    if (eye.SlowMultiplierOnPlayer < bestSlow) bestSlow = eye.SlowMultiplierOnPlayer;
                }
            }

            // Zerstörte Augen entfernen (durch Dynamit) - erst NACH dem Update-Durchlauf,
            // damit kein Augen-Index währenddessen verschoben wird.
            CurrentLevel.Eyes.RemoveAll(e => e.IsDestroyed);

            PlayerIsSpotted = spotted;
            Player.IsSpottedThisFrame = spotted;
            Player.SlowMultiplier = spotted ? bestSlow : 1f;

            if (spotted)
            {
                Player.TakeDamage(bestDamage, dt);
            }

            if (!Player.IsAlive)
            {
                State = GameState.GameOver;
                return;
            }

            if (CurrentLevel.PlayerReachedExit(Player.Position, Player.Radius))
            {
                AwardCoins(GameConstants.CoinsPerLevelComplete);
                State = GameState.LevelTransition;
                _transitionTimer = 0f;
            }
        }

        private void UpdateDynamiteAndExplosions(float dt)
        {
            foreach (var dyn in PlacedDynamite)
            {
                dyn.Update(dt);
                if (dyn.ShouldExplode)
                {
                    dyn.MarkExploded();
                    TriggerExplosion(dyn.Position);
                }
            }
            PlacedDynamite.RemoveAll(d => d.HasExploded);

            foreach (var explosion in ActiveExplosions)
            {
                explosion.Update(dt);
            }
            ActiveExplosions.RemoveAll(e => e.IsFinished);
        }

        /// <summary>
        /// Wendet alle Auswirkungen einer Explosion sofort und einmalig an: zerstört
        /// angeknackste Wände im Radius, schädigt Augen im direkten Treffer-Bereich und
        /// löst ein Geräusch-Event aus, das entferntere Augen zur Untersuchung bewegt.
        /// </summary>
        private void TriggerExplosion(Vector2 position)
        {
            float radius = GameConstants.ExplosionBaseRadius;
            float damage = GameConstants.ExplosionBaseDamage;
            if (Progress.HasStrongerDynamite)
            {
                radius *= 1.5f;
                damage *= 1.5f;
            }

            ActiveExplosions.Add(new Explosion(position, radius, GameConstants.ExplosionVisualDuration));

            // Angeknackste Wände im Radius zerstören.
            foreach (var (col, row) in CurrentLevel.CellsWithinRadius(position, radius))
            {
                if (CurrentLevel.GetWallType(col, row) == WallType.Cracked)
                {
                    CurrentLevel.DestroyWallAt(col, row);
                }
            }

            // Augen im direkten Treffer-Bereich schädigen, ggf. zerstören und belohnen.
            float directHitRadius = radius * GameConstants.ExplosionDirectHitRadiusFactor;
            foreach (var eye in CurrentLevel.Eyes)
            {
                if (eye.IsDestroyed) continue;
                float dist = Vector2.Distance(eye.CurrentPosition, position);
                if (dist <= directHitRadius)
                {
                    eye.TakeExplosionDamage(damage);
                    if (eye.IsDestroyed)
                    {
                        AwardCoins(GameConstants.CoinsPerEyeDestroyed);
                    }
                }
            }

            // Geräusch auslösen - Augen außerhalb des direkten Radius hören die Explosion nur.
            EmitNoise(new NoiseEvent(position, radius * GameConstants.ExplosionNoiseRadiusFactor));
        }

        private void AwardCoins(int amount)
        {
            Progress.Coins += amount;
            RunCoinsEarned += amount;
        }

        /// <summary>
        /// Bewegt den Spieler in Richtung der Mausposition, begrenzt durch die aktuelle
        /// Geschwindigkeit, und löst Wandkollision pro Achse getrennt (erst X, dann Y),
        /// damit der Spieler an Wänden entlanggleitet statt einfach stehen zu bleiben.
        /// </summary>
        private void MovePlayerTowards(Vector2 target, float dt)
        {
            Vector2 diff = target - Player.Position;
            float distance = diff.Length();
            if (distance < 0.0001f) return;

            float maxStep = Player.CurrentSpeed * dt;
            Vector2 move = distance <= maxStep ? diff : diff / distance * maxStep;

            Vector2 candidateX = new Vector2(Player.Position.X + move.X, Player.Position.Y);
            if (!CurrentLevel.CollidesWithWall(candidateX, Player.Radius))
            {
                Player.Position = candidateX;
            }

            Vector2 candidateY = new Vector2(Player.Position.X, Player.Position.Y + move.Y);
            if (!CurrentLevel.CollidesWithWall(candidateY, Player.Radius))
            {
                Player.Position = candidateY;
            }
        }
    }
}
