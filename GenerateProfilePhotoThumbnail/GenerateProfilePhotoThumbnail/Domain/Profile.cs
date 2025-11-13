namespace GenerateProfilePhotoThumbnail.Domain
{
    public class Profile
    {
        public Guid Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Address { get; set; }
        public string? TempPictureName { get; private set; }
        public bool HasThumbnail { get; set; }= false;
        public string? ThumbnailName { get; set; }
        public required string Mobile { get; set; }
        public string? Email { get; set; }
        public DateTime CreateDateTime { get; set; }= DateTime.Now;

        public void SetTempPicture(string tempPictureName)
        {
            TempPictureName = tempPictureName;
            HasThumbnail = false;
        }
    }
}
