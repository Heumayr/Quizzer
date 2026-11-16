using System.Net;

namespace Quizzer.Client
{
    /// <summary>Der Server hat mit einem Fehler geantwortet; <see cref="Exception.Message"/> darf man zeigen.</summary>
    public class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }

    /// <summary>Die Sitzung ist abgelaufen oder widerrufen - neu anmelden.</summary>
    public sealed class SessionExpiredException(string message) : ApiException(HttpStatusCode.Unauthorized, message);

    /// <summary>Das Kennwort stimmt, das Konto verlangt aber einen Code aus der Authenticator-App (M36 A5).</summary>
    public sealed class TotpRequiredException(string message) : ApiException(HttpStatusCode.Unauthorized, message);

    /// <summary>Admin-Konto ohne Zwei-Faktor-Anmeldung: der Server erlaubt nur die Einrichtung.</summary>
    public sealed class TotpSetupRequiredException(string message) : ApiException(HttpStatusCode.Forbidden, message);

    /// <summary>Der Server ist nicht erreichbar (Netz, Zertifikat, Zeitueberschreitung).</summary>
    public sealed class ServerUnreachableException(string message, Exception inner) : Exception(message, inner);

    /// <summary>
    /// Der Client hat einen Abgleich verweigert, weil er nach einem Fehler aussieht. <see cref="MassDeletion"/> nennt,
    /// was gelöscht würde - ein ausdrücklicher Abgleich kann danach fragen und genau das freigeben (V06).
    /// </summary>
    public sealed class SyncSafetyException(string message, MassDeletion massDeletion) : Exception(message)
    {
        public MassDeletion MassDeletion { get; } = massDeletion;
    }
}
