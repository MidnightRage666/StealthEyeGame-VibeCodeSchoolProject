using System;
using System.Numerics;
using StealthEyeGame.Core;

namespace StealthEyeGame.Entities
{
    /// <summary>
    /// Der Spieler wird direkt durch die Mausposition gesteuert.
    /// Diese Klasse hält nur den Zustand (Position, HP, Geschwindigkeit) -
    /// die eigentliche Bewegungs- und Kollisionslogik lebt im GameManager,
    /// damit diese Klasse ein reines Datenmodell bleibt.
    /// </summary>
    public class Player
    {
        public Vector2 Position;
        public readonly float Radius = GameConstants.PlayerRadius;

        /// <summary>
        /// Maximale HP dieses Runs. Kann durch dauerhafte Shop-Upgrades
        /// (z. B. "Mehr HP") höher liegen als der Basiswert.
        /// </summary>
        public float MaxHP { get; }
        public float HP { get; private set; }

        public float BaseSpeed { get; } = GameConstants.PlayerBaseSpeed;

        /// <summary>1.0 = normale Geschwindigkeit, kleiner = verlangsamt.</summary>
        public float SlowMultiplier { get; set; } = 1f;

        public float CurrentSpeed => BaseSpeed * SlowMultiplier;

        public bool IsAlive => HP > 0f;

        /// <summary>Ist der Spieler in diesem Frame von mindestens einem Auge entdeckt?</summary>
        public bool IsSpottedThisFrame { get; set; }

        public Player(Vector2 startPosition, float maxHp = GameConstants.PlayerMaxHP)
        {
            Position = startPosition;
            MaxHP = maxHp;
            HP = MaxHP;
        }

        public void TakeDamage(float damagePerSecond, float dt)
        {
            HP = MathF.Max(0f, HP - damagePerSecond * dt);
        }

        public void ResetForNewRun(Vector2 startPosition)
        {
            Position = startPosition;
            HP = MaxHP;
            SlowMultiplier = 1f;
            IsSpottedThisFrame = false;
        }

        public void MoveToNewLevel(Vector2 startPosition)
        {
            Position = startPosition;
            SlowMultiplier = 1f;
            IsSpottedThisFrame = false;
        }
    }
}
