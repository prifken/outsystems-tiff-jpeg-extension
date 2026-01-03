using OutSystems.ExternalLibraries.SDK;

namespace ImageConverterLibrary;

/// <summary>
/// Interface for TIFF to JPEG conversion operations with S3 support
/// This interface is exposed to OutSystems ODC as an External Logic library
/// </summary>
[OSInterface(
    Name = "ImageConverter",
    Description = "TIFF to JPEG image converter with AWS S3 integration")]
public interface IImageConverter
{
    /// <summary>
    /// Tests the connection to AWS S3 using provided credentials
    /// </summary>
    /// <param name="awsAccessKey">AWS Access Key ID</param>
    /// <param name="awsSecretKey">AWS Secret Access Key</param>
    /// <param name="awsRegion">AWS Region (default: us-east-1)</param>
    /// <returns>Formatted string with connection result and bucket list</returns>
    [OSAction(
        Description = "Test S3 connection using provided AWS credentials",
        ReturnName = "result",
        ReturnDescription = "Formatted connection result with bucket list or error message",
        ReturnType = OSDataType.Text)]
    string TestS3Connection(
        [OSParameter(
            Description = "AWS Access Key ID",
            DataType = OSDataType.Text)]
        string awsAccessKey,

        [OSParameter(
            Description = "AWS Secret Access Key",
            DataType = OSDataType.Text)]
        string awsSecretKey,

        [OSParameter(
            Description = "AWS Region (default: us-east-1)",
            DataType = OSDataType.Text)]
        string awsRegion = "us-east-1");

    /// <summary>
    /// Converts a TIFF file to JPEG format using file paths
    /// </summary>
    /// <param name="inputPath">Path to input TIFF file</param>
    /// <param name="outputPath">Path for output JPEG file</param>
    /// <param name="quality">JPEG quality (1-100)</param>
    /// <returns>Conversion result</returns>
    [OSAction(
        Description = "Converts a TIFF file to JPEG format using file paths",
        ReturnName = "result",
        ReturnDescription = "Conversion operation result",
        ReturnType = OSDataType.InferredFromDotNetType)]
    ConversionResult ConvertTiffToJpeg(
        [OSParameter(
            Description = "Path to input TIFF file",
            DataType = OSDataType.Text)]
        string inputPath,

        [OSParameter(
            Description = "Path for output JPEG file",
            DataType = OSDataType.Text)]
        string outputPath,

        [OSParameter(
            Description = "JPEG quality (1-100)",
            DataType = OSDataType.Integer)]
        int quality = 85);

    /// <summary>
    /// Converts TIFF binary data to JPEG binary data
    /// Recommended for small files. For large files, use S3-based conversion.
    /// </summary>
    /// <param name="tiffData">TIFF file as binary data</param>
    /// <param name="quality">JPEG quality (1-100)</param>
    /// <returns>Conversion result with JPEG data in OutputData field</returns>
    [OSAction(
        Description = "Converts TIFF binary data to JPEG binary data (for small files)",
        ReturnName = "result",
        ReturnDescription = "Conversion result with JPEG binary in OutputData field",
        ReturnType = OSDataType.InferredFromDotNetType)]
    ConversionResult ConvertTiffToJpegBinary(
        [OSParameter(
            Description = "TIFF file as binary data",
            DataType = OSDataType.BinaryData)]
        byte[] tiffData,

        [OSParameter(
            Description = "JPEG quality (1-100)",
            DataType = OSDataType.Integer)]
        int quality = 85);

    /// <summary>
    /// Converts TIFF file from S3 to JPEG and saves back to S3
    /// Recommended for large files (>5MB). Handles files of any size.
    /// Currently converts first page only for multi-page TIFFs.
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="inputS3Key">S3 key of input TIFF file</param>
    /// <param name="outputS3Key">S3 key for output JPEG file</param>
    /// <param name="awsAccessKey">AWS Access Key ID</param>
    /// <param name="awsSecretKey">AWS Secret Access Key</param>
    /// <param name="awsRegion">AWS Region (default: us-east-1)</param>
    /// <param name="quality">JPEG quality (1-100)</param>
    /// <returns>Conversion result with output S3 key in OutputPath field</returns>
    [OSAction(
        Description = "Converts TIFF from S3 to JPEG and saves back to S3 (for large files)",
        ReturnName = "result",
        ReturnDescription = "Conversion result with output S3 key in OutputPath field",
        ReturnType = OSDataType.InferredFromDotNetType)]
    ConversionResult ConvertTiffToJpegS3(
        [OSParameter(
            Description = "S3 bucket name",
            DataType = OSDataType.Text)]
        string bucketName,

        [OSParameter(
            Description = "S3 key of input TIFF file (e.g., 'uploads/document.tiff')",
            DataType = OSDataType.Text)]
        string inputS3Key,

        [OSParameter(
            Description = "S3 key for output JPEG file (e.g., 'converted/document.jpg')",
            DataType = OSDataType.Text)]
        string outputS3Key,

        [OSParameter(
            Description = "AWS Access Key ID",
            DataType = OSDataType.Text)]
        string awsAccessKey,

        [OSParameter(
            Description = "AWS Secret Access Key",
            DataType = OSDataType.Text)]
        string awsSecretKey,

        [OSParameter(
            Description = "AWS Region (default: us-east-1)",
            DataType = OSDataType.Text)]
        string awsRegion = "us-east-1",

        [OSParameter(
            Description = "JPEG quality (1-100)",
            DataType = OSDataType.Integer)]
        int quality = 85);

    /// <summary>
    /// Generates a pre-signed S3 URL for direct browser upload
    /// Allows large file uploads without going through OutSystems server
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="s3Key">S3 key for the file (e.g., 'uploads/document.tiff')</param>
    /// <param name="contentType">File content type (e.g., 'image/tiff')</param>
    /// <param name="awsAccessKey">AWS Access Key ID</param>
    /// <param name="awsSecretKey">AWS Secret Access Key</param>
    /// <param name="awsRegion">AWS Region (default: us-east-1)</param>
    /// <param name="expirationMinutes">URL expiration time in minutes (default: 15)</param>
    /// <returns>Pre-signed upload URL result</returns>
    [OSAction(
        Description = "Generate pre-signed S3 upload URL for direct browser upload (bypasses 5.5MB limit)",
        ReturnName = "result",
        ReturnDescription = "Pre-signed URL result with upload URL and expiration",
        ReturnType = OSDataType.InferredFromDotNetType)]
    S3UploadUrlResult GenerateS3UploadUrl(
        [OSParameter(
            Description = "S3 bucket name",
            DataType = OSDataType.Text)]
        string bucketName,

        [OSParameter(
            Description = "S3 key for upload (e.g., 'uploads/document.tiff')",
            DataType = OSDataType.Text)]
        string s3Key,

        [OSParameter(
            Description = "File content type (e.g., 'image/tiff', 'application/octet-stream')",
            DataType = OSDataType.Text)]
        string contentType,

        [OSParameter(
            Description = "AWS Access Key ID",
            DataType = OSDataType.Text)]
        string awsAccessKey,

        [OSParameter(
            Description = "AWS Secret Access Key",
            DataType = OSDataType.Text)]
        string awsSecretKey,

        [OSParameter(
            Description = "AWS Region (default: us-east-1)",
            DataType = OSDataType.Text)]
        string awsRegion = "us-east-1",

        [OSParameter(
            Description = "URL expiration in minutes (default: 15)",
            DataType = OSDataType.Integer)]
        int expirationMinutes = 15);

    /// <summary>
    /// Gets the current server timestamp for testing
    /// </summary>
    /// <returns>Current UTC timestamp</returns>
    [OSAction(Description = "Get current timestamp for testing")]
    string GetCurrentTimestamp();

    /// <summary>
    /// Echoes a test message with timestamp
    /// </summary>
    /// <param name="message">Message to echo</param>
    /// <returns>Echoed message with timestamp</returns>
    [OSAction(Description = "Echo a test message")]
    string EchoMessage(string message);

    /// <summary>
    /// Gets the build version and metadata for this library
    /// </summary>
    /// <returns>Build version information</returns>
    [OSAction(Description = "Get library build version and metadata")]
    string GetBuildVersion();
}
