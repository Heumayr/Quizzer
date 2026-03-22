using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quizzer.DataModels;
using Quizzer.DataModels.Exceptions;
using Quizzer.DataModels.Models;
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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = Settings.ConnectionString;

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
                .WithMany()
                .HasForeignKey(qr => qr.GameGridCoordinateId)
                .OnDelete(DeleteBehavior.NoAction);

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<GameGridCoordinate> GameGridCoordinates { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Header> Headers { get; set; }
        public DbSet<PlayerXGame> PlayerXGames { get; set; }

        public DbSet<QuestionResult> QuestionResults { get; set; }
        public DbSet<QuestionStepResource> QuestionStepResources { get; set; }

        public DbSet<QuestionBase> QuestionBases { get; set; }
        public DbSet<DefaultQuestion> DefaultQuestions { get; set; }
        public DbSet<MultipleChoiceQuestion> MultipleChoiceQuestions { get; set; }
        public DbSet<StepXStep> StepXSteps { get; set; }

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