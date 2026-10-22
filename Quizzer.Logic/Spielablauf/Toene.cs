namespace Quizzer.Logic.Spielablauf
{
    /// <summary>
    /// Die Töne auf dem Beamer (V5, 2026-10-22): wer zuerst gebuzzert hat, ein Tick in den letzten
    /// Sekunden des Countdowns, ein Gong an seinem Ende.
    /// <para>
    /// <b>Gerechnet, nicht mitgeliefert</b> - wie die Demo-Flaggen: keine Lizenzfrage, keine Datei,
    /// die im Ausgabeordner fehlen kann. Ein WAV aus ein paar Sinustönen mit weichem Ein- und
    /// Ausklang (ohne knackt es).
    /// </para>
    /// </summary>
    public static class Toene
    {
        public const int Abtastrate = 22050;

        /// <summary>Zwei helle Töne hintereinander, aufsteigend - „da war wer".</summary>
        public static byte[] Buzzer() => Wav(Folge((880, 0.09), (1320, 0.16)), 0.55);

        /// <summary>Ein kurzer, leiser Tick.</summary>
        public static byte[] Tick() => Wav(Folge((1000, 0.04)), 0.3);

        /// <summary>Ein tiefer, abklingender Ton - „Zeit ist um".</summary>
        public static byte[] Ende() => Wav(Gong(392, 0.9), 0.6);

        private static float[] Folge(params (double Hz, double Sekunden)[] toene)
        {
            var teile = toene.Select(t => Ton(t.Hz, t.Sekunden)).ToList();
            return teile.SelectMany(t => t).ToArray();
        }

        private static float[] Ton(double hz, double sekunden)
        {
            var n = (int)(Abtastrate * sekunden);
            var rampe = Math.Min(n / 4, Abtastrate / 200); // 5 ms
            var werte = new float[n];

            for (var i = 0; i < n; i++)
            {
                var huelle = Math.Min(1.0, Math.Min(i, n - 1 - i) / (double)Math.Max(1, rampe));
                werte[i] = (float)(Math.Sin(2 * Math.PI * hz * i / Abtastrate) * huelle);
            }

            return werte;
        }

        private static float[] Gong(double hz, double sekunden)
        {
            var n = (int)(Abtastrate * sekunden);
            var werte = new float[n];

            for (var i = 0; i < n; i++)
            {
                var t = i / (double)Abtastrate;
                var einsatz = Math.Min(1.0, i / (Abtastrate * 0.005));
                var abklingen = Math.Exp(-4.0 * t);
                var klang = Math.Sin(2 * Math.PI * hz * t) + 0.35 * Math.Sin(2 * Math.PI * hz * 2 * t);
                werte[i] = (float)(klang / 1.35 * einsatz * abklingen);
            }

            return werte;
        }

        /// <summary>16 Bit, mono, PCM - das kann jede Windows-Wiedergabe.</summary>
        internal static byte[] Wav(float[] werte, double lautstaerke)
        {
            using var ziel = new MemoryStream();
            using var w = new BinaryWriter(ziel);

            var daten = werte.Length * 2;

            w.Write("RIFF"u8);
            w.Write(36 + daten);
            w.Write("WAVE"u8);
            w.Write("fmt "u8);
            w.Write(16);
            w.Write((short)1);            // PCM
            w.Write((short)1);            // mono
            w.Write(Abtastrate);
            w.Write(Abtastrate * 2);      // Bytes je Sekunde
            w.Write((short)2);            // Bytes je Abtastwert
            w.Write((short)16);           // Bit
            w.Write("data"u8);
            w.Write(daten);

            foreach (var wert in werte)
                w.Write((short)Math.Round(Math.Clamp(wert * lautstaerke, -1, 1) * short.MaxValue));

            w.Flush();
            return ziel.ToArray();
        }
    }
}
