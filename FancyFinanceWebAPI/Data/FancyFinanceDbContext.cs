using System;
using FancyFinanceWebAPI.Modules.Users;
using FancyFinanceWebAPI.Modules.Incomes;
using FancyFinanceWebAPI.Modules.Expenses;
using FancyFinanceWebAPI.Modules.Accounts;
using FancyFinanceWebAPI.Modules.Transactions;
using FancyFinanceWebAPI.Shared.Category;
using FancyFinanceWebAPI.Shared.Currency;
using FancyFinanceWebAPI.Shared.Frequency;
using FancyFinanceWebAPI.Modules.Transactions.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FancyFinanceWebAPI.Data
{
    public class FancyFinanceDbContext : DbContext
    {
        public FancyFinanceDbContext(
            DbContextOptions<FancyFinanceDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Frequency> Frequencies { get; set; }
        public DbSet<Income> Incomes { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<StatementImport> StatementImports { get; set; }

        protected override void ConfigureConventions(
            ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            // Existing tables do not have indexes on their foreign keys.
            // Declare the indexes we want explicitly below.
            configurationBuilder.Conventions.Remove(
                typeof(ForeignKeyIndexConvention));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StatementImport>(entity =>
            {
                entity.HasOne(x => x.Account)
                    .WithMany()
                    .HasForeignKey(x => x.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new
                {
                    x.AccountId,
                    x.FileHash
                })
                .IsUnique();
            });

            modelBuilder.Entity<User>()
                .Property<bool>("IsAdmin")
                .HasDefaultValue(false);

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(x => x.CategoryId)
                    .HasName("categories_pkey");

                entity.Property(x => x.CategoryId)
                    .UseIdentityByDefaultColumn();

                entity.Property(x => x.Name)
                    .HasMaxLength(255);

                entity.HasIndex(x => x.Name)
                    .IsUnique()
                    .HasDatabaseName("categories_category_name_key");

                ConfigureExistingAudit(entity, "categories");
            });

            modelBuilder.Entity<Currency>(entity =>
            {
                entity.HasKey(x => x.CurrencyId)
                    .HasName("currencies_pkey");

                entity.Property(x => x.CurrencyId)
                    .UseIdentityByDefaultColumn();

                entity.Property(x => x.Name)
                    .HasMaxLength(255);

                entity.Property(x => x.IsoCode)
                    .HasColumnType("character(3)")
                    .HasMaxLength(3)
                    .IsFixedLength();

                entity.Property(x => x.Symbol)
                    .HasMaxLength(5);

                entity.HasIndex(x => x.IsoCode)
                    .IsUnique()
                    .HasDatabaseName("currencies_iso_code_key");

                entity.HasIndex(x => x.NumericCode)
                    .IsUnique()
                    .HasDatabaseName("currencies_numeric_code_key");

                ConfigureExistingAudit(entity, "currencies");
            });

            modelBuilder.Entity<Frequency>(entity =>
            {
                entity.HasKey(x => x.FrequencyId)
                    .HasName("frequencies_pkey");

                entity.Property(x => x.FrequencyId)
                    .UseIdentityByDefaultColumn();

                entity.Property(x => x.Name)
                    .HasMaxLength(255);

                entity.HasIndex(x => x.Name)
                    .IsUnique()
                    .HasDatabaseName("frequencies_frequency_name_key");

                ConfigureExistingAudit(entity, "frequencies");
            });

            modelBuilder.Entity<Income>(entity =>
            {
                entity.HasKey(x => x.IncomeId)
                    .HasName("incomes_pkey");

                entity.Property(x => x.IncomeId)
                    .UseIdentityByDefaultColumn();

                entity.Property(x => x.IncomeSource)
                    .HasMaxLength(255);

                entity.Property(x => x.Amount)
                    .HasPrecision(12, 2);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("incomes_user_id_fkey");

                entity.HasOne(x => x.Currency)
                    .WithMany()
                    .HasForeignKey(x => x.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("incomes_currency_id_fkey");

                entity.HasOne(x => x.Frequency)
                    .WithMany()
                    .HasForeignKey(x => x.FrequencyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("incomes_frequency_id_fkey");

                ConfigureExistingAudit(entity, "incomes");
            });

            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(x => x.ExpenseId)
                    .HasName("expenses_pkey");

                entity.Property(x => x.ExpenseId)
                    .UseIdentityByDefaultColumn();

                entity.Property(x => x.Description)
                    .HasMaxLength(255);

                entity.Property(x => x.Amount)
                    .HasPrecision(12, 2);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("expenses_user_id_fkey");

                entity.HasOne(x => x.Category)
                    .WithMany()
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("expenses_category_id_fkey");

                entity.HasOne(x => x.Currency)
                    .WithMany()
                    .HasForeignKey(x => x.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("expenses_currency_id_fkey");

                entity.HasOne(x => x.Frequency)
                    .WithMany()
                    .HasForeignKey(x => x.FrequencyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("expenses_frequency_id_fkey");

                ConfigureExistingAudit(entity, "expenses");
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Currency)
                    .WithMany()
                    .HasForeignKey(x => x.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.UserId);
                entity.HasIndex(x => x.CurrencyId);
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasOne(x => x.Account)
                    .WithMany()
                    .HasForeignKey(x => x.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Category)
                    .WithMany()
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new
                {
                    x.AccountId,
                    x.TransactionDate,
                    x.Sequence
                });

                entity.HasIndex(x => x.CategoryId);
            });
        }

        private static void ConfigureExistingAudit<TEntity>(
            EntityTypeBuilder<TEntity> entity,
            string tableName)
            where TEntity : class
        {
            // Preserve clock values without assigning a timezone
            // to historical values whose timezone is unknown.
            var timestampConverter = new ValueConverter<DateTime, DateTime>(
                value => DateTime.SpecifyKind(
                    value, DateTimeKind.Unspecified),
                value => DateTime.SpecifyKind(
                    value, DateTimeKind.Unspecified));

            entity.Property<DateTime?>("CreatedAt")
                .HasColumnType("timestamp without time zone")
                .HasConversion(timestampConverter)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired(false);

            entity.Property<DateTime?>("UpdatedAt")
                .HasColumnType("timestamp without time zone")
                .HasConversion(timestampConverter)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired(false);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey("CreatedBy")
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName($"{tableName}_created_by_fkey");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey("UpdatedBy")
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName($"{tableName}_updated_by_fkey");
        }
    }
}