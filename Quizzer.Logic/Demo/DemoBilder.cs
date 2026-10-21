using Quizzer.DataModels;
using System.IO.Compression;
using System.Text;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Zeichnet die Bilder der Typenschau selbst: einfache Flaggen als PNG.
    /// <para>
    /// <b>Warum gezeichnet und nicht mitgeliefert:</b> die erste Demo hängt an, „was im
    /// Ressourcenordner ohnehin liegt" - auf einem frischen Rechner liegt dort nichts, und dann
    /// fehlt genau die Bilderfrage, die man prüfen wollte. Eine Aufdeckfrage ohne Bild ist sogar
    /// unspeicherbar. Flaggen sind Streifen und Kreise; dafür reicht ein Bildpunkt-Raster und ein
    /// PNG-Schreiber von sechzig Zeilen, ohne Grafikbibliothek, auf jedem Betriebssystem.
    /// </para>
    /// </summary>
    internal static class DemoBilder
    {
        internal const int Breite = 600;
        internal const int Hoehe = 400;

        private readonly record struct Farbe(byte R, byte G, byte B);

        private static readonly Farbe Weiss = new(255, 255, 255);
        private static readonly Farbe Schwarz = new(0, 0, 0);
        private static readonly Farbe Rot = new(221, 0, 0);
        private static readonly Farbe Gold = new(255, 206, 0);
        private static readonly Farbe Gruen = new(0, 146, 70);
        private static readonly Farbe Blau = new(0, 85, 164);
        private static readonly Farbe Orange = new(255, 136, 62);
        private static readonly Farbe Japanrot = new(188, 0, 45);
        private static readonly Farbe Schweizrot = new(218, 41, 28);
        private static readonly Farbe Belgiengelb = new(253, 218, 36);
        private static readonly Farbe Belgienrot = new(239, 51, 64);

        /// <summary>Die Flaggen, die die Typenschau braucht - Dateiname ohne Endung zu Zeichnung.</summary>
        private static readonly Dictionary<string, Func<Farbe[,]>> Flaggen = new()
        {
            ["deutschland"] = () => Quer(Schwarz, Rot, Gold),
            ["oesterreich"] = () => Quer(Rot, Weiss, Rot),
            ["italien"] = () => Laengs(Gruen, Weiss, Rot),
            ["frankreich"] = () => Laengs(Blau, Weiss, Rot),
            ["irland"] = () => Laengs(Gruen, Weiss, Orange),
            ["belgien"] = () => Laengs(Schwarz, Belgiengelb, Belgienrot),
            ["japan"] = Japan,
            ["schweiz"] = Schweiz,
        };

        /// <summary>Der Dateiname, unter dem eine Flagge im Ressourcenordner liegt.</summary>
        internal static string Dateiname(string flagge) => $"demo-flagge-{flagge}.png";

        /// <summary>
        /// Schreibt alle Flaggen in den Ressourcenordner, soweit sie fehlen.
        /// </summary>
        /// <returns>
        /// <c>false</c>, wenn der Ordner nicht anlegbar ist (etwa weil das Laufwerk aus
        /// <c>appsettings.json</c> fehlt) - dann legt die Typenschau die Bilderfragen nicht an.
        /// </returns>
        internal static bool SchreibeAlle()
        {
            var ordner = Settings.ResourceRootFolder;

            if (string.IsNullOrWhiteSpace(ordner))
                return false;

            try
            {
                Directory.CreateDirectory(ordner);

                foreach (var (name, zeichne) in Flaggen)
                {
                    var pfad = Path.Combine(ordner, Dateiname(name));

                    // Nie ueberschreiben: liegt dort schon etwas, hat es jemand hingelegt.
                    if (!File.Exists(pfad))
                        File.WriteAllBytes(pfad, Png(zeichne()));
                }

                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static Farbe[,] Quer(Farbe oben, Farbe mitte, Farbe unten)
        {
            var bild = new Farbe[Breite, Hoehe];

            for (var y = 0; y < Hoehe; y++)
            {
                var farbe = y < Hoehe / 3 ? oben : y < Hoehe * 2 / 3 ? mitte : unten;

                for (var x = 0; x < Breite; x++)
                    bild[x, y] = farbe;
            }

            return bild;
        }

        private static Farbe[,] Laengs(Farbe links, Farbe mitte, Farbe rechts)
        {
            var bild = new Farbe[Breite, Hoehe];

            for (var x = 0; x < Breite; x++)
            {
                var farbe = x < Breite / 3 ? links : x < Breite * 2 / 3 ? mitte : rechts;

                for (var y = 0; y < Hoehe; y++)
                    bild[x, y] = farbe;
            }

            return bild;
        }

        private static Farbe[,] Japan()
        {
            var bild = new Farbe[Breite, Hoehe];
            var r = Hoehe * 3 / 10;

            for (var x = 0; x < Breite; x++)
            {
                for (var y = 0; y < Hoehe; y++)
                {
                    var dx = x - Breite / 2;
                    var dy = y - Hoehe / 2;

                    bild[x, y] = dx * dx + dy * dy <= r * r ? Japanrot : Weiss;
                }
            }

            return bild;
        }

        private static Farbe[,] Schweiz()
        {
            var bild = new Farbe[Breite, Hoehe];

            // Das Kreuz: Balken von 1/5 der Hoehe, Arme bis 3/10 vom Mittelpunkt.
            var balken = Hoehe / 10;
            var arm = Hoehe * 3 / 10;

            for (var x = 0; x < Breite; x++)
            {
                for (var y = 0; y < Hoehe; y++)
                {
                    var dx = Math.Abs(x - Breite / 2);
                    var dy = Math.Abs(y - Hoehe / 2);

                    var kreuz = (dx <= balken && dy <= arm) || (dy <= balken && dx <= arm);

                    bild[x, y] = kreuz ? Weiss : Schweizrot;
                }
            }

            return bild;
        }

        /// <summary>Ein PNG ohne Transparenz: Kopf, ein Datenblock, Ende.</summary>
        private static byte[] Png(Farbe[,] bild)
        {
            var breite = bild.GetLength(0);
            var hoehe = bild.GetLength(1);

            using var roh = new MemoryStream();

            using (var zlib = new ZLibStream(roh, CompressionLevel.Optimal, leaveOpen: true))
            {
                var zeile = new byte[1 + breite * 3];

                for (var y = 0; y < hoehe; y++)
                {
                    zeile[0] = 0; // kein Filter

                    for (var x = 0; x < breite; x++)
                    {
                        var f = bild[x, y];
                        zeile[1 + x * 3] = f.R;
                        zeile[2 + x * 3] = f.G;
                        zeile[3 + x * 3] = f.B;
                    }

                    zlib.Write(zeile);
                }
            }

            using var png = new MemoryStream();

            png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

            var kopf = new byte[13];
            SchreibeZahl(kopf, 0, (uint)breite);
            SchreibeZahl(kopf, 4, (uint)hoehe);
            kopf[8] = 8;  // Bit je Kanal
            kopf[9] = 2;  // RGB
            // Kompression, Filter, Zeilensprung: 0

            Block(png, "IHDR", kopf);
            Block(png, "IDAT", roh.ToArray());
            Block(png, "IEND", []);

            return png.ToArray();
        }

        private static void Block(Stream ziel, string typ, byte[] daten)
        {
            var laenge = new byte[4];
            SchreibeZahl(laenge, 0, (uint)daten.Length);
            ziel.Write(laenge);

            var typUndDaten = new byte[4 + daten.Length];
            Encoding.ASCII.GetBytes(typ).CopyTo(typUndDaten, 0);
            daten.CopyTo(typUndDaten, 4);
            ziel.Write(typUndDaten);

            var pruefsumme = new byte[4];
            SchreibeZahl(pruefsumme, 0, Crc32(typUndDaten));
            ziel.Write(pruefsumme);
        }

        private static void SchreibeZahl(byte[] ziel, int stelle, uint wert)
        {
            ziel[stelle] = (byte)(wert >> 24);
            ziel[stelle + 1] = (byte)(wert >> 16);
            ziel[stelle + 2] = (byte)(wert >> 8);
            ziel[stelle + 3] = (byte)wert;
        }

        private static readonly uint[] CrcTabelle = BaueCrcTabelle();

        private static uint[] BaueCrcTabelle()
        {
            var tabelle = new uint[256];

            for (uint n = 0; n < 256; n++)
            {
                var c = n;

                for (var k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;

                tabelle[n] = c;
            }

            return tabelle;
        }

        private static uint Crc32(byte[] daten)
        {
            var c = 0xFFFFFFFFu;

            foreach (var b in daten)
                c = CrcTabelle[(c ^ b) & 0xFF] ^ (c >> 8);

            return c ^ 0xFFFFFFFFu;
        }
    }
}
