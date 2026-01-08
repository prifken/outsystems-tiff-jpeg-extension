# TiffConverter - TIFF to PDF/JPEG Converter for OutSystems ODC

Convert TIFF files to PDF or JPEG format using AWS S3 for seamless handling of large files in OutSystems Developer Cloud (ODC).

## Overview

TiffConverter is an External Logic library that enables TIFF image conversion in OutSystems ODC applications. It bypasses ODC's 5.5MB payload limit by using AWS S3 for file storage and processing, making it ideal for document management, scanning workflows, and archival systems.

## Key Features

- **Multi-format conversion**: Convert TIFF to PDF (multi-page) or JPEG (first page)
- **Intelligent compression**: PDF compression reduces file size by 70-90% while maintaining quality for OCR/LLM workflows
- **Large file support**: Handle files of any size using S3 pre-signed URLs
- **Quality control**: Adjustable quality settings (1-100) for both PDF and JPEG output
- **Multi-page support**: Preserve all pages when converting to PDF
- **S3 integration**: Direct upload/download URLs for browser-based file handling

## Prerequisites

- **OutSystems Developer Cloud (ODC)** environment
- **AWS Account** with S3 access
- **AWS S3 Bucket** configured for your files
- **AWS IAM Credentials** with S3 read/write permissions

## Installation

1. Download the latest release from [Forge](https://www.outsystems.com/forge/)
2. In ODC Portal, go to **Libraries** → **External Libraries**
3. Click **Upload Library** and select the downloaded ZIP file
4. Approve the library revision
5. Add the library to your ODC application

## Functions

### ConvertTiffS3

Convert TIFF files to PDF or JPEG format.

**Parameters:**
- `bucketName` (Text): Your S3 bucket name
- `inputS3Key` (Text): S3 path to input TIFF file (e.g., "uploads/document.tiff")
- `outputS3Key` (Text): S3 path for output file (e.g., "converted/document.pdf")
- `awsAccessKey` (Text): AWS Access Key ID
- `awsSecretKey` (Text): AWS Secret Access Key
- `awsRegion` (Text, optional): AWS region (default: "us-east-1")
- `outputFormat` (Text, optional): "PDF" or "JPEG" (default: "PDF")
- `quality` (Integer, optional): Quality 1-100 (default: 85)
- `compressPdf` (Boolean, optional): Compress PDF (default: true)

**Returns:** ConversionResult structure with Success, Message, OutputPath, PagesConverted, and DetailedLog

**Examples:**

```
// Convert TIFF to compressed PDF (recommended)
Result = TiffConverter.ConvertTiffS3(
    bucketName: "my-bucket",
    inputS3Key: "uploads/scan.tiff",
    outputS3Key: "converted/scan.pdf",
    awsAccessKey: "<your-access-key>",
    awsSecretKey: "<your-secret-key>",
    outputFormat: "PDF",
    quality: 85,
    compressPdf: True
)

// Convert TIFF to JPEG (first page only)
Result = TiffConverter.ConvertTiffS3(
    bucketName: "my-bucket",
    inputS3Key: "uploads/photo.tiff",
    outputS3Key: "converted/photo.jpg",
    awsAccessKey: "<your-access-key>",
    awsSecretKey: "<your-secret-key>",
    outputFormat: "JPEG",
    quality: 90
)
```

### GenerateS3UploadUrl

Generate a pre-signed URL for direct browser upload to S3 (bypasses ODC's 5.5MB limit).

**Parameters:**
- `bucketName` (Text): Your S3 bucket name
- `s3Key` (Text): Desired S3 path for uploaded file
- `awsAccessKey` (Text): AWS Access Key ID
- `awsSecretKey` (Text): AWS Secret Access Key
- `awsRegion` (Text, optional): AWS region (default: "us-east-1")
- `expirationMinutes` (Integer, optional): URL expiration (default: 15 minutes)

**Returns:** S3UploadUrlResult with UploadUrl, S3Key, and ExpiresAt

### GenerateS3DownloadUrl

Generate a pre-signed URL for direct browser download from S3.

**Parameters:**
- `bucketName` (Text): Your S3 bucket name
- `s3Key` (Text): S3 path of file to download
- `awsAccessKey` (Text): AWS Access Key ID
- `awsSecretKey` (Text): AWS Secret Access Key
- `awsRegion` (Text, optional): AWS region (default: "us-east-1")
- `expirationMinutes` (Integer, optional): URL expiration (default: 15 minutes)

**Returns:** S3DownloadUrlResult with DownloadUrl, S3Key, and ExpiresAt

### TestS3Connection

Verify AWS credentials and S3 connectivity.

**Parameters:**
- `awsAccessKey` (Text): AWS Access Key ID
- `awsSecretKey` (Text): AWS Secret Access Key
- `awsRegion` (Text, optional): AWS region (default: "us-east-1")

**Returns:** Text with connection status and accessible bucket list

### DownloadFileFromS3

Download small files (<5.5MB) directly as binary data.

**Parameters:**
- `bucketName` (Text): Your S3 bucket name
- `s3Key` (Text): S3 path of file to download
- `awsAccessKey` (Text): AWS Access Key ID
- `awsSecretKey` (Text): AWS Secret Access Key
- `awsRegion` (Text, optional): AWS region (default: "us-east-1")

**Returns:** S3DownloadResult with FileData, FileName, ContentType, and FileSize

**Note:** For files larger than 5.5MB, use GenerateS3DownloadUrl instead.

## Common Use Cases

### Document Scanning Workflow

1. User uploads TIFF scan via browser → S3 (using GenerateS3UploadUrl)
2. Convert TIFF to compressed PDF (using ConvertTiffS3)
3. Generate download link for user (using GenerateS3DownloadUrl)

### Multi-page Document Archival

1. Upload multi-page TIFF documents to S3
2. Convert to compressed PDF with quality=90 for archival
3. Store PDF in S3 for long-term retention

### Image Thumbnail Generation

1. Upload TIFF image to S3
2. Convert to JPEG (first page only) with quality=85
3. Use JPEG as thumbnail in your application

## Configuration Recommendations

### Quality Settings

- **OCR/LLM Processing**: quality=80-85 (good balance, ~75% size reduction)
- **Web Display**: quality=70-80 (smaller files, faster loading)
- **Archival/Print**: quality=90-95 (highest quality, larger files)

### PDF Compression

- **Enabled (compressPdf=true)**: 70-90% smaller files, ideal for most use cases
- **Disabled (compressPdf=false)**: Original quality/size, rarely needed

### AWS Region

Always specify the region where your S3 bucket is located for optimal performance:
- US East: "us-east-1"
- EU West: "eu-west-1"
- Asia Pacific: "ap-southeast-1"

## Security Best Practices

1. **Never hardcode AWS credentials** - Use site properties or secure vaults
2. **Use IAM users with minimal permissions** - Only grant S3 access to specific buckets
3. **Enable S3 bucket encryption** - Encrypt data at rest
4. **Set appropriate CORS policies** - If using pre-signed URLs from browsers
5. **Monitor AWS CloudTrail logs** - Track S3 access and conversions

## Troubleshooting

### Error: "The resource name 'icon.png' was not found"
This is a library packaging issue. Ensure you're using the latest version from Forge.

### Error: "AWS credentials cannot be empty"
Verify your AWS Access Key and Secret Key are correctly configured in your ODC application.

### Error: "File not found in S3"
Check that:
- The bucket name is correct
- The S3 key path is correct (e.g., "uploads/file.tiff", not "/uploads/file.tiff")
- Your IAM user has read permissions on the bucket

### Conversion fails with "Invalid image format"
Ensure the input file is a valid TIFF format. Some proprietary TIFF formats may not be supported.

### Pre-signed URL expired
Default expiration is 15 minutes. Generate a new URL or increase `expirationMinutes` parameter.

## Performance Notes

- **Processing time**: Typically 2-5 seconds per page for TIFF to PDF conversion
- **File size**: Compressed PDFs are 70-90% smaller than original TIFFs
- **Memory usage**: TIFF processing requires 2-3x file size in memory during conversion

### File Size Limits

While S3 supports files up to 5TB, practical conversion limits are imposed by ODC External Logic (AWS Lambda) constraints:

**Tested & Verified:**
- ✅ Files up to **100MB** work reliably
- ✅ Multi-page TIFFs with up to **50 pages** process successfully

**Expected to Work (pending testing):**
- ⚠️ Files **100MB - 500MB** should work, depending on page count and complexity
- ⚠️ Multi-page TIFFs with **50-200 pages** may work, but approach timeout limits

**May Exceed Limits:**
- ❌ Files **>500MB** may hit the 15-minute Lambda execution timeout
- ❌ TIFFs with **>200 pages** may timeout during processing
- ❌ Very high-resolution TIFFs may exceed Lambda's ~3GB memory limit

**Recommendation**: For very large files (>100MB) or high page counts (>50 pages), consider:
1. Testing with your specific files before production use
2. Splitting large multi-page TIFFs into smaller batches
3. Using lower quality settings to reduce processing time and memory usage

If you encounter timeout issues with large files, please report them on GitHub so we can investigate optimizations.

## Support

For issues, questions, or feature requests:
- **GitHub**: [outsystems-tiff-jpeg-extension](https://github.com/prifken/outsystems-tiff-jpeg-extension)
- **Forge**: Leave a comment on the library page

## Version History

### v0.1.x (Current)
- Initial release
- Unified ConvertTiffS3 function (PDF/JPEG)
- S3 pre-signed URL generation (upload/download)
- Custom conversion icon
- Comprehensive error logging

## License

MIT License - See LICENSE file for details

## Credits

Built for OutSystems Developer Cloud using the External Libraries SDK.

---

**Developed by**: Peter Rifken
**Powered by**: OutSystems ODC External Libraries SDK
