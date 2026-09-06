using System;
using System.Collections.Generic;
using System.Numerics;
using StealthEyeGame.Entities;
using StealthEyeGame.Levels;
using StealthEyeGame.Systems;

namespace StealthEyeGame.Core
{
    public enum GameState
    {
        MainMenu,
        Playing,
        LevelTransition,
        GameOver,
        Shop,
        Paused,
        LoadMenu,
        SaveMenu,
        NewGameConfirmation
    }

    public class GameManager
    {
        public Player Player { get; private set; } = null!;
        public Level CurrentLevel { get; private set; } = null!;

        public int LevelNumber { get; private set; } = 1;

        public GameState State { get; private set; } =
            GameState.Playing;

        public PersistentProgress Progress { get; } =
            new PersistentProgress();

        public int RunCoinsEarned { get; private set; }

        public List<Dynamite> PlacedDynamite { get; } =
            new();

        public List<Explosion> ActiveExplosions { get; } =
            new();

        public bool IsPlacingDynamite { get; private set; }
        public bool PlayerIsSpotted { get; private set; }
        public bool IsMovementPaused { get; private set; }

        private float _totalTime;
        private float _transitionTimer;

        private const float TransitionDuration = 0.7f;

        private float _dashCooldownTimer = 0f;
        private float _dashTimer = 0f;
        private Vector2 _dashDirection = Vector2.Zero;

        private float _spawnProtectionTimer = 0f;

        private const float SpawnProtectionDuration = 1.0f;

        private const float DashSpeed = 900f;
        private const float DashDuration = 0.12f;
        private const float DashCooldown = 1.0f;

        private const float DynamitePlacementRadius = 200f;

        private readonly Random _rng =
            new Random();

        public GameManager()
        {
            State =
                GameState.MainMenu;
        }

        // ============================================================
        // NEUES SPIEL
        // ============================================================

        public void StartNewGame()
        {
            LevelNumber = 1;
            _totalTime = 0f;
            RunCoinsEarned = 0;

            State =
                GameState.Playing;

            IsPlacingDynamite = false;

            PlacedDynamite.Clear();
            ActiveExplosions.Clear();

            CurrentLevel =
                LevelGenerator.Generate(
                    LevelNumber,
                    _rng);

            float maxHp =
                GameConstants.PlayerBaseMaxHP +
                Progress.BonusMaxHP;

            Player =
                new Player(
                    CurrentLevel.PlayerStart,
                    maxHp);

            IsMovementPaused = false;

            _spawnProtectionTimer =
                SpawnProtectionDuration;
        }

        public void ContinueGame()
        {
            if (!LoadGame(1))
            {
                StartNewGame();
            }
        }

        // ============================================================
        // SPEICHERN
        // ============================================================

        public bool SaveGame(int slot)
        {
            if (State != GameState.Paused &&
                State != GameState.SaveMenu)
            {
                return false;
            }

            if (Player == null ||
                CurrentLevel == null)
            {
                return false;
            }

            // --------------------------------------------------------
            // WÄNDE
            // --------------------------------------------------------

            int[][] wallGrid =
                new int[GameConstants.Cols][];

            for (int col = 0;
                 col < GameConstants.Cols;
                 col++)
            {
                wallGrid[col] =
                    new int[GameConstants.Rows];

                for (int row = 0;
                     row < GameConstants.Rows;
                     row++)
                {
                    wallGrid[col][row] =
                        (int)CurrentLevel.WallGrid[
                            col,
                            row];
                }
            }

            // --------------------------------------------------------
            // AUGEN
            // --------------------------------------------------------

            List<SavedEyeData> savedEyes =
                new List<SavedEyeData>();

            foreach (var eye in CurrentLevel.Eyes)
            {
                savedEyes.Add(
                    new SavedEyeData
                    {
                        X =
                            eye.CurrentPosition.X,

                        Y =
                            eye.CurrentPosition.Y,

                        FacingAngle =
                            eye.FacingAngle,

                        GazeAngle =
                            eye.GazeAngle,

                        HP =
                            eye.HP,

                        State =
                            (int)eye.State
                    });
            }

            // --------------------------------------------------------
            // SAVE-DATA
            // --------------------------------------------------------

            SaveData data =
                new SaveData
                {
                    CurrentLevel =
                        LevelNumber,

                    PlayerX =
                        Player.Position.X,

                    PlayerY =
                        Player.Position.Y,

                    PlayerHealth =
                        Player.HP,

                    Coins =
                        Progress.Coins,

                    DynamiteOwned =
                        Progress.DynamiteOwned,

                    MedkitsOwned =
                        Progress.MedkitsOwned,

                    BonusMaxHP =
                        Progress.BonusMaxHP,

                    HasStrongerDynamite =
                        Progress.HasStrongerDynamite,

                    SaveDate =
                        DateTime.Now,

                    WallGrid =
                        wallGrid,

                    PlayerStartX =
                        CurrentLevel.PlayerStart.X,

                    PlayerStartY =
                        CurrentLevel.PlayerStart.Y,

                    ExitX =
                        CurrentLevel.ExitRect.X,

                    ExitY =
                        CurrentLevel.ExitRect.Y,

                    ExitWidth =
                        CurrentLevel.ExitRect.Width,

                    ExitHeight =
                        CurrentLevel.ExitRect.Height,

                    Eyes =
                        savedEyes
                };

            return SaveSystem.Save(
                slot,
                data);
        }

        // ============================================================
        // PAUSE
        // ============================================================

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                IsMovementPaused = true;
                State = GameState.Paused;
            }
            else if (State == GameState.Paused)
            {
                IsMovementPaused = false;
                State = GameState.Playing;
            }
        }

        public void GoToMainMenu()
        {
            State =
                GameState.MainMenu;

            IsMovementPaused = true;
            IsPlacingDynamite = false;
        }

        // ============================================================
        // MENÜS
        // ============================================================

        public void OpenShop() =>
            State = GameState.Shop;

        public void OpenLoadMenu()
        {
            State =
                GameState.LoadMenu;
        }

        public void OpenNewGameConfirmation()
        {
            State =
                GameState.NewGameConfirmation;
        }

        public void CancelNewGame()
        {
            State =
                GameState.MainMenu;
        }

        public void ConfirmNewGame()
        {
            StartNewGame();
        }

        public void OpenSaveMenu()
        {
            if (State != GameState.Paused)
                return;

            State =
                GameState.SaveMenu;
        }

        public void CloseSaveMenu()
        {
            State =
                GameState.Paused;
        }

        // ============================================================
        // LADEN
        // ============================================================

        public bool LoadGame(int slot)
        {
            SaveData? data =
                SaveSystem.Load(slot);

            if (data == null)
                return false;

            // --------------------------------------------------------
            // SPIELER-FORTSCHRITT
            // --------------------------------------------------------

            LevelNumber =
                data.CurrentLevel;

            Progress.Coins =
                data.Coins;

            Progress.DynamiteOwned =
                data.DynamiteOwned;

            Progress.MedkitsOwned =
                data.MedkitsOwned;

            Progress.BonusMaxHP =
                data.BonusMaxHP;

            Progress.HasStrongerDynamite =
                data.HasStrongerDynamite;

            // --------------------------------------------------------
            // FALLBACK-LEVEL
            // --------------------------------------------------------
            //
            // Wir brauchen die generierten Augen nur als Vorlage
            // für ihre Werte wie VisionRange, Damage usw.
            //
            // Wände, Positionen und Ausgang werden anschließend
            // durch die gespeicherten Daten ersetzt.
            // --------------------------------------------------------

            Level generatedLevel =
                LevelGenerator.Generate(
                    LevelNumber,
                    _rng);

            // --------------------------------------------------------
            // WÄNDE
            // --------------------------------------------------------

            WallType[,] restoredGrid;

            if (data.WallGrid != null)
            {
                restoredGrid =
                    RestoreWallGrid(
                        data.WallGrid);
            }
            else
            {
                restoredGrid =
                    generatedLevel.WallGrid;
            }

            // --------------------------------------------------------
            // SPIELER-START
            // --------------------------------------------------------

            Vector2 playerStart;

            if (data.PlayerStartX != 0 ||
                data.PlayerStartY != 0)
            {
                playerStart =
                    new Vector2(
                        data.PlayerStartX,
                        data.PlayerStartY);
            }
            else
            {
                playerStart =
                    generatedLevel.PlayerStart;
            }

            // --------------------------------------------------------
            // AUSGANG
            // --------------------------------------------------------

            System.Drawing.RectangleF exitRect;

            if (data.ExitWidth > 0 &&
                data.ExitHeight > 0)
            {
                exitRect =
                    new System.Drawing.RectangleF(
                        data.ExitX,
                        data.ExitY,
                        data.ExitWidth,
                        data.ExitHeight);
            }
            else
            {
                exitRect =
                    generatedLevel.ExitRect;
            }

            // --------------------------------------------------------
            // AUGEN
            // --------------------------------------------------------

            List<Eye> restoredEyes =
                new List<Eye>();

            if (data.Eyes != null &&
                data.Eyes.Count > 0)
            {
                int count =
                    Math.Min(
                        data.Eyes.Count,
                        generatedLevel.Eyes.Count);

                for (int i = 0;
                     i < count;
                     i++)
                {
                    Eye generatedEye =
                        generatedLevel.Eyes[i];

                    SavedEyeData savedEye =
                        data.Eyes[i];

                    generatedEye.RestoreSaveState(
                        generatedEye.HomePosition,
                        new Vector2(
                            savedEye.X,
                            savedEye.Y),
                        savedEye.FacingAngle,
                        savedEye.GazeAngle,
                        savedEye.HP,
                        (EyeState)savedEye.State);

                    restoredEyes.Add(
                        generatedEye);
                }
            }
            else
            {
                // Alte Savegames ohne Eye-Daten
                restoredEyes =
                    generatedLevel.Eyes;
            }

            // --------------------------------------------------------
            // LEVEL ZUSAMMENBAUEN
            // --------------------------------------------------------

            CurrentLevel =
                new Level(
                    LevelNumber,
                    restoredGrid,
                    playerStart,
                    exitRect,
                    restoredEyes);

            // --------------------------------------------------------
            // SPIELER
            // --------------------------------------------------------

            float maxHp =
                GameConstants.PlayerBaseMaxHP +
                Progress.BonusMaxHP;

            Player =
                new Player(
                    new Vector2(
                        data.PlayerX,
                        data.PlayerY),
                    maxHp);

            Player.RestoreHealth(
                data.PlayerHealth);

            // --------------------------------------------------------
            // RESET
            // --------------------------------------------------------

            RunCoinsEarned = 0;

            IsPlacingDynamite = false;
            PlayerIsSpotted = false;
            IsMovementPaused = false;

            PlacedDynamite.Clear();
            ActiveExplosions.Clear();

            _spawnProtectionTimer =
                SpawnProtectionDuration;

            _dashTimer = 0f;
            _dashCooldownTimer = 0f;
            _dashDirection = Vector2.Zero;

            State =
                GameState.Playing;

            return true;
        }

        // ============================================================
        // WALL GRID WIEDERHERSTELLEN
        // ============================================================

        private WallType[,] RestoreWallGrid(
            int[][] savedGrid)
        {
            WallType[,] grid =
                new WallType[
                    GameConstants.Cols,
                    GameConstants.Rows];

            for (int col = 0;
                 col < GameConstants.Cols;
                 col++)
            {
                for (int row = 0;
                     row < GameConstants.Rows;
                     row++)
                {
                    grid[col, row] =
                        (WallType)savedGrid[
                            col][row];
                }
            }

            return grid;
        }

        // ============================================================
        // NÄCHSTES LEVEL
        // ============================================================

        private void LoadLevel(int number)
        {
            LevelNumber =
                number;

            CurrentLevel =
                LevelGenerator.Generate(
                    LevelNumber,
                    _rng);

            Player.MoveToNewLevel(
                CurrentLevel.PlayerStart);

            _spawnProtectionTimer =
                SpawnProtectionDuration;
        }

        // ============================================================
        // SHOP
        // ============================================================

        public void BuyItem(
            ShopItemType itemType)
        {
            foreach (var item in ShopCatalog.Items)
            {
                if (item.ItemType != itemType)
                    continue;

                if (item.CanPurchase(Progress))
                    item.Purchase(Progress);

                return;
            }
        }

        public bool UseMedkit()
        {
            if (State != GameState.Playing)
                return false;

            if (!Player.IsAlive)
                return false;

            if (Progress.MedkitsOwned <= 0)
                return false;

            if (Player.HP >= Player.MaxHP)
                return false;

            const float healAmount = 25f;

            Player.Heal(
                healAmount);

            Progress.MedkitsOwned--;

            return true;
        }

        public void RestartAfterGameOver() =>
            StartNewGame();

        // ============================================================
        // BEWEGUNG PAUSIEREN
        // ============================================================

        public void ToggleMovementPause()
        {
            if (State != GameState.Playing)
                return;

            if (IsPlacingDynamite)
                return;

            IsMovementPaused =
                !IsMovementPaused;
        }

        // ============================================================
        // DASH
        // ============================================================

        public bool TryDash(
            Vector2 mouseFieldPos)
        {
            if (State != GameState.Playing)
                return false;

            if (IsPlacingDynamite)
                return false;

            if (_dashCooldownTimer > 0f)
                return false;

            Vector2 direction =
                mouseFieldPos -
                Player.Position;

            if (direction.LengthSquared() <
                0.001f)
            {
                return false;
            }

            _dashDirection =
                Vector2.Normalize(
                    direction);

            _dashTimer =
                DashDuration;

            _dashCooldownTimer =
                DashCooldown;

            return true;
        }

        // ============================================================
        // DYNAMIT
        // ============================================================

        public void ToggleDynamitePlacementMode()
        {
            if (State != GameState.Playing)
                return;

            if (Progress.DynamiteOwned <= 0)
            {
                IsPlacingDynamite = false;
                return;
            }

            IsPlacingDynamite =
                !IsPlacingDynamite;
        }

        public bool TryPlaceDynamiteAt(
            Vector2 fieldPosition)
        {
            if (!IsPlacingDynamite ||
                State != GameState.Playing)
            {
                return false;
            }

            if (Progress.DynamiteOwned <= 0)
            {
                IsPlacingDynamite = false;
                return false;
            }

            if (fieldPosition.X < 0 ||
                fieldPosition.Y < 0 ||
                fieldPosition.X >
                    GameConstants.CanvasWidth ||
                fieldPosition.Y >
                    GameConstants.CanvasHeight)
            {
                return false;
            }

            float distance =
                Vector2.Distance(
                    Player.Position,
                    fieldPosition);

            if (distance >
                DynamitePlacementRadius)
            {
                return false;
            }

            Progress.DynamiteOwned--;

            PlacedDynamite.Add(
                new Dynamite(
                    fieldPosition,
                    GameConstants.DynamiteFuseSeconds));

            IsPlacingDynamite = false;

            return true;
        }

        // ============================================================
        // GERÄUSCH
        // ============================================================

        public void EmitNoise(
            NoiseEvent noise)
        {
            foreach (var eye in CurrentLevel.Eyes)
            {
                if (eye.IsDestroyed)
                    continue;

                float dist =
                    Vector2.Distance(
                        eye.HomePosition,
                        noise.Position);

                float distCurrent =
                    Vector2.Distance(
                        eye.CurrentPosition,
                        noise.Position);

                if (MathF.Min(
                        dist,
                        distCurrent) <=
                    noise.Radius)
                {
                    eye.NotifyNoise(
                        noise.Position);
                }
            }
        }

        // ============================================================
        // UPDATE
        // ============================================================

        public void Update(
            float dt,
            Vector2 mouseFieldPos)
        {
            switch (State)
            {
                case GameState.Playing:

                    UpdatePlaying(
                        dt,
                        mouseFieldPos);

                    break;

                case GameState.LevelTransition:

                    _transitionTimer += dt;

                    if (_transitionTimer >=
                        TransitionDuration)
                    {
                        _transitionTimer = 0f;

                        LoadLevel(
                            LevelNumber + 1);

                        State =
                            GameState.Playing;
                    }

                    break;

                case GameState.GameOver:
                case GameState.Shop:
                case GameState.Paused:
                    break;
            }
        }

        // ============================================================
        // PLAYING UPDATE
        // ============================================================

        private void UpdatePlaying(
            float dt,
            Vector2 mouseFieldPos)
        {
            _totalTime += dt;

            if (_spawnProtectionTimer > 0f)
            {
                _spawnProtectionTimer =
                    MathF.Max(
                        0f,
                        _spawnProtectionTimer - dt);
            }

            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer =
                    MathF.Max(
                        0f,
                        _dashCooldownTimer - dt);
            }

            if (!IsPlacingDynamite &&
                !IsMovementPaused)
            {
                MovePlayerTowards(
                    mouseFieldPos,
                    dt);

                UpdateDash(dt);
            }

            UpdateDynamiteAndExplosions(dt);

            float bestDamage = 0f;
            float bestSlow = 1f;
            bool spotted = false;

            foreach (var eye in CurrentLevel.Eyes)
            {
                eye.Update(
                    dt,
                    Player.Position,
                    CurrentLevel.CollidesWithWall,
                    CurrentLevel.HasWallBetween);

                if (eye.DetectedThisFrame)
                {
                    spotted = true;

                    if (eye.DamagePerSecond >
                        bestDamage)
                    {
                        bestDamage =
                            eye.DamagePerSecond;
                    }

                    if (eye.SlowMultiplierOnPlayer <
                        bestSlow)
                    {
                        bestSlow =
                            eye.SlowMultiplierOnPlayer;
                    }
                }
            }

            CurrentLevel.Eyes.RemoveAll(
                e => e.IsDestroyed);

            PlayerIsSpotted =
                spotted;

            Player.IsSpottedThisFrame =
                spotted;

            Player.SlowMultiplier =
                spotted
                    ? bestSlow
                    : 1f;

            if (spotted &&
                _spawnProtectionTimer <= 0f)
            {
                Player.TakeDamage(
                    bestDamage,
                    dt);
            }

            if (!Player.IsAlive)
            {
                State =
                    GameState.GameOver;

                return;
            }

            if (CurrentLevel.PlayerReachedExit(
                    Player.Position,
                    Player.Radius))
            {
                AwardCoins(
                    GameConstants.CoinsPerLevelComplete);

                State =
                    GameState.LevelTransition;

                _transitionTimer = 0f;
            }
        }

        // ============================================================
        // DYNAMIT + EXPLOSION
        // ============================================================

        private void UpdateDynamiteAndExplosions(
            float dt)
        {
            foreach (var dyn in PlacedDynamite)
            {
                dyn.Update(dt);

                if (dyn.ShouldExplode)
                {
                    dyn.MarkExploded();

                    TriggerExplosion(
                        dyn.Position);
                }
            }

            PlacedDynamite.RemoveAll(
                d => d.HasExploded);

            foreach (var explosion in ActiveExplosions)
            {
                explosion.Update(dt);
            }

            ActiveExplosions.RemoveAll(
                e => e.IsFinished);
        }

        private void TriggerExplosion(
            Vector2 position)
        {
            float radius =
                GameConstants.ExplosionBaseRadius;

            float damage =
                GameConstants.ExplosionBaseDamage;

            if (Progress.HasStrongerDynamite)
            {
                radius *= 1.5f;
                damage *= 1.5f;
            }

            ActiveExplosions.Add(
                new Explosion(
                    position,
                    radius,
                    GameConstants.ExplosionVisualDuration));

            foreach (var (col, row) in
                CurrentLevel.CellsWithinRadius(
                    position,
                    radius))
            {
                if (CurrentLevel.GetWallType(
                        col,
                        row) ==
                    WallType.Solid)
                {
                    CurrentLevel.DestroyWallAt(
                        col,
                        row);
                }
            }

            float directHitRadius =
                radius *
                GameConstants.ExplosionDirectHitRadiusFactor;

            foreach (var eye in CurrentLevel.Eyes)
            {
                if (eye.IsDestroyed)
                    continue;

                float dist =
                    Vector2.Distance(
                        eye.CurrentPosition,
                        position);

                if (dist <= directHitRadius)
                {
                    eye.TakeExplosionDamage(
                        damage);

                    if (eye.IsDestroyed)
                    {
                        AwardCoins(
                            GameConstants.CoinsPerEyeDestroyed);
                    }
                }
            }

            EmitNoise(
                new NoiseEvent(
                    position,
                    radius *
                    GameConstants.ExplosionNoiseRadiusFactor));
        }

        // ============================================================
        // COINS
        // ============================================================

        private void AwardCoins(int amount)
        {
            Progress.Coins += amount;
            RunCoinsEarned += amount;
        }

        // ============================================================
        // SPIELERBEWEGUNG
        // ============================================================

        private void MovePlayerTowards(
            Vector2 target,
            float dt)
        {
            Vector2 diff =
                target -
                Player.Position;

            float distance =
                diff.Length();

            if (distance < 0.0001f)
                return;

            float maxStep =
                Player.CurrentSpeed *
                dt;

            Vector2 move =
                distance <= maxStep
                    ? diff
                    : diff /
                      distance *
                      maxStep;

            Vector2 candidateX =
                new Vector2(
                    Player.Position.X +
                    move.X,
                    Player.Position.Y);

            if (!CurrentLevel.CollidesWithWall(
                    candidateX,
                    Player.Radius))
            {
                Player.Position =
                    candidateX;
            }

            Vector2 candidateY =
                new Vector2(
                    Player.Position.X,
                    Player.Position.Y +
                    move.Y);

            if (!CurrentLevel.CollidesWithWall(
                    candidateY,
                    Player.Radius))
            {
                Player.Position =
                    candidateY;
            }
        }

        // ============================================================
        // DASH UPDATE
        // ============================================================

        private void UpdateDash(float dt)
        {
            if (_dashTimer <= 0f)
                return;

            _dashTimer -= dt;

            Vector2 move =
                _dashDirection *
                DashSpeed *
                dt;

            Vector2 candidateX =
                new Vector2(
                    Player.Position.X +
                    move.X,
                    Player.Position.Y);

            if (!CurrentLevel.CollidesWithWall(
                    candidateX,
                    Player.Radius))
            {
                Player.Position =
                    candidateX;
            }

            Vector2 candidateY =
                new Vector2(
                    Player.Position.X,
                    Player.Position.Y +
                    move.Y);

            if (!CurrentLevel.CollidesWithWall(
                    candidateY,
                    Player.Radius))
            {
                Player.Position =
                    candidateY;
            }
        }
    }
}