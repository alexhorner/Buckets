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
    public class BucketClient
    {
        private readonly HttpClient _client;
        private readonly bool _tokenAvailable;
        private readonly Dictionary<string, bool> _authenticationRequirements;
        
        /// <summary>
        /// Instantiate a new bucket server client
        /// </summary>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public BucketClient(string baseUrl, string? token = null)
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
        public async Task<string[]> BucketList()
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
        public async Task<string[]?> ObjectList(string bucket)
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
        
        /// <summary>
        /// Get an object's metadata from a bucket
        /// </summary>
        /// <param name="bucket">The bucket in which the object is stored</param>
        /// <param name="id">The ID of the object</param>
        /// <returns>The requested object or null if it cannot be found</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public async Task<BucketObjectMetadataSized?> GetObjectMetadata(string bucket, string id)
        {
            if (!_tokenAvailable && _authenticationRequirements["ObjectRead"]) throw new NotAuthorizedException();

            HttpResponseMessage? response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"Bucket/{WebUtility.UrlEncode(bucket)}/{WebUtility.UrlEncode(id)}"));

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

            return new BucketObjectMetadataSized
            {
                Id = response.Headers.GetValues("X-Buckets-Object-ID").FirstOrDefault() ?? id,
                Name = response.Headers.GetValues("X-Buckets-Object-Name").FirstOrDefault() ?? id,
                MimeType = response.Headers.GetValues("Content-Type").FirstOrDefault() ?? "application/octet-stream",
                Bucket = response.Headers.GetValues("X-Buckets-Bucket-Name").FirstOrDefault() ?? bucket,
                DataSize = long.TryParse(response.Headers.GetValues("Content-Length").FirstOrDefault() ?? "0", out long size) ? size : 0
            };
        }

        /// <summary>
        /// Get an object from a bucket
        /// </summary>
        /// <param name="bucket">The bucket in which the object is stored</param>
        /// <param name="id">The ID of the object</param>
        /// <returns>The requested object or null if it cannot be found</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public async Task<BucketObject?> GetObject(string bucket, string id)
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
                Id = response.Headers.GetValues("X-Buckets-Object-ID").FirstOrDefault() ?? id,
                Name = response.Headers.GetValues("X-Buckets-Object-Name").FirstOrDefault() ?? id,
                MimeType = response.Headers.GetValues("Content-Type").FirstOrDefault() ?? "application/octet-stream",
                Bucket = response.Headers.GetValues("X-Buckets-Bucket-Name").FirstOrDefault() ?? bucket,
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
        public Task<BucketObjectMetadata?> CreateObject(string bucket, byte[] data, string mimeType, string name)
        {
            return InternalCreateObject(() => _client.PutAsync($"Bucket/{WebUtility.UrlEncode(bucket)}", new ByteArrayContent(data)), bucket, mimeType, name);
        }
        
        /// <inheritdoc cref="CreateObject(string,byte[],string,string)" />
        public Task<BucketObjectMetadata?> CreateObject(string bucket, Stream data, string mimeType, string name)
        {
            return InternalCreateObject(() => _client.PutAsync($"Bucket/{WebUtility.UrlEncode(bucket)}", new StreamContent(data)), bucket, mimeType, name);
        }

        /// <summary>
        /// Remove an object from a bucket
        /// </summary>
        /// <param name="bucket">The bucket in which the object will be stored</param>
        /// <param name="id">The ID of the object</param>
        /// <returns>Whether the object existed to be deleted</returns>
        /// <exception cref="NotAuthorizedException">An access token has not been provided or is invalid but is required</exception>
        /// <exception cref="HttpRequestException">An error occured connecting to or on the bucket server</exception>
        public async Task<bool> DeleteObject(string bucket, string id)
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
        private async Task<BucketObjectMetadata?> InternalCreateObject(Func<Task<HttpResponseMessage>> httpResponseMessageFunc, string bucket, string mimeType, string name)
        {
            if (!_tokenAvailable && _authenticationRequirements["ObjectCreate"]) throw new NotAuthorizedException();

            HttpResponseMessage response = await httpResponseMessageFunc.Invoke();

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