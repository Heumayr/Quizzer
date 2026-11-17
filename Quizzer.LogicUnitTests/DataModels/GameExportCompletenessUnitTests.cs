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
                ["PlayedAt"] = "Verlauf (M37 E06) - eine importierte Zelle ist ungespielt",
                ["CurrentPoints"] = "wird beim Import neu gerechnet, sonst waeren es die Punkte des Quell-Spiels",
                ["CurrentMinusPoints"] = "dasselbe",
            },
            [typeof(Header)] = new()
            {
                ["Id"] = "wird neu vergeben",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["GameId"] = "wird beim Anlegen gesetzt",
            },
            [typeof(GameTeam)] = new()
            {
                ["Id"] = "wird neu vergeben",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["GameId"] = "wird beim Anlegen gesetzt",
                ["Designation"] = "ein Team traegt seinen Namen in Name",
                ["SpeakerTurn"] = "Verlauf (M37 T1) - der Sprecher beginnt beim ersten",
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
                ["IsShared"] = "das Teilen gilt am jeweiligen Server (M9), nicht für den Inhalt einer Spieldatei - eine importierte Frage ist erst einmal privat",
            },
            [typeof(MinigameQuestion)] = new()
            {
                ["Id"] = "wird neu vergeben",
                ["RowVersion"] = "gehoert der Zieldatenbank",
                ["CategoryId"] = "der Verweis laeuft ueber KategorieIndex im Dokument",
                ["OwnerPlayerId"] = "beim Import gilt der angemeldete Spielleiter",
                ["IsShared"] = "das Teilen gilt am jeweiligen Server (M9)",
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

            // DateTimeOffset seit M37 E06 (GameGridCoordinate.PlayedAt) - vorher sah die Pruefung das Feld gar nicht.
            return typ.IsPrimitive || typ.IsEnum
                || typ == typeof(string) || typ == typeof(Guid)
                || typ == typeof(DateTime) || typ == typeof(DateTimeOffset) || typ == typeof(decimal);
        }

        /// <summary>
        /// Ein Wert ungleich der Vorgabe - damit ein Verlust auffällt. Vorgabe ist der Wert eines frisch gebauten
        /// Objekts, denn genau den hat der Import, wenn das Feld im Bündel fehlt (V04: ein gestreuter bool traf
        /// sie zufällig, und <c>KiUngeprueft</c> fehlte unbemerkt).
        /// </summary>
        private static object? AbweichenderWert(Type typ, object? vorgabe, int streuung)
        {
            var kern = Nullable.GetUnderlyingType(typ) ?? typ;
            object? wert = null;

            if (kern == typeof(string)) wert = "Probe-" + streuung;
            else if (kern == typeof(bool)) wert = vorgabe is not true;
            else if (kern == typeof(int)) wert = 4200 + streuung;
            else if (kern == typeof(double)) wert = 3.5 + streuung;
            else if (kern == typeof(decimal)) wert = 3.5m + streuung;
            else if (kern == typeof(DateTime)) wert = new DateTime(2001, 2, 3).AddDays(streuung);
            else if (kern == typeof(DateTimeOffset)) wert = new DateTimeOffset(2001, 2, 3, 20, 0, 0, TimeSpan.FromHours(1)).AddDays(streuung);
            else if (kern == typeof(Guid)) wert = Guid.NewGuid();
            else if (kern.IsEnum)
            {
                var andere = Enum.GetValues(kern).Cast<object>().Where(w => !Equals(w, vorgabe)).ToArray();
                wert = andere.Length == 0 ? null : andere[streuung % andere.Length];
            }

            return Equals(wert, vorgabe) ? null : wert;
        }

        /// <summary>Was ein frisch gebautes Objekt desselben Typs in diesem Feld trägt.</summary>
        private static object? Vorgabe(Type typ, PropertyInfo p)
            => p.GetValue(Activator.CreateInstance(typ));

        /// <summary>Füllt jedes gespeicherte Feld mit einem Wert ungleich seiner Vorgabe.</summary>
        private static void Fuelle(object ziel, ref int streuung)
        {
            var typ = ziel.GetType();

            foreach (var p in typ.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(IstGespeichertesFeld))
            {
                var wert = AbweichenderWert(p.PropertyType, Vorgabe(typ, p), streuung++);

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

            // Die Pruefung laesst 0 bis 600 s und Farben 0 bis 7 durch (M37 T1) - abweichend von der Vorgabe bleiben sie.
            spiel.TeamCouncilSeconds = 45;
            var team = new GameTeam { Id = Guid.NewGuid() };
            Fuelle(team, ref streuung);
            team.ColorIndex = 6;
            spiel.GameTeams.Add(team);

            var kopf = new Header { Id = Guid.NewGuid() };
            Fuelle(kopf, ref streuung);
            spiel.Headers.Add(kopf);

            var zelle = new GameGridCoordinate { Id = Guid.NewGuid() };
            Fuelle(zelle, ref streuung);
            zelle.QuestionBaseId = frage.Id;

            // Der Pruefer laesst nur 0 bis 100 Prozent durch (M35 C3b) - ein abweichender Wert bleibt es trotzdem.
            zelle.SpecialRuleShare = 42;
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
            Vergleiche(team, bausatz.Spiel.GameTeams.Single(), funde);
            Vergleiche(kopf, bausatz.Kopfzeilen.Single(), funde);
            Vergleiche(zelle, bausatz.Zellen.Single().Zelle, funde);
            Vergleiche(frage, bausatz.Fragen.Single(), funde);
            Vergleiche(schritt, bausatz.Fragen.Single().Steps.Single(), funde);

            Assert.AreEqual(0, funde.Count,
                "Diese gespeicherten Felder ueberleben den Export nicht. Entweder fehlen sie im "
                + "Dokument, oder sie gehoeren mit Grund auf die Ausnahmeliste in dieser Datei:"
                + Environment.NewLine + string.Join(Environment.NewLine, funde));
        }

        /// <summary>
        /// Das Minispiel (M35 B2) uebersteht den Rundlauf samt Art und Parametern.
        /// <para>
        /// <b>Die Art wird am Dokument geprueft</b>, nicht nur am Ergebnis: solange es nur eine Art gibt,
        /// ist sie zugleich die Vorgabe des Konstruktors - ein vergessenes Feld fiele am Ende nicht auf.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheMinigameSurvivesTheRoundTrip()
        {
            var streuung = 1;
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Probe-Kategorie" };

            var frage = new MinigameQuestion { Id = Guid.NewGuid() };
            Fuelle(frage, ref streuung);
            frage.CategoryId = kategorie.Id;

            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Probe" };
            spiel.GameGridCoordinates.Add(new GameGridCoordinate { Id = Guid.NewGuid(), QuestionBaseId = frage.Id });

            var dokument = GameExportMapper.ToDocument(spiel, [frage], [kategorie], null, _ => null);

            Assert.AreEqual(frage.Kind, dokument.Fragen.Single().Typeigen?.MinigameKind,
                "Die Art steht nicht im Bündel.");

            using var strom = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(GameExportSerializer.ToJson(dokument)));
            var zurueck = GameExportMapper.ToEntities(GameExportSerializer.Read(strom), _ => null).Fragen.Single();

            Assert.IsInstanceOfType<MinigameQuestion>(zurueck);

            var funde = new List<string>();
            Vergleiche(frage, zurueck, funde);

            Assert.AreEqual(0, funde.Count, string.Join(Environment.NewLine, funde));
        }

        /// <summary>Ein Buendel mit einer unbekannten Minispiel-Art wird abgewiesen, nicht halb gelesen.</summary>
        [TestMethod]
        public void AnUnknownMinigameKindIsRejected()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Probe-Kategorie" };
            var frage = new MinigameQuestion { Id = Guid.NewGuid(), CategoryId = kategorie.Id, Designation = "X" };

            var dokument = GameExportMapper.ToDocument(new Game { Id = Guid.NewGuid() }, [frage], [kategorie], null, _ => null);
            var typeigen = dokument.Fragen.Single().Typeigen;
            Assert.IsNotNull(typeigen, "Das Minispiel traegt keinen eigenen Feldblock.");
            typeigen.MinigameKind = (MinigameKind)99;

            Assert.ThrowsExactly<InvalidDataException>(() => GameExportSerializer.Pruefe(dokument));
        }

        /// <summary>
        /// V04: ein Bündel von vor M37 kennt <c>KiUngeprueft</c> nicht. Es bleibt lesbar (Format 1, das Feld kam
        /// dazu, seine Bedeutung blieb), und die Frage gilt als geprüft.
        /// </summary>
        [TestMethod]
        public void ABundleWithoutTheAiFlagStaysReadable()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Probe-Kategorie" };
            var frage = new DefaultQuestion { Id = Guid.NewGuid(), CategoryId = kategorie.Id, Designation = "Alt", KiUngeprueft = true };
            var dokument = GameExportMapper.ToDocument(new Game { Id = Guid.NewGuid() }, [frage], [kategorie], null, _ => null);

            var knoten = System.Text.Json.Nodes.JsonNode.Parse(GameExportSerializer.ToJson(dokument))!;
            var fragen = knoten["Fragen"]!.AsArray();
            Assert.IsTrue(fragen.Single()!.AsObject().Remove(nameof(GameExportDocument.QuestionData.KiUngeprueft)),
                "Das Feld stand gar nicht im Bündel - dann misst diese Probe kein altes Bündel.");

            using var strom = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(knoten.ToJsonString()));
            var gelesen = GameExportSerializer.Read(strom);

            Assert.AreEqual(1, gelesen.FormatVersion);
            Assert.IsFalse(GameExportMapper.ToEntities(gelesen, _ => null).Fragen.Single().KiUngeprueft,
                "Eine Frage aus einem alten Bündel gilt als ungeprüfter KI-Entwurf.");
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

                // Je Eigenschaft: gleicht der Probewert der Vorgabe, bliebe ein Verlust grün (V04).
                if (Equals(a, Vorgabe(typ, p)))
                    funde.Add($"{typ.Name}.{p.Name}: der Probewert <{a}> gleicht der Vorgabe - ein Verlust fiele nicht auf");
                else if (!Equals(a, b))
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
