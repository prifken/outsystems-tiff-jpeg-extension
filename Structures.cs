using OutSystems.ExternalLibraries.SDK;

namespace ImageConverterLibrary;

/// <summary>
/// AWS credentials structure for S3 operations
/// </summary>
[OSStructure(Description = "AWS credentials for S3 access")]
public struct AWSCredentials
{
    [OSStructureField(
        Description = "AWS Access Key ID",
        DataType = OSDataType.Text,
        IsMandatory = true)]
    public string AccessKey;

    [OSStructureField(
        Description = "AWS Secret Access Key",
        DataType = OSDataType.Text,
        IsMandatory = true)]
    public string SecretKey;

    [OSStructureField(
        Description = "AWS Region (e.g., us-east-1)",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string Region;
}

/// <summary>
/// Result structure for S3 connection test
/// </summary>
[OSStructure(Description = "Result from S3 connection test")]
public struct S3ConnectionResult
{
    [OSStructureField(
        Description = "Indicates if the connection was successful",
        DataType = OSDataType.Boolean,
        IsMandatory = true)]
    public bool Success;

    [OSStructureField(
        Description = "Connection status message",
        DataType = OSDataType.Text,
        IsMandatory = true)]
    public string Message;

    [OSStructureField(
        Description = "AWS Region being used",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string Region;

    [OSStructureField(
        Description = "Number of accessible buckets",
        DataType = OSDataType.Integer,
        IsMandatory = false)]
    public int BucketCount;
}

/// <summary>
/// Result structure for TIFF to JPEG conversion operations
/// </summary>
[OSStructure(Description = "Result from image conversion operation")]
public struct ConversionResult
{
    [OSStructureField(
        Description = "Indicates if the conversion was successful",
        DataType = OSDataType.Boolean,
        IsMandatory = true)]
    public bool Success;

    [OSStructureField(
        Description = "Status message or error details",
        DataType = OSDataType.Text,
        IsMandatory = true)]
    public string Message;

    [OSStructureField(
        Description = "Output file path or S3 key (for file-based operations)",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string OutputPath;

    [OSStructureField(
        Description = "Number of pages converted (for multi-page TIFFs)",
        DataType = OSDataType.Integer,
        IsMandatory = false)]
    public int PagesConverted;

    [OSStructureField(
        Description = "Output JPEG data as binary (for binary-based operations)",
        DataType = OSDataType.BinaryData,
        IsMandatory = false)]
    public byte[] OutputData;

    [OSStructureField(
        Description = "Detailed execution log for troubleshooting (timestamps, steps, diagnostics)",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string DetailedLog;
}

/// <summary>
/// Result structure for S3 pre-signed upload URL generation
/// </summary>
[OSStructure(Description = "Pre-signed S3 upload URL for direct browser uploads")]
public struct S3UploadUrlResult
{
    [OSStructureField(
        Description = "Indicates if URL generation was successful",
        DataType = OSDataType.Boolean,
        IsMandatory = true)]
    public bool Success;

    [OSStructureField(
        Description = "Status message or error details",
        DataType = OSDataType.Text,
        IsMandatory = true)]
    public string Message;

    [OSStructureField(
        Description = "Pre-signed URL for uploading (PUT request)",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string UploadUrl;

    [OSStructureField(
        Description = "S3 key where file will be uploaded",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string S3Key;

    [OSStructureField(
        Description = "URL expiration time (UTC)",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string ExpiresAt;
}

/// <summary>
/// Result structure for S3 file download operations
/// </summary>
[OSStructure(Description = "Result from downloading a file from S3")]
public struct S3DownloadResult
{
    [OSStructureField(
        Description = "Indicates if download was successful",
        DataType = OSDataType.Boolean,
        IsMandatory = true)]
    public bool Success;

    [OSStructureField(
        Description = "Status message or error details",
        DataType = OSDataType.Text,
        IsMandatory = true)]
    public string Message;

    [OSStructureField(
        Description = "Downloaded file as binary data",
        DataType = OSDataType.BinaryData,
        IsMandatory = false)]
    public byte[] FileData;

    [OSStructureField(
        Description = "Original filename extracted from S3 key",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string FileName;

    [OSStructureField(
        Description = "File content type (e.g., 'image/jpeg')",
        DataType = OSDataType.Text,
        IsMandatory = false)]
    public string ContentType;

    [OSStructureField(
        Description = "File size in bytes",
        DataType = OSDataType.LongInteger,
        IsMandatory = false)]
    public long FileSize;
}
