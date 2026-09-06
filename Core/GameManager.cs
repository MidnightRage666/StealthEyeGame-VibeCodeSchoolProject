using System;
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
        NewGameConfirmation
    }

    public class GameManager
    {
        public Player Player { get; private set; } = null!;
        public Level CurrentLevel { get; private set; } = null!;
        public int LevelNumber { get; private set; } = 1;
        public GameState State { get; private set; } = GameState.Playing;

        public PersistentProgress Progress { get; } = new PersistentProgress();
        public int RunCoinsEarned { get; private set; }

        public System.Collections.Generic.List<Dynamite> PlacedDynamite { get; } = new();
        public System.Collections.Generic.List<Explosion> ActiveExplosions { get; } = new();

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

        private readonly Random _rng = new Random();

        public GameManager()
        {
            State = GameState.MainMenu;
        }

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

        public bool SaveGame()
        {
            if (State != GameState.Playing &&
                State != GameState.Paused)
            {
                return false;
            }

            if (Player == null ||
                CurrentLevel == null)
            {
                return false;
            }

            SaveData data = new SaveData
            {
                CurrentLevel = LevelNumber,

                PlayerX = Player.Position.X,
                PlayerY = Player.Position.Y,
                PlayerHealth = Player.HP,

                Coins = Progress.Coins,
                DynamiteOwned = Progress.DynamiteOwned,
                MedkitsOwned = Progress.MedkitsOwned,

                BonusMaxHP = Progress.BonusMaxHP,
                HasStrongerDynamite = Progress.HasStrongerDynamite
            };

            return SaveSystem.Save(1, data);
        }

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
            State = GameState.MainMenu;
            IsMovementPaused = true;
            IsPlacingDynamite = false;
        }

        public bool LoadGame()
        {
            SaveData? data = SaveSystem.Load(1);

            if (data == null)
            {
                return false;
            }

            LevelNumber = data.CurrentLevel;

            Progress.Coins = data.Coins;
            Progress.DynamiteOwned = data.DynamiteOwned;
            Progress.MedkitsOwned = data.MedkitsOwned;
            Progress.BonusMaxHP = data.BonusMaxHP;
            Progress.HasStrongerDynamite =
                data.HasStrongerDynamite;

            CurrentLevel =
                LevelGenerator.Generate(
                    LevelNumber,
                    _rng);

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

            RunCoinsEarned = 0;

            IsPlacingDynamite = false;
            PlayerIsSpotted = false;
            IsMovementPaused = false;

            PlacedDynamite.Clear();
            ActiveExplosions.Clear();

            _spawnProtectionTimer =
                SpawnProtectionDuration;

            State = GameState.Playing;

            return true;
        }

        private void LoadLevel(int number)
        {
            LevelNumber = number;
            CurrentLevel =
                LevelGenerator.Generate(
                    LevelNumber,
                    _rng);

            Player.MoveToNewLevel(
                CurrentLevel.PlayerStart);

            _spawnProtectionTimer =
                SpawnProtectionDuration;
        }

        public void OpenShop() =>
            State = GameState.Shop;

        public void OpenLoadMenu()
        {
            State = GameState.LoadMenu;
        }

        public bool LoadGame(int slot)
        {
            SaveData? data = SaveSystem.Load(slot);

            if (data == null)
                return false;

            LevelNumber = data.CurrentLevel;

            Progress.Coins = data.Coins;
            Progress.DynamiteOwned = data.DynamiteOwned;
            Progress.MedkitsOwned = data.MedkitsOwned;
            Progress.BonusMaxHP = data.BonusMaxHP;
            Progress.HasStrongerDynamite = data.HasStrongerDynamite;

            CurrentLevel =
                LevelGenerator.Generate(
                    LevelNumber,
                    _rng);

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

            RunCoinsEarned = 0;

            IsPlacingDynamite = false;
            PlayerIsSpotted = false;
            IsMovementPaused = false;

            PlacedDynamite.Clear();
            ActiveExplosions.Clear();

            _spawnProtectionTimer =
                SpawnProtectionDuration;

            State =
                GameState.Playing;

            return true;
        }

        public void BuyItem(ShopItemType itemType)
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

            Player.Heal(healAmount);
            Progress.MedkitsOwned--;

            return true;
        }

        public void RestartAfterGameOver() =>
            StartNewGame();

        public void ToggleMovementPause()
        {
            if (State != GameState.Playing)
                return;

            if (IsPlacingDynamite)
                return;

            IsMovementPaused =
                !IsMovementPaused;
        }

        public bool TryDash(Vector2 mouseFieldPos)
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

            if (direction.LengthSquared() < 0.001f)
                return false;

            _dashDirection =
                Vector2.Normalize(direction);

            _dashTimer =
                DashDuration;

            _dashCooldownTimer =
                DashCooldown;

            return true;
        }

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

            Progress.DynamiteOwned -= 1;

            PlacedDynamite.Add(
                new Dynamite(
                    fieldPosition,
                    GameConstants.DynamiteFuseSeconds));

            IsPlacingDynamite = false;
            return true;
        }

        public void EmitNoise(NoiseEvent noise)
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

            PlayerIsSpotted = spotted;
            Player.IsSpottedThisFrame = spotted;

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
                explosion.Update(dt);

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
                // Jede innere Solid-Wand ist zerstörbar.
                // DestroyWallAt schützt automatisch die Außenborder.
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

        private void AwardCoins(int amount)
        {
            Progress.Coins += amount;
            RunCoinsEarned += amount;
        }

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
                    : diff / distance *
                      maxStep;

            Vector2 candidateX =
                new Vector2(
                    Player.Position.X + move.X,
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
                    Player.Position.Y + move.Y);

            if (!CurrentLevel.CollidesWithWall(
                    candidateY,
                    Player.Radius))
            {
                Player.Position =
                    candidateY;
            }
        }

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
                    Player.Position.X + move.X,
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
                    Player.Position.Y + move.Y);

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
