namespace StealthEyeGame.Entities
{
    /// <summary>
    /// Zustände der Augen-KI. Siehe <see cref="Eye"/> für die Übergangslogik.
    /// </summary>
    public enum EyeState
    {
        /// <summary>Steht am Heimatplatz, Pupille schaut gelegentlich in zufällige Richtungen.</summary>
        Idle,

        /// <summary>Läuft zu einer gehörten Geräuschquelle (z. B. Explosion), um nachzusehen.</summary>
        Investigation,

        /// <summary>Hat den Spieler gesehen oder verfolgt dessen letzte bekannte Position.</summary>
        Alert,

        /// <summary>Ist am Zielort angekommen (letzte Spielerposition oder Geräuschquelle) und schaut sich um.</summary>
        Searching,

        /// <summary>Hat die Suche aufgegeben und läuft zurück zum ursprünglichen Standort.</summary>
        Returning
    }
}
