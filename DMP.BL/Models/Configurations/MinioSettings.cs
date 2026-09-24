namespace DMP.BL.Models.Configurations;

public class MinioSettings
{
    public const string Position = "Minio";

    public string S3PublicEndpoint { get; set; } = string.Empty;
}
