using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Buckets.Common;
using Buckets.Web.Attributes;
using Buckets.Web.Bucketing;
using Buckets.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Buckets.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BucketController : ControllerBase
    {
        private readonly BucketStore _bucketStore;

        public BucketController(BucketStore bucketStore)
        {
            _bucketStore = bucketStore;
        }
        
        /// <summary>
        /// Get the metadata of an object and check its existence
        /// </summary>
        /// <param name="bucket">The bucket in which the object is stored</param>
        /// <param name="id">The object ID</param>
        /// <remarks>Checks the ObjectRead authentication requirement</remarks>
        [HttpHead("{bucket}/{id}")]
        [AuthenticationCheck("ObjectRead")]
        [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        public IActionResult Head([FromRoute] string bucket, [FromRoute] string id)
        {
            BucketObjectMetadata? metadata;
            long objectSize;

            try
            {
                metadata = _bucketStore.GetObjectMetadata(bucket, id, out long objectSizeInternal);
                objectSize = objectSizeInternal;
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                return BadRequest(new BasicResponse
                {
                    Message = argumentOutOfRangeException.Message,
                    Error = true
                });
            }
            catch (Exception)
            {
                return StatusCode(500);
            }

            if (metadata == null) return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "The specified object could not be found",
                Instance = HttpContext.Request.Path
            });
                
            Response.Headers.Add(new KeyValuePair<string, StringValues>("X-Buckets-Bucket-Name", metadata.Bucket));
            Response.Headers.Add(new KeyValuePair<string, StringValues>("X-Buckets-Object-Name", metadata.Name));
            Response.Headers.Add(new KeyValuePair<string, StringValues>("X-Buckets-Object-ID", metadata.Id));
            Response.Headers.Add(new KeyValuePair<string, StringValues>("Content-Type", metadata.MimeType));
            Response.Headers.Add(new KeyValuePair<string, StringValues>("Content-Length", objectSize.ToString()));
            
            return NoContent();
        }
        
        /// <summary>
        /// Get an object and its metadata
        /// </summary>
        /// <param name="bucket">The bucket in which the object is stored</param>
        /// <param name="id">The object ID</param>
        /// <remarks>Checks the ObjectRead authentication requirement</remarks>
        [HttpGet("{bucket}/{id}")]
        [AuthenticationCheck("ObjectRead")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        public IActionResult Get([FromRoute] string bucket, [FromRoute] string id)
        {
            BucketObject? metadataAndData;

            try
            {
                metadataAndData = _bucketStore.GetObject(bucket, id, out long _);
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                return BadRequest(new BasicResponse
                {
                    Message = argumentOutOfRangeException.Message,
                    Error = true
                });
            }
            catch (Exception)
            {
                return StatusCode(500);
            }

            if (metadataAndData == null) return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "The specified object could not be found",
                Instance = HttpContext.Request.Path
            });
                
            Response.Headers.Add(new KeyValuePair<string, StringValues>("X-Buckets-Bucket-Name", metadataAndData.Bucket));
            Response.Headers.Add(new KeyValuePair<string, StringValues>("X-Buckets-Object-Name", metadataAndData.Name));
            Response.Headers.Add(new KeyValuePair<string, StringValues>("X-Buckets-Object-ID", metadataAndData.Id));
            
            return new FileContentResult(metadataAndData.Data, metadataAndData.MimeType);
        }

        /// <summary>
        /// Create a new object
        /// </summary>
        /// <param name="bucket">The bucket in which to store the object</param>
        /// <param name="file">The file for this object to store in the bucket</param>
        /// <param name="nameOverride">Override the file's name</param>
        /// <param name="mimeOverride">Override the file's mimetype</param>
        /// <remarks>Checks the ObjectCreate authentication requirement</remarks>
        [HttpPut("{bucket}")]
        [AuthenticationCheck("ObjectCreate")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Put([FromRoute] string bucket, [Required] IFormFile file, [FromQuery] string? nameOverride, [FromQuery] string? mimeOverride)
        {
            Stream fileStream = file.OpenReadStream();
            MemoryStream fileMemoryStream = new();

            await fileStream.CopyToAsync(fileMemoryStream);
            await fileStream.DisposeAsync();

            string objectId;

            try
            {
                objectId = _bucketStore.CreateObject(new BucketObject
                {
                    MimeType = string.IsNullOrWhiteSpace(mimeOverride) ? (string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType) : mimeOverride,
                    Name = string.IsNullOrWhiteSpace(nameOverride) ? (string.IsNullOrWhiteSpace(file.Name) ? Guid.NewGuid().ToString() : file.Name) : nameOverride,
                    Bucket = bucket,
                    Data = fileMemoryStream.ToArray()
                });
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                return BadRequest(new BasicResponse
                {
                    Message = argumentOutOfRangeException.Message,
                    Error = true
                });
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
            finally
            {
                await fileMemoryStream.DisposeAsync();   
            }

            return new JsonResult(objectId);
        }
        
        /// <summary>
        /// Delete an object
        /// </summary>
        /// <param name="bucket">The bucket in which the object is stored</param>
        /// <param name="id">The object ID</param>
        /// <remarks>Checks the ObjectDelete authentication requirement</remarks>
        [HttpDelete("{bucket}/{id}")]
        [AuthenticationCheck("ObjectDelete")]
        [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        public IActionResult Delete([FromRoute] string bucket, [FromRoute] string id)
        {
            try
            {
                _bucketStore.DeleteObject(bucket, id);
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                return BadRequest(new BasicResponse
                {
                    Message = argumentOutOfRangeException.Message,
                    Error = true
                });
            }
            catch (FileNotFoundException)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "The specified object could not be found",
                    Instance = HttpContext.Request.Path
                });
            }
            catch (Exception)
            {
                return StatusCode(500);
            }

            return Ok();
        }
        
        /// <summary>
        /// Get a list of buckets
        /// </summary>
        /// <remarks>Checks the BucketList authentication requirement</remarks>
        [HttpGet("[action]")]
        [AuthenticationCheck("BucketList")]
        [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
        public IActionResult List()
        {
            try
            {
                return new JsonResult(_bucketStore.ListBuckets());
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
        
        /// <summary>
        /// Get a list of objects in a bucket
        /// </summary>
        /// <param name="bucket">The bucket in which objects are stored</param>
        /// <remarks>Checks the ObjectList authentication requirement</remarks>
        [HttpGet("{bucket}/[action]")]
        [AuthenticationCheck("ObjectList")]
        [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
        public IActionResult List([FromRoute] string bucket)
        {
            try
            {
                return new JsonResult(_bucketStore.ListBucketObjects(bucket));
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                return BadRequest(new BasicResponse
                {
                    Message = argumentOutOfRangeException.Message,
                    Error = true
                });
            }
            catch (FileNotFoundException)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "The specified object could not be found",
                    Instance = HttpContext.Request.Path
                });
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
    }
}