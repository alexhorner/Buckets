using System;
using System.IO;
using System.Linq;
using Buckets.Common;
using Newtonsoft.Json;

namespace Buckets.Web.Bucketing
{
    /// <summary>
    /// A store for processing and accessing bucket and object data at the specified bucket path
    /// </summary>
    public class BucketStore
    {
        private readonly string _bucketStoragePath;
        
        /// <summary>
        /// Instantiate a new bucket store for accessing buckets at the specified path
        /// </summary>
        /// <param name="bucketStoragePath">The path where buckets are stored</param>
        public BucketStore(string bucketStoragePath)
        {
            _bucketStoragePath = bucketStoragePath;
        }
        
        /// <summary>
        /// Get an object from the specified bucket
        /// </summary>
        /// <param name="bucket">The bucket name</param>
        /// <param name="id">The object ID</param>
        /// <param name="objectSize">The size, in bytes, of the object data</param>
        /// <returns>The object, or null if the object cannot be found</returns>
        /// <exception cref="ArgumentOutOfRangeException">A parameter is not valid</exception>
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

        /// <summary>
        /// Get an object's metadata from the specified bucket
        /// </summary>
        /// <param name="bucket">The bucket name</param>
        /// <param name="id">The object ID</param>
        /// <param name="objectSize">The size, in bytes, of the object data</param>
        /// <returns>The object's metadata, or null if the object cannot be found</returns>
        /// <exception cref="ArgumentOutOfRangeException">A parameter is not valid</exception>
        public BucketObjectMetadata? GetObjectMetadata(string bucket, string id, out long objectSize)
        {
            if (string.IsNullOrWhiteSpace(bucket)) throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket name may not be null or whitespace");
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentOutOfRangeException(nameof(id), "The object id may not be null or whitespace");

            id = id.Replace("..", ""); //. is a valid file char but .. could cause a directory traversal
            string newBucketName = bucket.Replace("..", ""); //. is a valid file char but .. could cause a directory traversal

            if (bucket != newBucketName) throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket name contains invalid characters"); //Should be the same if bucket name was valid

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                if (bucket.Contains(invalidChar)) throw new ArgumentOutOfRangeException(nameof(bucket), "The bucket name contains invalid characters");
                if (id.Contains(invalidChar)) throw new ArgumentOutOfRangeException(nameof(id), "The object id contains invalid characters");
            }

            objectSize = 0;
            
            if (!File.Exists(Path.Join(_bucketStoragePath, bucket, id + ".json"))) return null;
            if (!File.Exists(Path.Combine(_bucketStoragePath, bucket, id + ".bktobj"))) throw new Exception("Object metadata exists but object does not");

            FileInfo fileInfo = new(Path.Combine(_bucketStoragePath, bucket, id + ".bktobj"));

            objectSize = fileInfo.Length;
            
            return JsonConvert.DeserializeObject<BucketObjectMetadata>(File.ReadAllText(Path.Join(_bucketStoragePath, bucket, id + ".json")));
        }
        
        /// <summary>
        /// Put an object into a bucket
        /// </summary>
        /// <param name="bucketObject">The base object to create, without an object ID</param>
        /// <returns>The object ID created</returns>
        /// <exception cref="ArgumentOutOfRangeException">A parameter is not valid</exception>
        public string CreateObject(BucketObject bucketObject)
        {
            if (string.IsNullOrWhiteSpace(bucketObject.Bucket)) throw new ArgumentOutOfRangeException(nameof(bucketObject.Bucket), "The bucket name may not be null or whitespace");
            if (string.IsNullOrWhiteSpace(bucketObject.MimeType)) throw new ArgumentOutOfRangeException(nameof(bucketObject.MimeType), "The mime type may not be null or whitespace");
            
            string newBucketName = bucketObject.Bucket.Replace("..", ""); //. is a valid file char but .. could cause a directory traversal
            
            if (bucketObject.Bucket != newBucketName) throw new ArgumentOutOfRangeException(nameof(bucketObject.Bucket), "The bucket name contains invalid characters"); //Should be the same if bucket name was valid
            
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                if (bucketObject.Bucket.Contains(invalidChar)) throw new ArgumentOutOfRangeException(nameof(bucketObject.Bucket), "The bucket name contains invalid characters");
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
        
        /// <summary>
        /// Remove an object from a bucket
        /// </summary>
        /// <param name="bucket">The bucket name</param>
        /// <param name="id">The object ID</param>
        /// <exception cref="ArgumentOutOfRangeException">A parameter is not valid</exception>
        /// <exception cref="FileNotFoundException">The specified object cannot be found</exception>
        public void DeleteObject(string bucket, string id)
        {
            BucketObjectMetadata? metadata = GetObjectMetadata(bucket, id, out long _);
            
            if (metadata == null) throw new FileNotFoundException("The specified object could not be found");
            
            File.Delete(Path.Combine(_bucketStoragePath, metadata.Bucket, metadata.Id + ".bktobj"));
            File.Delete(Path.Combine(_bucketStoragePath, metadata.Bucket, metadata.Id + ".json"));

            if (!Directory.GetFiles(Path.Join(_bucketStoragePath, metadata.Bucket)).Any()) Directory.Delete(Path.Join(_bucketStoragePath, metadata.Bucket));
        }
        
        /// <summary>
        /// Get a list of buckets in this <see cref="BucketStore" />
        /// </summary>
        /// <returns>A list of bucket names</returns>
        public string[] ListBuckets()
        {
            return Directory.GetDirectories(_bucketStoragePath).Select(bucket => new DirectoryInfo(bucket).Name).ToArray();
        }
        
        /// <summary>
        /// Get a list of objects in the specified bucket
        /// </summary>
        /// <param name="bucket">The bucket name</param>
        /// <returns>A list of object IDs</returns>
        /// <exception cref="ArgumentOutOfRangeException">A parameter is not valid</exception>
        /// <exception cref="FileNotFoundException">The specified bucket cannot be found</exception>
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