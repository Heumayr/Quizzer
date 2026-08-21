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
                throw new ArgumentException("Ohne Id laesst sich nichts umwandeln.", nameof(questionId));

            var current = await GetAsync(questionId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Die Frage wurde nicht gefunden.");

            if (current.Typ == targetType)
                return current;

            var sourceProfile = QuestionTypeProfiles.For(current.Typ);
            var targetProfile = QuestionTypeProfiles.For(targetType);

            // Tabellennamen kommen aus dem Profil - eine feste Aufzaehlung, nie aus Eingaben
            // zusammengesetzt.
            var oldTable = sourceProfile.TableName;
            var newTable = targetProfile.TableName;

            var context = CurrentContext;

            await using var transaction = await context.Database
                .BeginTransactionAsync().ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(
                $"DELETE FROM [question].[{oldTable}] WHERE [Id] = @id",
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

            await transaction.CommitAsync().ConfigureAwait(false);

            // Der Kontext haelt die Frage noch als alten Typ - erst nach dem Vergessen liest
            // ein Get sie als den neuen.
            context.ChangeTracker.Clear();

            return await GetAsync(questionId).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Die umgewandelte Frage wurde nicht gefunden.");
        }

        /// <summary>
        /// Die Schaetzfrage ist die einzige Untertabelle mit eigenen Spalten; sie bekommt beim
        /// Anlegen ausdrueckliche Startwerte, damit sie nicht von Standardwerten der Datenbank
        /// abhaengt.
        /// </summary>
        private static string InsertStatementFor(string tableName)
            => tableName == nameof(AppreciateQestion)
                ? $"INSERT INTO [question].[{tableName}] ([Id], [ValueKind], [Unit], [ExpectedValue], [ExpectedDate]) "
                  + "VALUES (@id, 0, 0, 0, NULL)"
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
                effects.Add("Sollwert und Einheit der Schaetzfrage gehen verloren.");

            if (to == QuestionType.Appreciate)
                effects.Add("Der Sollwert muss danach neu gesetzt werden.");

            if (from == QuestionType.MultipleChoice && to != QuestionType.MultipleChoice)
                effects.Add("Die Loesungsmarkierungen bleiben erhalten, werden aber nicht mehr "
                          + "zur Wertung herangezogen.");

            if (to == QuestionType.MultipleChoice)
                effects.Add("Mindestens ein Schritt muss als Loesung markiert sein, und die Zahl "
                          + "der waehlbaren Antworten muss dazu passen.");

            effects.Add($"Anzeige und Bedienung wechseln auf die Vorgaben fuer {target.DisplayName}.");
            effects.Add("Schritte, Medien, Spielfeldzellen und bisherige Ergebnisse bleiben unberuehrt.");

            return effects;
        }
    }
}
