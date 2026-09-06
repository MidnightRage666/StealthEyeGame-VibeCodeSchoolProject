using System;
using System.Collections.Generic;

namespace StealthEyeGame.Systems
{
    public class SaveData
    {
        // =========================
        // SPIELSTAND
        // =========================

        public int CurrentLevel { get; set; }


        // =========================
        // SPIELER
        // =========================

        public float PlayerX { get; set; }
        public float PlayerY { get; set; }

        public float PlayerHealth { get; set; }


        // =========================
        // SPIELER-FORTSCHRITT
        // =========================

        public int Coins { get; set; }

        public int DynamiteOwned { get; set; }

        public int MedkitsOwned { get; set; }

        public float BonusMaxHP { get; set; }

        public bool HasStrongerDynamite { get; set; }


        // =========================
        // LEVEL
        // =========================

        // Gespeichertes Wandraster
        public int[][]? WallGrid { get; set; }


        // Gespeicherter Spieler-Startpunkt
        public float PlayerStartX { get; set; }
        public float PlayerStartY { get; set; }


        // Gespeicherter Ausgang
        public float ExitX { get; set; }
        public float ExitY { get; set; }

        public float ExitWidth { get; set; }
        public float ExitHeight { get; set; }


        // =========================
        // AUGEN
        // =========================

        public List<SavedEyeData> Eyes { get; set; }


        // =========================
        // SPEICHERDATUM
        // =========================

        public DateTime SaveDate { get; set; }


        // =========================
        // KONSTRUKTOR
        // =========================

        public SaveData()
        {
            CurrentLevel = 1;

            PlayerX = 0;
            PlayerY = 0;
            PlayerHealth = 0;

            Coins = 0;
            DynamiteOwned = 0;
            MedkitsOwned = 0;

            BonusMaxHP = 0;
            HasStrongerDynamite = false;

            WallGrid = null;

            PlayerStartX = 0;
            PlayerStartY = 0;

            ExitX = 0;
            ExitY = 0;
            ExitWidth = 0;
            ExitHeight = 0;

            Eyes = new List<SavedEyeData>();

            SaveDate = DateTime.Now;
        }
    }


    // =====================================================
    // GESPEICHERTER ZUSTAND EINES AUGES
    // =====================================================

    public class SavedEyeData
    {
        // Aktuelle Position des Auges
        public float X { get; set; }
        public float Y { get; set; }


        // Ursprüngliche Blickrichtung
        public float FacingAngle { get; set; }


        // Aktuelle Blickrichtung / Pupillenbewegung
        public float GazeAngle { get; set; }


        // Lebenspunkte
        public float HP { get; set; }


        // Aktueller KI-Zustand
        // Wird als int gespeichert, weil EyeState ein Enum ist.
        public int State { get; set; }


        public SavedEyeData()
        {
            X = 0;
            Y = 0;

            FacingAngle = 0;
            GazeAngle = 0;

            HP = 0;

            State = 0;
        }
    }
}