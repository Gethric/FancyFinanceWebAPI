using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FancyFinanceWebAPI.Modules.Accounts;

namespace FancyFinanceWebAPI.Modules.Transactions.Import
{
    [Table("statement_imports")]
    public class StatementImport
    {
        [Key]
        [Column("statement_import_id")]
        public int StatementImportId { get; set; }

        [Column("account_id")]
        public int AccountId { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account? Account { get; set; }

        [Required]
        [MaxLength(64)]
        [Column("file_hash")]
        public string FileHash { get; set; } = string.Empty;

        [Column("statement_date", TypeName = "date")]
        public DateOnly StatementDate { get; set; }

        [Column("imported_transaction_count")]
        public int ImportedTransactionCount { get; set; }

        [Column("imported_at")]
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    }
}