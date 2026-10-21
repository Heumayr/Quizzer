using System.Collections.Concurrent;

namespace LocalBuzzer.Service.Base
{
    /// <summary>Ein Name, unter dem man sich am Telefon zum Spiel melden kann.</summary>
    public sealed record JoinCandidate(Guid PlayerId, string Name);

    public enum JoinStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired,
    }

    /// <summary>
    /// Ein Telefon moechte mitspielen (M10). Entweder als bekannter Mitspieler
    /// (<see cref="PlayerId"/>) oder als Gast (<see cref="GuestName"/>). Entschieden wird am
    /// Laptop: der Gamemaster nimmt an oder lehnt ab.
    /// <para>
    /// <see cref="Id"/> ist zufaellig und nur dem anfragenden Telefon bekannt - es fragt damit
    /// nach, ob es hinein darf. Ein Kennwort geht dabei nie ueber das unverschluesselte WLAN
    /// (Rueckfrage M10).
    /// </para>
    /// </summary>
    public sealed class JoinRequest
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public Guid? PlayerId { get; init; }

        public string? GuestName { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string RemoteAddress { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public JoinStatus Status { get; internal set; } = JoinStatus.Pending;

        /// <summary>Unter welchem Mitspieler das Telefon nach dem Annehmen verbindet.</summary>
        public Guid? AssignedPlayerId { get; internal set; }

        public bool IsGuest => PlayerId == null;
    }

    /// <summary>
    /// Die offenen Beitrittsanfragen. Begrenzt, damit ein Telefon im WLAN die Liste des
    /// Gamemasters nicht fluten kann: hoechstens zwei offene Anfragen je Adresse, insgesamt
    /// dreissig, und nach zehn Minuten verfaellt eine unbeantwortete.
    /// </summary>
    public sealed class JoinRequestRegistry
    {
        public const int MaxPendingPerAddress = 2;

        public const int MaxPendingTotal = 30;

        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        public const int MaxGuestNameLength = 40;

        private readonly ConcurrentDictionary<Guid, JoinRequest> requests = new();
        private readonly TimeProvider time;

        public JoinRequestRegistry(TimeProvider? time = null)
        {
            this.time = time ?? TimeProvider.System;
        }

        /// <summary>Wird auf einem Hintergrund-Thread ausgeloest.</summary>
        public event Action<JoinRequest>? Requested;

        public enum CreateResult { Created, Invalid, TooMany }

        public (CreateResult Result, JoinRequest? Request) Create(Guid? playerId, string? guestName, IReadOnlyList<JoinCandidate> candidates, string remoteAddress)
        {
            ExpireOld();

            string displayName;

            if (playerId is { } id)
            {
                var candidate = candidates.FirstOrDefault(c => c.PlayerId == id);
                if (candidate == null)
                    return (CreateResult.Invalid, null);

                displayName = candidate.Name;
                guestName = null;
            }
            else
            {
                displayName = (guestName ?? string.Empty).Trim();
                // Auch unsichtbare Formatzeichen (Unicode Cf, etwa U+202E "rechts nach links") -
                // mit ihnen laesst sich ein Name bauen, der am Laptop wie ein anderer aussieht
                // (Sicherheitspruefung 2026-10-21).
                if (displayName.Length == 0 || displayName.Length > MaxGuestNameLength
                    || displayName.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
                    return (CreateResult.Invalid, null);

                guestName = displayName;
            }

            var pending = requests.Values.Where(r => r.Status == JoinStatus.Pending).ToList();
            if (pending.Count >= MaxPendingTotal || pending.Count(r => r.RemoteAddress == remoteAddress) >= MaxPendingPerAddress)
                return (CreateResult.TooMany, null);

            var request = new JoinRequest
            {
                PlayerId = playerId,
                GuestName = guestName,
                DisplayName = displayName,
                RemoteAddress = remoteAddress,
                CreatedAt = time.GetUtcNow(),
            };

            requests[request.Id] = request;
            Requested?.Invoke(request);
            return (CreateResult.Created, request);
        }

        public JoinRequest? Get(Guid id)
        {
            ExpireOld();
            return requests.TryGetValue(id, out var request) ? request : null;
        }

        public IReadOnlyList<JoinRequest> Pending()
        {
            ExpireOld();
            return requests.Values.Where(r => r.Status == JoinStatus.Pending).OrderBy(r => r.CreatedAt).ToList();
        }

        public bool Accept(Guid id, Guid assignedPlayerId)
        {
            if (!requests.TryGetValue(id, out var request) || request.Status != JoinStatus.Pending)
                return false;

            request.AssignedPlayerId = assignedPlayerId;
            request.Status = JoinStatus.Accepted;
            return true;
        }

        public bool Decline(Guid id)
        {
            if (!requests.TryGetValue(id, out var request) || request.Status != JoinStatus.Pending)
                return false;

            request.Status = JoinStatus.Declined;
            return true;
        }

        private void ExpireOld()
        {
            var now = time.GetUtcNow();

            foreach (var request in requests.Values)
            {
                if (request.Status == JoinStatus.Pending && now - request.CreatedAt > Lifetime)
                    request.Status = JoinStatus.Expired;

                // Erledigte Anfragen bleiben noch eine Weile abrufbar, damit das Telefon sein
                // Ergebnis sieht - danach werden sie vergessen.
                if (request.Status != JoinStatus.Pending && now - request.CreatedAt > Lifetime * 2)
                    requests.TryRemove(request.Id, out _);
            }
        }
    }
}
