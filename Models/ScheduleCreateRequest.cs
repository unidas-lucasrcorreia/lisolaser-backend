using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LisoLaser.Backend.Models;

public sealed class ScheduleCreateRequest 
{
    public string Date { get; set; } = default!;
    public int FranchiseIdentifier { get; set; }
    public string Hour { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string CellPhone { get; set; } = default!;
    public int RoomId { get; set; }
    public string? Email { get; set; }

    public int? DealActivityId { get; set; } 
}