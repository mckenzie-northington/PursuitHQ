using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Saves uploads to S3-compatible object storage. Written for Cloudflare R2,
    /// but anything that speaks the S3 API works.
    ///
    /// This exists because local disk does not survive a restart on the hosts
    /// PursuitHQ deploys to. Messages live in the database and come back; their
    /// attachments would not, leaving broken cards in a thread that otherwise
    /// looks intact, which reads as data loss because it is.
    ///
    /// The stored key format is identical to the local implementation's - two
    /// hex characters, a slash, then a GUID and the extension - so rows written
    /// by one work unchanged with the other. Only the bytes need moving.
    /// </summary>
    public class S3FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _client;
        private readonly string _bucket;
        private readonly ILogger<S3FileStorageService> _logger;

        public S3FileStorageService(
            IOptions<FileStorageOptions> options,
            ILogger<S3FileStorageService> logger)
        {
            var settings = options.Value;
            _bucket = settings.Bucket;
            _logger = logger;

            var config = new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl,

                // R2 addresses buckets by path, not as a subdomain of the host.
                ForcePathStyle = true,

                // R2 has no regions, but the SDK still needs one to build a
                // signature. "auto" is what Cloudflare's own documentation uses.
                AuthenticationRegion = "auto"
            };

            _client = new AmazonS3Client(settings.AccessKeyId, settings.SecretAccessKey, config);
        }

        public async Task<string?> CheckAsync(CancellationToken ct = default)
        {
            // Two probes on purpose, because they fail differently and the
            // difference is the whole diagnosis.
            //
            // Listing needs only read permission. Writing needs write. If
            // listing works and writing does not, the credentials are right and
            // the token is read-only. If neither works, the credentials
            // themselves are wrong - a mistyped secret, or the token value
            // pasted in place of the access key id. Those two problems look
            // identical from the outside and have nothing in common.
            try
            {
                await _client.ListObjectsV2Async(
                    new ListObjectsV2Request { BucketName = _bucket, MaxKeys = 1 }, ct);
            }
            catch (Exception ex)
            {
                return $"cannot read bucket \"{_bucket}\" - {ex.GetType().Name}: {ex.Message}";
            }

            var key = $".write-check-{Guid.NewGuid():N}";

            try
            {
                await _client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    ContentBody = "ok",
                    DisablePayloadSigning = true,
                    DisableDefaultChecksumValidation = true
                }, ct);
            }
            catch (Exception ex)
            {
                return $"can read bucket \"{_bucket}\" but cannot write to it - "
                    + $"{ex.GetType().Name}: {ex.Message}";
            }

            // Tidy up. A failure here is not worth reporting: the object is 2
            // bytes and the thing being checked already passed.
            try
            {
                await _client.DeleteObjectAsync(
                    new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not remove the storage write-check object.");
            }

            return null;
        }

        public async Task<string> SaveAsync(
            Stream content, string originalFileName, CancellationToken ct = default)
        {
            // Same rule as local storage: the client's file name never becomes
            // the key. It could contain path characters, or collide with
            // somebody else's upload.
            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            var storedName = $"{Guid.NewGuid():N}{extension}";
            var key = $"{storedName[..2]}/{storedName}";

            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,

                // Deliberately not set from the upload: content type is a claim
                // the uploader made, and nothing here should treat it as fact.
                // Downloads set their own headers from the database row.
                ContentType = "application/octet-stream",

                // Both required by Cloudflare R2, and both named in Cloudflare's
                // own AWS SDK for .NET documentation.
                //
                // Version 4 of the SDK streams uploads using AWS's Streaming
                // SigV4 and attaches a trailing checksum by default. R2 does not
                // implement either, so it rejects every upload - silently
                // enough, from the caller's side, that it looks like the file
                // simply did not save. This broke every upload path at once the
                // moment object storage was switched on: study materials,
                // message attachments, profile photos and group photos.
                //
                // Nothing is given up by turning them off. The transport is
                // still HTTPS, and the request is still signed - it is the
                // payload-streaming variant of the signature that R2 cannot
                // read.
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            }, ct);

            return key;
        }

        public async Task<Stream> OpenAsync(string storedPath, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _bucket,
                    Key = storedPath
                }, ct);

                // Copied out and the response closed, rather than handing back a
                // stream still tied to a live HTTP connection. The callers here
                // read a file and then do other awaited work before disposing,
                // and a held-open S3 response exhausts the connection pool under
                // any real traffic.
                var buffer = new MemoryStream();

                using (response)
                using (var source = response.ResponseStream)
                {
                    await source.CopyToAsync(buffer, ct);
                }

                buffer.Position = 0;
                return buffer;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Matched to the local implementation so callers need only one
                // catch, whichever storage is configured.
                throw new FileNotFoundException("Stored file is missing.", storedPath);
            }
        }

        public async Task DeleteAsync(string storedPath, CancellationToken ct = default)
        {
            try
            {
                await _client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _bucket,
                    Key = storedPath
                }, ct);
            }
            catch (Exception ex)
            {
                // A file that will not delete must not fail the request. The
                // database row is what matters; log the orphan and move on.
                _logger.LogWarning(ex, "Could not delete stored object {StoredPath}", storedPath);
            }
        }
    }
}
