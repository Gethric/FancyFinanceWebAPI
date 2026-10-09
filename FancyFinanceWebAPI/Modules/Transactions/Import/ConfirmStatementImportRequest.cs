using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FancyFinanceWebAPI.Modules.Transactions.Import
{
    public class ConfirmStatementImportRequest : StatementUploadRequest
    {
        [Required]
        public string PreviewToken { get; set; } = string.Empty;

        // Explicit confirmation of an earlier tracking date and balance.
        public bool AcceptTrackingStartChange { get; set; }

        // Separate acknowledgement of an unverified gap or balance mismatch.
        public bool AcceptContinuityWarning { get; set; }

        // Statement row sequences the user identifies as already recorded.
        public List<int> SkipSequences { get; set; } = new();
    }
}