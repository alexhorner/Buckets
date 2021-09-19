namespace Buckets.Common
{
    public class BucketObject : BucketObjectMetadata
    {
        public byte[] Data { get; set; } = null!;
    }
}