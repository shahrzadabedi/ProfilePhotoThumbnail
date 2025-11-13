using GenerateProfilePhotoThumbnail.Application;
using GenerateProfilePhotoThumbnail.Domain;
using GenerateProfilePhotoThumbnail.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Minio.DataModel.Args;

namespace GenerateProfilePhotoThumbnail.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly IMinioService _minio;
        private readonly AppDbContext _context;
        public ProfileController(IMinioService minio, AppDbContext context)
        {
            _minio = minio;
            _context   = context;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateProfile(Profile profile, CancellationToken cancellationToken)
        {
            profile.Id = Guid.NewGuid();

            _context.Profile.Add(profile);
            await _context.SaveChangesAsync(cancellationToken);

            return Created("",profile);
        }

        [HttpPost("upload/{userId}")]
        public async Task<IActionResult> Upload(IFormFile file,Guid userId, CancellationToken cancellationToken)
        {
            var profile = await _context.Profile.FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);
            if (profile == null)
            {
                throw new Exception("Profile not found");
            }

            var bucketName = "profiles";
            var objectName = $"{userId}/uploads/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            bool found = await _minio.Client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), cancellationToken);
            if (!found)
            {
                await _minio.Client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName),cancellationToken);
            }

            await using var stream = file.OpenReadStream();
            var response = await _minio.Client.PutObjectAsync(new PutObjectArgs().WithBucket(bucketName).WithObject(objectName).WithStreamData(stream).WithObjectSize(stream.Length), cancellationToken);

            profile.SetTempPicture(response.ObjectName);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok("File uploaded");
        }
    }
}
