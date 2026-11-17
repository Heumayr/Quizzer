using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Context
{
    /// <summary>Die Teams (M37 T1) - eigene Teildatei, damit <c>DataContext.cs</c> nicht weiter wächst.</summary>
    internal partial class DataContext
    {
        public DbSet<GameTeam> GameTeams { get; set; }

        /// <summary>
        /// Alle drei Fremdschlüssel NO ACTION: CASCADE oder SET NULL an Zuteilung und Ergebnis wären ein zweiter
        /// Pfad vom Spiel (Msg 1785). Riegel je Weg: Spiel <c>GamesController.BeforeActionAsync</c> (Teams nach
        /// Zuteilungen und Ergebnissen), Zuteilung <c>GameTeamsController</c> (nimmt die Mitglieder heraus), Ergebnis
        /// <c>GameTeamsController</c> (ein Team mit Ergebnissen lässt sich nicht löschen); am Server
        /// <c>WriteRules.Team</c> und <c>SyncStore.ReleaseTeamReferencesAsync</c>.
        /// </summary>
        private static void ConfigureTeams(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Game>().HasMany(g => g.GameTeams).WithOne().HasForeignKey(t => t.GameId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PlayerXGame>().HasOne<GameTeam>().WithMany().HasForeignKey(x => x.GameTeamId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<QuestionResult>().HasOne<GameTeam>().WithMany().HasForeignKey(r => r.GameTeamId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
