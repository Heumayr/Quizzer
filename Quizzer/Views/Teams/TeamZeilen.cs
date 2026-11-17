using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using System.ComponentModel;
using System.Windows.Media;

namespace Quizzer.Views.Teams
{
    /// <summary>
    /// Ein Team im Dialog „Teams bearbeiten“ (M37 T2) - ein Entwurf, geschrieben wird erst mit „Übernehmen“. Die Farbe
    /// steht nie allein: Fläche, Symbol und Name (<c>standards-bedienbarkeit.md</c> Regel 3).
    /// </summary>
    public sealed class TeamZeile : ViewCommonBase, INotifyPropertyChanged
    {
        private string name;
        private TeamFarbe farbe;
        private int mitglieder;

        public TeamZeile(Guid id, string name, TeamFarbe farbe)
        {
            Id = id;
            this.name = name;
            this.farbe = farbe;
        }

        /// <summary>Platzhalter „ohne Team“ in der Auswahl je Mitspieler.</summary>
        public static TeamZeile Ohne { get; } = new(Guid.Empty, "(ohne Team)", new TeamFarbe(-1, "keine", "#00000000", "–"));

        public Guid Id { get; }

        public string Name
        {
            get => name;
            set { name = value; OnPropertyChanged(); }
        }

        /// <summary>Die Grenze des Namensfelds - dieselbe wie Spalte, Server und Bündel.</summary>
        public int MaxNameLength => GameTeam.MaxNameLength;

        public TeamFarbe Farbe
        {
            get => farbe;
            set
            {
                farbe = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Pinsel));
                OnPropertyChanged(nameof(Symbol));
            }
        }

        public string Symbol => Farbe.Symbol;

        public Brush Pinsel => PinselFuer(Farbe);

        public int Mitglieder
        {
            get => mitglieder;
            set { mitglieder = value; OnPropertyChanged(); OnPropertyChanged(nameof(MitgliederText)); }
        }

        public string MitgliederText => Mitglieder == 1 ? "1 Mitglied" : $"{Mitglieder} Mitglieder";

        /// <summary>Die Fläche einer Palettenfarbe, eingefroren - auch für die Farbauswahl.</summary>
        public static Brush PinselFuer(TeamFarbe farbe)
        {
            var pinsel = new SolidColorBrush((Color)ColorConverter.ConvertFromString(farbe.Hex));
            pinsel.Freeze();
            return pinsel;
        }
    }

    /// <summary>Eine Palettenfarbe in der Farbauswahl - Fläche, Symbol, Name.</summary>
    public sealed record FarbWahl(TeamFarbe Farbe)
    {
        public string Name => Farbe.Name;

        public string Symbol => Farbe.Symbol;

        public Brush Pinsel { get; } = TeamZeile.PinselFuer(Farbe);
    }

    /// <summary>Ein Mitspieler und sein Team im Dialog; <see cref="TeamZeile.Ohne"/> heißt: keinem zugeteilt.</summary>
    public sealed class MitgliedZeile : ViewCommonBase, INotifyPropertyChanged
    {
        private readonly Action geaendert;
        private TeamZeile auswahl;

        public MitgliedZeile(Guid playerId, string name, TeamZeile auswahl, Action geaendert)
        {
            PlayerId = playerId;
            Name = name;
            this.auswahl = auswahl;
            this.geaendert = geaendert;
        }

        public Guid PlayerId { get; }

        public string Name { get; }

        /// <summary>Das gewählte Team. <c>null</c> kommt von WPF, wenn das Team aus der Liste fällt - dann ohne Team.</summary>
        public TeamZeile? Auswahl
        {
            get => auswahl;
            set
            {
                auswahl = value ?? TeamZeile.Ohne;
                OnPropertyChanged();
                geaendert();
            }
        }

        public Guid? TeamId => auswahl == TeamZeile.Ohne ? null : auswahl.Id;
    }
}
