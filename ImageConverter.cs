using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.Text;

namespace ImageConverterLibrary;

/// <summary>
/// Implementation of TIFF to JPEG converter with AWS S3 support
/// This class implements the IImageConverter interface for OutSystems ODC External Logic
/// </summary>
public class ImageConverter : IImageConverter
{
    /// <summary>
    /// Tests the connection to AWS S3 using provided credentials
    /// </summary>
    /// <param name="awsAccessKey">AWS Access Key ID</param>
    /// <param name="awsSecretKey">AWS Secret Access Key</param>
    /// <param name="awsRegion">AWS Region (default: us-east-1)</param>
    /// <returns>Formatted string with connection result and bucket list</returns>
    public string TestS3Connection(string awsAccessKey, string awsSecretKey, string awsRegion = "us-east-1")
    {
        try
        {
            // Create AWS credentials from provided access key and secret key
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);

            // Get the AWS region endpoint
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);

            // Create S3 client with explicit credentials and region
            using var s3Client = new AmazonS3Client(credentials, regionEndpoint);

            // List all accessible S3 buckets
            ListBucketsResponse response = s3Client.ListBucketsAsync().GetAwaiter().GetResult();

            int bucketCount = response.Buckets?.Count ?? 0;

            // Build formatted response string with bucket list
            var result = new StringBuilder();
            result.AppendLine("✅ Connected to AWS Account");
            result.AppendLine($"Region: {awsRegion}");
            result.AppendLine($"Buckets found: {bucketCount}");

            if (bucketCount > 0)
            {
                foreach (var bucket in response.Buckets)
                {
                    result.AppendLine($"- {bucket.BucketName}");
                }
            }

            return result.ToString().TrimEnd();
        }
        catch (AmazonS3Exception s3Ex)
        {
            // S3-specific errors (permissions, bucket access, etc.)
            return $"❌ S3 Error: {s3Ex.Message} (ErrorCode: {s3Ex.ErrorCode})";
        }
        catch (Exception ex)
        {
            // General errors (credentials, network, etc.)
            return $"❌ Connection Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Converts a TIFF file to JPEG format
    /// TODO: Implement TIFF to JPEG conversion logic
    /// </summary>
    /// <param name="inputPath">Path to input TIFF file</param>
    /// <param name="outputPath">Path for output JPEG file</param>
    /// <param name="quality">JPEG quality (1-100)</param>
    /// <returns>Conversion operation result</returns>
    public ConversionResult ConvertTiffToJpeg(string inputPath, string outputPath, int quality = 85)
    {
        // Placeholder implementation - to be completed in future iteration
        return new ConversionResult
        {
            Success = false,
            Message = "ConvertTiffToJpeg not yet implemented",
            OutputPath = string.Empty,
            PagesConverted = 0
        };
    }

    /// <summary>
    /// Gets the current server timestamp for testing
    /// </summary>
    /// <returns>Current UTC timestamp</returns>
    public string GetCurrentTimestamp()
    {
        return $"Server time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}";
    }

    /// <summary>
    /// Echoes a test message with timestamp and server info
    /// </summary>
    /// <param name="message">Message to echo</param>
    /// <returns>Echoed message with timestamp and environment info</returns>
    public string EchoMessage(string message)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        return $"Echo: {message} | Timestamp: {timestamp} | Environment: {environment}";
    }

    /// <summary>
    /// Gets the build version and metadata for this library
    /// This changes with every build to force unique revisions in ODC
    /// </summary>
    /// <returns>Build version information</returns>
    public string GetBuildVersion()
    {
        // This line is updated by CI/CD on every build
        var buildMetadata = "BUILD_METADATA_PLACEHOLDER";
        return $"ImageConverter Build Version | {buildMetadata}";
    }
}
