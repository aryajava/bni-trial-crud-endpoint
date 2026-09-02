using System.ComponentModel.DataAnnotations;

namespace cobaproject.Dtos;

public class ApprovalDecisionRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Versi tidak valid.")]
    public int Version { get; set; }

    public string? Reason { get; set; }
}