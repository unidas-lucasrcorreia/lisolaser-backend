namespace LisoLaser.Backend.Configuration
{
    public class UnobjectOptions
    {
        public const string SectionName = "LisoLaser:Unobject"; // <-- ajuste aqui
        public string BaseUrl { get; set; } = string.Empty;
        public string PublicToken { get; set; } = string.Empty;
    }
}
