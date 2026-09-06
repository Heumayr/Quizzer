namespace Quizzer.DataModels.Questions
{
    /// <summary>Wo das Bild im Fenster wirklich liegt - in Punkten der Anzeige.</summary>
    /// <param name="Links">Linker Rand des Bildes.</param>
    /// <param name="Oben">Oberer Rand des Bildes.</param>
    /// <param name="Breite">Ausgelegte Breite.</param>
    /// <param name="Hoehe">Ausgelegte Höhe.</param>
    public readonly record struct Bildlage(double Links, double Oben, double Breite, double Hoehe);

    /// <summary>
    /// Die Formen einer Aufdeckfrage: Ecken ausrechnen, drehen, treffen.
    /// <para>
    /// <b>Gedreht wird in Anzeigekoordinaten, gespeichert werden Ecken.</b> Das ist der Kern der
    /// Sache: eine Drehung im relativen 0-bis-1-Raum <i>schert</i> jede Figur, sobald das Bild
    /// nicht quadratisch ist - aus einem rechten Winkel wird ein schiefer. Also wird gedreht, wo
    /// die Punkte gleich weit auseinanderliegen, und das Ergebnis relativ zurückgeschrieben.
    /// </para>
    /// <para>
    /// <b>Deshalb steht auch kein Winkel im Modell.</b> Ein gespeicherter Winkel wäre eine
    /// Anweisung, die jeder Leser gleich ausführen müsste - und jeder Leser hätte eine andere
    /// Bildgröße. Ecken sind ein Ergebnis: was der Editor zeichnet, ist buchstäblich das, was der
    /// Beamer zeichnet.
    /// </para>
    /// <para>
    /// <b>WPF-frei</b>, wie <see cref="QuestionValidator"/> und aus demselben Grund: so bleibt
    /// jede Rechnung ohne Oberflächen-Thread prüfbar. Gemessen wird gegen ein <b>unquadratisches</b>
    /// Bild - ein quadratisches deckt den Scherfehler nie auf, und Editor und Beamer sähen beide
    /// gleich falsch aus.
    /// </para>
    /// </summary>
    public static class RevealFormen
    {
        /// <summary>Die kleinste Zahl an Werten, die eine Eckenfolge brauchbar macht: drei Paare.</summary>
        public const int MindestwerteFuerEcken = 6;

        /// <summary>
        /// Die Ecken einer Fläche in Punkten der Anzeige.
        /// <para>
        /// <b>Ohne brauchbare Ecken fällt es auf die Hülle zurück</b>, nicht auf ein leeres
        /// Vieleck: lieber zu viel gedeckt als eine Lücke, die niemand meldet.
        /// </para>
        /// </summary>
        public static (double X, double Y)[] EckenAnzeige(RevealArea flaeche, Bildlage lage)
        {
            ArgumentNullException.ThrowIfNull(flaeche);

            var ecken = flaeche.Corners;

            if (ecken == null || ecken.Count < MindestwerteFuerEcken || ecken.Count % 2 != 0)
                return Kastenecken(flaeche, lage);

            var ergebnis = new (double X, double Y)[ecken.Count / 2];

            for (var i = 0; i < ergebnis.Length; i++)
            {
                ergebnis[i] = (
                    lage.Links + ecken[i * 2] * lage.Breite,
                    lage.Oben + ecken[i * 2 + 1] * lage.Hoehe);
            }

            return ergebnis;
        }

        private static (double X, double Y)[] Kastenecken(RevealArea f, Bildlage lage)
        {
            var links = lage.Links + f.X * lage.Breite;
            var oben = lage.Oben + f.Y * lage.Hoehe;
            var rechts = links + f.W * lage.Breite;
            var unten = oben + f.H * lage.Hoehe;

            return [(links, oben), (rechts, oben), (rechts, unten), (links, unten)];
        }

        /// <summary>
        /// Baut eine Fläche aus Anzeigeecken - relativ gespeichert, mit nachgezogener Hülle.
        /// </summary>
        public static RevealArea AusEckenAnzeige(
            IReadOnlyList<(double X, double Y)> ecken, Bildlage lage, int schritt)
        {
            ArgumentNullException.ThrowIfNull(ecken);

            if (ecken.Count < 3 || lage.Breite <= 0 || lage.Hoehe <= 0)
                throw new ArgumentException("Zu wenige Ecken oder keine Bildlage.", nameof(ecken));

            var flach = new List<double>(ecken.Count * 2);

            foreach (var (x, y) in ecken)
            {
                flach.Add((x - lage.Links) / lage.Breite);
                flach.Add((y - lage.Oben) / lage.Hoehe);
            }

            var links = flach.Where((_, i) => i % 2 == 0).Min();
            var rechts = flach.Where((_, i) => i % 2 == 0).Max();
            var oben = flach.Where((_, i) => i % 2 == 1).Min();
            var unten = flach.Where((_, i) => i % 2 == 1).Max();

            return new RevealArea(
                links, oben, rechts - links, unten - oben, flach, schritt);
        }

        /// <summary>
        /// Dreht eine Fläche um ihren eigenen Schwerpunkt - <b>in Anzeigekoordinaten</b>, siehe
        /// Klassenkommentar.
        /// </summary>
        /// <param name="flaeche">Die Fläche.</param>
        /// <param name="winkelGrad">Um wie viel, im Uhrzeigersinn.</param>
        /// <param name="lage">Wo das Bild liegt.</param>
        public static RevealArea Gedreht(RevealArea flaeche, double winkelGrad, Bildlage lage)
        {
            var ecken = EckenAnzeige(flaeche, lage);

            var mitteX = ecken.Average(e => e.X);
            var mitteY = ecken.Average(e => e.Y);

            var bogen = winkelGrad * Math.PI / 180.0;
            var sin = Math.Sin(bogen);
            var cos = Math.Cos(bogen);

            var gedreht = ecken
                .Select(e =>
                {
                    var dx = e.X - mitteX;
                    var dy = e.Y - mitteY;

                    return (X: mitteX + dx * cos - dy * sin, Y: mitteY + dx * sin + dy * cos);
                })
                .ToList();

            return AusEckenAnzeige(gedreht, lage, flaeche.Step ?? 0);
        }

        /// <summary>
        /// Ob ein Punkt der Anzeige in dieser Fläche liegt - Strahlenmethode über die Ecken.
        /// <para>
        /// Gerechnet wird über die <b>echten Ecken</b>, nicht über die Hülle. Sonst wäre bei einem
        /// Dreieck fast die halbe Hülle anklickbar, in der nichts liegt - und die Nachbarfläche
        /// darunter unerreichbar.
        /// </para>
        /// </summary>
        public static bool Trifft(RevealArea flaeche, double x, double y, Bildlage lage)
        {
            var ecken = EckenAnzeige(flaeche, lage);

            var drin = false;

            for (int i = 0, j = ecken.Length - 1; i < ecken.Length; j = i++)
            {
                var (xi, yi) = ecken[i];
                var (xj, yj) = ecken[j];

                if (yi > y != yj > y
                    && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                {
                    drin = !drin;
                }
            }

            return drin;
        }

        /// <summary>
        /// Ein Dreieck über der aufgezogenen Fläche: Spitze oben Mitte, Grundlinie unten.
        /// <para>
        /// <b>Aus derselben Ziehgeste wie das Rechteck.</b> Eckpunkte einzeln zu klicken wäre
        /// Grafikarbeit; hier werden abends vor einem Quiz Fragen angelegt.
        /// </para>
        /// </summary>
        public static RevealArea Dreieck(double x, double y, double w, double h, int schritt)
            => new(x, y, w, h,
                [x + w / 2, y, x + w, y + h, x, y + h],
                schritt);

        /// <summary>Ein Rechteck als Fläche - die Ecken bleiben leer, der Kasten genügt.</summary>
        public static RevealArea Rechteck(double x, double y, double w, double h, int schritt)
            => new(x, y, w, h, null, schritt);
    }
}
