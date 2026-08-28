namespace StealthEyeGame.Systems
{
    /// <summary>
    /// Alles, was über den Tod eines Runs hinaus erhalten bleibt: Coins, gekaufte
    /// Verbrauchsgegenstände (Dynamit) und dauerhafte Upgrades. Diese Instanz lebt
    /// für die gesamte Programmlaufzeit im GameManager und wird beim Neustart eines
    /// Runs NICHT zurückgesetzt - nur levelspezifischer Zustand (Spieler, Level,
    /// platziertes Dynamit) wird pro Run neu erzeugt.
    /// </summary>
    public class PersistentProgress
    {
        public int Coins { get; set; }

        public int DynamiteOwned { get; set; }

        /// <summary>Dauerhafter HP-Bonus on top von GameConstants.PlayerBaseMaxHP.</summary>
        public float BonusMaxHP { get; set; }

        /// <summary>Einmaliges Upgrade: stärkere Explosionen (größerer Radius, mehr Schaden).</summary>
        public bool HasStrongerDynamite { get; set; }

        public bool TrySpend(int amount)
        {
            if (Coins < amount) return false;
            Coins -= amount;
            return true;
        }
    }
}
