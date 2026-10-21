using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quizzer.DataModels;
using Quizzer.DataModels.Exceptions;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Auth;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.Logic.Context
{
    internal class DataContext : DbContext
    {
        static DataContext()
        {
            if (string.IsNullOrEmpty(Settings.ConnectionString))
            {
                Settings.LoadSettings();
            }
        }

        private readonly string? connectionStringOverride;

        public DataContext()
        {
            SavedChanges += (_, e) => DataChangeNotifier.RaiseSaved(e.EntitiesSavedCount);
        }

        /// <summary>
        /// Ein Kontext auf eine bestimmte Datenbank statt auf <see cref="Settings.ConnectionString"/>.
        /// Der Abgleich braucht das: im Test laufen Server und Client im selben Prozess, jeder
        /// gegen seine eigene Datenbank.
        /// </summary>
        public DataContext(string connectionString) : this()
        {
            connectionStringOverride = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = connectionStringOverride ?? Settings.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new RuntimeException("No valid connection string!");
            }

            optionsBuilder.UseSqlServer(connectionString);

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QuestionBase>().UseTptMappingStrategy();

            modelBuilder.Entity<QuestionResult>()
                .HasOne(qr => qr.GameGridCoordinate)
                .WithMany(c => c.QuestionResults)
                .HasForeignKey(qr => qr.GameGridCoordinateId)
                .OnDelete(DeleteBehavior.NoAction);

            // Bewusst WithMany() ohne Gegenstueck: Game.QuestionResults traegt [NotMapped] und
            // rechnet sich aus den Zellen (SelectMany), ist also keine Sammlung, die EF fuellen
            // koennte. Als Navigation angemeldet, scheitert jedes Laden eines Ergebnisses samt
            // Spiel mit "does not implement ICollection<QuestionResult>". Fremdschluessel und
            // Loeschregel bleiben unveraendert - am Schema aendert sich nichts.
            modelBuilder.Entity<QuestionResult>()
                .HasOne(qr => qr.Game)
                .WithMany()
                .HasForeignKey(qr => qr.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            // B11: OwnerPlayerId war eine nackte Guid?-Spalte ohne Fremdschluessel. Wurde der
            // Besitzer entfernt, blieb die Kennung stehen und zeigte ins Leere -
            // QuestionOwnership.IsVisible laesst nur null oder die eigene Id durch, die Frage
            // war damit fuer JEDEN unsichtbar, ohne Meldung und ohne Weg zurueck.
            // SetNull statt NoAction: die Frage soll den Besitzerwechsel ueberleben und in den
            // gemeinsamen Bestand fallen, nicht das Loeschen des Mitspielers verhindern.
            // Der Riegel gehoert ins Schema und nicht an eine Aufrufstelle: DemoDataSeeder.Remove
            // ist ein zweiter Weg, der Besitzer loescht, ohne OwnerPlayerId je anzufassen.
            modelBuilder.Entity<QuestionBase>()
                .HasOne<Player>()
                .WithMany()
                .HasForeignKey(q => q.OwnerPlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            // M9: ein Konto haengt an genau einem Mitspieler. Restrict statt Cascade - ein
            // Mitspieler mit Konto laesst sich nicht nebenbei loeschen, erst das Konto.
            modelBuilder.Entity<Account>()
                .HasOne(a => a.Player)
                .WithMany()
                .HasForeignKey(a => a.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(t => t.Account)
                .WithMany()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<GameTheme> GameThemes { get; set; }
        public DbSet<GameGridCoordinate> GameGridCoordinates { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Header> Headers { get; set; }
        public DbSet<PlayerXGame> PlayerXGames { get; set; }

        public DbSet<QuestionResult> QuestionResults { get; set; }
        public DbSet<QuestionStepResource> QuestionStepResources { get; set; }

        public DbSet<QuestionBase> QuestionBases { get; set; }
        public DbSet<DefaultQuestion> DefaultQuestions { get; set; }
        public DbSet<MultipleChoiceQuestion> MultipleChoiceQuestions { get; set; }
        public DbSet<PropertiesQuestion> PropertiesQuestions { get; set; }
        public DbSet<AppreciateQestion> AppreciateQestions { get; set; }

        public DbSet<RevealQuestion> RevealQuestions { get; set; }

        public DbSet<OrderingQuestion> OrderingQuestions { get; set; }

        public DbSet<MatchingQuestion> MatchingQuestions { get; set; }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        /// <summary>
        /// Get the data set according to the entity type.
        /// </summary>
        /// <typeparam name="TEntity">entity type</typeparam>
        /// <returns></returns>
        /// <exception cref="LogicException">Thrown if no data set is found.</exception>
        //internal DbSet<TEntity> GetDbSet<TEntity>() where TEntity : ModelBase
        //{
        //    DbSet<TEntity>? dbSet = null;

        //    if (typeof(Category) == typeof(TEntity))
        //        dbSet = Categories as DbSet<TEntity>;
        //    else if (typeof(Game) == typeof(TEntity))
        //        dbSet = Games as DbSet<TEntity>;
        //    else if (typeof(GameGridCoordinate) == typeof(TEntity))
        //        dbSet = GameGridCoordinates as DbSet<TEntity>;
        //    else if (typeof(Player) == typeof(TEntity))
        //        dbSet = Players as DbSet<TEntity>;
        //    else if (typeof(Header) == typeof(TEntity))
        //        dbSet = Headers as DbSet<TEntity>;
        //    else if (typeof(PlayerXGame) == typeof(TEntity))
        //        dbSet = PlayerXGames as DbSet<TEntity>;
        //    else if (typeof(QuestionResult) == typeof(TEntity))
        //        dbSet = QuestionResults as DbSet<TEntity>;
        //    else if (typeof(QuestionStepResource) == typeof(TEntity))
        //        dbSet = QuestionStepResources as DbSet<TEntity>;
        //    else if (typeof(QuestionBase) == typeof(TEntity))
        //        dbSet = QuestionBases as DbSet<TEntity>;

        //    else if (typeof(DefaultQuestion) == typeof(TEntity))
        //        dbSet = DefaultQuestions as DbSet<TEntity>;
        //    else if (typeof(MultipleChoiceQuestion) == typeof(TEntity))
        //        dbSet = MultipleChoiceQuestions as DbSet<TEntity>;

        //    return dbSet ?? throw new RuntimeException("No database set found.");
        //}
        internal DbSet<TEntity> GetDbSet<TEntity>() where TEntity : ModelBase
        {
            try
            {
                return Set<TEntity>();
            }
            catch (Exception ex)
            {
                throw new RuntimeException($"No database set found for {typeof(TEntity).Name}.", ex);
            }
        }
    }
}