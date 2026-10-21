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

    /// <summary>Der Server ist nicht erreichbar (Netz, Zertifikat, Zeitueberschreitung).</summary>
    public sealed class ServerUnreachableException(string message, Exception inner) : Exception(message, inner);
}
