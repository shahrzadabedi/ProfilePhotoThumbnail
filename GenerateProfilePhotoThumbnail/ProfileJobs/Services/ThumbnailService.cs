using Hangfire;
using Microsoft.EntityFrameworkCore;
using Minio.DataModel.Args;
using ProfileJobs.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ProfileJobs.Services;

public class ThumbnailService
{
    private readonly IMinioService _minio;
    private readonly IServiceScopeFactory _scopeFactory;

    public ThumbnailService(IMinioService minio, IServiceScopeFactory scopeFactory)
    {
        _minio = minio;
        _scopeFactory = scopeFactory;
    }

    // این متد رو Hangfire صدا میزنه
    public void EnqueueThumbnailJob(Guid profileId, string bucket, string objectName)
    {
        BackgroundJob.Enqueue(() => BuildThumbnail(profileId, bucket, objectName));
    }

    public async Task BuildThumbnail(Guid profileId, string bucket, string objectName)
    {
        // 🔹 dedicated thread برای CPU-bound
        await System.Threading.Tasks.Task.Factory.StartNew(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. فایل اصلی رو از MinIO بخون
            using var ms = new MemoryStream();
            await _minio.Client.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(ms);
                    })
            );
            ms.Position = 0;

            // 2. thumbnail بساز (CPU-bound)
            using var image = SixLabors.ImageSharp.Image.Load(ms);
            image.Mutate(x => x.Resize(150, 150));

            // 3. ذخیره thumbnail روی MinIO
            var thumbBucket = "profile-thumbnails";
            var thumbName = $"{profileId}-thumb.jpg";

            bool thumbBucketExists = await _minio.Client.BucketExistsAsync(new BucketExistsArgs().WithBucket(thumbBucket));
            if (!thumbBucketExists)
                await _minio.Client.MakeBucketAsync(new MakeBucketArgs().WithBucket(thumbBucket));

            await using var thumbStream = new MemoryStream();
            await image.SaveAsJpegAsync(thumbStream);
            thumbStream.Position = 0;

            await _minio.Client.PutObjectAsync(new PutObjectArgs()
                .WithBucket(thumbBucket)
                .WithObject(thumbName)
                .WithStreamData(thumbStream)
                .WithObjectSize(thumbStream.Length)
            );

            // 🔹 برگرد روی ThreadPool برای DB update (I/O-bound)
            await Task.Run(async () =>
            {
                var profile = await db.Profile.FirstOrDefaultAsync(p => p.Id == profileId);
                profile.HasThumbnail = true;
                profile.ThumbnailName = thumbName;
                await db.SaveChangesAsync();
            });

        }, TaskCreationOptions.LongRunning).Unwrap(); // Unwrap چون async delegate داریم
    }
}
