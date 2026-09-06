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
    }
}