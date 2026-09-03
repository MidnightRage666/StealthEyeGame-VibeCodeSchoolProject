using System;
using System.Numerics;
using StealthEyeGame.Core;

namespace StealthEyeGame.Entities
{
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

        public Vector2? LastKnownPlayerPos { get; private set; }

        public float HP { get; private set; } = GameConstants.EyeMaxHP;
        public bool IsDestroyed => HP <= 0f;

        // ------------------------------------------------------------
        // Normale Bewegung
        // ------------------------------------------------------------

        private Vector2 _movementTarget;
        private bool _hasMovementTarget;
        private bool _isPreparingMovement;

        private float _movementWaitTimer;

        // Blickverhalten während des Laufens
        private float _walkLookTimer;
        private float _walkLookOffset;

        private readonly Random _rng;

        private const float WalkGazeTurnRate = 4.0f;
        private const float AlertTurnRate = 6.5f;
        private const float ReturnGazeTurnRate = 3.0f;
        private const float SearchTurnRate = 3.5f;

        // Wie weit das Auge beim normalen Herumlaufen schauen darf
        private const float RandomLookAngle = 0.65f;

        // ------------------------------------------------------------
        // Investigation
        // ------------------------------------------------------------

        private Vector2 _investigationTarget;

        // ------------------------------------------------------------
        // Searching
        // ------------------------------------------------------------

        private static readonly float[] SearchOffsets =
        {
            -1.05f,
            0f,
            1.05f,
            0f,
            -2.0f,
            0f,
            2.0f
        };

        private float _searchBaseAngle;
        private int _searchSegmentIndex;
        private float _searchSegmentTimer;
        private float _searchStateTimer;
        private float _searchDuration;

        public Eye(
            Vector2 position,
            float facingAngle,
            float visionRange,
            float visionHalfAngle,
            float damagePerSecond,
            float slowMultiplierOnPlayer,
            float sweepAmplitude,
            int seed)
        {
            HomePosition = position;
            CurrentPosition = position;

            FacingAngle = facingAngle;
            GazeAngle = facingAngle;

            VisionRange = visionRange;
            VisionHalfAngle = visionHalfAngle;

            DamagePerSecond = damagePerSecond;
            SlowMultiplierOnPlayer = slowMultiplierOnPlayer;

            _rng = new Random(seed);

            // Beim Start erstmal kurze Pause,
            // danach sucht sich das Auge sein erstes Ziel.
            _movementWaitTimer = RandomWaitTime();
            _walkLookTimer = RandomLookTime();
        }

        public void NotifyNoise(Vector2 sourcePosition)
        {
            if (State == EyeState.Alert)
                return;

            State = EyeState.Investigation;
            _investigationTarget = sourcePosition;
            _hasMovementTarget = false;
        }

        public void TakeExplosionDamage(float damage)
        {
            HP = MathF.Max(0f, HP - damage);
        }

        public void Update(
            float dt,
            Vector2 playerPos,
            Func<Vector2, float, bool> collidesWithWall,
            Func<Vector2, Vector2, bool> hasWallBetween)
        {
            if (IsDestroyed)
                return;

            bool wasDetectedLastFrame = DetectedThisFrame;

            switch (State)
            {
                case EyeState.Idle:
                    UpdateIdle(dt, collidesWithWall);
                    break;

                case EyeState.Investigation:
                    UpdateInvestigation(dt, collidesWithWall);
                    break;

                case EyeState.Alert:
                    UpdateAlert(
                        dt,
                        playerPos,
                        wasDetectedLastFrame,
                        collidesWithWall);
                    break;

                case EyeState.Searching:
                    UpdateSearching(dt);
                    break;

                case EyeState.Returning:
                    UpdateReturning(dt, collidesWithWall);
                    break;
            }

            // --------------------------------------------------------
            // Echte Sichtprüfung
            // --------------------------------------------------------

            Vector2 toPlayer = playerPos - CurrentPosition;
            float distance = toPlayer.Length();

            bool inRange = distance <= VisionRange;

            bool inAngle = false;

            if (inRange && distance > 0.001f)
            {
                float angleToPlayer =
                    MathF.Atan2(toPlayer.Y, toPlayer.X);

                inAngle =
                    MathF.Abs(
                        MathUtil.AngleDifference(
                            GazeAngle,
                            angleToPlayer))
                    <= VisionHalfAngle;
            }

            bool clearLine =
                inAngle &&
                !hasWallBetween(CurrentPosition, playerPos);

            DetectedThisFrame = clearLine;

            if (DetectedThisFrame)
            {
                LastKnownPlayerPos = playerPos;
                State = EyeState.Alert;
            }
        }

        // ============================================================
        // IDLE / NORMALES HERUMLAUFEN
        // ============================================================

        private void UpdateIdle(
            float dt,
            Func<Vector2, float, bool> collidesWithWall)
        {
            // Noch kein Ziel?
            if (!_hasMovementTarget)
            {
                _movementWaitTimer -= dt;

                UpdateIdleLooking(dt);

                if (_movementWaitTimer <= 0f)
                {
                    FindNewMovementTarget(collidesWithWall);
                }

                return;
            }

            // ------------------------------------------------------------
            // Erst in Richtung des neuen Ziels drehen
            // ------------------------------------------------------------

            if (_isPreparingMovement)
            {
                Vector2 direction =
                    _movementTarget - CurrentPosition;

                if (direction.LengthSquared() > 0.01f)
                {
                    float targetAngle =
                        MathF.Atan2(
                            direction.Y,
                            direction.X);

                    GazeAngle =
                        MathUtil.RotateTowards(
                            GazeAngle,
                            targetAngle,
                            WalkGazeTurnRate * dt);

                    float angleDifference =
                        MathF.Abs(
                            MathUtil.AngleDifference(
                                GazeAngle,
                                targetAngle));

                    // Erst loslaufen, wenn das Auge
                    // fast vollständig in die Richtung schaut.
                    if (angleDifference < 0.12f)
                    {
                        _isPreparingMovement = false;
                    }
                }

                return;
            }

            // ------------------------------------------------------------
            // Jetzt tatsächlich laufen
            // ------------------------------------------------------------

            MoveToward(
                _movementTarget,
                dt,
                collidesWithWall);

            UpdateWalkingLook(
                dt,
                _movementTarget);

            if (Vector2.Distance(
                    CurrentPosition,
                    _movementTarget)
                <= GameConstants.EyeArriveThreshold)
            {
                CurrentPosition = _movementTarget;

                _hasMovementTarget = false;
                _isPreparingMovement = false;

                _movementWaitTimer = RandomWaitTime();
                _walkLookTimer = 0f;
            }
        }

        private void FindNewMovementTarget(
            Func<Vector2, float, bool> collidesWithWall)
        {
            for (int i = 0; i < 40; i++)
            {
                float angle =
                    (float)(_rng.NextDouble() * MathF.Tau);

                float distance =
                    100f +
                    (float)_rng.NextDouble() * 180f;

                Vector2 target =
                    CurrentPosition +
                    new Vector2(
                        MathF.Cos(angle),
                        MathF.Sin(angle)) * distance;

                // Ziel muss frei sein
                if (collidesWithWall(
                        target,
                        GameConstants.EyeRadius))
                {
                    continue;
                }

                // Den kompletten Weg zum Ziel prüfen,
                // damit das Auge nicht durch eine Wand laufen will.
                bool pathBlocked = false;

                const int pathChecks = 8;

                for (int step = 1; step <= pathChecks; step++)
                {
                    float t = step / (float)pathChecks;

                    Vector2 checkPosition =
                        Vector2.Lerp(
                            CurrentPosition,
                            target,
                            t);

                    if (collidesWithWall(
                            checkPosition,
                            GameConstants.EyeRadius))
                    {
                        pathBlocked = true;
                        break;
                    }
                }

                if (pathBlocked)
                    continue;

                // Gültiges Bewegungsziel gefunden
                _movementTarget = target;
                _hasMovementTarget = true;

                // WICHTIG:
                // Noch NICHT sofort drehen.
                // Erst wird der Sichtkegel langsam zum Ziel geschwenkt.
                _isPreparingMovement = true;

                _walkLookTimer = RandomLookTime();

                return;
            }

            // Kein vernünftiges Ziel gefunden.
            // Lieber kurz stehen bleiben als herumzuzappeln.
            _hasMovementTarget = false;
            _isPreparingMovement = false;
            _movementWaitTimer = 0.5f;
        }

        // ============================================================
        // BLICKVERHALTEN
        // ============================================================

        private void UpdateWalkingLook(
            float dt,
            Vector2 movementTarget)
        {
            Vector2 movement =
                movementTarget - CurrentPosition;

            if (movement.LengthSquared() < 0.01f)
                return;

            float movementAngle =
                MathF.Atan2(
                    movement.Y,
                    movement.X);

            _walkLookTimer -= dt;

            if (_walkLookTimer <= 0f)
            {
                // Meistens nach vorne schauen.
                // Manchmal leicht links/rechts.
                float random =
                    (float)_rng.NextDouble();

                if (random < 0.55f)
                {
                    _walkLookOffset = 0f;
                }
                else
                {
                    _walkLookOffset =
                        (float)(
                            (_rng.NextDouble() * 2.0 - 1.0)
                            * RandomLookAngle);
                }

                _walkLookTimer = RandomLookTime();
            }

            float targetAngle =
                movementAngle + _walkLookOffset;

            GazeAngle =
                MathUtil.RotateTowards(
                    GazeAngle,
                    targetAngle,
                    WalkGazeTurnRate * dt);
        }

        private void UpdateIdleLooking(float dt)
        {
            _walkLookTimer -= dt;

            if (_walkLookTimer <= 0f)
            {
                float random =
                    (float)_rng.NextDouble();

                if (random < 0.35f)
                {
                    _walkLookOffset = 0f;
                }
                else
                {
                    _walkLookOffset =
                        (float)(
                            (_rng.NextDouble() * 2.0 - 1.0)
                            * 1.3f);
                }

                _walkLookTimer = RandomLookTime();
            }

            float targetAngle =
                FacingAngle + _walkLookOffset;

            GazeAngle =
                MathUtil.RotateTowards(
                    GazeAngle,
                    targetAngle,
                    WalkGazeTurnRate * dt);
        }

        // ============================================================
        // INVESTIGATION
        // ============================================================

        private void UpdateInvestigation(
            float dt,
            Func<Vector2, float, bool> collidesWithWall)
        {
            MoveToward(
                _investigationTarget,
                dt,
                collidesWithWall);

            AimGazeTowardsMovement(
                _investigationTarget,
                WalkGazeTurnRate,
                dt);

            if (Vector2.Distance(
                    CurrentPosition,
                    _investigationTarget)
                <= GameConstants.EyeArriveThreshold)
            {
                EnterSearching();
            }
        }

        // ============================================================
        // ALERT
        // ============================================================

        private void UpdateAlert(
            float dt,
            Vector2 playerPos,
            bool wasDetectedLastFrame,
            Func<Vector2, float, bool> collidesWithWall)
        {
            if (wasDetectedLastFrame)
            {
                Vector2 toPlayer =
                    playerPos - CurrentPosition;

                float distance = toPlayer.Length();

                if (distance > 0.01f)
                {
                    Vector2 direction =
                        toPlayer / distance;

                    const float minimumDistance = 100f;

                    // Punkt 100 Pixel vor dem Spieler
                    Vector2 chaseTarget =
                        playerPos - direction * minimumDistance;

                    Vector2 toTarget =
                        chaseTarget - CurrentPosition;

                    float targetDistance =
                        toTarget.Length();

                    // Auge schaut immer direkt zum Spieler
                    float liveAngle =
                        MathF.Atan2(
                            toPlayer.Y,
                            toPlayer.X);

                    GazeAngle =
                        MathUtil.RotateTowards(
                            GazeAngle,
                            liveAngle,
                            AlertTurnRate * dt);

                    // Nur bis zum Mindestabstand bewegen
                    if (targetDistance > 1f)
                    {
                        float step =
                            MathF.Min(
                                targetDistance,
                                GameConstants.EyeMoveSpeed * 1.8f * dt);

                        CurrentPosition +=
                            toTarget / targetDistance * step;
                    }
                }

                return;
            }

            Vector2 target =
                LastKnownPlayerPos ?? CurrentPosition;

            Vector2 diff =
                target - CurrentPosition;

            float distanceToTarget =
                diff.Length();

            if (distanceToTarget > 0.01f)
            {
                float angle =
                    MathF.Atan2(
                        diff.Y,
                        diff.X);

                GazeAngle =
                    MathUtil.RotateTowards(
                        GazeAngle,
                        angle,
                        AlertTurnRate * 0.7f * dt);

                float step =
                    MathF.Min(
                        distanceToTarget,
                        GameConstants.EyeMoveSpeed * 1.8f * dt);

                CurrentPosition +=
                    diff / distanceToTarget * step;
            }

            if (distanceToTarget <= GameConstants.EyeArriveThreshold)
            {
                EnterSearching();
            }
        }

        // ============================================================
        // SEARCHING
        // ============================================================

        private void UpdateSearching(float dt)
        {
            _searchSegmentTimer -= dt;

            if (_searchSegmentTimer <= 0f)
            {
                _searchSegmentIndex =
                    (_searchSegmentIndex + 1)
                    % SearchOffsets.Length;

                _searchSegmentTimer =
                    Lerp(
                        GameConstants.SearchSegmentMinDuration,
                        GameConstants.SearchSegmentMaxDuration,
                        (float)_rng.NextDouble());
            }

            float target =
                _searchBaseAngle +
                SearchOffsets[_searchSegmentIndex];

            GazeAngle =
                MathUtil.RotateTowards(
                    GazeAngle,
                    target,
                    SearchTurnRate * dt);

            _searchStateTimer += dt;

            if (_searchStateTimer >= _searchDuration)
            {
                State = EyeState.Returning;
            }
        }

        // ============================================================
        // RETURNING
        // ============================================================

        private void UpdateReturning(
            float dt,
            Func<Vector2, float, bool> collidesWithWall)
        {
            Vector2 diff = HomePosition - CurrentPosition;

            float distance = diff.Length();

            if (distance > 0.01f)
            {
                float angle =
                    MathF.Atan2(
                        diff.Y,
                        diff.X);

                GazeAngle =
                    MathUtil.RotateTowards(
                        GazeAngle,
                        angle,
                        ReturnGazeTurnRate * dt);

                float step =
                    MathF.Min(
                        distance,
                        GameConstants.EyeMoveSpeed * dt);

                // Beim Zurückkehren werden Wände ignoriert.
                CurrentPosition +=
                    diff / distance * step;
            }

            if (distance <= GameConstants.EyeArriveThreshold)
            {
                CurrentPosition = HomePosition;

                State = EyeState.Idle;

                _hasMovementTarget = false;
                _movementWaitTimer = 0f;
            }
        }

        // ============================================================
        // SEARCH START
        // ============================================================

        private void EnterSearching()
        {
            State = EyeState.Searching;

            _searchBaseAngle = GazeAngle;

            _searchSegmentIndex = 0;

            _searchSegmentTimer =
                Lerp(
                    GameConstants.SearchSegmentMinDuration,
                    GameConstants.SearchSegmentMaxDuration,
                    (float)_rng.NextDouble());

            _searchStateTimer = 0f;

            _searchDuration =
                Lerp(
                    GameConstants.SearchMinDuration,
                    GameConstants.SearchMaxDuration,
                    (float)_rng.NextDouble());
        }

        // ============================================================
        // BEWEGUNG
        // ============================================================

        private void MoveToward(
            Vector2 target,
            float dt,
            Func<Vector2, float, bool> collidesWithWall)
        {
            Vector2 diff = target - CurrentPosition;

            float distance = diff.Length();

            if (distance < 0.01f)
                return;

            float step = MathF.Min(
                distance,
                GameConstants.EyeMoveSpeed * dt);

            Vector2 move =
                diff / distance * step;

            Vector2 newPosition =
                CurrentPosition + move;

            if (!collidesWithWall(
                    newPosition,
                    GameConstants.EyeRadius))
            {
                CurrentPosition = newPosition;
            }
        }

        private void AimGazeTowardsMovement(
            Vector2 target,
            float turnRate,
            float dt)
        {
            Vector2 diff =
                target - CurrentPosition;

            if (diff.LengthSquared() < 0.01f)
                return;

            float angle =
                MathF.Atan2(
                    diff.Y,
                    diff.X);

            GazeAngle =
                MathUtil.RotateTowards(
                    GazeAngle,
                    angle,
                    turnRate * dt);
        }

        // ============================================================
        // RANDOM
        // ============================================================

        private float RandomWaitTime()
        {
            return 0.5f +
                   (float)_rng.NextDouble() * 1.8f;
        }

        private float RandomLookTime()
        {
            return 0.5f +
                   (float)_rng.NextDouble() * 1.2f;
        }

        private static float Lerp(
            float a,
            float b,
            float t)
        {
            return a + (b - a) * t;
        }
    }
}