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
