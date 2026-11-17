using Quizzer.Base;
using Quizzer.Logic.Statistics;
using System.Windows;
using System.Windows.Media;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>Eine Linie im Diagramm: Punkte im Zeichenfeld, Farbe und Strich, dazu Name und Endstand am Ende.</summary>
    /// <param name="NameOben">Wo der Name steht - bei gleichem Endstand auseinandergerückt; der Verbinder führt zur Linie.</param>
    public sealed record VerlaufLinie(
        string Name, PointCollection Punkte, Brush Farbe, DoubleCollection Strich,
        double EndeX, double EndeY, double NameLinks, double NameOben, string NameText)
    {
        /// <summary>Wohin der Verbinder vom Linienende führt - die Mitte der Namenszeile.</summary>
        public double NameMitte => NameOben + PunkteverlaufViewModel.Zeilenabstand / 2;
    }

    /// <summary>
    /// Die Tafel „Punkteverlauf" am Spielende (M37 E06): rechnet die Reihen aus <see cref="Punkteverlauf"/> in ein festes
    /// Zeichenfeld um - der Beamer skaliert es mit seiner Viewbox. Nie Information allein über Farbe: jede Linie trägt am
    /// Ende Namen und Endstand, ab der elften Farbe gestrichelt.
    /// </summary>
    public sealed class PunkteverlaufViewModel : UcViewModelBase
    {
        public const double Breite = 1500;
        public const double Hoehe = 760;

        /// <summary>Das Zeichenfeld der Linien: links Platz für die Achsenwerte, rechts für die Namen.</summary>
        public const double Links = 110, Rechts = 1160, Oben = 30, Unten = 680;

        /// <summary>Mindestabstand zweier Namen, eine Zeile bei Schriftgröße 30.</summary>
        public const double Zeilenabstand = 42;

        /// <summary>Hell auf dunklem Grund, untereinander unterscheidbar.</summary>
        private static readonly string[] Farben =
            ["#4FC3F7", "#FFB74D", "#81C784", "#F06292", "#FFF176", "#BA68C8", "#4DB6AC", "#E57373", "#A1887F", "#90A4AE"];

        private readonly double minimum;
        private readonly double maximum;

        public PunkteverlaufViewModel(Verlauf verlauf)
        {
            ArgumentNullException.ThrowIfNull(verlauf);
            Verlauf = verlauf;

            minimum = Math.Min(0, verlauf.Minimum);
            maximum = Math.Max(0, verlauf.Maximum);
            if (maximum == minimum)
                maximum = minimum + 1;

            Linien = Beschrifte(verlauf.Reihen.Select(Linie).ToList());
        }

        public Verlauf Verlauf { get; }

        public IReadOnlyList<VerlaufLinie> Linien { get; }

        public double ZeichenBreite => Breite;
        public double ZeichenHoehe => Hoehe;
        public double LinksX => Links;
        public double RechtsX => Rechts;
        public double RechtsAbstand => Breite - Rechts;

        public double NullLinieY => Y(0);
        public double ObenTextY => Oben - 20;
        public double NullTextY => NullLinieY - 20;
        public double UntenTextY => Unten - 20;
        public double AchseY => Unten + 24;

        public string ObenText => Verlauf.Maximum > 0 ? Verlauf.Maximum.ToString() : string.Empty;
        public string UntenText => Verlauf.Minimum < 0 ? Verlauf.Minimum.ToString() : string.Empty;

        public string FragenText => Verlauf.Zellen.Count == 1 ? "nach 1 Frage" : $"nach {Verlauf.Zellen.Count} Fragen";

        public double X(int schritt) => Links + schritt * (Rechts - Links) / Verlauf.Zellen.Count;

        public double Y(int punkte) => Oben + (maximum - punkte) * (Unten - Oben) / (maximum - minimum);

        private VerlaufLinie Linie(VerlaufReihe reihe, int index)
        {
            var punkte = new PointCollection(reihe.Punkte.Select((p, i) => new Point(X(i), Y(p))));
            punkte.Freeze();

            var farbe = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Farben[index % Farben.Length]));
            farbe.Freeze();

            var strich = index < Farben.Length ? new DoubleCollection() : new DoubleCollection([3, 2]);
            strich.Freeze();

            var endeY = Y(reihe.Endstand);
            return new VerlaufLinie(reihe.Name, punkte, farbe, strich, Rechts, endeY, Rechts + 24, endeY - Zeilenabstand / 2,
                $"{reihe.Name}: {reihe.Endstand}");
        }

        /// <summary>Rückt die Namen auseinander, wo Endstände nah beieinander liegen, und hält sie im Bild.</summary>
        private static List<VerlaufLinie> Beschrifte(List<VerlaufLinie> linien)
        {
            var geordnet = linien.OrderBy(l => l.NameOben).ToList();
            var oben = geordnet.Select(l => l.NameOben).ToArray();

            for (var i = 0; i < oben.Length; i++)
                oben[i] = Math.Max(oben[i], i == 0 ? 0 : oben[i - 1] + Zeilenabstand);

            for (var i = oben.Length - 1; i >= 0; i--)
                oben[i] = Math.Min(oben[i], i == oben.Length - 1 ? Hoehe - Zeilenabstand : oben[i + 1] - Zeilenabstand);

            var verschoben = geordnet.Select((l, i) => l with { NameOben = oben[i] }).ToList();
            return linien.Select(l => verschoben.First(v => ReferenceEquals(v.Punkte, l.Punkte))).ToList();
        }
    }
}
