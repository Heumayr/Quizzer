namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Ein Kartenmotiv des Memorys: das Zeichen und sein Name fuer Vorleser.</summary>
    public sealed record MemoryMotiv(string Symbol, string Name);

    /// <summary>
    /// Die Belegung des Memorys (M35 C5): welches Motiv auf welcher Karte liegt. Sie entsteht je Runde am
    /// Server und verlaesst ihn nie als Ganzes.
    /// <para>
    /// Emoji mit eindeutiger Form statt Farben - sie unterscheiden sich auch ohne Farbsehen. Keine mit
    /// Variantenzeichen (☂️, ✈️): die zeigen manche Telefone als Textzeichen.
    /// </para>
    /// </summary>
    public static class MemoryBelegung
    {
        /// <summary>Mehr Motive als Paare in der groessten Spielweise - jede Runde zieht daraus neu.</summary>
        public static IReadOnlyList<MemoryMotiv> Motive { get; } =
        [
            new("🍎", "Apfel"), new("🍌", "Banane"), new("🍇", "Trauben"), new("🍓", "Erdbeere"),
            new("🍕", "Pizza"), new("🐶", "Hund"), new("🐱", "Katze"), new("🐟", "Fisch"),
            new("🐢", "Schildkröte"), new("🦋", "Schmetterling"), new("🌻", "Sonnenblume"), new("🌙", "Mond"),
            new("⭐", "Stern"), new("⚽", "Fußball"), new("🎈", "Luftballon"), new("🎸", "Gitarre"),
            new("🚗", "Auto"), new("🚀", "Rakete"), new("🔑", "Schlüssel"), new("🎁", "Geschenk"),
            new("🔔", "Glocke"), new("🏠", "Haus"), new("👑", "Krone"), new("🌵", "Kaktus"),
        ];

        /// <summary>Die Paare zur Spielweise: klein 6, mittel 8, gross 10 - der Index ist <c>MinigameQuestion.Variant</c>.</summary>
        public static int PaareFuer(int variant) => variant switch
        {
            1 => 8,
            2 => 10,
            _ => 6,
        };

        /// <summary>
        /// Mischt eine Belegung: je Karte der Index in <see cref="Motive"/>, jedes Motiv genau zweimal.
        /// <para>
        /// <paramref name="zufall"/> wie <see cref="Random.Next(int, int)"/>. Die Reihenfolge der Wuerfe ist
        /// fest (Tests bauen darauf): erst je Paar k einer aus (k, Motive.Count) fuer die Motive, dann
        /// Fisher-Yates ueber die Karten von hinten, je Karte i einer aus (0, i + 1).
        /// </para>
        /// </summary>
        public static int[] Mische(int paare, Func<int, int, int> zufall)
        {
            ArgumentNullException.ThrowIfNull(zufall);
            paare = Math.Clamp(paare, 1, Motive.Count);

            var motive = Enumerable.Range(0, Motive.Count).ToArray();
            for (var k = 0; k < paare; k++)
            {
                var j = Math.Clamp(zufall(k, motive.Length), k, motive.Length - 1);
                (motive[k], motive[j]) = (motive[j], motive[k]);
            }

            var karten = Enumerable.Range(0, paare * 2).Select(i => motive[i / 2]).ToArray();
            for (var i = karten.Length - 1; i > 0; i--)
            {
                var j = Math.Clamp(zufall(0, i + 1), 0, i);
                (karten[i], karten[j]) = (karten[j], karten[i]);
            }

            return karten;
        }
    }
}
