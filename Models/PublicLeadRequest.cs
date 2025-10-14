using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LisoLaser.Backend.Models;

public sealed class PublicLeadRequest
{
    [Required, JsonPropertyName("franchiseId")] public int FranchiseId { get; init; }
    [Required, JsonPropertyName("name")]        public string Name { get; init; } = string.Empty;
    [Required, JsonPropertyName("cellPhone")]   public string CellPhone { get; init; } = string.Empty;

    [JsonPropertyName("email")]            public string? Email { get; init; }
    [JsonPropertyName("rating")]           public int? Rating { get; init; }
    [JsonPropertyName("observation")]      public string? Observation { get; init; }
    [JsonPropertyName("origin")]           public string? Origin { get; init; }
    [JsonPropertyName("campaignSlug")]     public string? CampaignSlug { get; init; }
    [JsonPropertyName("adCampaignName")]   public string? AdCampaignName { get; init; }
    [JsonPropertyName("adSetName")]        public string? AdSetName { get; init; }
    [JsonPropertyName("adName")]           public string? AdName { get; init; }
    [JsonPropertyName("facebookSourceId")] public string? FacebookSourceId { get; init; }
    [JsonPropertyName("facebookWaclId")]   public string? FacebookWaclId { get; init; }
    [JsonPropertyName("recentCheckDays")]  public int? RecentCheckDays { get; init; }
    [JsonPropertyName("bot")]              public bool? Bot { get; init; }
}
