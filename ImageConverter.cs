using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

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
    /// Supports both single-page and multi-page TIFF files
    /// </summary>
    /// <param name="inputPath">Path to input TIFF file</param>
    /// <param name="outputPath">Path for output JPEG file (or base path for multi-page TIFFs)</param>
    /// <param name="quality">JPEG quality (1-100)</param>
    /// <returns>Conversion operation result</returns>
    public ConversionResult ConvertTiffToJpeg(string inputPath, string outputPath, int quality = 85)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(inputPath))
                return new ConversionResult { Success = false, Message = "Input path cannot be empty" };

            if (string.IsNullOrWhiteSpace(outputPath))
                return new ConversionResult { Success = false, Message = "Output path cannot be empty" };

            if (!File.Exists(inputPath))
                return new ConversionResult { Success = false, Message = $"Input file not found: {inputPath}" };

            // Validate quality range
            if (quality < 1 || quality > 100)
                return new ConversionResult { Success = false, Message = "Quality must be between 1 and 100" };

            // Load the TIFF image
            using var image = Image.Load(inputPath);

            int frameCount = image.Frames.Count;
            var outputPaths = new List<string>();

            // Configure JPEG encoder with specified quality
            var jpegEncoder = new JpegEncoder
            {
                Quality = quality
            };

            if (frameCount == 1)
            {
                // Single-page TIFF: save directly to outputPath
                image.Save(outputPath, jpegEncoder);
                outputPaths.Add(outputPath);

                return new ConversionResult
                {
                    Success = true,
                    Message = $"Successfully converted single-page TIFF to JPEG (quality: {quality})",
                    OutputPath = outputPath,
                    PagesConverted = 1
                };
            }
            else
            {
                // Multi-page TIFF: save each frame as separate JPEG
                // Generate output filenames: outputPath_1.jpg, outputPath_2.jpg, etc.
                var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                var extension = ".jpg";

                for (int i = 0; i < frameCount; i++)
                {
                    var frameOutputPath = Path.Combine(directory, $"{fileNameWithoutExt}_page{i + 1}{extension}");

                    // Clone the specific frame and save it
                    using var frameImage = image.Frames.CloneFrame(i);
                    using var singleFrameImage = frameImage.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                    singleFrameImage.Save(frameOutputPath, jpegEncoder);

                    outputPaths.Add(frameOutputPath);
                }

                var firstOutputPath = outputPaths.First();
                var allPaths = string.Join(", ", outputPaths);

                return new ConversionResult
                {
                    Success = true,
                    Message = $"Successfully converted {frameCount}-page TIFF to {frameCount} JPEG files (quality: {quality}). Files: {allPaths}",
                    OutputPath = firstOutputPath,
                    PagesConverted = frameCount
                };
            }
        }
        catch (UnknownImageFormatException ex)
        {
            return new ConversionResult
            {
                Success = false,
                Message = $"Invalid image format: {ex.Message}. Ensure the input file is a valid TIFF.",
                OutputPath = string.Empty,
                PagesConverted = 0
            };
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false,
                Message = $"Conversion error: {ex.Message}",
                OutputPath = string.Empty,
                PagesConverted = 0
            };
        }
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
