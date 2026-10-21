using System.Security.Cryptography;
using System.Text;

namespace LocalBuzzer.Service.Base
{
    /// <summary>
    /// Der Zugangsschlüssel eines Telefons zum Buzzer - <b>nicht mehr die Spieler-Id</b>.
    /// <para>
    /// <b>Anlass: Sicherheitsprüfung 2026-10-21.</b> Bis dahin war <c>?playerid=&lt;Guid&gt;</c>
    /// der ganze Ausweis am Hub. Solange die Id nur im QR-Code beim Spielleiter stand, war sie
    /// faktisch geheim - seit der Selbstanmeldung (M10) gab <c>/join/candidates</c> sie aber
    /// jedem Telefon im WLAN heraus. Damit konnte jedes Gerät sich als jeder Spieler verbinden,
    /// den echten verdrängen und für ihn buzzern, ohne dass der Spielleiter etwas annahm.
    /// </para>
    /// <para>
    /// <b>Jetzt:</b> Schlüssel = HMAC-SHA256(Rechnergeheimnis, Spieler-Id), gekürzt auf 128 Bit.
    /// Ihn bekommt nur, wer den QR-Code des Spielleiters scannt oder vom Spielleiter angenommen
    /// wird. Die Kandidatenliste trägt eine <i>andere</i>, ebenfalls abgeleitete Kennung, aus der
    /// sich der Schlüssel nicht gewinnen lässt.
    /// </para>
    /// <para>
    /// <b>Abgeleitet statt gespeichert</b>, damit kein Zustand je Spieler nötig ist; <b>das
    /// Geheimnis liegt dauerhaft</b> im Benutzerordner, damit QR-Codes und angemeldete Telefone
    /// einen Neustart des Laptops überstehen.
    /// </para>
    /// </summary>
    public sealed class BuzzerAccessKeys
    {
        private const string SchluesselZweck = "quizzer-buzzer-zugang";
        private const string KandidatZweck = "quizzer-buzzer-kandidat";

        private readonly byte[] geheimnis;

        public BuzzerAccessKeys(byte[] geheimnis)
        {
            ArgumentNullException.ThrowIfNull(geheimnis);

            if (geheimnis.Length < 32)
                throw new ArgumentException("Das Geheimnis braucht mindestens 32 Byte.", nameof(geheimnis));

            this.geheimnis = (byte[])geheimnis.Clone();
        }

        /// <summary>Ein Satz Schlüssel nur für diesen Lauf - für Tests und als Rückfall.</summary>
        public static BuzzerAccessKeys Fluechtig() => new(RandomNumberGenerator.GetBytes(32));

        /// <summary>
        /// Liest das Rechnergeheimnis aus <paramref name="ordner"/> oder legt es an.
        /// <para>
        /// <b>Ein unlesbares oder zu kurzes Geheimnis wird ersetzt</b> - dann müssen die Telefone
        /// den QR-Code neu scannen, aber der Buzzer startet.
        /// </para>
        /// </summary>
        public static BuzzerAccessKeys LadeOderErzeuge(string ordner)
        {
            var pfad = Path.Combine(ordner, "buzzer-geheimnis.bin");

            try
            {
                if (File.Exists(pfad))
                {
                    var vorhanden = File.ReadAllBytes(pfad);

                    if (vorhanden.Length >= 32)
                        return new BuzzerAccessKeys(vorhanden);
                }

                Directory.CreateDirectory(ordner);

                var neu = RandomNumberGenerator.GetBytes(32);
                File.WriteAllBytes(pfad, neu);

                return new BuzzerAccessKeys(neu);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fluechtig();
            }
        }

        /// <summary>Der Zugangsschlüssel eines Spielers - steht im QR-Code und im Cookie des Telefons.</summary>
        public string SchluesselFuer(Guid playerId) => Ableiten(SchluesselZweck, playerId);

        /// <summary>Die Kennung eines Spielers in der Kandidatenliste - taugt nicht als Zugang.</summary>
        public string KandidatFuer(Guid playerId) => Ableiten(KandidatZweck, playerId);

        /// <summary>Welcher der <paramref name="spieler"/> zu <paramref name="schluessel"/> gehört - oder keiner.</summary>
        public T? FindeSchluessel<T>(string? schluessel, IEnumerable<T> spieler, Func<T, Guid> id) where T : class
            => Finde(SchluesselZweck, schluessel, spieler, id);

        /// <summary>Welcher der <paramref name="kandidaten"/> zu <paramref name="kennung"/> gehört - oder keiner.</summary>
        public T? FindeKandidat<T>(string? kennung, IEnumerable<T> kandidaten, Func<T, Guid> id) where T : class
            => Finde(KandidatZweck, kennung, kandidaten, id);

        private T? Finde<T>(string zweck, string? wert, IEnumerable<T> menge, Func<T, Guid> id) where T : class
        {
            if (string.IsNullOrWhiteSpace(wert) || wert.Length > 64)
                return null;

            var gesucht = Encoding.ASCII.GetBytes(wert);
            T? treffer = null;

            // Alle vergleichen, in fester Zeit je Vergleich - kein vorzeitiger Ausstieg.
            foreach (var kandidat in menge)
            {
                var erwartet = Encoding.ASCII.GetBytes(Ableiten(zweck, id(kandidat)));

                if (CryptographicOperations.FixedTimeEquals(erwartet, gesucht))
                    treffer = kandidat;
            }

            return treffer;
        }

        private string Ableiten(string zweck, Guid playerId)
        {
            var daten = Encoding.UTF8.GetBytes($"{zweck}:{playerId:N}");
            var mac = HMACSHA256.HashData(geheimnis, daten);

            return Convert.ToBase64String(mac, 0, 16)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
