namespace LisoLaser.Backend.Services.Unobject
{
    public interface IUnobjectService
    {
        Task<string> GetPublicFranchisesRawAsync(CancellationToken ct = default);
        Task<string> CreateLeadAsync(string jsonBody, CancellationToken ct = default);

        Task<string> GetScheduleHoursAsync(
            int franchiseId,
            string date,
            IDictionary<string, string?>? extraQuery = null,
            CancellationToken ct = default);

        Task<string> CreateScheduleAsync(int franchiseId, string jsonBody, CancellationToken ct = default);
    }
}
