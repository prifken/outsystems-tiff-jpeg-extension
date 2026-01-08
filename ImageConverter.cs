using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using ImageMagick;

namespace ImageConverterLibrary;

/// <summary>
/// Implementation of TIFF to JPEG converter with AWS S3 support
/// This class implements the IImageConverter interface for OutSystems ODC External Logic
/// </summary>
public class ImageConverter : IImageConverter
{
    /// <summary>
    /// Converts TIFF file from S3 to PDF or JPEG and saves back to S3
    /// Unified conversion function supporting multiple output formats
    /// </summary>
    public ConversionResult ConvertTiffS3(
        string bucketName,
        string inputS3Key,
        string outputS3Key,
        string awsAccessKey,
        string awsSecretKey,
        string awsRegion = "us-east-1",
        string outputFormat = "PDF",
        int quality = 85,
        bool compressPdf = true)
    {
        var normalizedFormat = outputFormat?.Trim().ToUpperInvariant() ?? "PDF";
        if (normalizedFormat != "PDF" && normalizedFormat != "JPEG")
        {
            return new ConversionResult
            {
                Success = false,
                Message = $"Invalid outputFormat {outputFormat}. Must be 'PDF' or 'JPEG'.",
                DetailedLog = $"Error: outputFormat parameter must be 'PDF' or 'JPEG', received: {outputFormat}"
            };
        }
        if (normalizedFormat == "JPEG")
        {
            return ConvertTiffToJpegS3(bucketName, inputS3Key, outputS3Key, awsAccessKey, awsSecretKey, awsRegion, quality);
        }
        else
        {
            if (compressPdf)
            {
                return ConvertTiffToCompressedPdfS3(bucketName, inputS3Key, outputS3Key, awsAccessKey, awsSecretKey, awsRegion, quality);
            }
            else
            {
                return ConvertTiffToPdfS3(bucketName, inputS3Key, outputS3Key, awsAccessKey, awsSecretKey, awsRegion);
            }
        }
    }

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

            if (bucketCount > 0 && response.Buckets != null)
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
        var log = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            log.AppendLine($"=== ConvertTiffToJpeg Execution Log ===");
            log.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Input Path: {inputPath}");
            log.AppendLine($"Output Path: {outputPath}");
            log.AppendLine($"Quality Parameter: {quality}");
            log.AppendLine();

            // Validate inputs
            log.AppendLine("[STEP 1] Validating inputs...");
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                log.AppendLine("ERROR: Input path is null or empty");
                return new ConversionResult { Success = false, Message = "Input path cannot be empty", DetailedLog = log.ToString() };
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                log.AppendLine("ERROR: Output path is null or empty");
                return new ConversionResult { Success = false, Message = "Output path cannot be empty", DetailedLog = log.ToString() };
            }

            if (!File.Exists(inputPath))
            {
                log.AppendLine($"ERROR: Input file not found at path: {inputPath}");
                return new ConversionResult { Success = false, Message = $"Input file not found: {inputPath}", DetailedLog = log.ToString() };
            }

            var inputFileInfo = new FileInfo(inputPath);
            log.AppendLine($"Input file found");
            log.AppendLine($"Input file size: {inputFileInfo.Length:N0} bytes ({inputFileInfo.Length / 1024.0:F2} KB)");

            // Validate quality range
            if (quality < 1 || quality > 100)
            {
                log.AppendLine($"ERROR: Quality value {quality} is out of range (1-100)");
                return new ConversionResult { Success = false, Message = "Quality must be between 1 and 100", DetailedLog = log.ToString() };
            }

            log.AppendLine("Validation passed");
            log.AppendLine();

            // Load the TIFF image
            log.AppendLine("[STEP 2] Loading TIFF image from file...");
            using var image = Image.Load(inputPath);

            log.AppendLine($"Image loaded successfully");
            log.AppendLine($"Image format: {image.Metadata.DecodedImageFormat?.Name ?? "Unknown"}");
            log.AppendLine($"Dimensions: {image.Width} x {image.Height} pixels");
            log.AppendLine($"Frame count: {image.Frames.Count}");
            log.AppendLine();

            int frameCount = image.Frames.Count;
            var outputPaths = new List<string>();

            // Configure JPEG encoder with specified quality
            log.AppendLine("[STEP 3] Configuring JPEG encoder...");
            var jpegEncoder = new JpegEncoder
            {
                Quality = quality
            };
            log.AppendLine($"JPEG quality set to: {quality}");
            log.AppendLine();

            if (frameCount == 1)
            {
                // Single-page TIFF: save directly to outputPath
                log.AppendLine("[STEP 4] Converting single-page TIFF to JPEG...");
                image.Save(outputPath, jpegEncoder);
                outputPaths.Add(outputPath);

                var outputFileInfo = new FileInfo(outputPath);
                log.AppendLine($"Conversion complete");
                log.AppendLine($"Output file: {outputPath}");
                log.AppendLine($"Output size: {outputFileInfo.Length:N0} bytes ({outputFileInfo.Length / 1024.0:F2} KB)");
                log.AppendLine($"Compression ratio: {(double)inputFileInfo.Length / outputFileInfo.Length:F2}x");
                log.AppendLine();

                var endTime = DateTime.UtcNow;
                log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
                log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
                log.AppendLine($"Status: SUCCESS");

                return new ConversionResult
                {
                    Success = true,
                    Message = $"Successfully converted single-page TIFF to JPEG (quality: {quality})",
                    OutputPath = outputPath,
                    PagesConverted = 1,
                    DetailedLog = log.ToString()
                };
            }
            else
            {
                // Multi-page TIFF: save each frame as separate JPEG
                log.AppendLine($"[STEP 4] Converting multi-page TIFF ({frameCount} pages)...");
                log.AppendLine($"Each page will be saved as a separate JPEG file");
                log.AppendLine();

                // Generate output filenames: outputPath_page1.jpg, outputPath_page2.jpg, etc.
                var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                var extension = ".jpg";

                log.AppendLine($"Output directory: {(string.IsNullOrEmpty(directory) ? "<current directory>" : directory)}");
                log.AppendLine($"Base filename: {fileNameWithoutExt}");
                log.AppendLine();

                long totalOutputSize = 0;

                for (int i = 0; i < frameCount; i++)
                {
                    var frameOutputPath = Path.Combine(directory, $"{fileNameWithoutExt}_page{i + 1}{extension}");
                    log.AppendLine($"Processing page {i + 1}/{frameCount}...");

                    // Clone the specific frame and save it
                    using var frameImage = image.Frames.CloneFrame(i);
                    using var singleFrameImage = frameImage.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                    singleFrameImage.Save(frameOutputPath, jpegEncoder);

                    var frameFileInfo = new FileInfo(frameOutputPath);
                    totalOutputSize += frameFileInfo.Length;
                    log.AppendLine($"  Saved: {Path.GetFileName(frameOutputPath)} ({frameFileInfo.Length:N0} bytes)");

                    outputPaths.Add(frameOutputPath);
                }

                var firstOutputPath = outputPaths.First();
                var allPaths = string.Join(", ", outputPaths.Select(Path.GetFileName));

                log.AppendLine();
                log.AppendLine($"All {frameCount} pages converted successfully");
                log.AppendLine($"Total output size: {totalOutputSize:N0} bytes ({totalOutputSize / 1024.0:F2} KB)");
                log.AppendLine($"Compression ratio: {(double)inputFileInfo.Length / totalOutputSize:F2}x");
                log.AppendLine();

                var endTime = DateTime.UtcNow;
                log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
                log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
                log.AppendLine($"Status: SUCCESS");

                return new ConversionResult
                {
                    Success = true,
                    Message = $"Successfully converted {frameCount}-page TIFF to {frameCount} JPEG files (quality: {quality}). Files: {allPaths}",
                    OutputPath = firstOutputPath,
                    PagesConverted = frameCount,
                    DetailedLog = log.ToString()
                };
            }
        }
        catch (UnknownImageFormatException ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine("=== EXCEPTION: UnknownImageFormatException ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: FAILED - Invalid image format");

            return new ConversionResult
            {
                Success = false,
                Message = $"Invalid image format: {ex.Message}. Ensure the input file is a valid TIFF.",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine($"=== EXCEPTION: {ex.GetType().Name} ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                log.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                log.AppendLine($"Inner Message: {ex.InnerException.Message}");
            }
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: FAILED");

            return new ConversionResult
            {
                Success = false,
                Message = $"Conversion error: {ex.Message}",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
    }

    /// <summary>
    /// Converts TIFF binary data to JPEG binary data
    /// Recommended for small files. For large files, use S3-based conversion.
    /// Multi-page TIFFs: only first page is converted (use S3 method for multi-page support)
    /// </summary>
    /// <param name="tiffData">TIFF file as binary data</param>
    /// <param name="quality">JPEG quality (1-100)</param>
    /// <returns>Conversion result with JPEG data in OutputData field</returns>
    public ConversionResult ConvertTiffToJpegBinary(byte[] tiffData, int quality = 85)
    {
        var log = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            log.AppendLine($"=== ConvertTiffToJpegBinary Execution Log ===");
            log.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Quality Parameter: {quality}");
            log.AppendLine();

            // Validate inputs
            log.AppendLine("[STEP 1] Validating inputs...");
            if (tiffData == null || tiffData.Length == 0)
            {
                log.AppendLine("ERROR: TIFF data is null or empty");
                return new ConversionResult
                {
                    Success = false,
                    Message = "TIFF data cannot be null or empty",
                    OutputData = Array.Empty<byte>(),
                    DetailedLog = log.ToString()
                };
            }

            log.AppendLine($"Input size: {tiffData.Length:N0} bytes ({tiffData.Length / 1024.0:F2} KB)");

            // Validate quality range
            if (quality < 1 || quality > 100)
            {
                log.AppendLine($"ERROR: Quality value {quality} is out of range (1-100)");
                return new ConversionResult
                {
                    Success = false,
                    Message = "Quality must be between 1 and 100",
                    OutputData = Array.Empty<byte>(),
                    DetailedLog = log.ToString()
                };
            }

            log.AppendLine("Validation passed");
            log.AppendLine();

            // Load TIFF from byte array
            log.AppendLine("[STEP 2] Loading TIFF image from memory stream...");
            using var inputStream = new MemoryStream(tiffData);
            using var image = Image.Load(inputStream);

            log.AppendLine($"Image loaded successfully");
            log.AppendLine($"Image format: {image.Metadata.DecodedImageFormat?.Name ?? "Unknown"}");
            log.AppendLine($"Dimensions: {image.Width} x {image.Height} pixels");
            log.AppendLine($"Frame count: {image.Frames.Count}");
            log.AppendLine();

            int frameCount = image.Frames.Count;

            // Configure JPEG encoder with specified quality
            log.AppendLine("[STEP 3] Configuring JPEG encoder...");
            var jpegEncoder = new JpegEncoder
            {
                Quality = quality
            };
            log.AppendLine($"JPEG quality set to: {quality}");
            log.AppendLine();

            // For binary operations, we'll only convert the first page
            // (Multi-page support can be added via S3 method or ZIP output)
            using var outputStream = new MemoryStream();

            if (frameCount == 1)
            {
                // Single-page TIFF
                log.AppendLine("[STEP 4] Converting single-page TIFF to JPEG...");
                image.Save(outputStream, jpegEncoder);
                log.AppendLine($"Conversion complete");
                log.AppendLine($"Output size: {outputStream.Length:N0} bytes ({outputStream.Length / 1024.0:F2} KB)");
                log.AppendLine($"Compression ratio: {(double)tiffData.Length / outputStream.Length:F2}x");
                log.AppendLine();

                var endTime = DateTime.UtcNow;
                log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
                log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
                log.AppendLine($"Status: SUCCESS");

                return new ConversionResult
                {
                    Success = true,
                    Message = $"Successfully converted single-page TIFF to JPEG (quality: {quality}, size: {outputStream.Length} bytes)",
                    OutputData = outputStream.ToArray(),
                    OutputPath = string.Empty,
                    PagesConverted = 1,
                    DetailedLog = log.ToString()
                };
            }
            else
            {
                // Multi-page TIFF: convert only first page
                log.AppendLine($"[STEP 4] Converting multi-page TIFF (page 1 of {frameCount})...");
                log.AppendLine($"NOTE: Only first page will be converted in binary mode");
                log.AppendLine($"For full multi-page support, use file-based or S3 method");
                log.AppendLine();

                using var frameImage = image.Frames.CloneFrame(0);
                log.AppendLine($"Frame 0 cloned successfully");

                using var singleFrameImage = frameImage.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                singleFrameImage.Save(outputStream, jpegEncoder);

                log.AppendLine($"Conversion complete");
                log.AppendLine($"Output size: {outputStream.Length:N0} bytes ({outputStream.Length / 1024.0:F2} KB)");
                log.AppendLine($"Compression ratio: {(double)tiffData.Length / outputStream.Length:F2}x");
                log.AppendLine();

                var endTime = DateTime.UtcNow;
                log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
                log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
                log.AppendLine($"Status: SUCCESS (partial - page 1 only)");

                return new ConversionResult
                {
                    Success = true,
                    Message = $"Converted first page of {frameCount}-page TIFF to JPEG (quality: {quality}, size: {outputStream.Length} bytes). Note: Only first page converted. Use file-based or S3 method for multi-page support.",
                    OutputData = outputStream.ToArray(),
                    OutputPath = string.Empty,
                    PagesConverted = 1,
                    DetailedLog = log.ToString()
                };
            }
        }
        catch (UnknownImageFormatException ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine("=== EXCEPTION: UnknownImageFormatException ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: FAILED - Invalid image format");

            return new ConversionResult
            {
                Success = false,
                Message = $"Invalid image format: {ex.Message}. Ensure the input data is a valid TIFF.",
                OutputData = Array.Empty<byte>(),
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine($"=== EXCEPTION: {ex.GetType().Name} ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                log.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                log.AppendLine($"Inner Message: {ex.InnerException.Message}");
            }
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: FAILED");

            return new ConversionResult
            {
                Success = false,
                Message = $"Conversion error: {ex.Message}",
                OutputData = Array.Empty<byte>(),
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
    }

    /// <summary>
    /// Converts TIFF file from S3 to JPEG and saves back to S3
    /// Recommended for large files (>5MB). Currently converts first page only.
    /// </summary>
    public ConversionResult ConvertTiffToJpegS3(
        string bucketName,
        string inputS3Key,
        string outputS3Key,
        string awsAccessKey,
        string awsSecretKey,
        string awsRegion = "us-east-1",
        int quality = 85)
    {
        var log = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            log.AppendLine($"=== ConvertTiffToJpegS3 Execution Log ===");
            log.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Bucket: {bucketName}");
            log.AppendLine($"Input S3 Key: {inputS3Key}");
            log.AppendLine($"Output S3 Key: {outputS3Key}");
            log.AppendLine($"Region: {awsRegion}");
            log.AppendLine($"Quality: {quality}");
            log.AppendLine();

            // Validate inputs
            log.AppendLine("[STEP 1] Validating inputs...");
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                log.AppendLine("ERROR: Bucket name is null or empty");
                return new ConversionResult { Success = false, Message = "Bucket name cannot be empty", DetailedLog = log.ToString() };
            }

            if (string.IsNullOrWhiteSpace(inputS3Key))
            {
                log.AppendLine("ERROR: Input S3 key is null or empty");
                return new ConversionResult { Success = false, Message = "Input S3 key cannot be empty", DetailedLog = log.ToString() };
            }

            if (string.IsNullOrWhiteSpace(outputS3Key))
            {
                log.AppendLine("ERROR: Output S3 key is null or empty");
                return new ConversionResult { Success = false, Message = "Output S3 key cannot be empty", DetailedLog = log.ToString() };
            }

            if (quality < 1 || quality > 100)
            {
                log.AppendLine($"ERROR: Quality value {quality} is out of range (1-100)");
                return new ConversionResult { Success = false, Message = "Quality must be between 1 and 100", DetailedLog = log.ToString() };
            }

            log.AppendLine("Validation passed");
            log.AppendLine();

            // Create S3 client
            log.AppendLine("[STEP 2] Creating S3 client...");
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);
            using var s3Client = new AmazonS3Client(credentials, regionEndpoint);
            log.AppendLine($"S3 client created for region: {awsRegion}");
            log.AppendLine();

            // Download TIFF from S3
            log.AppendLine("[STEP 3] Downloading TIFF from S3...");
            var getRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = inputS3Key
            };

            using var getResponse = s3Client.GetObjectAsync(getRequest).GetAwaiter().GetResult();
            log.AppendLine($"Download successful");
            log.AppendLine($"Content-Type: {getResponse.Headers.ContentType}");
            log.AppendLine($"Content-Length: {getResponse.ContentLength:N0} bytes ({getResponse.ContentLength / 1024.0 / 1024.0:F2} MB)");
            log.AppendLine($"Last-Modified: {getResponse.LastModified:yyyy-MM-dd HH:mm:ss UTC}");
            log.AppendLine();

            // Load TIFF from S3 stream
            log.AppendLine("[STEP 4] Loading TIFF image from S3 stream...");

            // Buffer the stream so we can handle errors and retry if needed
            log.AppendLine("Buffering S3 stream to memory...");
            using var memoryStream = new MemoryStream();
            getResponse.ResponseStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            log.AppendLine($"Stream buffered: {memoryStream.Length:N0} bytes");

            // Try loading with ImageSharp first (faster for simple TIFFs)
            MemoryStream jpegStream;

            try
            {
                log.AppendLine("Attempting to load TIFF with ImageSharp...");
                using var image = Image.Load(memoryStream);
                log.AppendLine("TIFF loaded successfully with ImageSharp");
                log.AppendLine($"Image format: {image.Metadata.DecodedImageFormat?.Name ?? "Unknown"}");
                log.AppendLine($"Dimensions: {image.Width} x {image.Height} pixels");
                log.AppendLine($"Frame count: {image.Frames.Count}");

                int frameCount = image.Frames.Count;
                if (frameCount > 1)
                {
                    log.AppendLine($"NOTE: Multi-page TIFF detected ({frameCount} pages). Only converting first page.");
                }
                log.AppendLine();

                // Convert to JPEG
                log.AppendLine("[STEP 5] Converting to JPEG with ImageSharp...");
                var jpegEncoder = new JpegEncoder { Quality = quality };
                jpegStream = new MemoryStream();

                // Always use the first frame to avoid issues with multi-page TIFFs
                if (frameCount == 1)
                {
                    image.Save(jpegStream, jpegEncoder);
                }
                else
                {
                    // Multi-page: extract and convert only first page
                    using var frameImage = image.Frames.CloneFrame(0);
                    using var singleFrameImage = frameImage.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                    singleFrameImage.Save(jpegStream, jpegEncoder);
                }

                jpegStream.Position = 0;
                log.AppendLine($"ImageSharp conversion complete");
                log.AppendLine($"JPEG size: {jpegStream.Length:N0} bytes ({jpegStream.Length / 1024.0:F2} KB)");
                log.AppendLine($"Compression ratio: {(double)getResponse.ContentLength / jpegStream.Length:F2}x");
            }
            catch (Exception loadEx) when (loadEx.Message.Contains("different sizes") || loadEx.Message.Contains("not supported"))
            {
                // ImageSharp failed - use ImageMagick fallback for complex TIFFs
                log.AppendLine($"ImageSharp failed: {loadEx.Message}");
                log.AppendLine("Falling back to ImageMagick for complex TIFF handling...");

                memoryStream.Position = 0;

                // Use ImageMagick to extract and convert first page
                using var magickImage = new MagickImage(memoryStream);
                log.AppendLine($"ImageMagick loaded TIFF successfully");
                log.AppendLine($"Format: {magickImage.Format}");
                log.AppendLine($"Dimensions: {magickImage.Width}x{magickImage.Height} pixels");
                log.AppendLine();

                // Convert first page to JPEG using ImageMagick
                log.AppendLine("[STEP 5] Converting to JPEG with ImageMagick...");
                magickImage.Format = MagickFormat.Jpeg;
                magickImage.Quality = (uint)quality;

                var jpegBytes = magickImage.ToByteArray();
                jpegStream = new MemoryStream(jpegBytes);
                jpegStream.Position = 0;

                log.AppendLine($"ImageMagick conversion complete");
                log.AppendLine($"JPEG size: {jpegStream.Length:N0} bytes ({jpegStream.Length / 1024.0:F2} KB)");
                log.AppendLine($"Compression ratio: {(double)getResponse.ContentLength / jpegStream.Length:F2}x");
            }
            log.AppendLine();

            // Upload JPEG to S3
            log.AppendLine("[STEP 6] Uploading JPEG to S3...");
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = outputS3Key,
                InputStream = jpegStream,
                ContentType = "image/jpeg"
            };

            var putResponse = s3Client.PutObjectAsync(putRequest).GetAwaiter().GetResult();
            log.AppendLine($"Upload successful");
            log.AppendLine($"Output S3 Key: {outputS3Key}");
            log.AppendLine($"ETag: {putResponse.ETag}");
            log.AppendLine();

            var endTime = DateTime.UtcNow;
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: SUCCESS");

            return new ConversionResult
            {
                Success = true,
                Message = $"Successfully converted TIFF to JPEG via S3 (quality: {quality}). Output: s3://{bucketName}/{outputS3Key}",
                OutputPath = outputS3Key,
                PagesConverted = 1,
                DetailedLog = log.ToString()
            };
        }
        catch (AmazonS3Exception s3Ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine("=== EXCEPTION: AmazonS3Exception ===");
            log.AppendLine($"Message: {s3Ex.Message}");
            log.AppendLine($"Error Code: {s3Ex.ErrorCode}");
            log.AppendLine($"Status Code: {s3Ex.StatusCode}");
            log.AppendLine($"Request ID: {s3Ex.RequestId}");
            log.AppendLine($"Stack Trace: {s3Ex.StackTrace}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: FAILED - S3 Error");

            return new ConversionResult
            {
                Success = false,
                Message = $"S3 Error: {s3Ex.Message} (ErrorCode: {s3Ex.ErrorCode})",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
        catch (UnknownImageFormatException ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine("=== EXCEPTION: UnknownImageFormatException ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: FAILED - Invalid image format");

            return new ConversionResult
            {
                Success = false,
                Message = $"Invalid image format: {ex.Message}. Ensure the S3 file is a valid TIFF.",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine($"=== EXCEPTION: {ex.GetType().Name} ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                log.AppendLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                log.AppendLine($"Inner Message: {ex.InnerException.Message}");
            }
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: FAILED");

            return new ConversionResult
            {
                Success = false,
                Message = $"Conversion error: {ex.Message}",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
    }

    /// <summary>
    /// Converts TIFF file from S3 to PDF and saves back to S3
    /// Preserves all pages in multi-page TIFFs
    /// </summary>
    public ConversionResult ConvertTiffToPdfS3(
        string bucketName,
        string inputS3Key,
        string outputS3Key,
        string awsAccessKey,
        string awsSecretKey,
        string awsRegion = "us-east-1")
    {
        var log = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            log.AppendLine($"=== ConvertTiffToPdfS3 Execution Log ===");
            log.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Bucket: {bucketName}");
            log.AppendLine($"Input S3 Key: {inputS3Key}");
            log.AppendLine($"Output S3 Key: {outputS3Key}");
            log.AppendLine($"Region: {awsRegion}");
            log.AppendLine();

            // Validate inputs
            log.AppendLine("[STEP 1] Validating inputs...");
            if (string.IsNullOrWhiteSpace(bucketName))
                return new ConversionResult { Success = false, Message = "Bucket name cannot be empty", DetailedLog = log.ToString() };

            if (string.IsNullOrWhiteSpace(inputS3Key))
                return new ConversionResult { Success = false, Message = "Input S3 key cannot be empty", DetailedLog = log.ToString() };

            if (string.IsNullOrWhiteSpace(outputS3Key))
                return new ConversionResult { Success = false, Message = "Output S3 key cannot be empty", DetailedLog = log.ToString() };

            log.AppendLine("✓ All inputs valid");
            log.AppendLine();

            // Create AWS credentials and S3 client
            log.AppendLine("[STEP 2] Creating AWS S3 client...");
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);
            using var s3Client = new AmazonS3Client(credentials, regionEndpoint);
            log.AppendLine($"✓ S3 client created for region: {awsRegion}");
            log.AppendLine();

            // Download TIFF from S3
            log.AppendLine("[STEP 3] Downloading TIFF from S3...");
            var getRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = inputS3Key
            };

            using var getResponse = s3Client.GetObjectAsync(getRequest).GetAwaiter().GetResult();
            log.AppendLine($"Download successful");
            log.AppendLine($"Content-Type: {getResponse.Headers.ContentType}");
            log.AppendLine($"Content-Length: {getResponse.ContentLength:N0} bytes ({getResponse.ContentLength / 1024.0 / 1024.0:F2} MB)");
            log.AppendLine();

            // Load TIFF and convert to PDF using ImageMagick
            log.AppendLine("[STEP 4] Loading TIFF and converting to PDF...");
            using var memoryStream = new MemoryStream();
            getResponse.ResponseStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            // Use ImageMagick for TIFF to PDF conversion (handles multi-page)
            using var magickImageCollection = new MagickImageCollection(memoryStream);
            log.AppendLine($"TIFF loaded successfully");
            log.AppendLine($"Page count: {magickImageCollection.Count}");

            // Convert all pages to PDF
            log.AppendLine("[STEP 5] Converting to PDF format...");
            using var pdfStream = new MemoryStream();
            magickImageCollection.Write(pdfStream, MagickFormat.Pdf);
            pdfStream.Position = 0;

            log.AppendLine($"Conversion complete");
            log.AppendLine($"PDF size: {pdfStream.Length:N0} bytes ({pdfStream.Length / 1024.0:F2} KB)");
            log.AppendLine($"Pages converted: {magickImageCollection.Count}");
            log.AppendLine($"Compression ratio: {(double)getResponse.ContentLength / pdfStream.Length:F2}x");
            log.AppendLine();

            // Upload PDF to S3
            log.AppendLine("[STEP 6] Uploading PDF to S3...");
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = outputS3Key,
                InputStream = pdfStream,
                ContentType = "application/pdf"
            };

            var putResponse = s3Client.PutObjectAsync(putRequest).GetAwaiter().GetResult();
            log.AppendLine($"Upload successful");
            log.AppendLine($"Output S3 Key: {outputS3Key}");
            log.AppendLine($"ETag: {putResponse.ETag}");
            log.AppendLine();

            var endTime = DateTime.UtcNow;
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: SUCCESS");

            return new ConversionResult
            {
                Success = true,
                Message = $"Successfully converted {magickImageCollection.Count}-page TIFF to PDF via S3. Output: s3://{bucketName}/{outputS3Key}",
                OutputPath = outputS3Key,
                PagesConverted = magickImageCollection.Count,
                DetailedLog = log.ToString()
            };
        }
        catch (AmazonS3Exception s3Ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine("=== EXCEPTION: AmazonS3Exception ===");
            log.AppendLine($"Message: {s3Ex.Message}");
            log.AppendLine($"Error Code: {s3Ex.ErrorCode}");
            log.AppendLine($"Status Code: {s3Ex.StatusCode}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");

            return new ConversionResult
            {
                Success = false,
                Message = $"S3 Error: {s3Ex.Message} (ErrorCode: {s3Ex.ErrorCode})",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine($"=== EXCEPTION: {ex.GetType().Name} ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");

            return new ConversionResult
            {
                Success = false,
                Message = $"Conversion error: {ex.Message}",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
    }

    /// <summary>
    /// Converts TIFF to compressed PDF optimized for OCR/LLM workflows
    /// Compresses each page to JPEG, then combines into PDF - typically 70-90% smaller
    /// </summary>
    public ConversionResult ConvertTiffToCompressedPdfS3(
        string bucketName,
        string inputS3Key,
        string outputS3Key,
        string awsAccessKey,
        string awsSecretKey,
        string awsRegion = "us-east-1",
        int quality = 85)
    {
        var log = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            log.AppendLine($"=== ConvertTiffToCompressedPdfS3 Execution Log ===");
            log.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Bucket: {bucketName}");
            log.AppendLine($"Input S3 Key: {inputS3Key}");
            log.AppendLine($"Output S3 Key: {outputS3Key}");
            log.AppendLine($"Region: {awsRegion}");
            log.AppendLine($"Quality: {quality}");
            log.AppendLine();

            // Validate inputs
            log.AppendLine("[STEP 1] Validating inputs...");
            if (string.IsNullOrWhiteSpace(bucketName))
                return new ConversionResult { Success = false, Message = "Bucket name cannot be empty", DetailedLog = log.ToString() };

            if (string.IsNullOrWhiteSpace(inputS3Key))
                return new ConversionResult { Success = false, Message = "Input S3 key cannot be empty", DetailedLog = log.ToString() };

            if (string.IsNullOrWhiteSpace(outputS3Key))
                return new ConversionResult { Success = false, Message = "Output S3 key cannot be empty", DetailedLog = log.ToString() };

            if (quality < 1 || quality > 100)
                return new ConversionResult { Success = false, Message = "Quality must be between 1 and 100", DetailedLog = log.ToString() };

            log.AppendLine("✓ All inputs valid");
            log.AppendLine();

            // Create AWS credentials and S3 client
            log.AppendLine("[STEP 2] Creating AWS S3 client...");
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);
            using var s3Client = new AmazonS3Client(credentials, regionEndpoint);
            log.AppendLine($"✓ S3 client created for region: {awsRegion}");
            log.AppendLine();

            // Download TIFF from S3
            log.AppendLine("[STEP 3] Downloading TIFF from S3...");
            var getRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = inputS3Key
            };

            using var getResponse = s3Client.GetObjectAsync(getRequest).GetAwaiter().GetResult();
            log.AppendLine($"Download successful");
            log.AppendLine($"Content-Length: {getResponse.ContentLength:N0} bytes ({getResponse.ContentLength / 1024.0 / 1024.0:F2} MB)");
            log.AppendLine();

            // Load TIFF
            log.AppendLine("[STEP 4] Loading TIFF and converting pages to JPEG...");
            using var memoryStream = new MemoryStream();
            getResponse.ResponseStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            // Load all pages as ImageMagick collection
            using var magickImages = new MagickImageCollection(memoryStream);
            log.AppendLine($"TIFF loaded successfully");
            log.AppendLine($"Page count: {magickImages.Count}");

            // Convert each page to JPEG with compression
            log.AppendLine($"[STEP 5] Compressing {magickImages.Count} pages to JPEG quality {quality}...");
            var compressedPages = new MagickImageCollection();
            int pageNum = 0;
            long totalUncompressedSize = 0;
            long totalCompressedSize = 0;

            // Only log details for small documents to avoid payload size issues
            bool verboseLogging = magickImages.Count <= 5;

            foreach (var page in magickImages)
            {
                pageNum++;

                // Get uncompressed size estimate
                using var uncompressedStream = new MemoryStream();
                page.Write(uncompressedStream);
                long uncompressedPageSize = uncompressedStream.Length;
                totalUncompressedSize += uncompressedPageSize;

                // Convert page to JPEG with compression
                using var jpegStream = new MemoryStream();
                page.Format = MagickFormat.Jpeg;
                page.Quality = (uint)quality;
                page.Write(jpegStream, MagickFormat.Jpeg);

                long compressedPageSize = jpegStream.Length;
                totalCompressedSize += compressedPageSize;

                // Only log per-page details for small documents
                if (verboseLogging)
                {
                    log.AppendLine($"  Page {pageNum}: {uncompressedPageSize:N0} → {compressedPageSize:N0} bytes ({(1 - (double)compressedPageSize / uncompressedPageSize) * 100:F1}% reduction)");
                }

                // Load compressed JPEG back as image for PDF
                // Use byte array to avoid "Cannot access a closed Stream" error
                var jpegBytes = jpegStream.ToArray();
                var compressedPage = new MagickImage(jpegBytes);
                compressedPages.Add(compressedPage);
            }

            log.AppendLine($"✓ All {compressedPages.Count} pages compressed");
            log.AppendLine($"  Total pages processed: {compressedPages.Count}");
            log.AppendLine($"  Total uncompressed: {totalUncompressedSize:N0} bytes ({totalUncompressedSize / 1024.0 / 1024.0:F2} MB)");
            log.AppendLine($"  Total compressed: {totalCompressedSize:N0} bytes ({totalCompressedSize / 1024.0 / 1024.0:F2} MB)");
            log.AppendLine($"  Overall compression: {(1 - (double)totalCompressedSize / totalUncompressedSize) * 100:F1}% size reduction");
            log.AppendLine();

            // Create PDF from compressed JPEG pages
            log.AppendLine("[STEP 6] Creating compressed PDF...");
            log.AppendLine($"Writing {compressedPages.Count} compressed pages to PDF format...");

            using var pdfStream = new MemoryStream();
            compressedPages.Write(pdfStream, MagickFormat.Pdf);
            pdfStream.Position = 0;

            log.AppendLine($"✓ PDF stream created");
            log.AppendLine($"  Stream length: {pdfStream.Length:N0} bytes");
            log.AppendLine($"  Stream position: {pdfStream.Position}");
            log.AppendLine($"  Stream can read: {pdfStream.CanRead}");
            log.AppendLine($"  Stream can seek: {pdfStream.CanSeek}");

            if (pdfStream.Length == 0)
            {
                log.AppendLine("ERROR: PDF stream is empty!");
                return new ConversionResult
                {
                    Success = false,
                    Message = "PDF creation failed - stream is empty",
                    DetailedLog = log.ToString()
                };
            }

            log.AppendLine($"Original TIFF size: {getResponse.ContentLength:N0} bytes ({getResponse.ContentLength / 1024.0 / 1024.0:F2} MB)");
            log.AppendLine($"Compressed PDF size: {pdfStream.Length:N0} bytes ({pdfStream.Length / 1024.0 / 1024.0:F2} MB)");
            log.AppendLine($"Size reduction: {(1 - (double)pdfStream.Length / getResponse.ContentLength) * 100:F1}%");
            log.AppendLine($"Compression ratio: {(double)getResponse.ContentLength / pdfStream.Length:F2}x");
            log.AppendLine();

            // Upload compressed PDF to S3
            log.AppendLine("[STEP 7] Uploading compressed PDF to S3...");
            log.AppendLine($"  Target bucket: {bucketName}");
            log.AppendLine($"  Target key: {outputS3Key}");
            log.AppendLine($"  Content type: application/pdf");
            log.AppendLine($"  Stream size: {pdfStream.Length:N0} bytes");

            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = outputS3Key,
                InputStream = pdfStream,
                ContentType = "application/pdf"
            };

            log.AppendLine("  Initiating S3 PutObject request...");
            var putResponse = s3Client.PutObjectAsync(putRequest).GetAwaiter().GetResult();

            log.AppendLine($"✓ S3 upload completed");
            log.AppendLine($"  Output S3 Key: {outputS3Key}");
            log.AppendLine($"  ETag: {putResponse.ETag}");
            log.AppendLine($"  HTTP Status: {putResponse.HttpStatusCode}");
            log.AppendLine($"  Request ID: {putResponse.ResponseMetadata.RequestId}");
            log.AppendLine();

            var endTime = DateTime.UtcNow;
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            log.AppendLine($"Total Duration: {(endTime - startTime).TotalMilliseconds:F2} ms");
            log.AppendLine($"Status: SUCCESS");

            var sizeReduction = (1 - (double)pdfStream.Length / getResponse.ContentLength) * 100;

            return new ConversionResult
            {
                Success = true,
                Message = $"Successfully converted {magickImages.Count}-page TIFF to compressed PDF ({sizeReduction:F1}% smaller). Quality: {quality}. Output: s3://{bucketName}/{outputS3Key}",
                OutputPath = outputS3Key,
                PagesConverted = magickImages.Count,
                DetailedLog = log.ToString()
            };
        }
        catch (AmazonS3Exception s3Ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine("=== EXCEPTION: AmazonS3Exception ===");
            log.AppendLine($"Message: {s3Ex.Message}");
            log.AppendLine($"Error Code: {s3Ex.ErrorCode}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");

            return new ConversionResult
            {
                Success = false,
                Message = $"S3 Error: {s3Ex.Message} (ErrorCode: {s3Ex.ErrorCode})",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            log.AppendLine();
            log.AppendLine($"=== EXCEPTION: {ex.GetType().Name} ===");
            log.AppendLine($"Message: {ex.Message}");
            log.AppendLine($"Stack Trace: {ex.StackTrace}");
            log.AppendLine($"End Time: {endTime:yyyy-MM-dd HH:mm:ss.fff} UTC");

            return new ConversionResult
            {
                Success = false,
                Message = $"Conversion error: {ex.Message}",
                OutputPath = string.Empty,
                PagesConverted = 0,
                DetailedLog = log.ToString()
            };
        }
    }

    /// <summary>
    /// Generates a pre-signed S3 URL for direct browser upload
    /// Bypasses OutSystems 5.5MB payload limit by allowing direct browser to S3 upload
    /// </summary>
    public S3UploadUrlResult GenerateS3UploadUrl(
        string bucketName,
        string s3Key,
        string contentType,
        string awsAccessKey,
        string awsSecretKey,
        string awsRegion = "us-east-1",
        int expirationMinutes = 15)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(bucketName))
                return new S3UploadUrlResult
                {
                    Success = false,
                    Message = "Bucket name cannot be empty",
                    UploadUrl = string.Empty,
                    S3Key = string.Empty,
                    ExpiresAt = string.Empty
                };

            if (string.IsNullOrWhiteSpace(s3Key))
                return new S3UploadUrlResult
                {
                    Success = false,
                    Message = "S3 key cannot be empty",
                    UploadUrl = string.Empty,
                    S3Key = string.Empty,
                    ExpiresAt = string.Empty
                };

            if (string.IsNullOrWhiteSpace(contentType))
                return new S3UploadUrlResult
                {
                    Success = false,
                    Message = "Content type cannot be empty",
                    UploadUrl = string.Empty,
                    S3Key = string.Empty,
                    ExpiresAt = string.Empty
                };

            if (expirationMinutes < 1 || expirationMinutes > 1440)
                return new S3UploadUrlResult
                {
                    Success = false,
                    Message = "Expiration must be between 1 and 1440 minutes (24 hours)",
                    UploadUrl = string.Empty,
                    S3Key = string.Empty,
                    ExpiresAt = string.Empty
                };

            // Create S3 client
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);
            using var s3Client = new AmazonS3Client(credentials, regionEndpoint);

            // Generate pre-signed URL for PUT operation with Content-Type
            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                Verb = HttpVerb.PUT,
                Expires = expiresAt,
                ContentType = contentType  // CRITICAL: Must match browser request
            };

            string uploadUrl = s3Client.GetPreSignedURL(request);

            return new S3UploadUrlResult
            {
                Success = true,
                Message = $"Pre-signed upload URL generated successfully. Expires in {expirationMinutes} minutes. Content-Type: {contentType}",
                UploadUrl = uploadUrl,
                S3Key = s3Key,
                ExpiresAt = expiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };
        }
        catch (AmazonS3Exception s3Ex)
        {
            return new S3UploadUrlResult
            {
                Success = false,
                Message = $"S3 Error: {s3Ex.Message} (ErrorCode: {s3Ex.ErrorCode})",
                UploadUrl = string.Empty,
                S3Key = s3Key,
                ExpiresAt = string.Empty
            };
        }
        catch (Exception ex)
        {
            return new S3UploadUrlResult
            {
                Success = false,
                Message = $"Error generating upload URL: {ex.Message}",
                UploadUrl = string.Empty,
                S3Key = s3Key,
                ExpiresAt = string.Empty
            };
        }
    }

    /// <summary>
    /// Generates a pre-signed S3 URL for downloading files
    /// Use this to download large files (>5.5MB) directly from S3 to the browser
    /// </summary>
    public S3UploadUrlResult GenerateS3DownloadUrl(
        string bucketName,
        string s3Key,
        string awsAccessKey,
        string awsSecretKey,
        string awsRegion = "us-east-1",
        int expirationMinutes = 15)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(bucketName))
                return new S3UploadUrlResult { Success = false, Message = "Bucket name cannot be empty" };

            if (string.IsNullOrWhiteSpace(s3Key))
                return new S3UploadUrlResult { Success = false, Message = "S3 key cannot be empty" };

            if (string.IsNullOrWhiteSpace(awsAccessKey) || string.IsNullOrWhiteSpace(awsSecretKey))
                return new S3UploadUrlResult { Success = false, Message = "AWS credentials cannot be empty" };

            if (expirationMinutes < 1 || expirationMinutes > 10080) // Max 7 days
                return new S3UploadUrlResult { Success = false, Message = "Expiration must be between 1 and 10080 minutes (7 days)" };

            // Create AWS credentials and S3 client
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);

            using var s3Client = new AmazonS3Client(credentials, regionEndpoint);

            // Generate pre-signed download URL
            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                Verb = HttpVerb.GET,  // GET for downloads
                Expires = expiresAt
            };

            string downloadUrl = s3Client.GetPreSignedURL(request);

            return new S3UploadUrlResult
            {
                Success = true,
                Message = $"Pre-signed download URL generated successfully. Expires in {expirationMinutes} minutes.",
                UploadUrl = downloadUrl,  // Reusing this field for download URL
                S3Key = s3Key,
                ExpiresAt = expiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };
        }
        catch (AmazonS3Exception s3Ex)
        {
            return new S3UploadUrlResult
            {
                Success = false,
                Message = $"S3 Error: {s3Ex.Message} (ErrorCode: {s3Ex.ErrorCode})",
                UploadUrl = string.Empty,
                S3Key = s3Key,
                ExpiresAt = string.Empty
            };
        }
        catch (Exception ex)
        {
            return new S3UploadUrlResult
            {
                Success = false,
                Message = $"Error generating download URL: {ex.Message}",
                UploadUrl = string.Empty,
                S3Key = s3Key,
                ExpiresAt = string.Empty
            };
        }
    }

    /// <summary>
    /// Downloads a file from S3 and returns it as binary data
    /// LIMITATION: Only works for files <5.5MB due to OutSystems payload limits
    /// Use GenerateS3DownloadUrl for larger files instead
    /// </summary>
    public S3DownloadResult DownloadFileFromS3(
        string bucketName,
        string s3Key,
        string awsAccessKey,
        string awsSecretKey,
        string awsRegion = "us-east-1")
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(bucketName))
                return new S3DownloadResult { Success = false, Message = "Bucket name cannot be empty" };

            if (string.IsNullOrWhiteSpace(s3Key))
                return new S3DownloadResult { Success = false, Message = "S3 key cannot be empty" };

            if (string.IsNullOrWhiteSpace(awsAccessKey) || string.IsNullOrWhiteSpace(awsSecretKey))
                return new S3DownloadResult { Success = false, Message = "AWS credentials cannot be empty" };

            // Create AWS credentials and S3 client
            var credentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);

            using var s3Client = new AmazonS3Client(credentials, regionEndpoint);

            // Download file from S3
            var getRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key
            };

            using var getResponse = s3Client.GetObjectAsync(getRequest).GetAwaiter().GetResult();

            // Read file data into memory
            using var memoryStream = new MemoryStream();
            getResponse.ResponseStream.CopyTo(memoryStream);
            var fileData = memoryStream.ToArray();

            // Extract filename from S3 key (everything after last '/')
            var fileName = s3Key.Contains('/') ? s3Key.Substring(s3Key.LastIndexOf('/') + 1) : s3Key;

            return new S3DownloadResult
            {
                Success = true,
                Message = $"File downloaded successfully from s3://{bucketName}/{s3Key}",
                FileData = fileData,
                FileName = fileName,
                ContentType = getResponse.Headers.ContentType,
                FileSize = getResponse.ContentLength
            };
        }
        catch (AmazonS3Exception s3Ex)
        {
            return new S3DownloadResult
            {
                Success = false,
                Message = $"S3 Error: {s3Ex.Message} (ErrorCode: {s3Ex.ErrorCode})",
                FileData = Array.Empty<byte>(),
                FileName = string.Empty,
                ContentType = string.Empty,
                FileSize = 0
            };
        }
        catch (Exception ex)
        {
            return new S3DownloadResult
            {
                Success = false,
                Message = $"Download error: {ex.Message}",
                FileData = Array.Empty<byte>(),
                FileName = string.Empty,
                ContentType = string.Empty,
                FileSize = 0
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
