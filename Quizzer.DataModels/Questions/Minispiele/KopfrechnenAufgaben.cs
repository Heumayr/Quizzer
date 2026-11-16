namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine Rechenaufgabe: was das Telefon zeigt und was nur der Server kennt.</summary>
    /// <param name="Text">„7 + 5" - ohne Gleichheitszeichen, das setzt die Anzeige.</param>
    /// <param name="Loesung">Bleibt am Server.</param>
    public sealed record KopfrechnenAufgabe(string Text, int Loesung);

    /// <summary>
    /// Die Aufgabenfolge des Kopfrechnens (M35 C5): Aufgabe n folgt allein aus Startwert, n und Stufe.
    /// So bekommen alle dieselbe Folge, und der Server muss keine Liste fuehren.
    /// <para>
    /// Eigener Zufall (SplitMix64) statt <see cref="Random"/>: dessen Folge zu einem Startwert ist ueber
    /// .NET-Fassungen nicht zugesagt. Alle Ergebnisse sind ganze Zahlen ab 0 - kein Minus am Telefon.
    /// </para>
    /// </summary>
    public static class KopfrechnenAufgaben
    {
        /// <summary>Plus und Minus bis 20 - der Index in den Spielweisen.</summary>
        public const int Leicht = 0;

        /// <summary>Plus und Minus bis 100, kleines Einmaleins.</summary>
        public const int Mittel = 1;

        /// <summary>Plus und Minus bis 1000, Mal und Geteilt.</summary>
        public const int Schwer = 2;

        /// <summary>Die Aufgabe Nummer <paramref name="nummer"/> (ab 1) zu diesem Startwert.</summary>
        public static KopfrechnenAufgabe Aufgabe(int startwert, int nummer, int stufe)
        {
            var wuerfel = new Wuerfel(startwert, nummer);

            return stufe switch
            {
                Mittel => MittelAufgabe(ref wuerfel),
                Schwer => SchwerAufgabe(ref wuerfel),
                _ => LeichtAufgabe(ref wuerfel),
            };
        }

        private static KopfrechnenAufgabe LeichtAufgabe(ref Wuerfel w)
        {
            if (w.Next(0, 2) == 0)
                return Plus(w.Next(1, 11), w.Next(1, 11));

            var a = w.Next(2, 21);
            return Minus(a, w.Next(1, a));
        }

        private static KopfrechnenAufgabe MittelAufgabe(ref Wuerfel w)
        {
            switch (w.Next(0, 3))
            {
                case 0:
                    var a = w.Next(10, 90);
                    return Plus(a, w.Next(2, 100 - a));
                case 1:
                    var m = w.Next(20, 100);
                    return Minus(m, w.Next(2, m));
                default:
                    return Mal(w.Next(2, 11), w.Next(2, 11));
            }
        }

        private static KopfrechnenAufgabe SchwerAufgabe(ref Wuerfel w)
        {
            switch (w.Next(0, 4))
            {
                case 0:
                    return Plus(w.Next(100, 900), w.Next(11, 100));
                case 1:
                    return Minus(w.Next(100, 1000), w.Next(11, 100));
                case 2:
                    return Mal(w.Next(11, 26), w.Next(3, 10));
                default:
                    var teiler = w.Next(3, 13);
                    var ergebnis = w.Next(3, 16);
                    return new($"{teiler * ergebnis} ÷ {teiler}", ergebnis);
            }
        }

        private static KopfrechnenAufgabe Plus(int a, int b) => new($"{a} + {b}", a + b);

        private static KopfrechnenAufgabe Minus(int a, int b) => new($"{a} − {b}", a - b);

        private static KopfrechnenAufgabe Mal(int a, int b) => new($"{a} × {b}", a * b);

        /// <summary>SplitMix64 ueber (Startwert, Nummer) - fest, ohne Abhaengigkeit von der Laufzeit.</summary>
        private struct Wuerfel
        {
            private ulong zustand;

            public Wuerfel(int startwert, int nummer)
                => zustand = ((ulong)(uint)startwert << 32) | (uint)nummer;

            /// <summary>Wie <see cref="Random.Next(int, int)"/>: von einschliesslich bis ausschliesslich.</summary>
            public int Next(int von, int bis)
            {
                zustand += 0x9E3779B97F4A7C15;
                var z = zustand;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
                z ^= z >> 31;

                return von + (int)(z % (ulong)(bis - von));
            }
        }
    }
}
