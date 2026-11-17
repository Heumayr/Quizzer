using System.Net;
using System.Net.Sockets;

namespace Quizzer.TestShared
{
    /// <summary>
    /// Netz der Testserver, für alle Testprojekte in einer Datei (eingebunden per <c>Compile Include</c>).
    /// </summary>
    internal static class TestNetwork
    {
        /// <summary>
        /// Jeder Testserver bindet nur hier (V14). Auf allen Adressen fragt Windows je neuem Arbeitsbaum nach
        /// dem Netzzugriff, und der Testserver stünde im Heimnetz. Gehalten von <c>TestServerLoopbackUnitTests</c>.
        /// </summary>
        public const string Address = "127.0.0.1";

        /// <summary>Ist der Port auf <see cref="Address"/> frei? Ein belegter heißt meist: ein abgebrochener Testlauf hält ihn.</summary>
        public static bool PortIsFree(int port)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>Fragt die Bedingung ab, bis sie gilt oder die Frist um ist - der Hub antwortet auf eigenen Threads.</summary>
        public static async Task<bool> WarteAsync(Func<bool> bedingung, int millis = 4000)
        {
            var bis = DateTime.UtcNow.AddMilliseconds(millis);
            while (DateTime.UtcNow < bis)
            {
                if (bedingung())
                    return true;
                await Task.Delay(30);
            }
            return bedingung();
        }
    }
}
