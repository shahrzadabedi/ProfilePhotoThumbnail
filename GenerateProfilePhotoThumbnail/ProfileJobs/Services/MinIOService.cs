namespace ProfileJobs.Services;

using Minio;

public interface IMinioService
{
    IMinioClient Client { get; }
}

public class MinioService : IMinioService
{
    public IMinioClient Client { get; }

    public MinioService(IConfiguration configuration)
    {
        var endpoint = configuration["Minio:Endpoint"];
        var accessKey = configuration["Minio:AccessKey"];
        var secretKey = configuration["Minio:SecretKey"];

        Client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .Build();
    }
}

