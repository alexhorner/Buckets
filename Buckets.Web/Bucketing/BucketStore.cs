using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Buckets.Web.Bucketing
{
    public class BucketStore
    {
        private readonly string _bucketStoragePath;

        public BucketStore(string bucketStoragePath)
        {
            _bucketStoragePath = bucketStoragePath;
        }
        
        public BucketObject? GetObject(string bucket, string id, out long objectSize)
        {
            BucketObjectMetadata? metadata = GetObjectMetadata(bucket, id, out long objectSizeInternal);

            objectSize = objectSizeInternal;
            
            if (metadata == null) return null;

            return new BucketObject
            {
                Id = metadata.Id,
                MimeType = metadata.MimeType,
                Name = metadata.Name,
                Data = File.ReadAllBytes(Path.Combine(_bucketStoragePath, bucket, id + ".bktobj"))
            };
        }

        public BucketObjectMetadata? GetObjectMetadata(string bucket, string id, out long objectSize)
        {
            if (string.IsNullOrWhiteSpace(bucket)) throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket name may not be null or whitespace");
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentOutOfRangeException(nameof(id), "The object id may not be null or whitespace");
            
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                if (bucket.Contains(invalidChar)) throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket name contains invalid characters");
            }

            objectSize = 0;
            
            if (!File.Exists(Path.Join(_bucketStoragePath, bucket, id + ".json"))) return null;
            if (!File.Exists(Path.Combine(_bucketStoragePath, bucket, id + ".bktobj"))) throw new Exception("Object metadata exists but object does not");

            FileInfo fileInfo = new(Path.Combine(_bucketStoragePath, bucket, id + ".bktobj"));

            objectSize = fileInfo.Length;
            
            return JsonConvert.DeserializeObject<BucketObjectMetadata>(File.ReadAllText(Path.Join(_bucketStoragePath, bucket, id + ".json")));
        }

        public string CreateObject(BucketObject bucketObject)
        {
            if (string.IsNullOrWhiteSpace(bucketObject.Bucket)) throw new Exception("The bucket name may not be null or whitespace");
            if (string.IsNullOrWhiteSpace(bucketObject.MimeType)) throw new Exception("The mime type may not be null or whitespace");

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                if (bucketObject.Bucket.Contains(invalidChar)) throw new Exception("The bucket name contains invalid characters");
            }
            
            Guid id;

            do
            {
                id = Guid.NewGuid();
            } while (File.Exists(Path.Join(_bucketStoragePath, bucketObject.Bucket, id + ".json")) || File.Exists(Path.Combine(_bucketStoragePath, bucketObject.Bucket, id + ".bktobj")));

            if (!Directory.Exists(Path.Join(_bucketStoragePath, bucketObject.Bucket))) Directory.CreateDirectory(Path.Join(_bucketStoragePath, bucketObject.Bucket));

            File.WriteAllText(Path.Join(_bucketStoragePath, bucketObject.Bucket, id + ".json"), JsonConvert.SerializeObject(new BucketObjectMetadata
            {
                Id = id.ToString(),
                Bucket = bucketObject.Bucket,
                Name = bucketObject.Name,
                MimeType = bucketObject.MimeType
            }));
            
            File.WriteAllBytes(Path.Combine(_bucketStoragePath, bucketObject.Bucket, id + ".bktobj"), bucketObject.Data);

            return id.ToString();
        }

        public void DeleteObject(string bucket, string id)
        {
            BucketObjectMetadata? metadata = GetObjectMetadata(bucket, id, out long _);
            
            if (metadata == null) throw new FileNotFoundException("The specified object could not be found");
            
            File.Delete(Path.Combine(_bucketStoragePath, metadata.Bucket, metadata.Id + ".bktobj"));
            File.Delete(Path.Combine(_bucketStoragePath, metadata.Bucket, metadata.Id + ".json"));

            if (!Directory.GetFiles(Path.Join(_bucketStoragePath, metadata.Bucket)).Any()) Directory.Delete(Path.Join(_bucketStoragePath, metadata.Bucket));
        }

        public string[] ListBuckets()
        {
            return Directory.GetDirectories(_bucketStoragePath).Select(bucket => new DirectoryInfo(bucket).Name).ToArray();
        }

        public string[] ListBucketObjects(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket)) throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket name may not be null or whitespace");
            
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                if (bucket.Contains(invalidChar)) throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket name contains invalid characters");
            }

            if (!Directory.Exists(Path.Combine(_bucketStoragePath, bucket))) throw new FileNotFoundException("The specified bucket does not exist");
            
            return Directory.GetFiles(Path.Combine(_bucketStoragePath, bucket), "*.bktobj").Select(obj =>
            {
                
                FileInfo fileInfo = new(obj);
                return fileInfo.Name.Substring(0, fileInfo.Name.Length - 7);
            }).ToArray();
        }
    }
}