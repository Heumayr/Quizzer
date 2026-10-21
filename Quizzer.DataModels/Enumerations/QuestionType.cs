using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Unterscheidet die konkreten Fragetypen, die per Table-per-Type (TPT)-Vererbung
    /// in der Datenbank gespeichert werden. Jeder Wert entspricht einer eigenen
    /// Unterklasse von <c>QuestionBase</c>.
    /// </summary>
    public enum QuestionType
    {
        /// <summary>Standardfrage ohne spezielle Mechanik (offene Antwort mit Buzzer).</summary>
        Default = 0,

        /// <summary>Multiple-Choice-Frage: Spieler wählen eine Antwort-Taste; Reihenfolge der Optionen wird zufällig gemischt.</summary>
        MultipleChoice = 1,

        /// <summary>Eigenschaften-Frage: schrittweise werden Hinweise aufgedeckt; Punkte sinken proportional mit jedem Schritt.</summary>
        Properties = 2,

        /// <summary>Schätzfrage: Spieler geben einen Freitext-Wert ein; wird über das Input-Layout des Buzzers beantwortet.</summary>
        Appreciate = 3,

        /// <summary>
        /// Aufdeckfrage: ein Bild wird schrittweise sichtbar - entweder fallen nacheinander
        /// verdeckende Flächen weg, oder das Bild wird von Schritt zu Schritt schärfer.
        /// <para>
        /// <b>Am Ende angefügt, nicht dazwischen.</b> Die Werte stehen als Zahlen in der
        /// Datenbank; ein verschobener Wert machte aus jeder gespeicherten Frage eine andere.
        /// </para>
        /// </summary>
        Reveal = 4,

        /// <summary>
        /// Reihenfolgefrage (M12, 2026-10-21): die Spieler tippen die Elemente am Telefon in der
        /// richtigen Reihenfolge an. Jedes Element am richtigen Platz bringt seinen Anteil.
        /// </summary>
        Ordering = 5,

        /// <summary>
        /// Zuordnungsfrage (M12, 2026-10-21): links feste, nummerierte Begriffe, rechts gemischte
        /// Gegenstücke mit Tasten. Am Telefon wird zu 1, 2, 3 … der Reihe nach das passende
        /// Gegenstück angetippt - technisch eine Reihenfolge, gewertet je richtigem Paar.
        /// </summary>
        Matching = 6,

        /// <summary>
        /// Wahr oder falsch (M12, 2026-10-21): mehrere Aussagen, alle Spieler beurteilen jede
        /// gleichzeitig am Telefon. Jede richtig beurteilte Aussage bringt ihren Anteil.
        /// </summary>
        TrueFalse = 7,
    }
}