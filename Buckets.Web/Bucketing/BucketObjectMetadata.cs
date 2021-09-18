namespace Buckets.Web.Bucketing
{
    public class BucketObjectMetadata
    {
        public string Id { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Bucket { get; set; } = null!;
    }
}