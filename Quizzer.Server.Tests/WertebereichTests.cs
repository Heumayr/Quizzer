using Microsoft.AspNetCore.Mvc.Testing;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quizzer.Server.Tests
{
    /// <summary>
    /// Wertebereich der Enum-Felder im Abgleich (M37 P1, V21): der Abgleich nimmt Enums als Zahl, und eine fremde Zahl wurde
    /// angenommen - eine unbekannte Sonderregel spielte das WPF als gewöhnliche Frage. Jedes Feld kommt aus dem Datenmodell,
    /// nicht aus einer Liste hier: ein neues Enum-Feld ist ohne Muster rot.
    /// </summary>
    [TestClass]
    public sealed class WertebereichTests
    {
        /// <summary>Gina leitet ein Spiel mit Frage, Zelle und Max als Mitspieler.</summary>
        private sealed record Lage(HttpClient Gina, SyncTestData.GameSet Spiel, Guid MaxId);

        /// <summary>Wo die Formprüfung schon vorher greift (<c>TeamRowCheck</c>, M37 T1); sonst die Form der Joker-Art.</summary>
        private static readonly Dictionary<(string Typ, string Feld), SyncRejectReason> Ausnahmen = new()
        {
            [(nameof(Game), nameof(Game.TeamMinigameScoring))] = SyncRejectReason.Invalid,
        };

        /// <summary>Je Zeilentyp mit Enum-Feld eine gültige neue Zeile in der Lage; Fragen baut <see cref="Frage"/>.</summary>
        private static readonly Dictionary<Type, Func<Lage, object>> Muster = new()
        {
            [typeof(Game)] = _ => new Game { Id = Guid.NewGuid(), Designation = "Zweites Spiel" },
            [typeof(GameGridCoordinate)] = l => new GameGridCoordinate { Id = Guid.NewGuid(), GameId = l.Spiel.Game.Id, QuestionBaseId = l.Spiel.Question.Id, X = 1, Y = 0 },
            [typeof(Header)] = l => new Header { Id = Guid.NewGuid(), GameId = l.Spiel.Game.Id, Designation = "Kopf", Index = 1 },
            [typeof(QuestionStepResource)] = l => new QuestionStepResource { Id = Guid.NewGuid(), QuestionBaseId = l.Spiel.Question.Id, StepText = "Hinweis" },
            [typeof(PlayerJoker)] = l => new PlayerJoker
            {
                Id = Guid.NewGuid(), GameId = l.Spiel.Game.Id, PlayerId = l.MaxId, Kind = JokerKind.Double, Reason = "Minispiel gewonnen", GrantedAt = DateTimeOffset.UtcNow,
            },
        };

        private static async Task<SyncPushResponse> PushAsync(HttpClient client, params object[] zeilen)
            => await PushRowsAsync(client, [.. zeilen.Select(SyncTestData.Row)]);

        private static async Task<SyncPushResponse> PushRowsAsync(HttpClient client, params SyncRow[] zeilen)
        {
            var request = new SyncPushRequest { Upserts = [.. zeilen.Select(z => new SyncUpsert { Row = z, BaseHash = null })] };
            var response = await client.PostAsJsonAsync("/api/v1/sync/push", request);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
        }

        private static async Task<Lage> LageAsync(WebApplicationFactory<Program> factory)
        {
            var (gina, _) = await ServerTestHost.NewMemberAsync(factory, "Gina");
            var spiel = SyncTestData.NewGame("Wertebereich");
            var max = new Player { Id = Guid.NewGuid(), Designation = "Max", DisplayName = "Max" };

            var push = spiel.AsNewPush();
            push.Upserts.Add(SyncTestData.New(max));
            push.Upserts.Add(SyncTestData.New(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Game.Id, PlayerId = max.Id }));
            var response = await gina.PostAsJsonAsync("/api/v1/sync/push", push);
            var angelegt = (await response.Content.ReadFromJsonAsync<SyncPushResponse>())!;
            Assert.AreEqual(0, angelegt.Rejected.Count, string.Join("; ", angelegt.Rejected.Select(r => r.Message)));

            return new Lage(gina, spiel, max.Id);
        }

        /// <summary>Eine Frage des Typs in der Kategorie der Lage - jeder Fragetyp trägt die Enum-Felder der Wurzel.</summary>
        private static QuestionBase Frage(Type typ, Lage lage)
        {
            var frage = (QuestionBase)Activator.CreateInstance(typ)!;
            frage.Id = Guid.NewGuid();
            frage.Designation = "Frage " + typ.Name;
            frage.QuestionText = "Wie viel?";
            frage.CategoryId = lage.Spiel.Category.Id;
            frage.Points = 100;
            return frage;
        }

        /// <summary>Die Enum-Felder jeder Zeilenart im Abgleich, so wie das Datenmodell sie kennt.</summary>
        private static IReadOnlyList<(SyncModel.Entry Eintrag, string Feld, Type Enum)> EnumFelder()
        {
            using var store = new SyncStore();
            return
            [
                .. SyncModel.Entries.SelectMany(e => SyncRowConverter.SyncedProperties(store.Context.Model, e.ClrType)
                    .Select(p => (Eintrag: e, Feld: p.Name, Enum: Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType))
                    .Where(f => f.Enum.IsEnum)),
            ];
        }

        /// <summary>Gleich hinter dem größten bekannten Wert - jenseits des Bereichs, aber im Zahlraum des Enums.</summary>
        private static long Jenseits(Type enumTyp) => Enum.GetValues(enumTyp).Cast<object>().Max(Convert.ToInt64) + 1;

        /// <summary>
        /// Je Feld zwei Pushs: die gültige Zeile geht durch (sonst prüfte die Abweisung etwas anderes), dieselbe Zeile mit
        /// einem Wert jenseits des Bereichs wird abgewiesen - in der Form der Joker-Art, auch dort, wo es bis V21 durchging.
        /// </summary>
        [TestMethod]
        public async Task EveryEnumFieldInTheSyncRefusesAValueBeyondItsRange()
        {
            await using var factory = ServerTestHost.CreateFactory();
            var lage = await LageAsync(factory);
            var felder = EnumFelder();
            var durch = new List<string>();

            Assert.IsTrue(felder.Any(f => f.Eintrag.Name == nameof(GameGridCoordinate) && f.Feld == nameof(GameGridCoordinate.SpecialRule)),
                "Die Sonderregel fehlt unter den Enum-Feldern - die Aufzählung greift nicht.");

            foreach (var (eintrag, feld, enumTyp) in felder)
            {
                var name = $"{eintrag.Name}.{feld}";
                Func<Lage, object>? bauen = SyncModel.IsQuestion(eintrag) ? l => Frage(eintrag.ClrType, l) : Muster.GetValueOrDefault(eintrag.ClrType);
                Assert.IsNotNull(bauen, $"Kein Muster für {eintrag.Name} - ein neues Enum-Feld braucht hier eine gültige Zeile.");

                var gueltig = await PushAsync(lage.Gina, bauen(lage));
                Assert.AreEqual(0, gueltig.Rejected.Count, $"{name}: die gültige Zeile wurde abgewiesen - {string.Join("; ", gueltig.Rejected.Select(r => r.Message))}");

                var zeile = SyncTestData.Row(bauen(lage));
                zeile.Values[feld] = JsonSerializer.SerializeToElement(Jenseits(enumTyp));
                var result = await PushRowsAsync(lage.Gina, zeile);
                var erwartet = Ausnahmen.GetValueOrDefault((eintrag.Name, feld), SyncRejectReason.Forbidden);

                if (!result.Rejected.Any(r => r.Id == zeile.Id && r.Reason == erwartet))
                    durch.Add($"{name} = {Jenseits(enumTyp)} ({(result.Accepted.Any(a => a.Id == zeile.Id) ? "angenommen" : string.Join("; ", result.Rejected.Select(r => $"{r.Reason}: {r.Message}")))})");
            }

            Assert.AreEqual(0, durch.Count, $"Nicht wie erwartet abgewiesen: {string.Join(" | ", durch)}");
        }

        /// <summary>Die Prüfung kennt nur Einzelwerte - ein <c>[Flags]</c>-Enum im Abgleich wiese jede Kombination ab.</summary>
        [TestMethod]
        public void NoEnumInTheSyncIsAFlagsEnum()
        {
            var flags = EnumFelder().Where(f => f.Enum.IsDefined(typeof(FlagsAttribute), false)).Select(f => $"{f.Eintrag.Name}.{f.Feld}").ToList();

            Assert.AreEqual(0, flags.Count, $"Flags-Enum im Abgleich: {string.Join(", ", flags)}");
        }
    }
}
