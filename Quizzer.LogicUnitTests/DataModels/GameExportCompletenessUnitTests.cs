using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Der Export nimmt jedes gespeicherte Feld mit - oder es steht namentlich auf der
    /// Ausnahmeliste.
    /// <para>
    /// <b>Warum das per Reflexion geht und nicht von Hand.</b> Ein Bündel ist ein Bauplan; fehlt
    /// darin ein Feld, entsteht auf dem Zielrechner ein Spiel, das <i>fast</i> stimmt. Das fällt
    /// niemandem auf - die sechs Rechenfaktoren des Spiels etwa ergeben andere Punkte, und die
    /// Zelle zeigt einfach eine andere Zahl. Diese Zusicherung wird deshalb <b>von selbst</b>
    /// rot, sobald jemand ein Feld an einer der Entitäten anlegt und im Dokument vergisst.
    /// </para>
    /// <para>
    /// <b>Die Ausnahmeliste ist der eigentliche Inhalt.</b> Jedes Feld darauf trägt einen Grund.
    /// Wer sie zu weit fasst, macht diese Zusicherung wertlos - dagegen steht
    /// <c>GameExportHistoryUnitTests</c>, die am JSON-Text misst, dass der Verlauf wirklich
    /// draußen bleibt.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameExportCompletenessUnitTests
    {
        /// <summary>
        /// Was bewusst nicht ins Bündel geht, je Typ und mit Grund.
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, string>> Ausnahmen = new()
        {
            [typeof(Game)] = new()
            {
                ["Id"] = "Ids werden beim Import neu vergeben - eine uebernommene zeigt ins Leere oder auf Fremdes",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["Designation"] = "wird geprueft, aber ueber das Spiel-Objekt, nicht ueber diese Schleife",
                ["Restart"] = "Verlauf",
                ["State"] = "Verlauf - ein importiertes Spiel beginnt im Aufbau",
                ["CurrentRound"] = "Verlauf",
                ["Phase"] = "Verlauf",
                ["RegularChoosingPlayerId"] = "Verlauf, und eine fremde Spieler-Id dazu",
                ["CurrentChoosingPlayerId"] = "Verlauf, und eine fremde Spieler-Id dazu",
                ["ModeratorPlayerId"] = "beim Import gilt der angemeldete Spielleiter (Nutzerentscheidung)",
                ["GameThemeId"] = "das Design geht als eigener Block mit, nicht als Id",
            },
            [typeof(GameGridCoordinate)] = new()
            {
                ["Id"] = "wird neu vergeben",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["Designation"] = "eine Zelle traegt keine eigene Bezeichnung",
                ["GameId"] = "wird beim Anlegen gesetzt",
                ["QuestionBaseId"] = "der Verweis laeuft ueber FrageIndex im Dokument",
                ["Phase"] = "Verlauf - eine importierte Zelle beginnt in Phase 1",
                ["IsDone"] = "Verlauf",
                ["CurrentPoints"] = "wird beim Import neu gerechnet, sonst waeren es die Punkte des Quell-Spiels",
                ["CurrentMinusPoints"] = "dasselbe",
            },
            [typeof(Header)] = new()
            {
                ["Id"] = "wird neu vergeben",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["GameId"] = "wird beim Anlegen gesetzt",
            },
            [typeof(QuestionStepResource)] = new()
            {
                ["Id"] = "wird neu vergeben",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["QuestionBaseId"] = "wird beim Anlegen gesetzt",
                ["ResourceFileName"] = "der Verweis laeuft ueber den Medienschluessel; der Name auf dem Zielsystem entsteht dort",
            },
            [typeof(AppreciateQestion)] = new()
            {
                ["Id"] = "wird neu vergeben",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["CategoryId"] = "der Verweis laeuft ueber KategorieIndex im Dokument",
                ["OwnerPlayerId"] = "beim Import gilt der angemeldete Spielleiter - eine fremde Id machte die Frage unsichtbar",
            },
        };

        private static bool IstGespeichertesFeld(PropertyInfo p)
        {
            // Ein oeffentlicher Setter ist Pflicht. QuestionBase.Typ hat einen nicht
            // oeffentlichen - CanWrite meldet trotzdem true, und die Vorrichtung hat damit
            // beim ersten Anlauf den Fragetyp selbst umgeschrieben.
            if (!p.CanRead || p.SetMethod?.IsPublic != true)
                return false;

            if (p.GetCustomAttribute<NotMappedAttribute>() != null)
                return false;

            var typ = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

            return typ.IsPrimitive || typ.IsEnum
                || typ == typeof(string) || typ == typeof(Guid)
                || typ == typeof(DateTime) || typ == typeof(decimal);
        }

        /// <summary>Ein Wert, der sich vom Standard unterscheidet - damit ein Verlust auffällt.</summary>
        private static object? AbweichenderWert(Type typ, int streuung)
        {
            var kern = Nullable.GetUnderlyingType(typ) ?? typ;

            if (kern == typeof(string)) return "Probe-" + streuung;
            if (kern == typeof(bool)) return streuung % 2 == 0;
            if (kern == typeof(int)) return 4200 + streuung;
            if (kern == typeof(double)) return 3.5 + streuung;
            if (kern == typeof(decimal)) return 3.5m + streuung;
            if (kern == typeof(DateTime)) return new DateTime(2001, 2, 3).AddDays(streuung);
            if (kern == typeof(Guid)) return Guid.NewGuid();

            if (kern.IsEnum)
            {
                var werte = Enum.GetValues(kern);

                return werte.GetValue(streuung % werte.Length);
            }

            return null;
        }

        /// <summary>Füllt jedes gespeicherte Feld mit einem abweichenden Wert.</summary>
        private static void Fuelle(object ziel, ref int streuung)
        {
            foreach (var p in ziel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(IstGespeichertesFeld))
            {
                var wert = AbweichenderWert(p.PropertyType, streuung++);

                if (wert != null)
                    p.SetValue(ziel, wert);
            }
        }

        /// <summary>
        /// Jedes gespeicherte Feld überlebt den Weg Modell → Dokument → JSON → Dokument → Modell,
        /// oder es steht mit Grund auf der Ausnahmeliste.
        /// </summary>
        [TestMethod]
        public void EveryStoredFieldSurvivesTheRoundTrip()
        {
            var streuung = 1;

            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Probe-Kategorie" };

            var frage = new AppreciateQestion { Id = Guid.NewGuid() };
            Fuelle(frage, ref streuung);
            frage.CategoryId = kategorie.Id;

            var schritt = new QuestionStepResource { Id = Guid.NewGuid() };
            Fuelle(schritt, ref streuung);
            frage.Steps.Add(schritt);

            var spiel = new Game { Id = Guid.NewGuid() };
            Fuelle(spiel, ref streuung);

            var kopf = new Header { Id = Guid.NewGuid() };
            Fuelle(kopf, ref streuung);
            spiel.Headers.Add(kopf);

            var zelle = new GameGridCoordinate { Id = Guid.NewGuid() };
            Fuelle(zelle, ref streuung);
            zelle.QuestionBaseId = frage.Id;
            spiel.GameGridCoordinates.Add(zelle);

            // Hin und zurueck, ueber den echten Text.
            var dokument = GameExportMapper.ToDocument(
                spiel, [frage], [kategorie], null, _ => "medium-1");

            // Das Medium im Buendel anmelden - der Pruefer laesst einen Verweis ins Leere nicht
            // durch, und das ist Absicht.
            dokument.Medien.Add(new GameExportDocument.MediaData
            {
                Schluessel = "medium-1",
                DateiImBuendel = "medien/medium-1.png",
                Endung = ".png",
                UrsprungsName = "probe.png",
            });

            var json = GameExportSerializer.ToJson(dokument);

            using var strom = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var gelesen = GameExportSerializer.Read(strom);
            var bausatz = GameExportMapper.ToEntities(gelesen, _ => "datei.png");

            var funde = new List<string>();

            Vergleiche(spiel, bausatz.Spiel, funde);
            Vergleiche(kopf, bausatz.Kopfzeilen.Single(), funde);
            Vergleiche(zelle, bausatz.Zellen.Single().Zelle, funde);
            Vergleiche(frage, bausatz.Fragen.Single(), funde);
            Vergleiche(schritt, bausatz.Fragen.Single().Steps.Single(), funde);

            Assert.AreEqual(0, funde.Count,
                "Diese gespeicherten Felder ueberleben den Export nicht. Entweder fehlen sie im "
                + "Dokument, oder sie gehoeren mit Grund auf die Ausnahmeliste in dieser Datei:"
                + Environment.NewLine + string.Join(Environment.NewLine, funde));
        }

        private static void Vergleiche(object original, object kopie, List<string> funde)
        {
            var typ = original.GetType();
            var ausnahmen = Ausnahmen.TryGetValue(typ, out var liste)
                ? liste
                : new Dictionary<string, string>();

            foreach (var p in typ.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(IstGespeichertesFeld))
            {
                if (ausnahmen.ContainsKey(p.Name))
                    continue;

                var a = p.GetValue(original);
                var b = p.GetValue(kopie);

                if (!Equals(a, b))
                    funde.Add($"{typ.Name}.{p.Name}: erwartet <{a}>, im Bündel steht <{b}>");
            }
        }

        /// <summary>
        /// Die Gegenrichtung: die Ausnahmeliste deckt kein Feld ab, das es gar nicht mehr gibt.
        /// <para>
        /// Ohne diese Probe wüchse die Liste still weiter: ein umbenanntes Feld bliebe als
        /// Ausnahme stehen, und das neue Feld unter neuem Namen wäre <b>ungeprüft</b>.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheExceptionListMentionsOnlyFieldsThatExist()
        {
            var funde = new List<string>();

            foreach (var (typ, liste) in Ausnahmen)
            {
                var vorhanden = typ.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var name in liste.Keys.Where(n => !vorhanden.Contains(n)))
                    funde.Add($"{typ.Name}.{name} steht auf der Ausnahmeliste, gibt es aber nicht mehr.");
            }

            Assert.AreEqual(0, funde.Count,
                "Die Ausnahmeliste ist veraltet:" + Environment.NewLine
                + string.Join(Environment.NewLine, funde));
        }
    }
}
