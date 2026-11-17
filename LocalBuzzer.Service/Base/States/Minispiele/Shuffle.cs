namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>
    /// Fisher-Yates ueber die Zufallsquelle des Rahmens (<see cref="MinigameRulesContext.Zufall"/>) - eine Stelle fuer
    /// Blitzrunde und Heisse Kartoffel (V13). Mit fester Quelle kommt jedes Mal dieselbe Folge.
    /// </summary>
    public static class Shuffle
    {
        /// <summary>
        /// Die Zahlen 0 bis <paramref name="anzahl"/> - 1 gemischt. <paramref name="zufall"/> wie
        /// <see cref="Random.Next(int, int)"/>; gewuerfelt wird von hinten, je Stelle i einer aus (0, i + 1).
        /// </summary>
        public static int[] Order(int anzahl, Func<int, int, int> zufall)
        {
            var folge = Enumerable.Range(0, anzahl).ToArray();

            for (var i = anzahl - 1; i > 0; i--)
            {
                var j = Math.Clamp(zufall(0, i + 1), 0, i);
                (folge[i], folge[j]) = (folge[j], folge[i]);
            }

            return folge;
        }
    }
}
