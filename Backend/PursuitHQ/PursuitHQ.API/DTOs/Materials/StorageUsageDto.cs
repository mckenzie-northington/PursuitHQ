namespace PursuitHQ.API.DTOs.Materials
{
    public class StorageUsageDto
    {
        public long UsedBytes { get; set; }
        public long QuotaBytes { get; set; }
        public long RemainingBytes => Math.Max(0, QuotaBytes - UsedBytes);
        public int FileCount { get; set; }
    }
}
