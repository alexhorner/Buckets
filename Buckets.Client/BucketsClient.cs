using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Buckets.Client.Exceptions;
using Buckets.Common;
using Newtonsoft.Json;

namespace Buckets.Client
{
    /// <summary>
    /// A client for interacting with bucket servers
    /// </summary>
    public class BucketsClient
    {
        private readonly HttpClient _client;
        private readonly bool _tokenAvailable;
        private readonly Dictionary<string, bool> _authenticationRequirements;
        
        /// <summary>
        /// Instantiate a new bucket server client
        /// </summary>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public BucketsClient(string baseUrl, string? token = null)
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") //Force trailing /
            };

            if (token != null)
            {
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _tokenAvailable = true;
            }
            
            HttpResponseMessage response = _client.GetAsync("System/AuthenticationRequirements").GetAwaiter().GetResult();

            response.EnsureSuccessStatusCode();

            _authenticationRequirements = JsonConvert.DeserializeObject<Dictionary<string, bool>>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
        }
        
        /// <summary>
        /// Get a list of buckets available on the bucket server
        /// </summary>
        /// <returns>A list of buckets available on the bucket server</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public async Task<string[]> BucketListAsync()
        {
            if (!_tokenAvailable && _authenticationRequirements["BucketList"]) throw new NotAuthorizedException();

            HttpResponseMessage? response = await _client.GetAsync($"Bucket/List");

            switch (response.StatusCode)
            {
                case HttpStatusCode.Forbidden:
                    throw new NotAuthorizedException();
                
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return JsonConvert.DeserializeObject<string[]>(await response.Content.ReadAsStringAsync())!;
        }
        
        /// <summary>
        /// Get a list of objects in a bucket
        /// </summary>
        /// <returns>A list of objects available in the bucket or null if the bucket does not exist</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public async Task<string[]?> ObjectListAsync(string bucket)
        {
            if (!_tokenAvailable && _authenticationRequirements["BucketList"]) throw new NotAuthorizedException();

            HttpResponseMessage? response = await _client.GetAsync($"Bucket/{WebUtility.UrlEncode(bucket)}/List");

            switch (response.StatusCode)
            {
                case HttpStatusCode.Forbidden:
                    throw new NotAuthorizedException();
                
                case HttpStatusCode.NotFound:
                    return null;
                
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return JsonConvert.DeserializeObject<string[]>(await response.Content.ReadAsStringAsync())!;
        }
        
        // /// <summary>
        // /// Get an object's metadata from a bucket
        // /// </summary>
        // /// <param name="bucket">The bucket in which the object is stored</param>
        // /// <param name="id">The ID of the object</param>
        // /// <returns>The requested object or null if it cannot be found</returns>
        // /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        // /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        // public async Task<BucketObjectMetadataSized?> GetObjectMetadataAsync(string bucket, string id)
        // {
        //     if (!_tokenAvailable && _authenticationRequirements["ObjectRead"]) throw new NotAuthorizedException();
        //
        //     HttpResponseMessage? response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"Bucket/{WebUtility.UrlEncode(bucket)}/{WebUtility.UrlEncode(id)}"));
        //
        //     switch (response.StatusCode)
        //     {
        //         case HttpStatusCode.Forbidden:
        //             throw new NotAuthorizedException();
        //         
        //         case HttpStatusCode.NotFound:
        //             return null;
        //         
        //         default:
        //             response.EnsureSuccessStatusCode();
        //             break;
        //     }
        //
        //     return new BucketObjectMetadataSized
        //     {
        //         Id = response.Headers.TryGetValues("X-Buckets-Object-ID", out IEnumerable<string> idHeaderValue) ? idHeaderValue.First() : id,
        //         Name = response.Headers.TryGetValues("X-Buckets-Object-Name", out IEnumerable<string> nameHeaderValue) ? nameHeaderValue.First() : id,
        //         MimeType = response.Content.Headers.ContentType.ToString() ?? "application/octet-stream",
        //         Bucket = response.Headers.TryGetValues("X-Buckets-Bucket-Name", out IEnumerable<string> bucketHeaderValue) ? bucketHeaderValue.First() : bucket,
        //         DataSize = response.Content.Headers.ContentLength ?? 0
        //     };
        // }

        /// <summary>
        /// Get an object from a bucket
        /// </summary>
        /// <param name="bucket">The bucket in which the object is stored</param>
        /// <param name="id">The ID of the object</param>
        /// <returns>The requested object or null if it cannot be found</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public async Task<BucketObject?> GetObjectAsync(string bucket, string id)
        {
            if (!_tokenAvailable && _authenticationRequirements["ObjectRead"]) throw new NotAuthorizedException();

            HttpResponseMessage? response = await _client.GetAsync($"Bucket/{WebUtility.UrlEncode(bucket)}/{WebUtility.UrlEncode(id)}");

            switch (response.StatusCode)
            {
                case HttpStatusCode.Forbidden:
                    throw new NotAuthorizedException();
                
                case HttpStatusCode.NotFound:
                    return null;
                
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return new BucketObject
            {
                Id = response.Headers.TryGetValues("X-Buckets-Object-ID", out IEnumerable<string> idHeaderValue) ? idHeaderValue.First() : id,
                Name = response.Headers.TryGetValues("X-Buckets-Object-Name", out IEnumerable<string> nameHeaderValue) ? nameHeaderValue.First() : id,
                MimeType = response.Content.Headers.ContentType.ToString() ?? "application/octet-stream",
                Bucket = response.Headers.TryGetValues("X-Buckets-Bucket-Name", out IEnumerable<string> bucketHeaderValue) ? bucketHeaderValue.First() : bucket,
                Data = await response.Content.ReadAsByteArrayAsync()
            };
        }

        /// <summary>
        /// Put an object into a bucket
        /// </summary>
        /// <param name="bucket">The bucket in which the object will be stored</param>
        /// <param name="data">The data of the object</param>
        /// <param name="mimeType">The object's MIME type</param>
        /// <param name="name">The object's name</param>
        /// <returns>The metadata of the newly created object</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public Task<BucketObjectMetadata> CreateObjectAsync(string bucket, byte[] data, string name, string mimeType = "application/octet-stream")
        {
            MultipartFormDataContent content = new();
            content.Add(new ByteArrayContent(data), "file", name);
            
            return InternalCreateObject(content, bucket, mimeType, name);
        }
        
        /// <inheritdoc cref="CreateObjectAsync(string,byte[],string,string)" />
        public Task<BucketObjectMetadata> CreateObjectAsync(string bucket, Stream data, string name, string mimeType = "application/octet-stream")
        {
            MultipartFormDataContent content = new();
            content.Add(new StreamContent(data), "file", name);
            
            return InternalCreateObject(content, bucket, mimeType, name);
        }

        /// <summary>
        /// Remove an object from a bucket
        /// </summary>
        /// <param name="bucket">The bucket in which the object will be stored</param>
        /// <param name="id">The ID of the object</param>
        /// <returns>Whether the object existed to be deleted</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public async Task<bool> DeleteObjectAsync(string bucket, string id)
        {
            if (!_tokenAvailable && _authenticationRequirements["ObjectDelete"]) throw new NotAuthorizedException();

            HttpResponseMessage? response = await _client.DeleteAsync($"Bucket/{WebUtility.UrlEncode(bucket)}/{WebUtility.UrlEncode(id)}");

            switch (response.StatusCode)
            {
                case HttpStatusCode.Forbidden:
                    throw new NotAuthorizedException();
                
                case HttpStatusCode.NotFound:
                    return false;
                
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return true;
        }
        
        // Shared implementation for object creation regardless of whether a Stream or byte array is passed
        private async Task<BucketObjectMetadata> InternalCreateObject(MultipartFormDataContent content, string bucket, string mimeType, string name)
        {
            if (!_tokenAvailable && _authenticationRequirements["ObjectCreate"]) throw new NotAuthorizedException();

            HttpResponseMessage response = await _client.PutAsync($"Bucket/{WebUtility.UrlEncode(bucket)}?mimeOverride={WebUtility.UrlEncode(mimeType)}&nameOverride={WebUtility.UrlEncode(name)}", content);

            switch (response.StatusCode)
            {
                case HttpStatusCode.Forbidden:
                    throw new NotAuthorizedException();
                
                default:
                    response.EnsureSuccessStatusCode();
                    break;
            }

            return new BucketObjectMetadata
            {
                Id = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync())!,
                Name = name,
                MimeType = mimeType,
                Bucket = bucket
            };
        }
    }
}