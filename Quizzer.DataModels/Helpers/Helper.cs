using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Helpers
{
    /// <summary>
    /// Allgemeine Hilfsmethoden, die projektübergreifend genutzt werden.
    /// </summary>
    public class Helper
    {
        /// <summary>
        /// Gibt den nächsten Bezeichner in einer Sequenz zurück, abhängig vom gewählten <paramref name="keyType"/>.
        /// Wird von <c>QuestionBase.CalculateOrderdSteps()</c> verwendet, um jedem normalen
        /// Schritt einer Frage einen eindeutigen Anzeige-Schlüssel (A, B, C … oder 1, 2, 3 …) zuzuweisen.
        /// </summary>
        /// <param name="current">Der aktuelle Schlüssel. Leerer String liefert den ersten Schlüssel der Sequenz.</param>
        /// <param name="keyType">Das Schlüsselschema (alphabetisch oder numerisch).</param>
        /// <returns>Den nächsten Schlüssel in der Sequenz.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Wenn ein unbekannter <paramref name="keyType"/> übergeben wird.</exception>
        public static string GetNextViewKey(string current, QuestionViewKeyType keyType)
        {
            switch (keyType)
            {
                case QuestionViewKeyType.Alphabetical:
                    return GetNextAlphabeticalEntry(current);

                case QuestionViewKeyType.Numerical:
                    return GetNextNumber(current);

                default:
                    throw new ArgumentOutOfRangeException(nameof(keyType), "Unsupported key type.");
            }
        }

        /// <summary>
        /// Gibt den nächsten numerischen Schlüssel zurück ("1", "2", "3", …).
        /// Bei leerem Input wird "1" zurückgegeben.
        /// </summary>
        private static string GetNextNumber(string current)
        {
            if (string.IsNullOrWhiteSpace(current))
                return "1";
            if (int.TryParse(current, out int number))
                return (number + 1).ToString();
            throw new ArgumentException("Input must be a valid integer.", nameof(current));
        }

        /// <summary>
        /// Gibt den nächsten alphabetischen Schlüssel zurück ("A", "B", …, "Z", "AA", "AB", …).
        /// Implementiert ein Excel-ähnliches Spalten-Inkrementierungsschema.
        /// Bei leerem Input wird "A" zurückgegeben.
        /// </summary>
        private static string GetNextAlphabeticalEntry(string current)
        {
            if (string.IsNullOrWhiteSpace(current))
                return "A";

            current = current.Trim().ToUpperInvariant();

            char[] chars = current.ToCharArray();
            int index = chars.Length - 1;

            while (index >= 0)
            {
                if (chars[index] < 'A' || chars[index] > 'Z')
                    throw new ArgumentException("Input must contain only letters A-Z.", nameof(current));

                if (chars[index] == 'Z')
                {
                    // Übertrag: 'Z' → 'A' und nächste Stelle inkrementieren
                    chars[index] = 'A';
                    index--;
                }
                else
                {
                    chars[index]++;
                    return new string(chars);
                }
            }

            // Alle Stellen waren 'Z' → neue Stelle vorne anfügen (z.B. "ZZ" → "AAA")
            return "A" + new string(chars);
        }
    }
}