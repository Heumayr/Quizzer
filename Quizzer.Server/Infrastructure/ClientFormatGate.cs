using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using System.Globalization;

namespace Quizzer.Server.Infrastructure
{
    /// <summary>
    /// Versionsriegel (M36 A4): ein WPF mit anderem Abgleichformat wird schon bei der Anmeldung
    /// abgewiesen, mit einer Meldung, die auch ein altes Programm anzeigt (es liest <see cref="ApiError"/>).
    /// Snapshot und Push pruefen das Format ohnehin - hier geht es um den frueheren, verstaendlichen Halt.
    /// </summary>
    internal static class ClientFormatGate
    {
        /// <summary>Ein Programm ohne Kopf stammt von vor dem Riegel und spricht Format 1.</summary>
        internal const int LegacyFormat = 1;

        internal const string TooOld = "Dieses Quizzer-Programm ist zu alt für den Server. Bitte Quizzer aktualisieren.";
        internal const string TooNew = "Der Server ist älter als dieses Quizzer-Programm. Bitte den Server aktualisieren lassen.";
        internal const string Unreadable = "Die Angabe des Abgleichformats ist ungültig.";

        /// <summary>Was jedes Programm erreichen muss, auch ein zu altes: die Versionsabfrage selbst.</summary>
        private static readonly string[] Exempt = [ApiInfo.BasePath + "/version"];

        public static void UseClientFormatGate(this WebApplication app) => app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var geprueft = path.StartsWithSegments(ApiInfo.BasePath)
                && !Exempt.Any(e => path.Equals(e, StringComparison.OrdinalIgnoreCase));

            var befund = geprueft ? Check(context.Request.Headers[ApiInfo.SyncFormatHeader].ToString()) : null;
            if (befund is not { } abweisung)
            {
                await next();
                return;
            }

            await Results.Json(new ApiError(abweisung.Message), statusCode: abweisung.Status).ExecuteAsync(context);
        });

        /// <summary>Liefert <c>null</c> zum Durchlassen, sonst Statuscode und Meldung.</summary>
        internal static (int Status, string Message)? Check(string? header)
        {
            var format = LegacyFormat;
            if (!string.IsNullOrWhiteSpace(header)
                && !int.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out format))
                return (StatusCodes.Status400BadRequest, Unreadable);

            if (format < SyncSnapshot.CurrentFormatVersion)
                return (StatusCodes.Status426UpgradeRequired, TooOld);

            if (format > SyncSnapshot.CurrentFormatVersion)
                return (StatusCodes.Status409Conflict, TooNew);

            return null;
        }
    }
}
