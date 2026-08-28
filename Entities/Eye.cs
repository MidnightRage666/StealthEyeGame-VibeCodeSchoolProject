using System;
using System.Numerics;
using StealthEyeGame.Core;

namespace StealthEyeGame.Entities
{
    /// <summary>
    /// Augen-Gegner mit vollständigem Zustandsautomat.
    ///
    /// Zustandsübergänge (siehe <see cref="EyeState"/>):
    ///
    ///   Idle --(Spieler gesehen)--> Alert
    ///   Idle --(Geräusch gehört)--> Investigation
    ///   Investigation --(Ziel erreicht)--> Searching
    ///   Investigation --(Spieler gesehen)--> Alert
    ///   Alert --(Spieler sichtbar)--> bleibt stehen, verfolgt mit der Pupille
    ///   Alert --(Sicht verloren)--> läuft zur letzten bekannten Position
    ///   Alert --(Position erreicht, kein Spieler)--> Searching
    ///   Searching --(Spieler gesehen)--> Alert
    ///   Searching --(Zeit abgelaufen)--> Returning
    ///   Returning --(Heimatposition erreicht)--> Idle
    ///   Returning --(Spieler gesehen)--> Alert
    ///
    /// Wichtig für Fairness: Das Auge kennt die Spielerposition NIEMALS direkt.
    /// Es kennt ausschließlich <see cref="LastKnownPlayerPos"/> - und die wird
    /// nur genau dann aktualisiert, wenn eine echte Sichtprüfung (Distanz +
    /// Winkel + freie Sichtlinie) in diesem Frame erfolgreich war.
    /// </summary>
    public class Eye
    {
        public Vector2 HomePosition { get; }
        public Vector2 CurrentPosition { get; private set; }

        public float FacingAngle { get; }
        public float GazeAngle { get; private set; }

        public float VisionRange { get; }
        public float VisionHalfAngle { get; }
        public float DamagePerSecond { get; }
        public float SlowMultiplierOnPlayer { get; }

        public EyeState State { get; private set; } = EyeState.Idle;
        public bool DetectedThisFrame { get; private set; }

        /// <summary>Die letzte tatsächlich gesehene Spielerposition, oder null, falls noch nie gesehen.</summary>
        public Vector2? LastKnownPlayerPos { get; private set; }

        public float HP { get; private set; } = GameConstants.EyeMaxHP;
        public bool IsDestroyed => HP <= 0f;

        // --- Ruhe-Blickverhalten ---
        private readonly float _sweepAmplitude;
        private float _idleHoldTimer;
        private float _idleTargetOffset;

        // --- Bewegungsziel für Investigation ---
        private Vector2 _investigationTarget;

        // --- Suchverhalten ---
        private static readonly float[] SearchOffsets = { -1.05f, 0f, 1.05f, 0f, -2.0f, 0f, 2.0f };
        private float _searchBaseAngle;
        private int _searchSegmentIndex;
        private float _searchSegmentTimer;
        private float _searchStateTimer;
        private float _searchDuration;

        private readonly Random _rng;

        private const float IdleTurnRate = 0.9f;
        private const float AlertTurnRate = 6.5f;
        private const float WalkGazeTurnRate = 3.0f;
        private const float ReturnGazeTurnRate = 2.0f;
        private const float SearchTurnRate = 3.5f;

        public Eye(Vector2 position, float facingAngle, float visionRange, float visionHalfAngle,
                   float damagePerSecond, float slowMultiplierOnPlayer,
                   float sweepAmplitude, int seed)
        {
            HomePosition = position;
            CurrentPosition = position;
            FacingAngle = facingAngle;
            GazeAngle = facingAngle;
            VisionRange = visionRange;
            VisionHalfAngle = visionHalfAngle;
            DamagePerSecond = damagePerSecond;
            SlowMultiplierOnPlayer = slowMultiplierOnPlayer;
            _sweepAmplitude = sweepAmplitude;
            _rng = new Random(seed);
        }

        /// <summary>Wird von einer Geräuschquelle (z. B. Explosion) aufgerufen.</summary>
        public void NotifyNoise(Vector2 sourcePosition)
        {
            // Ein Auge, das den Spieler gerade aktiv verfolgt, lässt sich von einem
            // bloßen Geräusch nicht ablenken - der echte Fund hat Priorität.
            if (State == EyeState.Alert) return;

            State = EyeState.Investigation;
            _investigationTarget = sourcePosition;
        }

        public void TakeExplosionDamage(float damage)
        {
            HP = MathF.Max(0f, HP - damage);
        }

        /// <summary>
        /// Aktualisiert Zustand, Bewegung, Blickrichtung und Spielererkennung für einen Frame.
        /// collidesWithWall(pos, radius) prüft Kollision für die Eigenbewegung des Auges.
        /// hasWallBetween(a, b) prüft, ob zwischen zwei Weltpunkten eine Wand die Sichtlinie blockiert.
        /// </summary>
        public void Update(float dt, Vector2 playerPos, Func<Vector2, float, bool> collidesWithWall,
                            Func<Vector2, Vector2, bool> hasWallBetween)
        {
            if (IsDestroyed) return;

            bool wasDetectedLastFrame = DetectedThisFrame;

            switch (State)
            {
                case EyeState.Idle:
                    UpdateIdleLook(dt);
                    break;

                case EyeState.Investigation:
                    UpdateInvestigation(dt, collidesWithWall);
                    break;

                case EyeState.Alert:
                    UpdateAlert(dt, playerPos, wasDetectedLastFrame, collidesWithWall);
                    break;

                case EyeState.Searching:
                    UpdateSearching(dt);
                    break;

                case EyeState.Returning:
                    UpdateReturning(dt, collidesWithWall);
                    break;
            }

            // Echte Sichtprüfung - läuft in JEDEM Zustand, damit die KI niemals schummelt.
            Vector2 toPlayer = playerPos - CurrentPosition;
            float distance = toPlayer.Length();
            float angleToPlayer = MathF.Atan2(toPlayer.Y, toPlayer.X);

            bool inRange = distance <= VisionRange;
            bool inAngle = inRange && MathF.Abs(MathUtil.AngleDifference(GazeAngle, angleToPlayer)) <= VisionHalfAngle;
            bool clearLine = inAngle && !hasWallBetween(CurrentPosition, playerPos);

            DetectedThisFrame = clearLine;

            if (DetectedThisFrame)
            {
                LastKnownPlayerPos = playerPos;
                State = EyeState.Alert;
            }
        }

        private void UpdateIdleLook(float dt)
        {
            _idleHoldTimer -= dt;
            if (_idleHoldTimer <= 0f)
            {
                _idleTargetOffset = _rng.NextDouble() < 0.35
                    ? 0f
                    : (float)(_rng.NextDouble() * 2.0 - 1.0) * _sweepAmplitude;
                _idleHoldTimer = Lerp(GameConstants.IdleLookMinDuration, GameConstants.IdleLookMaxDuration, (float)_rng.NextDouble());
            }
            GazeAngle = MathUtil.RotateTowards(GazeAngle, FacingAngle + _idleTargetOffset, IdleTurnRate * dt);
        }

        private void UpdateInvestigation(float dt, Func<Vector2, float, bool> collidesWithWall)
        {
            MoveToward(_investigationTarget, dt, collidesWithWall);
            AimGazeTowardsMovement(_investigationTarget, WalkGazeTurnRate, dt);

            if (Vector2.Distance(CurrentPosition, _investigationTarget) <= GameConstants.EyeArriveThreshold)
            {
                EnterSearching();
            }
        }

        private void UpdateAlert(float dt, Vector2 playerPos, bool wasDetectedLastFrame, Func<Vector2, float, bool> collidesWithWall)
        {
            if (wasDetectedLastFrame)
            {
                // Spieler war letzten Frame sichtbar - Auge bleibt stehen und verfolgt live mit der Pupille.
                Vector2 toPlayer = playerPos - CurrentPosition;
                float liveAngle = MathF.Atan2(toPlayer.Y, toPlayer.X);
                GazeAngle = MathUtil.RotateTowards(GazeAngle, liveAngle, AlertTurnRate * dt);
                return;
            }

            // Sicht verloren - zur letzten bekannten Position laufen (nicht zur echten Spielerposition!).
            Vector2 target = LastKnownPlayerPos ?? CurrentPosition;
            MoveToward(target, dt, collidesWithWall);
            AimGazeTowardsMovement(target, AlertTurnRate * 0.6f, dt);

            if (Vector2.Distance(CurrentPosition, target) <= GameConstants.EyeArriveThreshold)
            {
                EnterSearching();
            }
        }

        private void UpdateSearching(float dt)
        {
            _searchSegmentTimer -= dt;
            if (_searchSegmentTimer <= 0f)
            {
                _searchSegmentIndex = (_searchSegmentIndex + 1) % SearchOffsets.Length;
                _searchSegmentTimer = Lerp(GameConstants.SearchSegmentMinDuration, GameConstants.SearchSegmentMaxDuration, (float)_rng.NextDouble());
            }

            float target = _searchBaseAngle + SearchOffsets[_searchSegmentIndex];
            GazeAngle = MathUtil.RotateTowards(GazeAngle, target, SearchTurnRate * dt);

            _searchStateTimer += dt;
            if (_searchStateTimer >= _searchDuration)
            {
                State = EyeState.Returning;
            }
        }

        private void UpdateReturning(float dt, Func<Vector2, float, bool> collidesWithWall)
        {
            MoveToward(HomePosition, dt, collidesWithWall);
            AimGazeTowardsMovement(HomePosition, ReturnGazeTurnRate, dt);

            if (Vector2.Distance(CurrentPosition, HomePosition) <= GameConstants.EyeArriveThreshold)
            {
                CurrentPosition = HomePosition;
                State = EyeState.Idle;
                _idleHoldTimer = 0f; // sofort neue Blickrichtung im nächsten Frame wählen
            }
        }

        private void EnterSearching()
        {
            State = EyeState.Searching;
            _searchBaseAngle = GazeAngle;
            _searchSegmentIndex = 0;
            _searchSegmentTimer = Lerp(GameConstants.SearchSegmentMinDuration, GameConstants.SearchSegmentMaxDuration, (float)_rng.NextDouble());
            _searchStateTimer = 0f;
            _searchDuration = Lerp(GameConstants.SearchMinDuration, GameConstants.SearchMaxDuration, (float)_rng.NextDouble());
        }

        private void AimGazeTowardsMovement(Vector2 target, float turnRate, float dt)
        {
            Vector2 diff = target - CurrentPosition;
            if (diff.LengthSquared() < 0.01f) return;
            float angle = MathF.Atan2(diff.Y, diff.X);
            GazeAngle = MathUtil.RotateTowards(GazeAngle, angle, turnRate * dt);
        }

        private void MoveToward(Vector2 target, float dt, Func<Vector2, float, bool> collidesWithWall)
        {
            Vector2 diff = target - CurrentPosition;
            float distance = diff.Length();
            if (distance < 0.01f) return;

            float step = MathF.Min(distance, GameConstants.EyeMoveSpeed * dt);
            Vector2 move = diff / distance * step;

            Vector2 candidateX = new Vector2(CurrentPosition.X + move.X, CurrentPosition.Y);
            if (!collidesWithWall(candidateX, GameConstants.EyeRadius)) CurrentPosition = candidateX;

            Vector2 candidateY = new Vector2(CurrentPosition.X, CurrentPosition.Y + move.Y);
            if (!collidesWithWall(candidateY, GameConstants.EyeRadius)) CurrentPosition = candidateY;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
