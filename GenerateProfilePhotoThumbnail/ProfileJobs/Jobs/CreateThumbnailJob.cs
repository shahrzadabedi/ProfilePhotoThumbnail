using System.Data;
using Hangfire;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Minio.DataModel.Args;
using ProfileJobs.Infrastructure.Persistence;
using ProfileJobs.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

using ProfileJobs.Application;

namespace ProfileJobs.Jobs
{
    [DisableConcurrentExecution(3600)]
    public class CreateThumbnailJob
    {
        private readonly IMinioService _minio;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CreateThumbnailJob> _logger;
        private IUnitOfWork _unitOfWork;
        private AppDbContext db;

        public CreateThumbnailJob(IMinioService minio,
            IServiceScopeFactory scopeFactory,
            ILogger<CreateThumbnailJob> logger)
        {
            _minio = minio;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessProfilesWithoutThumbnails(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔍 Checking profiles without thumbnails...");
            
            using var scope = _scopeFactory.CreateScope();
            db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var profileIds = await db.Profile
                .Where(p => !p.HasThumbnail && p.TempPictureName != null)
                .OrderBy(p => p.CreateDateTime)
                .Select(p => p.Id)
                .Take(10)
                .ToListAsync(cancellationToken);

            if (!profileIds.Any())
            {
                _logger.LogInformation("✅ No profiles pending thumbnail generation.");
                return;
            }

            foreach (var profileId in profileIds)
            {
                try
                {
                    _logger.LogInformation($"🧩 Building thumbnail for profile {profileId}");
                    await BuildThumbnailAsync(profileId, db, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Error building thumbnail for profile {profileId}");
                }
            }
        }

        private static bool IsDbUpdateException(Exception ex)
        {
            if (ex is DbUpdateException dbEx && dbEx.InnerException is SqlException sql)
                return true;

            if (ex is InvalidOperationException invEx && invEx.InnerException is DbUpdateException dbEx2 && dbEx2.InnerException is SqlException sql2)
                return true;

            return false;
        }

        private async Task BuildThumbnailAsync(Guid profileId, AppDbContext db, CancellationToken cancellationToken)
        {
            const string thumbBucket = "profiles";

            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var profile = await db.Profile.FirstOrDefaultAsync(p => p.Id == profileId, cancellationToken);

            bool bucketExists = await _minio.Client.BucketExistsAsync(new BucketExistsArgs().WithBucket(thumbBucket));
            if (!bucketExists)
                await _minio.Client.MakeBucketAsync(new MakeBucketArgs().WithBucket(thumbBucket));

            using var originalStream = new MemoryStream();
            await GetFromMinio(originalStream,"profiles", profile.TempPictureName, cancellationToken);
            
            try
            {
                var thumbnailStream = ConvertToThumbnail(originalStream);

                var thumbName = profile.TempPictureName.Split("/").LastOrDefault();

                var thumbsName = $"{profile.Id.ToString()}/thumbnails/{thumbName}";

                await PutOnMinio(thumbBucket,thumbsName, thumbnailStream, cancellationToken);

                //Remove Temp picture
                var bucketName = "profiles";
                var objectName = $"{profile.Id}/uploads/{profile.TempPictureName}";
                await RemoveFromMinio(bucketName, objectName, cancellationToken);

                profile.HasThumbnail = true;
                profile.ThumbnailName = thumbsName;

                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (Exception ex) when (IsDbUpdateException(ex))
            {
                _logger.LogWarning(ex, $"⚠️ DbUpdateException detected while processing profile {profileId}. Skipping...");

                var thumbName = profile.TempPictureName.Split("/").LastOrDefault();
                var thumbsName = $"{profile.Id.ToString()}/thumbnails/{thumbName}";

                await Compensate(thumbBucket, thumbsName, cancellationToken);
            }
        }

        private async Task Compensate(string bucketName, string thumbsName,  CancellationToken cancellationToken)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);

            await RemoveFromMinio(bucketName, thumbsName, cancellationToken);
        }

        private async Task RemoveFromMinio(string bucketName, string objectName, CancellationToken cancellationToken)
        {
            await _minio.Client.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName),
                cancellationToken);
        }

        private MemoryStream ConvertToThumbnail(MemoryStream originalStream)
        {
            using var image = Image.Load(originalStream);
            image.Mutate(x => x.Resize(150, 150));
            var thumbnailStream = new MemoryStream();
            image.SaveAsJpeg(thumbnailStream);
            thumbnailStream.Position = 0;

            return thumbnailStream;
        }

        private async Task PutOnMinio(string bucketName, string objectName, MemoryStream stream, CancellationToken cancellationToken)
        {
            await _minio.Client.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length),
                cancellationToken);
        }

        private async Task GetFromMinio(MemoryStream originalStream, string bucketName, string objectName, CancellationToken cancellationToken)
        {
            await _minio.Client.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(async stream =>
                    {
                        await stream.CopyToAsync(originalStream);
                    })
            );

            originalStream.Position = 0;
        }
    }
}
