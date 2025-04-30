using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FancyFinanceWebAPI.Shared.Currency
{
    [Table("currencies")]
    public class Currency
    {
        [Key]
        [Column("currency_id")]
        public int CurrencyId { get; set; }

        [Required]
        [Column("currency_name")]
        public string CurrencyName { get; set; } = string.Empty;

        [Required]
        [Column("iso_code")]
        [StringLength(3)]
        public string IsoCode { get; set; } = string.Empty;

        [Required]
        [Column("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        [Column("decimal_precision")]
        public short DecimalPrecision { get; set; }

        [Required]
        [Column("numeric_code")]
        public short NumericCode { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; }
    }
}
