# OutSystems ODC Extension Development - Lessons Learned

**Last Updated:** 2026-01-08
**Project:** Image Converter Library for OutSystems ODC
**Purpose:** Knowledge base for developing and deploying OutSystems External Logic libraries

---

## Table of Contents

1. [OutSystems Extension Development](#outsystems-extension-development)
2. [Automated Deployment to ODC](#automated-deployment-to-odc)
3. [Project Structure & Configuration](#project-structure--configuration)
4. [Critical Issues & Solutions](#critical-issues--solutions)
5. [GitHub Actions Best Practices](#github-actions-best-practices)
6. [Testing & Validation](#testing--validation)
7. [Troubleshooting Guide](#troubleshooting-guide)

---

## OutSystems Extension Development

### 1.1 Required SDK and Setup

**Essential NuGet Package:**
```xml
<PackageReference Include="OutSystems.ExternalLibraries.SDK" Version="1.5.0" />
```

**Project Configuration (`.csproj`):**
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>

  <!-- CRITICAL: ODC Lambda deployment settings - DO NOT change -->
  <PublishTrimmed>false</PublishTrimmed>
  <PublishSingleFile>false</PublishSingleFile>
  <SelfContained>true</SelfContained>
</PropertyGroup>
```

**Why these settings matter:**
- `linux-x64`: ODC runs on AWS Lambda which uses Linux
- `PublishTrimmed>false`: Trimming can break reflection-based SDK functionality
- `PublishSingleFile>false`: ODC expects multi-file deployment packages
- `SelfContained>true`: All dependencies must be included in the ZIP

### 1.2 Interface and Implementation Pattern

**Always use interfaces for your external logic:**

```csharp
// IImageConverter.cs
using OutSystems.ExternalLibraries.SDK;

public interface IImageConverter
{
    [OSAction(
        Description = "Converts TIFF to JPEG",
        ReturnName = "result",
        ReturnDescription = "Conversion operation result")]
    ConversionResult ConvertTiffToJpeg(string inputPath, string outputPath, int quality = 85);
}

// ImageConverter.cs
public class ImageConverter : IImageConverter
{
    public ConversionResult ConvertTiffToJpeg(string inputPath, string outputPath, int quality = 85)
    {
        // Implementation
    }
}
```

**Best Practices:**
- Use `[OSAction]` attributes to expose methods to OutSystems
- Use `[OSStructure]` for complex data types (see Structures.cs)
- Use `[OSStructureField]` with proper DataType mappings
- Always provide clear descriptions - they show up in ODC Portal

### 1.3 Data Type Mapping

| C# Type | OSDataType | Notes |
|---------|------------|-------|
| `string` | `OSDataType.Text` | Standard text |
| `int` | `OSDataType.Integer` | 32-bit integer |
| `long` | `OSDataType.LongInteger` | 64-bit integer |
| `bool` | `OSDataType.Boolean` | True/false |
| `byte[]` | `OSDataType.BinaryData` | Files, images, binary content |
| `DateTime` | `OSDataType.DateTime` | UTC recommended |
| `decimal` | `OSDataType.Decimal` | Currency, precise numbers |

**CRITICAL:** Always use structures (not plain C# objects) for complex return types:

```csharp
[OSStructure(Description = "Result from conversion operation")]
public struct ConversionResult
{
    [OSStructureField(Description = "Success indicator", DataType = OSDataType.Boolean, IsMandatory = true)]
    public bool Success;

    [OSStructureField(Description = "Result message", DataType = OSDataType.Text, IsMandatory = true)]
    public string Message;
}
```

### 1.4 Large File Handling

**OutSystems has a ~5.5MB response payload limit.** For large files:

**DON'T:** Return large binary data directly
```csharp
// BAD - Will fail for files > 5.5MB
public ConversionResult Convert(byte[] input)
{
    return new ConversionResult { OutputData = largeByteArray }; // WILL FAIL
}
```

**DO:** Use S3 pre-signed URLs
```csharp
// GOOD - Return URLs, not data
public S3UploadUrlResult GenerateS3UploadUrl(string bucketName, string fileName) { }
public S3DownloadUrlResult GenerateS3DownloadUrl(string bucketName, string s3Key) { }
public ConversionResult ProcessFileFromS3(string bucketName, string inputKey, string outputKey) { }
```

**Pattern:**
1. OutSystems app requests pre-signed upload URL from extension
2. User/app uploads file directly to S3 (bypasses OutSystems)
3. OutSystems calls extension to process file (S3 → S3)
4. Extension returns pre-signed download URL
5. User/app downloads directly from S3

### 1.5 Standard Development & Testing Workflow

**CRITICAL:** This is the standard process for testing code changes in this project.

**Workflow:**
```
1. Local Development (in this directory with Claude Code)
   ↓
2. Git Commit & Push to GitHub (triggers CI/CD)
   ↓
3. GitHub Actions builds and deploys to ODC
   ↓
4. Approve library revision in ODC Portal
   ↓
5. Test the new functionality in ODC
```

**Why this process:**
- OutSystems ODC requires testing in the actual ODC environment
- Manual ZIP uploads are slow and error-prone
- Automated deployment ensures consistent builds
- Version tracking is automatic via GitHub Actions

**Commands to deploy for testing:**
```bash
# After making changes locally, stage ALL related files
git add <file1> <file2> <file3>  # Include code + docs together

# Single commit for related changes
git commit -m "Description of changes"

# Single push = single workflow run
git push origin main  # Triggers automatic deployment

# Then wait for GitHub Actions to complete
# Then approve in ODC Portal if required
# Then test in ODC
```

**DO NOT:**
- Manually build and upload ZIPs repeatedly
- Skip the CI/CD pipeline for testing
- Forget to push before asking to test
- **Push multiple times for related changes** (batches commits instead)

**Batch Related Changes:**
```bash
# BAD - 3 separate pushes = 3 workflow runs = 3 ODC revisions
git add file1 && git commit -m "Fix" && git push
git add file2 && git commit -m "Update docs" && git push
git add file3 && git commit -m "Fix docs" && git push

# GOOD - 1 push = 1 workflow run = 1 ODC revision
git add file1 file2 file3
git commit -m "Fix issue and update documentation"
git push origin main
```

**Why batching matters:**
- Each push triggers full build + deploy cycle (2-5 minutes)
- Each deployment creates a new ODC revision
- Multiple revisions for the same fix wastes GitHub Actions minutes
- Cleaner git history

**Documentation-Only Changes:**

**IMPORTANT:** Updating LESSONS_LEARNED.md or README.md should NOT trigger deployment workflows!

Currently, the workflow triggers on ANY push to main. This means documentation updates trigger unnecessary builds/deployments.

**Solution:** Add path filters to `deploy-to-odc.yml`:
```yaml
on:
  push:
    branches:
      - main
    paths-ignore:
      - '*.md'           # Ignore all markdown files
      - 'docs/**'        # Ignore docs directory
      - '.github/**'     # Ignore workflow changes (use workflow_dispatch to test)
```

**Why this matters:**
- Documentation updates = no code changes = no need to redeploy
- Saves GitHub Actions minutes
- Avoids creating empty ODC revisions
- Faster iteration on documentation

**Current workaround until workflow is updated:**
- Batch documentation updates with code changes when possible
- Or accept that docs-only pushes will trigger builds (they'll succeed quickly since code unchanged)

**Meta-lesson:** This very section was added after triggering 3 workflows when we should have triggered 1! 😄

**When you open this project in Claude Code:**
- Follow this workflow automatically for any code changes
- Always commit and push when changes need testing
- Batch related changes (code + docs) into single commits
- Include clear commit messages describing what changed

### 1.6 ODC Forge Preparation

**For publishing to OutSystems Forge, additional requirements apply:**

#### Custom Icons

Libraries need custom icons (default is a green circle). Icon must be:
- PNG format, recommended 64x64 or 96x96 pixels
- Placed in project root directory
- Referenced in `[OSInterface]` attribute
- Configured in `.csproj` to copy to output

**Implementation:**

```csharp
// IImageConverter.cs
[OSInterface(
    Name = "ImageConverter",
    Description = "TIFF to PDF/JPEG converter with AWS S3 integration",
    IconResourceName = "icon.png")]  // Add this line
public interface IImageConverter
{
    // ... actions
}
```

```xml
<!-- ImageConverterLibrary.csproj -->
<ItemGroup>
  <!-- Icon for ODC Forge - MUST be embedded resource, not copied file -->
  <EmbeddedResource Include="icon.png" />
</ItemGroup>
```

**CRITICAL:** The icon must be an `<EmbeddedResource>`, NOT `<None>` with `CopyToOutputDirectory`.
- ❌ **WRONG:** `<None>` with `CopyToOutputDirectory` - OutSystems can't find it
- ✅ **CORRECT:** `<EmbeddedResource>` - Embeds in DLL, OutSystems finds it

**CRITICAL:** The resource name must include the namespace prefix!

.NET embedded resources are named: `<RootNamespace>.<FileName>`

```csharp
// IImageConverter.cs
// ❌ WRONG - Missing namespace prefix
[OSInterface(
    Name = "ImageConverter",
    IconResourceName = "icon.png")]  // This won't work!

// ✅ CORRECT - Includes namespace from .csproj
[OSInterface(
    Name = "ImageConverter",
    IconResourceName = "ImageConverterLibrary.icon.png")]  // This works!
```

**How to determine the correct name:**
1. Check `<RootNamespace>` in your `.csproj` file (e.g., `ImageConverterLibrary`)
2. Add your icon filename: `<RootNamespace>.<IconFile>` (e.g., `ImageConverterLibrary.icon.png`)
3. Use this full name in `IconResourceName`

**Error if done incorrectly:**
```
The resource name 'icon.png' provided for the element 'ImageConverterLibrary.IImageConverter'
IconResourceName was not found. (OS-ELG-MODL-05009)
```

**Commit Reference:** `55ed569`

**Icon appears:**
- In ODC Portal library list
- In OutSystems Forge catalog
- In Service Studio when using the library

**Note:** The icon applies to the entire library (not individual functions).

#### API Simplification for Forge

**Problem:** Too many similar functions confuse users.

**Solution:** Consolidate related functions into unified functions with parameters.

**Example - Before:**
```csharp
ConversionResult ConvertTiffToJpegS3(...);
ConversionResult ConvertTiffToPdfS3(...);
ConversionResult ConvertTiffToCompressedPdfS3(...);
```

**After:**
```csharp
ConversionResult ConvertTiffS3(
    ...,
    string outputFormat = "PDF",     // "PDF" or "JPEG"
    int quality = 85,
    bool compressPdf = true          // Only for PDF
);
```

**Benefits:**
- Fewer functions to document
- Clearer API surface
- Easier to maintain
- Better user experience

**Implementation Pattern:**
1. Keep old internal implementations (make them `private`)
2. Create new public function that routes to appropriate internal implementation
3. Remove old functions from interface
4. Keep internal implementations for now (delete after testing confirms working)

**Commit Reference:** `6dc472f`

### 1.7 Logging and Debugging

**Always include detailed logging fields:**

```csharp
[OSStructureField(
    Description = "Detailed execution log for troubleshooting",
    DataType = OSDataType.Text,
    IsMandatory = false)]
public string DetailedLog;
```

**Implementation pattern:**
```csharp
public ConversionResult Convert(string input)
{
    var log = new StringBuilder();
    var startTime = DateTime.UtcNow;

    try
    {
        log.AppendLine($"=== Execution Log ===");
        log.AppendLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
        log.AppendLine($"Input: {input}");

        log.AppendLine("[STEP 1] Validating inputs...");
        // ... validation

        log.AppendLine("[STEP 2] Processing...");
        // ... processing

        return new ConversionResult
        {
            Success = true,
            Message = "Success",
            DetailedLog = log.ToString()
        };
    }
    catch (Exception ex)
    {
        log.AppendLine($"ERROR: {ex.Message}");
        log.AppendLine($"Stack Trace: {ex.StackTrace}");
        return new ConversionResult
        {
            Success = false,
            Message = ex.Message,
            DetailedLog = log.ToString()
        };
    }
}
```

**Benefits:**
- Easy troubleshooting in OutSystems logs
- Timestamps help identify performance bottlenecks
- Step-by-step execution tracking
- Full error context

---

## Automated Deployment to ODC

### 2.1 The Revision Detection Challenge

**CRITICAL INSIGHT:** OutSystems ODC uses content hashing to detect library changes.

**The Problem:**
- ODC computes a `modelDigest` hash from your library's compiled code
- If the hash is identical to a previous upload, **ODC rejects it** (returns existing revision)
- Simply changing version numbers, filenames, or metadata **DOES NOT** change the hash
- You cannot force new revisions without changing actual compiled code

**Failed Attempts:**
1. ✗ Unique ZIP filenames - ODC ignores filename
2. ✗ Different version numbers in build properties - Not part of hash
3. ✗ Different timestamps - Not part of hash
4. ✗ Non-deterministic builds - ODC still hashes final IL code

**THE SOLUTION:** Inject build metadata into runtime code

```csharp
// IImageConverter.cs
public interface IImageConverter
{
    /// <summary>
    /// Returns build version information
    /// This changes with every build to force unique revisions in ODC
    /// </summary>
    string GetBuildVersion();
}

// ImageConverter.cs
public class ImageConverter : IImageConverter
{
    public string GetBuildVersion()
    {
        // This line is updated by CI/CD on every build
        var buildMetadata = "BUILD_METADATA_PLACEHOLDER";
        return $"ImageConverter Build Version | {buildMetadata}";
    }
}
```

**GitHub Actions Workflow (Step 4):**
```powershell
- name: Inject Build Metadata
  shell: pwsh
  run: |
    $buildMetadata = "Run #${{ github.run_number }} | $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') UTC | Commit ${{ github.sha }}"

    $content = Get-Content ImageConverter.cs -Raw
    $content = $content -replace 'BUILD_METADATA_PLACEHOLDER', $buildMetadata
    $content | Out-File -FilePath ImageConverter.cs -Encoding UTF8 -NoNewline
```

**Why this works:**
- Modifies actual source code **before compilation**
- Changes public API surface (ODC detects this)
- Each build has different string literal in compiled IL code
- Forces different modelDigest → **new revision every time!**

**Result:** Every push to `main` creates revision 1, 2, 3, 4... automatically.

### 2.2 ODC API Deployment Flow

OutSystems ODC requires a specific 6-step process for automated deployments:

```
Step 1: Authenticate with ODC (OAuth2 client_credentials)
   ↓
Step 2: Get Pre-signed Upload URLs (GET /api/external-libraries/v1/uploads)
   ↓
Step 3: Upload ZIP to S3 (PUT to uploadUrl)
   ↓
Step 4: Start Generation Operation (POST /api/external-libraries/v1/generation-operations/)
   ↓
Step 5: Poll Operation Status (GET /api/external-libraries/v1/generation-operations/{key})
   ↓
Step 6: Tag Library Revision (PATCH /api/asset-repository/v1/assets/{key}/revisions/{number})
```

**Full workflow available in:** `.github/workflows/deploy-to-odc.yml`

### 2.3 Authentication (Step 1)

**Required GitHub Secrets:**
- `ODC_PORTAL_URL` - Your ODC portal URL (e.g., `https://your-org.outsystemscloud.com`)
- `ODC_CLIENT_ID` - API client ID from ODC Portal
- `ODC_CLIENT_SECRET` - API client secret

**Creating API Client in ODC Portal:**
1. Go to ODC Portal → Settings → API Management
2. Create new API client
3. Required permissions:
   - **Asset management > Create** (to upload libraries)
   - **Asset management > Read** (to query revisions)
   - **Asset management > Update** (to tag revisions)
4. Copy Client ID and Client Secret to GitHub Secrets

**Authentication Flow:**
```powershell
# Discover OIDC endpoints
$discoveryUrl = "${{ secrets.ODC_PORTAL_URL }}/identity/.well-known/openid-configuration"
$discovery = Invoke-RestMethod -Uri $discoveryUrl
$tokenEndpoint = $discovery.token_endpoint

# Request access token
$body = @{
    grant_type = "client_credentials"
    client_id = "${{ secrets.ODC_CLIENT_ID }}"
    client_secret = "${{ secrets.ODC_CLIENT_SECRET }}"
}
$response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body
$accessToken = $response.access_token
```

### 2.4 The ReadyForReview Status

**CRITICAL:** Some ODC environments require **manual approval** before creating library revisions.

**Status Flow:**
```
Generation operation submitted
        ↓
    Processing
        ↓
  ┌─────┴─────┐
  ↓           ↓
Completed   ReadyForReview  ← Manual approval required
  ↓           ↓
Auto-tag   (Wait for admin to approve in ODC Portal)
```

**How to handle in CI/CD:**
```powershell
if ($status -eq "Completed") {
    # Automatically tag revision
    Write-Host "✅ Generation completed - proceeding to tag revision"
}
elseif ($status -eq "ReadyForReview") {
    # Provide manual approval instructions
    Write-Host "⏸️  Manual approval required in ODC Portal"
    Write-Host "Go to: Libraries → External Libraries → Approve"
    # Exit successfully - upload worked, just needs approval
}
```

**DO NOT treat ReadyForReview as failure!** The upload succeeded, it just needs a human to click "Approve" in the ODC Portal.

### 2.5 Version Tagging Strategy

**Auto-increment patch versions:**
```powershell
# Query all existing revisions
$revisions = Invoke-RestMethod -Uri "$ODC_PORTAL_URL/api/asset-repository/v1/assets/$LIBRARY_KEY/revisions"

# Find latest version tag
$allTags = $revisions.results | Where-Object { $_.tag } | Select-Object -ExpandProperty tag
$latestTag = $allTags | Sort-Object -Descending | Select-Object -First 1

# Auto-increment
if ($latestTag -match '^(\d+)\.(\d+)\.(\d+)') {
    $major = [int]$matches[1]
    $minor = [int]$matches[2]
    $patch = [int]$matches[3] + 1
    $newTag = "$major.$minor.$patch"
}

# Tag the new revision
$body = @{
    tag = $newTag
    releaseNotes = "Automated deployment from GitHub Actions"
} | ConvertTo-Json

Invoke-RestMethod -Uri "$ODC_PORTAL_URL/api/asset-repository/v1/assets/$LIBRARY_KEY/revisions/$revisionNumber" `
    -Method Patch -Headers $headers -Body $body
```

---

## Project Structure & Configuration

### 3.1 Recommended File Structure

```
your-extension/
├── .github/
│   └── workflows/
│       ├── build-pr.yml          # PR validation workflow
│       └── deploy-to-odc.yml     # Main deployment workflow
├── IYourLibrary.cs               # Interface with [OSAction] attributes
├── YourLibrary.cs                # Implementation
├── Structures.cs                 # All [OSStructure] types
├── YourLibrary.csproj            # Project configuration
├── build.ps1                     # Local build script
├── README.md                     # Project documentation
├── LESSONS_LEARNED.md            # This file!
└── .gitignore                    # Exclude bin/, obj/, *.zip

```

### 3.2 Essential .gitignore Entries

```gitignore
# Build outputs
bin/
obj/
publish/

# Deployment packages
*.zip

# IDE files
.vs/
.vscode/
.idea/

# Claude Code
.claude/

# OS files
.DS_Store
Thumbs.db
```

### 3.3 Build Script Pattern

See `build.ps1` for a complete local build script. Key steps:

1. Restore NuGet packages
2. Build in Release configuration
3. Publish for `linux-x64` runtime
4. Create ZIP from publish directory
5. Validate package size and contents

**Usage:**
```powershell
# Standard build
.\build.ps1

# Clean build
.\build.ps1 -Clean
```

---

## Critical Issues & Solutions

### 4.1 Stream Already Closed Error

**Problem:** `System.ObjectDisposedException: Cannot access a closed Stream`

**Root Cause:** Disposing of streams while they're still being read (especially in async operations)

**Solution:**
```csharp
// BAD - Stream disposed before async operation completes
using (var memStream = new MemoryStream())
{
    await s3Client.GetObjectAsync(...); // Stream disposed here!
}

// GOOD - Keep stream alive until operation completes
var memStream = new MemoryStream();
try
{
    var response = await s3Client.GetObjectAsync(...);
    await response.ResponseStream.CopyToAsync(memStream);
    memStream.Position = 0;
    // Use memStream
}
finally
{
    memStream?.Dispose();
}

// BETTER - Use synchronous API in synchronous contexts
var memStream = new MemoryStream();
var response = s3Client.GetObjectAsync(...).GetAwaiter().GetResult();
response.ResponseStream.CopyTo(memStream);
memStream.Position = 0;
```

**Commit Reference:** `7896c1b`

### 4.2 Payload Size Errors

**Problem:** `System.Text.Json.JsonException: The maximum payload size for comments has been exceeded`

**Root Cause:** Too much logging output in GitHub Actions

**Solution:** Reduce logging verbosity in CI/CD
```csharp
// Development: Verbose logging
log.AppendLine($"Processing byte {i} of {total}...");

// Production: Summary logging only
log.AppendLine($"Processed {total} bytes in {duration}ms");
```

**Commit Reference:** `3f18b95`

### 4.3 Multi-page TIFF Handling

**Problem:** ImageSharp struggles with complex multi-page TIFFs

**Solution:** Implement fallback to ImageMagick
```csharp
try
{
    // Try ImageSharp first (faster, smaller)
    using var image = Image.Load(inputPath);
    // Process with ImageSharp
}
catch (Exception ex)
{
    log.AppendLine($"ImageSharp failed: {ex.Message}");
    log.AppendLine("Falling back to ImageMagick...");

    // Fallback to ImageMagick (handles complex TIFFs)
    using var images = new MagickImageCollection(inputPath);
    foreach (var page in images)
    {
        // Process each page
    }
}
```

**Required NuGet packages:**
```xml
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.7" />
<PackageReference Include="Magick.NET-Q8-AnyCPU" Version="14.2.0" />
```

**Commit Reference:** `5db0461`

### 4.4 JPEG Compression Quality

**Problem:** Default JPEG quality produced excessively large files

**Solution:** Explicitly set quality level
```csharp
var encoder = new JpegEncoder
{
    Quality = quality // User-specified quality (1-100)
};

image.Save(outputPath, encoder);
```

**Recommended values:**
- `85`: Good balance (default)
- `90-95`: High quality for archival
- `70-80`: Smaller files for web use

**Commit Reference:** `8331c95`

### 4.5 Lessons from API Consolidation (January 2026)

**Session Goal:** Consolidate 8 functions down to 5 for Forge readiness, add custom icon.

**Errors Made:**

**Error 1: Using sed for complex C# modifications**
```bash
# BAD - This broke the file structure
sed -i '/^public class ImageConverter/a\
    public ConversionResult ConvertTiffS3(...) { ... }'
```
**Problem:**
- Sed's escape sequences are complex for multi-line C# code
- String interpolation conflicts with shell escaping
- File structure easily corrupted

**Solution:** Use PowerShell for Windows systems, or simple append operations
```powershell
# GOOD - Simple text replacement
$content = Get-Content ImageConverter.cs -Raw
$content = $content -replace 'placeholder', 'new value'
$content | Out-File ImageConverter.cs
```

**Error 2: Creating temporary partial class files**
```csharp
// ImageConverter_NEW.cs - BAD IDEA
public partial class ImageConverter
{
    public ConversionResult ConvertTiffS3(...) { }
}
```
**Problem:**
- Forgot to mark original class as `partial`
- Caused duplicate definition errors
- Created confusion in build system

**Solution:** Either edit original file directly OR use git to manage versions
```bash
# If edit goes wrong, just revert
git checkout ImageConverter.cs
# Then try again with simpler approach
```

**Error 3: Modifying files without reading them first**
**Problem:** Edit tool requires file to be read before writing
**Solution:** Always read files before attempting edits in Claude Code

**What Worked:**

**1. Interface-first approach:**
- Updated `IImageConverter.cs` to remove old functions
- This defined the public API contract
- Then implemented routing in `ImageConverter.cs`

**2. Keeping internal implementations:**
```csharp
// Public API
public ConversionResult ConvertTiffS3(...)
{
    if (format == "JPEG")
        return ConvertTiffToJpegS3Internal(...);  // Still exists, just private
    // ...
}

// Private implementation (renamed, not deleted)
private ConversionResult ConvertTiffToJpegS3Internal(...) { }
```
**Benefits:**
- No need to rewrite complex logic
- Easy to test incrementally
- Can delete later after confirming it works

**3. Simple sed usage for adding methods:**
```bash
# This worked - simple pattern matching and insertion
sed -i '/^public class ImageConverter : IImageConverter$/,/^{$/{
    /^{$/a\
    public ConversionResult ConvertTiffS3(...) { ... }
}' ImageConverter.cs
```
**Key:** Keep the inserted code simple, avoid complex string manipulation

**Best Practices Learned:**
1. For complex file modifications in Windows environments, prefer PowerShell over bash/sed
2. When consolidating APIs, route to existing implementations rather than rewriting
3. Use git as safety net - commit before risky changes, revert if needed
4. Test locally before pushing (we built with `dotnet build` to verify)
5. Icons must be in `.csproj` with `<CopyToOutputDirectory>Always</CopyToOutputDirectory>`

**Testing Process:**
```bash
# 1. Make changes
# 2. Build locally to verify compiles
dotnet build ImageConverterLibrary.csproj

# 3. If build succeeds, commit and push
git add <files>
git commit -m "message"
git push origin main

# 4. Monitor GitHub Actions
# 5. Test in ODC
```

**Time Saved:** By keeping internal implementations and just adding a router function, we avoided rewriting ~800 lines of complex conversion logic.

**Commit Reference:** `6dc472f`

### 4.6 ODC External Logic Output Payload Limit (CRITICAL)

**Problem:** `OS-ELRT-60009 [Communication] Output payload is too large (7MB), maximum allowed is 5.5MB`

**Root Cause:** ODC External Logic has a **strict 5.5MB limit** on response payloads. The `DetailedLog` field in ConversionResult was accumulating megabytes of diagnostic information for large multi-page TIFFs, exceeding this limit.

**Symptoms:**
- Conversion succeeds in Lambda (output file appears in S3)
- But ODC returns generic error: "Something went wrong on our side"
- Browser console shows payload size error
- DetailedLog for 200MB TIFF with 50+ pages = ~7MB of text

**Why it happens:**
- Large files have many pages to process
- Each page generates logging output (compression stats, timestamps, etc.)
- Even with `verboseLogging = magickImages.Count <= 5`, summary logs still accumulate
- Multiple conversion steps × many pages = multi-megabyte logs

**Solution:** Return minimal logs on success, detailed logs only on failure
```csharp
// BAD - Returns full detailed log even on success (can be 7MB+)
return new ConversionResult
{
    Success = true,
    Message = "...",
    DetailedLog = log.ToString()  // Contains all diagnostic output
};

// GOOD - Returns minimal summary on success (< 500 bytes)
var successLog = new StringBuilder();
successLog.AppendLine("=== Conversion Summary ===");
successLog.AppendLine($"Status: SUCCESS");
successLog.AppendLine($"Input: {inputSizeMB:F2} MB");
successLog.AppendLine($"Output: {outputSizeMB:F2} MB");
successLog.AppendLine($"Pages: {pageCount}");
successLog.AppendLine($"Quality: {quality}");
successLog.AppendLine($"Size Reduction: {sizeReduction:F1}%");
successLog.AppendLine($"Duration: {duration:F1} seconds");

return new ConversionResult
{
    Success = true,
    Message = "...",
    DetailedLog = successLog.ToString()  // Only essential summary
};

// On FAILURE - Still return full detailed log for debugging
catch (Exception ex)
{
    log.AppendLine($"ERROR: {ex.Message}");
    log.AppendLine($"Stack: {ex.StackTrace}");
    return new ConversionResult
    {
        Success = false,
        DetailedLog = log.ToString()  // Full diagnostics needed for debugging
    };
}
```

**Key Insight:**
- **Success cases don't need verbose logs** - user just wants to know it worked
- **Failure cases NEED verbose logs** - to diagnose what went wrong
- This dramatically reduces payload size (from 7MB to < 1KB) while preserving debugging capability

**ODC Payload Limit applies to:**
- Output from External Logic functions
- Binary data (byte arrays)
- Text fields (like DetailedLog)
- Structure fields combined

**Testing Strategy:**
1. Use `GetS3FileInfo()` diagnostic (quick HEAD request, < 1KB response)
2. Try conversion with `quality=25` first (faster processing, smaller logs)
3. If success, increase quality gradually
4. For very large files (>200MB), consider lower quality to stay within execution time limits

**Related Issue:** Initial suspicion was "SSL handshake error" or "timeout" because error appeared after 20-30 seconds. Actually the conversion was succeeding, but the response was too large to return to ODC.

**Commit Reference:** `2b6adc6`

---

## GitHub Actions Best Practices

### 5.1 Two-Workflow Strategy

**build-pr.yml** - Validates pull requests
- Runs on PR open/sync/reopen
- Compiles code
- Creates deployment package
- Validates package structure
- Provides build summary
- **Does NOT deploy to ODC**

**deploy-to-odc.yml** - Deploys to ODC
- Runs on push to `main` (after PR merge)
- Can be manually triggered via `workflow_dispatch`
- Builds and deploys to ODC
- Tags revisions automatically
- Provides deployment summary

**Benefits:**
- PRs are validated before merge
- Only approved code reaches ODC
- Manual deployments possible when needed

### 5.2 Environment Variables Pattern

```yaml
env:
  DOTNET_VERSION: '8.0.x'
  PROJECT_FILE: 'ImageConverterLibrary.csproj'
  ARTIFACT_NAME: 'ImageConverterLibrary'
```

Define once at workflow level, reuse in all steps.

### 5.3 Diagnostic Logging in CI/CD

The deployment workflow includes extensive diagnostics:

```powershell
Write-Host "════════════════════════════════════════════════════════════════"
Write-Host "STEP 1: AUTHENTICATION - DIAGNOSTIC MODE"
Write-Host "════════════════════════════════════════════════════════════════"

# Network diagnostics
$dnsResult = Resolve-DnsName -Name $portalDomain
Write-Host "DNS Resolution: $($dnsResult.IPAddress -join ', ')"

# Connectivity test
$testConnection = Test-NetConnection -ComputerName $portalDomain -Port 443
Write-Host "TCP Connection: $($testConnection.TcpTestSucceeded)"
```

**Benefits:**
- Easy troubleshooting when workflows fail
- Network issues immediately visible
- Clear step-by-step progress tracking

### 5.4 Build Summaries

Use GitHub's step summary feature:

```powershell
Write-Output "## 🎉 Deployment Successful" >> $env:GITHUB_STEP_SUMMARY
Write-Output "" >> $env:GITHUB_STEP_SUMMARY
Write-Output "**Library Key:** $libraryKey" >> $env:GITHUB_STEP_SUMMARY
Write-Output "**Version:** $versionTag" >> $env:GITHUB_STEP_SUMMARY
```

Appears in GitHub Actions UI for quick status overview.

---

## Testing & Validation

### 6.1 Local Testing Before Deployment

**Always test locally first:**

```powershell
# 1. Build locally
.\build.ps1 -Clean

# 2. Verify ZIP contents
Expand-Archive ImageConverterLibrary.zip -DestinationPath ./test-extract

# 3. Check for required files
Test-Path ./test-extract/ImageConverterLibrary.dll
Test-Path ./test-extract/OutSystems.ExternalLibraries.SDK.dll

# 4. Check package size (should be < 50MB)
(Get-Item ImageConverterLibrary.zip).Length / 1MB
```

### 6.2 PR Validation Workflow

The `build-pr.yml` workflow validates:
- ✅ Code compiles without errors
- ✅ All NuGet dependencies restore
- ✅ Project publishes for linux-x64
- ✅ Deployment package created
- ✅ Required files present in package
- ✅ Package structure is valid

### 6.3 Manual Upload Testing

Before automating deployment, test manual upload:

1. Build locally: `.\build.ps1`
2. Go to ODC Portal → Libraries → External Libraries
3. Click "Upload Library"
4. Upload the generated ZIP file
5. Verify it processes correctly

Once manual upload works, automation will work.

---

## Troubleshooting Guide

### 7.1 Common Error Messages

| Error | Cause | Solution |
|-------|-------|----------|
| "Cannot access a closed Stream" | Stream disposed prematurely | Keep streams alive until async operations complete |
| "The maximum payload size has been exceeded" | Too much logging | Reduce logging verbosity in production |
| "401 Unauthorized" | Invalid API credentials | Verify ODC_CLIENT_ID and ODC_CLIENT_SECRET |
| "No operationKey in response" | API request failed | Check API client permissions in ODC Portal |
| "Generation failed" | Code compilation error in ODC | Check for missing dependencies or incompatible code |
| "modelDigest unchanged" | No code changes detected | Ensure GetBuildVersion() is updating with unique values |

### 7.2 Debugging Deployment Failures

**Step 1 fails (Authentication):**
- Verify `ODC_PORTAL_URL` is correct (no trailing slash)
- Check API client exists in ODC Portal
- Verify client ID and secret are correct
- Check API client has required permissions

**Step 2 fails (Get Upload URL):**
- Verify API client has "Asset management > Create" permission
- Check access token is valid (from Step 1)

**Step 3 fails (Upload to S3):**
- Network connectivity issue
- File too large (check limits)
- S3 permissions issue (rare - ODC manages this)

**Step 4 fails (Start Generation):**
- Invalid ZIP structure
- Missing required files (*.dll, deps.json)
- Check fileName and downloadUrl are correct

**Step 5 fails (Poll Operation):**
- If status is "Failed": Compilation error in library code
- If timeout: Operation taking too long (increase polling time)
- If "ReadyForReview": Normal - requires manual approval

**Step 6 fails (Tag Revision):**
- Only runs if status is "Completed"
- If revision number not found: API response structure changed
- If tag already exists: Version conflict

### 7.3 Checking ODC Portal Logs

When automated deployment succeeds but library doesn't work:

1. ODC Portal → Libraries → External Libraries
2. Find your library
3. Click on revision
4. View "Generation Logs"
5. Look for errors or warnings

Common issues:
- Missing NuGet packages
- Platform incompatibility (must be linux-x64)
- SDK version mismatch

### 7.4 Testing Individual Workflow Steps

You can test individual steps of the deployment workflow:

```powershell
# Test authentication
$tokenResponse = Invoke-RestMethod -Uri "$ODC_PORTAL_URL/auth/realms/.../token" -Method Post -Body @{
    grant_type = "client_credentials"
    client_id = "your-client-id"
    client_secret = "your-secret"
}

# Test upload URL generation
$uploadResponse = Invoke-RestMethod `
    -Uri "$ODC_PORTAL_URL/api/external-libraries/v1/uploads" `
    -Method Get `
    -Headers @{ Authorization = "Bearer $($tokenResponse.access_token)" }
```

---

## Appendix: Quick Reference Checklist

### New Project Setup Checklist

- [ ] Create .NET 8 project with `RuntimeIdentifier>linux-x64`
- [ ] Add OutSystems.ExternalLibraries.SDK NuGet package
- [ ] Set `PublishTrimmed>false`, `PublishSingleFile>false`, `SelfContained>true`
- [ ] Create interface with `[OSAction]` attributes
- [ ] Create implementation class
- [ ] Define structures with `[OSStructure]` and `[OSStructureField]`
- [ ] Add GetBuildVersion() method with BUILD_METADATA_PLACEHOLDER
- [ ] Create build.ps1 script
- [ ] Set up .gitignore (exclude bin/, obj/, *.zip)
- [ ] Test local build: `.\build.ps1 -Clean`
- [ ] Test manual upload to ODC Portal
- [ ] Create API client in ODC Portal with proper permissions
- [ ] Set up GitHub Secrets (ODC_PORTAL_URL, ODC_CLIENT_ID, ODC_CLIENT_SECRET)
- [ ] Copy and adapt .github/workflows/ from this project
- [ ] Test PR workflow
- [ ] Test deployment workflow
- [ ] Document your specific extension in README.md

### Pre-Deployment Checklist

- [ ] Code compiles locally without errors
- [ ] All tests pass (if you have tests)
- [ ] Build script completes successfully
- [ ] ZIP package < 50MB
- [ ] All required DLLs present in publish folder
- [ ] Manual upload to ODC works
- [ ] PR build workflow passes
- [ ] GitHub Secrets configured correctly
- [ ] API client permissions verified

---

## Contributing to This Document

As you encounter new issues or discover better patterns, **update this document**:

1. Add new sections for new problem categories
2. Update version numbers when dependencies change
3. Add commit references for significant fixes
4. Include code examples for clarity
5. Keep the troubleshooting guide up to date

This document should evolve with the project!

---

**Project Repository:** [outsystems-tiff-jpeg-extension](https://github.com/your-username/outsystems-tiff-jpeg-extension)
**OutSystems Documentation:** [External Libraries SDK](https://success.outsystems.com/documentation/outsystems_developer_cloud/building_apps/extend_your_apps_with_external_logic/)
**Created by:** Claude Code & Peter
**License:** MIT
