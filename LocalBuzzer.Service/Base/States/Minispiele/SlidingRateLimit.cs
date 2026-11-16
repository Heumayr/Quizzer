namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>
    /// Plausibilitaetsgrenze je Spieler (M35 C5): hoechstens <c>max</c> Abgaben in einem gleitenden Fenster
    /// an der Serveruhr. Gleitend statt Mindestabstand, weil das WLAN zwei ehrliche Abgaben zusammenschieben kann.
    /// </summary>
    internal sealed class SlidingRateLimit
    {
        private readonly int max;
        private readonly TimeSpan fenster;
        private readonly Queue<TimeSpan> zeiten = new();

        public SlidingRateLimit(int max, TimeSpan fenster)
        {
            this.max = max;
            this.fenster = fenster;
        }

        /// <summary>Nimmt eine Abgabe zum Zeitpunkt <paramref name="jetzt"/> an; <c>false</c> heisst zu viele.</summary>
        public bool TryTake(TimeSpan jetzt)
        {
            while (zeiten.Count > 0 && jetzt - zeiten.Peek() >= fenster)
                zeiten.Dequeue();

            if (zeiten.Count >= max)
                return false;

            zeiten.Enqueue(jetzt);
            return true;
        }
    }
}
