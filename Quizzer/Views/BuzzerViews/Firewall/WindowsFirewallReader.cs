using System.Diagnostics;

namespace Quizzer.Views.BuzzerViews.Firewall
{
    /// <summary>
    /// Fragt die Windows-Firewall ueber ihre COM-Schnittstelle <c>HNetCfg.FwPolicy2</c> ab.
    /// Lesend und ohne Verwalterrechte; ein Fehlschlag wird gemeldet, nicht geworfen.
    /// </summary>
    public sealed class WindowsFirewallReader : IFirewallReader
    {
        private const string ProgId = "HNetCfg.FwPolicy2";

        public bool IsAvailable { get; private set; } = true;

        /// <inheritdoc />
        public int ActiveProfiles
        {
            get
            {
                var policy = CreatePolicy();

                if (policy == null)
                    return 0;

                try
                {
                    return (int)policy.CurrentProfileTypes;
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return 0;
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<FirewallRule> ReadRules()
        {
            var policy = CreatePolicy();

            if (policy == null)
                return Array.Empty<FirewallRule>();

            var rules = new List<FirewallRule>();

            try
            {
                foreach (dynamic rule in policy.Rules)
                {
                    var gelesen = ReadRule(rule);

                    if (gelesen != null)
                        rules.Add(gelesen);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
                return Array.Empty<FirewallRule>();
            }

            return rules;
        }

        /// <summary>
        /// Uebersetzt eine einzelne COM-Regel. Einzelne Regeln koennen beim Lesen scheitern -
        /// dann faellt genau sie weg, nicht die ganze Abfrage.
        /// </summary>
        private static FirewallRule? ReadRule(dynamic rule)
        {
            try
            {
                return new FirewallRule(
                    Name: (string?)rule.Name ?? string.Empty,
                    ApplicationName: (string?)rule.ApplicationName,
                    Enabled: (bool)rule.Enabled,
                    Inbound: (int)rule.Direction == 1,
                    Allow: (int)rule.Action == 1,
                    Profiles: (int)rule.Profiles,
                    Protocol: (int)rule.Protocol,
                    LocalPorts: (string?)rule.LocalPorts ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Firewall-Regel nicht lesbar: {ex.Message}");
                return null;
            }
        }

        private dynamic? CreatePolicy()
        {
            try
            {
                var typ = Type.GetTypeFromProgID(ProgId);

                if (typ == null)
                {
                    IsAvailable = false;
                    return null;
                }

                return Activator.CreateInstance(typ);
            }
            catch (Exception ex)
            {
                Fail(ex);
                return null;
            }
        }

        private void Fail(Exception ex)
        {
            IsAvailable = false;
            Debug.WriteLine($"Firewall nicht abfragbar: {ex.Message}");
        }
    }
}
