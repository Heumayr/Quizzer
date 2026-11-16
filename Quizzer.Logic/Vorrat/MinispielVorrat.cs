using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Vorrat
{
    /// <summary>
    /// Der Vorrat geteilter Minispiele (M35 C8, V4): je inhaltsfreier Art eine fertige Frage, geteilt mit allen Konten
    /// (<see cref="DataModels.Models.QuestionBase.IsShared"/>), damit Freunde sie im Servermodus fertig ins Raster ziehen.
    /// <para>
    /// <b>Feste Ids, aus dem Wert der Art abgeleitet</b> - so legt ein zweites Konto nach dem Abgleich nichts doppelt an.
    /// Was es mit der Id schon gibt, bleibt unangetastet. <b>Echter Bestand, keine Demo:</b> ohne Marke, also auch
    /// nicht Teil von <c>demo-entfernen</c>; Zellen verweisen mit NO ACTION darauf.
    /// </para>
    /// </summary>
    public static class MinispielVorrat
    {
        /// <summary>Wie die Kategorie des Vorrats heißt.</summary>
        public const string KategorieName = "Minispiel-Vorrat";

        /// <summary>Die Punkte jeder Vorratsfrage - ohne Schwierigkeit, also in jedem Spiel mit Vorgaben genau so viele.</summary>
        public const int Punkte = 200;

        /// <summary>Gemeinsamer Anfang aller festen Ids; die letzten zwölf Stellen tragen den Wert der Art (0 = Kategorie).</summary>
        private const string IdAnfang = "c8d0a7f1-3e5b-4c35-9a08-";

        /// <summary>Die feste Id der Kategorie - eine Art mit dem Wert 0 gibt es nicht.</summary>
        public static Guid KategorieId { get; } = new(IdAnfang + "000000000000");

        /// <summary>Die feste Id der Vorratsfrage einer Art.</summary>
        public static Guid IdFor(MinigameKind art) => new($"{IdAnfang}{(int)art:x12}");

        /// <summary>
        /// Die inhaltsfreien Arten, aus den Profilen gelesen: das Profil verlangt keine Zeilen - weder eine Mindestzahl
        /// noch eine je Runde. Eine neue Art landet von selbst hier oder nicht.
        /// </summary>
        public static IReadOnlyList<MinigameKind> Arten
            => MinigameProfiles.All.Where(p => p.MinOptions == 0 && !p.OptionPerRound).Select(p => p.Kind).ToList();

        /// <summary>Was ein Anlegen bewirkt hat.</summary>
        /// <param name="Neu">So viele Fragen sind neu entstanden.</param>
        /// <param name="Vorhanden">So viele gab es mit ihrer Id schon - sie blieben unangetastet.</param>
        public sealed record Ergebnis(int Neu, int Vorhanden);

        /// <summary>Legt an, was vom Vorrat fehlt. <paramref name="besitzer"/> ist, wer anlegt (ohne Anmeldung niemand).</summary>
        public static async Task<Ergebnis> CreateAsync(Guid? besitzer)
        {
            using var fragen = new QuestionBasesController();
            return await CreateAsync(fragen, besitzer);
        }

        /// <summary>Wie <see cref="CreateAsync(Guid?)"/>, im Kontext eines gegebenen Controllers (Tests, Klammer).</summary>
        public static async Task<Ergebnis> CreateAsync(QuestionBasesController fragen, Guid? besitzer)
        {
            ArgumentNullException.ThrowIfNull(fragen);

            using var kategorien = new CategoriesController(fragen);

            if (!await kategorien.EntitySet.AnyAsync(k => k.Id == KategorieId))
                await kategorien.InsertAsync(new Category { Id = KategorieId, Designation = KategorieName });

            var ids = Arten.ToDictionary(IdFor);
            var gesucht = ids.Keys.ToList();
            var vorhanden = await fragen.EntitySet.Where(q => gesucht.Contains(q.Id)).Select(q => q.Id).ToListAsync();

            foreach (var (id, art) in ids.Where(e => !vorhanden.Contains(e.Key)))
                await fragen.InsertAsync(Baue(art, besitzer));

            await fragen.SaveChangesAsync();

            return new Ergebnis(ids.Count - vorhanden.Count, vorhanden.Count);
        }

        /// <summary>Die Vorratsfrage einer Art: Vorgaben des Profils, keine Zeilen, geteilt.</summary>
        public static MinigameQuestion Baue(MinigameKind art, Guid? besitzer)
        {
            var profil = MinigameProfiles.For(art);
            var frage = new MinigameQuestion
            {
                Id = IdFor(art),
                CategoryId = KategorieId,
                Designation = $"Minispiel: {profil.DisplayName}",
                DesignationShort = profil.DisplayName,
                Points = Punkte,
                MinusPoints = 0,
                Difficulty = Difficulty.Level0,
                OwnerPlayerId = besitzer,
                IsShared = true,
            };

            profil.ApplyDefaults(frage);
            frage.QuestionText = Aufrufe.GetValueOrDefault(art, $"Minispiel: {profil.DisplayName}!");

            return frage;
        }

        /// <summary>Was am Beamer als Frage steht; eine neue Art ohne eigenen Satz bekommt ihren Namen.</summary>
        private static readonly Dictionary<MinigameKind, string> Aufrufe = new()
        {
            [MinigameKind.Reaction] = "Wer ist am schnellsten? Erst bei Grün tippen!",
            [MinigameKind.TapSprint] = "Wer tippt am häufigsten? Daumen locker machen!",
            [MinigameKind.Stopwatch] = "Wer hat das beste Zeitgefühl? Stoppt genau bei der Zielzeit!",
            [MinigameKind.Duel] = "Wer das Feld aufgedeckt hat, fordert jemanden heraus!",
            [MinigameKind.RobberAndGuard] = "Bestehlen oder Schild? Alle wählen geheim und gleichzeitig.",
            [MinigameKind.ShareOrTake] = "Teilen oder nehmen? Zwei entscheiden geheim über die Punkte des Felds.",
            [MinigameKind.Crash] = "Wer traut sich am längsten? Steigt aus, bevor es abstürzt!",
            [MinigameKind.WheelOfFortune] = "Das Glücksrad! Wer das Feld aufgedeckt hat, dreht.",
            [MinigameKind.MentalArithmetic] = "Wer rechnet am schnellsten? So viele Aufgaben wie möglich!",
            [MinigameKind.Memory] = "Wer findet zuerst alle Paare?",
        };
    }
}
