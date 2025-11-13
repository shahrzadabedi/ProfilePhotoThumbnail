using Minio;

namespace GenerateProfilePhotoThumbnail.Application
{

    public interface IMinioService
    {
        IMinioClient Client { get; }
    }
}
