﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FancyFinanceWebAPI.Shared.Frequency
{
    [Table("frequencies")]
    public class Frequency
    {
        [Key]
        [Column("frequency_id")]
        public int FrequencyId { get; set; }

        [Required]
        [Column("frequency_name")]
        public string Name { get; set; } = string.Empty;

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
