using System;

namespace StealthEyeGame.Systems
{
    public class SaveData
    {
        public int CurrentLevel { get; set; }

        public float PlayerX { get; set; }

        public float PlayerY { get; set; }

        public float PlayerHealth { get; set; }

        public int Coins { get; set; }

        public int DynamiteOwned { get; set; }

        public int MedkitsOwned { get; set; }

        public float BonusMaxHP { get; set; }

        public bool HasStrongerDynamite { get; set; }

        public DateTime SaveDate { get; set; }

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
            SaveDate = DateTime.Now;
        }
    }
}