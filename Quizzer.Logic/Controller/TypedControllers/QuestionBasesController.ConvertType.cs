using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;

namespace Quizzer.Logic.Controller.TypedControllers
{
    public partial class QuestionBasesController
    {
        /// <summary>
        /// Wandelt eine bestehende Frage in einen anderen Fragetyp um.
        /// <para>
        /// Die naheliegende Umsetzung - loeschen und neu anlegen - waere hier zerstoerend:
        /// <c>QuestionResult</c> haengt mit <c>OnDelete(Cascade)</c> an der Frage, das Loeschen
        /// wuerde also die gesamte Spielhistorie mitnehmen. Und <c>GameGridCoordinate</c>
        /// verweist mit NO ACTION darauf, das Loeschen schluege ohnehin fehl, sobald irgendein
        /// Spiel die Frage verwendet.
        /// </para>
        /// <para>
        /// Weil die vier Untertabellen der Table-per-Type-Vererbung praktisch nur aus der Id
        /// bestehen, geht es einfacher: die Id bleibt, es wird nur die Zeile in der Untertabelle
        /// getauscht und die Basiszeile auf die Werte des neuen Typs gesetzt. Schritte,
        /// Ressourcen, Spielfeldzellen und Ergebnisse bleiben dabei unberuehrt.
        /// </para>
        /// </summary>
        /// <param name="questionId">Die umzuwandelnde Frage.</param>
        /// <param name="targetType">Der Zieltyp.</param>
        /// <returns>Die umgewandelte Frage.</returns>
        public async Task<QuestionBase> ConvertTypeAsync(Guid questionId, QuestionType targetType)
        {
            if (questionId == Guid.Empty)
                throw new ArgumentException("Ohne Id lässt sich nichts umwandeln.", nameof(questionId));

            var current = await GetAsync(questionId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Die Frage wurde nicht gefunden.");

            if (current.Typ == targetType)
                return current;

            var sourceProfile = QuestionTypeProfiles.For(current.Typ);
            var targetProfile = QuestionTypeProfiles.For(targetType);

            // Ein Tabellenname kann in SQL nicht als Parameter uebergeben werden, er muss in den
            // Text. Deshalb geht er durch eine Weissliste: erlaubt ist nur, was in den Profilen
            // als TableName steht. Bisher stand dieser Beweis nur im Kommentar - jetzt scheitert
            // ein unbekannter Name, statt in die Anweisung zu wandern.
            var oldTable = EnsureKnownTable(sourceProfile.TableName);
            var newTable = EnsureKnownTable(targetProfile.TableName);

            var context = CurrentContext;

            await using var transaction = await context.Database
                .BeginTransactionAsync().ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(
                DeleteStatementFor(oldTable),
                new SqlParameter("@id", questionId)).ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(
                InsertStatementFor(newTable),
                new SqlParameter("@id", questionId)).ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE [question].[QuestionBase]
                   SET [Typ] = @typ,
                       [BuzzerControlsLayout] = @buzzerLayout,
                       [StepDisplayLayoutMode] = @stepLayout,
                       [QuestionViewKeyType] = @keyType,
                       [UseRandomSequenceOnNoneFinishSteps] = @random,
                       [UseProportionalScoreReductionOnStep] = @proportional
                 WHERE [Id] = @id
                """,
                new SqlParameter("@typ", (int)targetType),
                new SqlParameter("@buzzerLayout", (int)targetProfile.BuzzerControlsLayout),
                new SqlParameter("@stepLayout", (int)targetProfile.StepDisplayLayoutMode),
                new SqlParameter("@keyType", (int)targetProfile.QuestionViewKeyType),
                new SqlParameter("@random", targetProfile.UseRandomSequenceOnNoneFinishSteps),
                new SqlParameter("@proportional", targetProfile.UseProportionalScoreReductionOnStep),
                new SqlParameter("@id", questionId)).ConfigureAwait(false);

            // Ein Zieltyp, der nur EINE Loesung vertraegt, bekommt auch nur eine. Ohne diesen
            // Schritt war eine Multiple-Choice-Frage mit zwei richtigen Antworten nach dem
            // Umwandeln in eine Schaetzfrage UNSPEICHERBAR: der Pruefer beanstandet die zweite
            // Markierung, und die Schaetzfragen-Maske zeigt weder Zeilenliste noch Haekchen -
            // es gab keinen Weg heran ausser zurueckzuwandeln, und darauf wies nichts hin.
            // Gemessen 2026-09-07.
            if (!targetProfile.AllowsMultipleResultSteps)
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE [question].[QuestionStepResource]
                       SET [IsResult] = 0
                     WHERE [QuestionBaseId] = @id
                       AND [IsResult] = 1
                       AND [Id] <> (SELECT TOP 1 [Id]
                                      FROM [question].[QuestionStepResource]
                                     WHERE [QuestionBaseId] = @id AND [IsResult] = 1
                                     ORDER BY [SequenceNumber], [Id])
                    """,
                    new SqlParameter("@id", questionId)).ConfigureAwait(false);
            }

            await transaction.CommitAsync().ConfigureAwait(false);

            // Der Kontext haelt die Frage noch als alten Typ - erst nach dem Vergessen liest
            // ein Get sie als den neuen.
            context.ChangeTracker.Clear();

            return await GetAsync(questionId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Die umgewandelte Frage wurde nicht gefunden.");
        }

        /// <summary>
        /// Die einzigen Tabellennamen, die in eine SQL-Anweisung dieser Klasse gelangen duerfen:
        /// die Untertabellen der vier Fragetypen, wie sie in den Profilen stehen.
        /// </summary>
        private static readonly HashSet<string> KnownTables =
            QuestionTypeProfiles.All.Select(p => p.TableName).ToHashSet(StringComparer.Ordinal);

        /// <summary>
        /// Laesst nur bekannte Tabellennamen durch. Wirft, wenn ein Profil je einen Namen
        /// mitbraechte, der nicht zu einer Untertabelle gehoert.
        /// </summary>
        internal static string EnsureKnownTable(string tableName)
        {
            if (!KnownTables.Contains(tableName))
            {
                throw new InvalidOperationException(
                    $"Unbekannte Fragetabelle '{tableName}'. Erlaubt sind nur die Untertabellen "
                    + $"der Fragetypen: {string.Join(", ", KnownTables.OrderBy(t => t, StringComparer.Ordinal))}.");
            }

            return tableName;
        }

        /// <summary>Loescht die Zeile in der Untertabelle des bisherigen Typs.</summary>
        private static string DeleteStatementFor(string tableName)
            => $"DELETE FROM [question].[{tableName}] WHERE [Id] = @id";

        /// <summary>
        /// Startwerte fuer die Untertabellen, die eigene Pflichtspalten haben.
        /// <para>
        /// <b>Der Kommentar hier behauptete bis 2026-09-07, die Schaetzfrage sei die einzige
        /// solche Tabelle</b> - das galt fuer vier Typen und stimmt seit der Aufdeckfrage nicht
        /// mehr. Die Folge: jede Umwandlung IN eine Aufdeckfrage scheiterte mit einem rohen
        /// <c>SqlException 515</c>, weil das <c>INSERT</c> nur die Id schrieb.
        /// </para>
        /// <para>
        /// <b>Die Werte sind dieselben, die der jeweilige Konstruktor setzt.</b> Sie stehen hier
        /// ausdruecklich, statt sich auf Standardwerte der Datenbank zu verlassen -
        /// <c>RevealQuestion</c> hat naemlich keine (nachgemessen ueber
        /// <c>sys.default_constraints</c>).
        /// </para>
        /// <para>
        /// Wer einen Fragetyp hinzufuegt und das hier vergisst, wird von
        /// <c>QuestionTypeConversionUnitTests.EveryTypeCanBeReachedAndLeft</c> erwischt - die
        /// laeuft ueber <c>QuestionTypeProfiles.All</c> und nicht ueber eine Liste von Hand.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, string> Startwerte = new()
        {
            [nameof(AppreciateQestion)] =
                "([Id], [ValueKind], [Unit], [ExpectedValue], [ExpectedDate]) VALUES (@id, 0, 0, 0, NULL)",

            [nameof(RevealQuestion)] =
                "([Id], [Mode], [ImageFileName], [AreasJson], [BlurStart]) VALUES (@id, 0, N'', N'[]', 40)",
        };

        private static string InsertStatementFor(string tableName)
            => Startwerte.TryGetValue(tableName, out var spalten)
                ? $"INSERT INTO [question].[{tableName}] {spalten}"
                : $"INSERT INTO [question].[{tableName}] ([Id]) VALUES (@id)";

        /// <summary>
        /// Was beim Umwandeln in Klartext verloren geht oder seine Bedeutung verliert. Der
        /// Editor zeigt das vor der Rueckfrage an.
        /// </summary>
        public static IReadOnlyList<string> DescribeConversionEffects(
            QuestionType from, QuestionType to)
        {
            var effects = new List<string>();

            if (from == to)
                return effects;

            var target = QuestionTypeProfiles.For(to);

            if (from == QuestionType.Appreciate)
                effects.Add("Sollwert und Einheit der Schätzfrage gehen verloren.");

            if (to == QuestionType.Appreciate)
                effects.Add("Der Sollwert muss danach neu gesetzt werden.");

            if (from == QuestionType.MultipleChoice && to != QuestionType.MultipleChoice)
                effects.Add("Die Lösungsmarkierungen bleiben erhalten, werden aber nicht mehr "
                          + "zur Wertung herangezogen.");

            if (to == QuestionType.MultipleChoice)
                effects.Add("Mindestens ein Schritt muss als Lösung markiert sein, und die Zahl "
                          + "der wählbaren Antworten muss dazu passen.");

            // Die Aufdeckfrage ist neben der Schaetzfrage die zweite Untertabelle mit eigenen
            // Spalten. Bis 2026-09-07 stand hier kein Wort davon - stattdessen die Zusage, dass
            // Medien unberuehrt bleiben, waehrend das Bild samt Flaechen geloescht wurde.
            if (from == QuestionType.Reveal)
                effects.Add("Bild, Aufdeckflächen und Betriebsart der Aufdeckfrage gehen "
                          + "verloren und lassen sich nicht wiederherstellen.");

            if (to == QuestionType.Reveal)
                effects.Add("Bild und Aufdeckflächen müssen danach neu eingerichtet werden.");

            if (!target.AllowsMultipleResultSteps)
                effects.Add($"{target.DisplayName} verträgt nur eine Lösungsmarkierung - "
                          + "weitere werden entfernt.");

            effects.Add($"Anzeige und Bedienung wechseln auf die Vorgaben für {target.DisplayName}.");

            // "Medien" hiess hier bis 2026-09-07 auch das Bild der Aufdeckfrage mit - und das
            // ging sehr wohl verloren. Gemeint waren immer nur die Medien AN DEN SCHRITTEN.
            effects.Add("Schritte samt ihrer Medien, Spielfeldzellen und bisherige Ergebnisse "
                      + "bleiben unberührt.");

            return effects;
        }
    }
}
