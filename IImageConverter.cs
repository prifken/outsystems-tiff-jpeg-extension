using OutSystems.ExternalLibraries.SDK;

namespace ImageConverterLibrary;

/// <summary>
/// Interface for TIFF conversion operations with S3 support
/// This interface is exposed to OutSystems ODC as an External Logic library
/// </summary>
[OSInterface(
    Name = "TiffConverter",
    Description = "Convert TIFF files to PDF or JPEG format. Uses AWS S3 for storage to handle large files.",
    IconResourceName = "ImageConverterLibrary.icon.png")]
public interface IImageConverter
{
    /// <summary>
    /// Converts TIFF file from S3 to PDF or JPEG and saves back to S3
    /// Supports both compressed PDF (multi-page) and JPEG (first page only)
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="inputS3Key">S3 key of input TIFF file</param>
    /// <param name="outputS3Key">S3 key for output file</param>
    /// <param name="awsAccessKey">AWS Access Key ID</param>
    /// <param name="awsSecretKey">AWS Secret Access Key</param>
    /// <param name="awsRegion">AWS Region (default: us-east-1)</param>
    /// <param name="outputFormat">Output format: "PDF" (multi-page) or "JPEG" (first page only)</param>
    /// <param name="quality">Quality setting (1-100). For JPEG: image quality. For PDF: compression quality (default: 85)</param>
    /// <param name="compressPdf">Compress PDF (true = 70-90% smaller, ideal for OCR/LLM. false = no compression). Only applies when outputFormat="PDF"</param>
    /// <returns>Conversion result with output S3 key and metadata</returns>
    [OSAction(
        Description = "Convert TIFF to PDF (multi-page, compressed) or JPEG (first page) via S3",
        ReturnName = "result",
        ReturnDescription = "Conversion result with output S3 key, page count, and compression info",
        ReturnType = OSDataType.InferredFromDotNetType)]
    ConversionResult ConvertTiffS3(
        [OSParameter(
            Description = "S3 bucket name",
            DataType = OSDataType.Text)]
        string bucketName,

        [OSParameter(
            Description = "S3 key of input TIFF file (e.g., 'uploads/document.tiff')",
            DataType = OSDataType.Text)]
        string inputS3Key,

        [OSParameter(
            Description = "S3 key for output file (e.g., 'converted/document.pdf' or 'converted/document.jpg')",
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
            Description = "Output format (case-insensitive). Accepted values: 'PDF' = multi-page document, 'JPEG' = first page only. Default: 'PDF'",
            DataType = OSDataType.Text)]
        string outputFormat = "PDF",

        [OSParameter(
            Description = "Quality (1-100). JPEG: image quality. PDF: compression quality. Default: 85. OCR: 80-85. Archival: 90-95.",
            DataType = OSDataType.Integer)]
        int quality = 85,

        [OSParameter(
            Description = "Compress PDF (70-90% smaller, ideal for OCR/LLM). Only applies to PDF output. Default: true.",
            DataType = OSDataType.Boolean)]
        bool compressPdf = true);

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
    /// Downloads a file from S3 and returns it as binary data
    /// LIMITATION: Only works for files smaller than 5.5 MB due to OutSystems payload limits.
    /// For larger files, use GenerateS3DownloadUrl instead.
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="s3Key">S3 key of file to download (e.g., 'converted/document.jpg')</param>
    /// <param name="awsAccessKey">AWS Access Key ID</param>
    /// <param name="awsSecretKey">AWS Secret Access Key</param>
    /// <param name="awsRegion">AWS Region (default: us-east-1)</param>
    /// <returns>Download result with binary file data</returns>
    [OSAction(
        Description = "Download a file from S3 as binary data (ONLY for files <5.5MB - use GenerateS3DownloadUrl for larger files)",
        ReturnName = "result",
        ReturnDescription = "Download result with binary file data, filename, and metadata",
        ReturnType = OSDataType.InferredFromDotNetType)]
    S3DownloadResult DownloadFileFromS3(
        [OSParameter(
            Description = "S3 bucket name",
            DataType = OSDataType.Text)]
        string bucketName,

        [OSParameter(
            Description = "S3 key of file to download (e.g., 'converted/document.jpg')",
            DataType = OSDataType.Text)]
        string s3Key,

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
    /// Generates a pre-signed S3 URL for downloading files (bypasses 5.5MB limit)
    /// Use this URL to download large files directly from S3 to the browser
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="s3Key">S3 key of file to download (e.g., 'converted/document.jpg')</param>
    /// <param name="awsAccessKey">AWS Access Key ID</param>
    /// <param name="awsSecretKey">AWS Secret Access Key</param>
    /// <param name="awsRegion">AWS Region (default: us-east-1)</param>
    /// <param name="expirationMinutes">URL expiration time in minutes (default: 15)</param>
    /// <returns>Pre-signed download URL result</returns>
    [OSAction(
        Description = "Generate pre-signed S3 download URL for large files (bypasses 5.5MB limit)",
        ReturnName = "result",
        ReturnDescription = "Pre-signed URL result with download URL and expiration",
        ReturnType = OSDataType.InferredFromDotNetType)]
    S3DownloadUrlResult GenerateS3DownloadUrl(
        [OSParameter(
            Description = "S3 bucket name",
            DataType = OSDataType.Text)]
        string bucketName,

        [OSParameter(
            Description = "S3 key of file to download (e.g., 'converted/document.jpg')",
            DataType = OSDataType.Text)]
        string s3Key,

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
}
