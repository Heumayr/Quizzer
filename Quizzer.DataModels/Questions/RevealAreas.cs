using System.Text.Json;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Eine verdeckende Fläche über dem Bild einer Aufdeckfrage - relativ zum Bild, nicht in
    /// Bildpunkten.
    /// <para>
    /// <b>Relativ, weil dasselbe Bild in drei Größen erscheint:</b> auf dem Beamer, im
    /// Spielleiterfenster und in der Vorschau. Absolute Punkte säßen überall woanders.
    /// </para>
    /// <para>
    /// <b>Seit Meldung 23 (2026-09-06) trägt sie zwei Dinge mehr:</b> zu welchem Schritt sie
    /// gehört - mehrere Flächen dürfen zusammen fallen - und ihre Ecken, denn ein gedrehtes
    /// Dreieck ist kein Kasten mit einem Winkel daneben.
    /// </para>
    /// <para>
    /// <b>X/Y/W/H bleiben Pflicht</b> und sind bei einem Vieleck die <i>Hülle</i> seiner Ecken.
    /// Sie sind kein Altlastfeld, sondern der Notnagel: ein Stand ohne Kenntnis der Ecken malt die
    /// Hülle, und die enthält das Vieleck. Dieser Fragetyp darf in genau eine Richtung nie irren -
    /// er darf nie <b>mehr</b> zeigen als gewollt.
    /// </para>
    /// <para>
    /// <b>Kein <c>==</c>, kein <c>Distinct</c>, kein <c>Contains</c> auf dieser Art.</b> Der
    /// record vergleicht <c>Corners</c> per Referenz; zwei strukturgleiche Flächen sind deshalb
    /// <b>nicht</b> gleich. Heute ruft das niemand - es bleibt eine Mine.
    /// </para>
    /// </summary>
    /// <param name="X">Linker Rand der Hülle, 0 bis 1.</param>
    /// <param name="Y">Oberer Rand der Hülle, 0 bis 1.</param>
    /// <param name="W">Breite der Hülle, 0 bis 1.</param>
    /// <param name="H">Höhe der Hülle, 0 bis 1.</param>
    /// <param name="Corners">
    /// Die Ecken als flache Folge relativer Zahlenpaare (x1,y1,x2,y2,…) - drei Paare ein Dreieck,
    /// vier ein auch gedrehtes Viereck. <c>null</c> heißt: die Fläche ist der Kasten selbst.
    /// </param>
    /// <param name="Step">
    /// Zu welchem Aufdeckschritt sie gehört, ab 0. <b><c>null</c> ist die Kennung des
    /// Altbestands</b> und heißt „meine Position in der Liste ist mein Schritt" - genau die Regel,
    /// die bis zum 2026-09-06 galt. Deshalb <c>int?</c> und nicht <c>int</c>: ein fehlendes Feld
    /// wäre von einer ausgeschriebenen <c>0</c> sonst nicht zu unterscheiden.
    /// </param>
    public sealed record RevealArea(
        double X, double Y, double W, double H,
        IReadOnlyList<double>? Corners = null,
        int? Step = null);

    /// <summary>
    /// Liest und schreibt die Flächen einer Aufdeckfrage - und rechnet aus, was auf einem
    /// bestimmten Schritt zu sehen ist.
    /// <para>
    /// <b>Die Rechnung steht hier und nicht in der Ansicht.</b> Beamer, Spielleiterfenster und
    /// Vorschau zeigen dasselbe Bild; drei Stellen mit derselben Rechnung laufen auseinander.
    /// </para>
    /// </summary>
    public static class RevealAreas
    {
        /// <summary>
        /// <b><c>WhenWritingNull</c> ist nicht Kosmetik.</b> Nur damit wird eine unveränderte
        /// Altfläche zeichengleich zurückgeschrieben - ohne die beiden neuen Felder als
        /// <c>null</c>. Deshalb ändert das bloße Öffnen und Übernehmen einer Bestandsfrage nichts.
        /// </summary>
        private static readonly JsonSerializerOptions Options = new()
        {
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Liest die Flächen. Eine unlesbare Angabe ergibt eine leere Liste statt einer Ausnahme -
        /// eine kaputte Frage soll den Spielabend nicht beenden.
        /// </summary>
        public static List<RevealArea> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<RevealArea>>(json, Options) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>Schreibt die Flächen zurück.</summary>
        public static string ToJson(IEnumerable<RevealArea> flaechen)
            => JsonSerializer.Serialize(flaechen.ToList(), Options);

        /// <summary>
        /// Welche Flächen auf diesem Schritt noch verdecken.
        /// <para>
        /// Bis einschließlich des Fragebildschirms ist alles verdeckt; danach fällt je
        /// Inhaltsschritt eine Fläche weg, und auf dem Abschluss ist das Bild frei.
        /// </para>
        /// </summary>
        /// <param name="flaechen">Alle Flächen der Frage.</param>
        /// <param name="aufgedeckt">Wie viele Schritte bereits gezeigt wurden.</param>
        public static IEnumerable<RevealArea> NochVerdeckt(
            IReadOnlyList<RevealArea> flaechen, int aufgedeckt)
            => flaechen.Where((f, i) => SchrittVon(f, i) >= Math.Max(aufgedeckt, 0));

        /// <summary>
        /// Zu welchem Schritt eine Fläche gehört.
        /// <para>
        /// <b>Ohne eigene Angabe gilt die Position</b> - das ist keine Übergangsregel, sondern die
        /// Definition des Altbestands: dort stand jede Fläche allein in ihrem Schritt. Für eine
        /// Altliste liefert die neue Rechnung zeichenweise dieselbe Menge wie das frühere
        /// <c>Skip(n)</c>.
        /// </para>
        /// </summary>
        public static int SchrittVon(RevealArea flaeche, int index)
            => Math.Max(flaeche.Step ?? index, 0);

        /// <summary>Wie viele Aufdeckschritte die Flächen ergeben.</summary>
        public static int Schrittzahl(IReadOnlyList<RevealArea> flaechen)
            => flaechen.Count == 0 ? 0 : flaechen.Select(SchrittVon).Max() + 1;

        /// <summary>
        /// Schreibt jeder Fläche ihre Schrittnummer ausdrücklich hin - <b>beim Laden im Editor</b>.
        /// <para>
        /// <b>Sonst hängt die Taktung an der Position, und Löschen verschiebt sie.</b> Wer aus
        /// einer Altliste die dritte von fünf Flächen entfernt, ändert damit lautlos den Schritt
        /// aller folgenden - bis zum Spielabend unbemerkt.
        /// </para>
        /// </summary>
        public static List<RevealArea> MitSchritt(IReadOnlyList<RevealArea> flaechen)
            => [.. flaechen.Select((f, i) => f.Step == null ? f with { Step = SchrittVon(f, i) } : f)];

        /// <summary>
        /// Bringt die Flächen in den Zustand, auf den sich alles Weitere verlässt - <b>beim
        /// Übernehmen</b>.
        /// <para>
        /// Drei Zusagen, und sie sind keine Kosmetik: <b>nach Schritt sortiert</b>, <b>lückenlos ab
        /// 0</b>, <b>mindestens eine Fläche je Schritt</b>. Nur damit gilt „Schritt kleiner gleich
        /// Position" - und nur daraus folgt, dass ein Stand ohne Kenntnis der Schritte <i>mehr</i>
        /// verdeckt statt weniger. Fiele eine der drei weg, könnte eine ältere Fassung ein Stück
        /// Bild verraten.
        /// </para>
        /// <para>
        /// <b>Und die Hülle wird nachgezogen</b>, wo Ecken gesetzt sind. Eine Hülle, die kleiner
        /// ist als ihr Vieleck, ist der einzige Weg, auf dem diese Fassung zu viel zeigen kann.
        /// </para>
        /// </summary>
        public static List<RevealArea> Normalisiert(IReadOnlyList<RevealArea> flaechen)
        {
            var gruppen = MitSchritt(flaechen)
                .GroupBy(f => f.Step ?? 0)
                .OrderBy(g => g.Key)
                .ToList();

            var ergebnis = new List<RevealArea>();

            for (var neuerSchritt = 0; neuerSchritt < gruppen.Count; neuerSchritt++)
            {
                foreach (var flaeche in gruppen[neuerSchritt])
                    ergebnis.Add(MitHuelle(flaeche) with { Step = neuerSchritt });
            }

            return ergebnis;
        }

        /// <summary>Rechnet die Hülle aus den Ecken nach, wo welche gesetzt sind.</summary>
        private static RevealArea MitHuelle(RevealArea flaeche)
        {
            var ecken = flaeche.Corners;

            if (ecken == null || ecken.Count < 6 || ecken.Count % 2 != 0)
                return flaeche;

            double links = double.MaxValue, oben = double.MaxValue;
            double rechts = double.MinValue, unten = double.MinValue;

            for (var i = 0; i + 1 < ecken.Count; i += 2)
            {
                links = Math.Min(links, ecken[i]);
                rechts = Math.Max(rechts, ecken[i]);
                oben = Math.Min(oben, ecken[i + 1]);
                unten = Math.Max(unten, ecken[i + 1]);
            }

            return flaeche with { X = links, Y = oben, W = rechts - links, H = unten - oben };
        }

        /// <summary>
        /// Wie unscharf das Bild auf diesem Schritt ist.
        /// <para>
        /// Läuft von <paramref name="start"/> auf null - der letzte Inhaltsschritt zeigt das Bild
        /// scharf. Ohne Inhaltsschritte gibt es nichts zu schärfen, dann ist es sofort scharf:
        /// ein dauerhaft unscharfes Bild wäre eine Frage, die niemand beantworten kann.
        /// </para>
        /// </summary>
        /// <param name="start">Die Unschärfe im ersten Schritt.</param>
        /// <param name="schritte">Wie viele Inhaltsschritte es gibt.</param>
        /// <param name="aufgedeckt">Wie viele davon schon gezeigt wurden.</param>
        public static double Unschaerfe(double start, int schritte, int aufgedeckt)
        {
            if (schritte <= 0 || start <= 0)
                return 0;

            var offen = Math.Clamp(schritte - aufgedeckt, 0, schritte);

            return start * offen / schritte;
        }

        /// <summary>
        /// Wie grob das Raster auf diesem Schritt ist - als Kantenlänge eines Blocks, gemessen in
        /// Punkten der Anzeige.
        /// <para>
        /// Läuft von grob auf fein und ist im letzten Schritt <c>0</c>, also unverpixelt. Ohne
        /// Inhaltsschritte gibt es nichts zu verfeinern, dann ist das Bild sofort scharf -
        /// dieselbe Regel wie bei der Unschärfe, und aus demselben Grund.
        /// </para>
        /// <para>
        /// <b>Dieselbe Einheit wie der Unschärferadius</b>, und deshalb dieselbe Rechnung: der
        /// Schieber im Editor stellt beide Betriebsarten, ein Wert von 40 muss in beiden ungefähr
        /// gleich stark wirken.
        /// </para>
        /// </summary>
        /// <param name="start">Die Blockkante im ersten Schritt.</param>
        /// <param name="schritte">Wie viele Inhaltsschritte es gibt.</param>
        /// <param name="aufgedeckt">Wie viele davon schon gezeigt wurden.</param>
        public static double Rasterung(double start, int schritte, int aufgedeckt)
            => Unschaerfe(start, schritte, aufgedeckt);

    }
}
