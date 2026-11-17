using Quizzer.Base;
using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.Teams;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Ein Team am Beamer (M37 T3): Fläche, Symbol und Name über die Vorlage <c>TeamAnzeige</c>, die Summe groß
    /// (<see cref="Game.Teamstand"/>, über den Stempel) und darunter die heutigen Mitglieder klein - deren Punkte bleiben am
    /// Spieler (F-M37-01). Ohne <see cref="Team"/> ist es die Gruppe „Ohne Team": wer etwa am Telefon beigetreten ist (M10)
    /// und noch nicht zugeteilt wurde - deren Punkte zählen für kein Team.
    /// </summary>
    public sealed class TeamKachel : ViewCommonBase, INotifyPropertyChanged
    {
        /// <summary>Was die Gruppe „Ohne Team" statt einer Summe zeigt.</summary>
        public const string OhneTeamText = "zählt für kein Team";

        private static readonly Brush OhneTeamPinsel = Gefroren(Color.FromRgb(0x55, 0x55, 0x55));
        private static readonly Brush PlusBrush = Gefroren(Color.FromRgb(0x1E, 0x9E, 0x4A));
        private static readonly Brush MinusBrush = Gefroren(Color.FromRgb(0xC6, 0x28, 0x28));

        private readonly Game spiel;
        private int? gezeigt;
        private DispatcherTimer? ausblender;

        internal TeamKachel(Game spiel, GameTeam? team, IReadOnlyList<PlayerStatsContext> mitglieder)
        {
            this.spiel = spiel;
            Team = team;
            Mitglieder = mitglieder;

            var farbe = FarbeVon(team);
            Symbol = farbe?.Symbol ?? "–";
            Pinsel = PinselVon(farbe);
        }

        /// <summary>Die Palettenfarbe eines Teams; <c>null</c> ohne Team oder bei einem Platz außerhalb der Palette (A10).</summary>
        internal static TeamFarbe? FarbeVon(GameTeam? team) =>
            team != null && TeamFarben.IsValid(team.ColorIndex) ? TeamFarben.Alle[team.ColorIndex] : null;

        /// <summary>Die Fläche dazu; ohne Farbe ein neutrales Grau.</summary>
        internal static Brush PinselVon(TeamFarbe? farbe) => farbe != null ? TeamZeile.PinselFuer(farbe) : OhneTeamPinsel;

        public GameTeam? Team { get; }

        public bool IstOhneTeam => Team == null;

        public string Name => Team?.Name ?? "Ohne Team";

        public string Symbol { get; }

        public Brush Pinsel { get; }

        public IReadOnlyList<PlayerStatsContext> Mitglieder { get; }

        /// <summary>Die Summe des Teams; die Gruppe „Ohne Team" hat keine.</summary>
        public int Summe => Team == null ? 0 : spiel.Teamstand(Team.Id);

        public string SummeText => IstOhneTeam ? OhneTeamText : Summe.ToString();

        /// <summary>Der Platz in der Siegerehrung; 0 während des Spiels und für „Ohne Team".</summary>
        public int Platz { get; private set; }

        public string PlatzText => Platz == 0 ? string.Empty : $"{Platz}.";

        /// <summary>„+300" bzw. „−100" nach einer Frage, einige Sekunden lang - wie bei der Spielerkachel.</summary>
        public string WechselText { get; private set; } = string.Empty;

        public bool WechselAktiv { get; private set; }

        public Brush WechselBrush { get; private set; } = PlusBrush;

        /// <summary>Neu lesen nach einem Zug; <paramref name="platz"/> nur in der Siegerehrung, sonst 0.</summary>
        internal void Aktualisieren(int platz, bool siegerehrung)
        {
            Platz = IstOhneTeam ? 0 : platz;
            OnPropertyChanged(nameof(Platz));
            OnPropertyChanged(nameof(PlatzText));
            OnPropertyChanged(nameof(Summe));
            OnPropertyChanged(nameof(SummeText));

            if (!IstOhneTeam)
                ZeigeWechsel(Summe, siegerehrung);
        }

        /// <summary>Gemessen gegen den zuletzt gezeigten Stand; beim ersten Aufbau und in der Siegerehrung ohne Plakette.</summary>
        private void ZeigeWechsel(int neu, bool siegerehrung)
        {
            var delta = neu - (gezeigt ?? neu);
            gezeigt = neu;

            if (delta == 0 || siegerehrung)
                return;

            WechselText = Punktewechsel.Text(delta);
            WechselBrush = delta > 0 ? PlusBrush : MinusBrush;
            WechselAktiv = true;
            OnPropertyChanged(nameof(WechselText));
            OnPropertyChanged(nameof(WechselBrush));
            OnPropertyChanged(nameof(WechselAktiv));

            ausblender ??= new DispatcherTimer(Punktewechsel.Anzeigedauer, DispatcherPriority.Normal, (_, _) => Blende(), Dispatcher.CurrentDispatcher);
            ausblender.Stop();
            ausblender.Start();
        }

        private void Blende()
        {
            ausblender?.Stop();
            WechselAktiv = false;
            OnPropertyChanged(nameof(WechselAktiv));
        }

        private static Brush Gefroren(Color farbe)
        {
            var brush = new SolidColorBrush(farbe);
            brush.Freeze();
            return brush;
        }
    }
}
