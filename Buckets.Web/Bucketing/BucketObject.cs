namespace Buckets.Web.Bucketing
{
    public class BucketObject : BucketObjectMetadata
    {
        public byte[] Data { get; set; } = null!;
    }
}