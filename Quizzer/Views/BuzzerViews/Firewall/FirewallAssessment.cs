namespace Quizzer.Views.BuzzerViews.Firewall
{
    /// <summary>
    /// Beurteilt, ob die Telefone den Buzzer-Server ueberhaupt erreichen koennen.
    /// <para>
    /// Anlass: am 2026-08-24 und wieder am 2026-09-05 galten die Regeln fuer die Anwendung nur
    /// fuer das Profil Oeffentlich, waehrend das Netz Privat war. Der Server lief, die Anzeige
    /// sagte "Running", und kein Telefon kam durch. Ein Abruf vom selben Rechner beweist dabei
    /// nichts - er laeuft gar nicht durch die Firewall.
    /// </para>
    /// </summary>
    public static class FirewallAssessment
    {
        private const int ProtocolTcp = 6;
        private const int ProtocolAny = 256;

        /// <summary>Die drei Profilbits der Windows-Firewall, in der Sprache des Spielleiters.</summary>
        private static readonly (int Bit, string Name)[] ProfileNames =
        {
            (1, "Domäne"),
            (2, "Privat"),
            (4, "Öffentlich"),
        };

        /// <summary>
        /// Prueft, ob jedes aktive Netzprofil eine passende Freigabe hat.
        /// </summary>
        /// <param name="rules">Die eingehenden Regeln der Firewall.</param>
        /// <param name="activeProfiles">Bitmaske der gerade aktiven Netzprofile.</param>
        /// <param name="exePath">Pfad des laufenden Programms; leer, wenn unbekannt.</param>
        /// <param name="port">Der Port, auf dem der Buzzer-Server lauscht.</param>
        public static FirewallVerdict Assess(
            IReadOnlyList<FirewallRule> rules, int activeProfiles, string? exePath, int port)
        {
            if (activeProfiles == 0)
                return FirewallVerdict.Ok();

            var passende = rules.Where(r => Matches(r, exePath, port)).ToList();

            if (passende.Count == 0)
                return NoRule(activeProfiles, port);

            var abgedeckt = passende.Aggregate(0, (summe, r) => summe | r.Profiles);
            var fehlend = MissingProfileNames(activeProfiles, abgedeckt);

            if (fehlend.Count == 0)
                return FirewallVerdict.Ok();

            return new FirewallVerdict(FirewallState.FalschesProfil, fehlend, WrongProfileHint(fehlend, port));
        }

        /// <summary>Zaehlt eine Regel fuer diesen Server?</summary>
        private static bool Matches(FirewallRule rule, string? exePath, int port)
        {
            if (!rule.Enabled || !rule.Inbound || !rule.Allow)
                return false;

            if (rule.Protocol != ProtocolTcp && rule.Protocol != ProtocolAny)
                return false;

            if (!string.IsNullOrWhiteSpace(rule.ApplicationName))
            {
                return !string.IsNullOrWhiteSpace(exePath)
                    && string.Equals(rule.ApplicationName, exePath, StringComparison.OrdinalIgnoreCase);
            }

            // Regel ohne Programm: dann muss sie den Port abdecken.
            return CoversPort(rule.LocalPorts, port);
        }

        /// <summary>Deckt eine Portangabe wie <c>*</c>, <c>80,5000</c> oder <c>4000-6000</c> den Port ab?</summary>
        internal static bool CoversPort(string? localPorts, int port)
        {
            if (string.IsNullOrWhiteSpace(localPorts))
                return false;

            foreach (var teil in localPorts.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var eintrag = teil.Trim();

                if (eintrag is "*" or "Any")
                    return true;

                var grenzen = eintrag.Split('-', StringSplitOptions.RemoveEmptyEntries);

                if (grenzen.Length == 2
                    && int.TryParse(grenzen[0], out var von)
                    && int.TryParse(grenzen[1], out var bis)
                    && port >= von && port <= bis)
                {
                    return true;
                }

                if (grenzen.Length == 1 && int.TryParse(grenzen[0], out var einzeln) && einzeln == port)
                    return true;
            }

            return false;
        }

        private static List<string> MissingProfileNames(int activeProfiles, int coveredProfiles)
        {
            return ProfileNames
                .Where(p => (activeProfiles & p.Bit) != 0 && (coveredProfiles & p.Bit) == 0)
                .Select(p => p.Name)
                .ToList();
        }

        internal static List<string> ActiveProfileNames(int activeProfiles) =>
            ProfileNames.Where(p => (activeProfiles & p.Bit) != 0).Select(p => p.Name).ToList();

        private static FirewallVerdict NoRule(int activeProfiles, int port)
        {
            var aktiv = string.Join(", ", ActiveProfileNames(activeProfiles));

            return new FirewallVerdict(
                FirewallState.KeineRegel,
                ActiveProfileNames(activeProfiles),
                $"Für Quizzer gibt es keine Firewall-Freigabe. Telefone im Netz ({aktiv}) "
                + $"erreichen den Server nicht. „Freigeben“ legt eine Regel für Port {port} an.");
        }

        private static string WrongProfileHint(IReadOnlyList<string> missing, int port)
        {
            var fehlend = string.Join(", ", missing);

            return $"Die Firewall-Freigabe gilt nicht für das Netz „{fehlend}“. Telefone in diesem "
                 + $"Netz erreichen den Server nicht. „Freigeben“ legt eine Regel für Port {port} an.";
        }
    }
}
