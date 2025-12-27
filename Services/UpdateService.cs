using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Linq;
using ATS_TwoWheeler_Simulator.Core;

namespace ATS_TwoWheeler_Simulator.Services
{
    /// <summary>
    /// Handles checking GitHub Releases for newer versions and downloading update packages.
    /// This class is purely client-side (no UI) and is driven by MainWindow.
    /// </summary>
    public sealed class UpdateService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        // NOTE: This must match your real GitHub repository so the client can reach Releases API.
        // Update these to match your simulator repository
        private const string RepositoryOwner = "shubhamavl";  // Update with your GitHub username
        private const string RepositoryName = "ATS_TwoWheeler_Simulator";  // Update with your repository name

        // We look for an asset that contains this prefix and has .zip extension.
        // Example: ATS_TwoWheeler_Simulator_Portable_v1.2.0.zip
        private const string AssetNameSubstring = "ATS_TwoWheeler_Simulator_Portable";
        private const string AssetExtension = ".zip";

        // Allowed download domains for security (only GitHub)
        private static readonly string[] AllowedDomains = { "github.com", "githubusercontent.com" };

        // Rate limiting: minimum time between update checks (1 hour)
        private static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromHours(1);
        private static readonly string LastCheckTimeFile = Path.Combine(PathHelper.ApplicationDirectory, "Data", "last_update_check.txt");

        public sealed class UpdateInfo
        {
            public Version CurrentVersion { get; init; } = new Version(0, 0, 0, 0);
            public Version LatestVersion { get; init; } = new Version(0, 0, 0, 0);
            public string DownloadUrl { get; init; } = string.Empty;
            public string AssetFileName { get; init; } = string.Empty;
            public string? ReleaseNotes { get; init; }
            public string? ExpectedSha256Hash { get; init; }  // SHA-256 hash for integrity verification

            public bool IsUpdateAvailable => LatestVersion.CompareTo(CurrentVersion) > 0;
        }

        public sealed class UpdateCheckResult
        {
            public UpdateInfo? Info { get; init; }
            public string? ErrorMessage { get; init; }
            public bool IsRateLimited { get; init; }
            public bool IsNetworkError { get; init; }
            public bool IsSuccess => Info != null && ErrorMessage == null;

            public static UpdateCheckResult Success(UpdateInfo info) => new() { Info = info };
            public static UpdateCheckResult RateLimited() => new() { ErrorMessage = "Update check was performed recently. Please wait at least 1 hour between checks.", IsRateLimited = true };
            public static UpdateCheckResult NetworkError(string message) => new() { ErrorMessage = message, IsNetworkError = true };
            public static UpdateCheckResult Error(string message) => new() { ErrorMessage = message };
        }

        /// <summary>
        /// Queries GitHub Releases API for the latest release and compares it with the current app version.
        /// Returns detailed result with error information for better user feedback.
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var currentVersion = GetCurrentVersion();
                GithubReleaseDto? latest;
                try
                {
                    latest = await GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException httpEx)
                {
                    return UpdateCheckResult.NetworkError($"Network error: {httpEx.Message}. Please check your internet connection.");
                }
                catch (TaskCanceledException)
                {
                    return UpdateCheckResult.NetworkError("Update check timed out. Please check your internet connection and try again.");
                }
                catch (Exception ex)
                {
                    return UpdateCheckResult.Error($"Failed to connect to GitHub: {ex.Message}");
                }

                if (latest == null)
                {
                    return UpdateCheckResult.Error("No releases found on GitHub. The repository may not have any published releases yet.");
                }

                // Find a suitable asset
                var asset = latest.Assets?.Find(a =>
                    !string.IsNullOrEmpty(a.BrowserDownloadUrl) &&
                    !string.IsNullOrEmpty(a.Name) &&
                    a.Name.Contains(AssetNameSubstring, StringComparison.OrdinalIgnoreCase) &&
                    a.Name.EndsWith(AssetExtension, StringComparison.OrdinalIgnoreCase));

                if (asset == null)
                {
                    return UpdateCheckResult.Error($"Release '{latest.TagName}' found, but no matching portable ZIP file was found. Expected filename containing '{AssetNameSubstring}'.");
                }

                // Validate download URL is from allowed domain
                if (string.IsNullOrEmpty(asset.BrowserDownloadUrl) || !IsValidDownloadUrl(asset.BrowserDownloadUrl))
                {
                    return UpdateCheckResult.Error("Download URL validation failed. The release asset URL is not from a trusted source.");
                }

                var latestVersion = ParseVersionFromTag(latest.TagName);
                if (latestVersion == null)
                {
                    return UpdateCheckResult.Error($"Could not parse version from release tag '{latest.TagName}'. Expected format: v1.2.3 or 1.2.3");
                }

                // Extract SHA-256 hash from release notes
                var expectedHash = ExtractSha256FromReleaseNotes(latest.Body);

                return UpdateCheckResult.Success(new UpdateInfo
                {
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    DownloadUrl = asset.BrowserDownloadUrl ?? string.Empty,
                    AssetFileName = asset.Name ?? "update.zip",
                    ReleaseNotes = latest.Body,
                    ExpectedSha256Hash = expectedHash
                });
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Error($"Unexpected error: {ex.Message}");
            }
        }

        public sealed class DownloadResult
        {
            public string? FilePath { get; init; }
            public string? ErrorMessage { get; init; }
            public bool IsSuccess => FilePath != null && ErrorMessage == null;
            public bool IsNetworkError { get; init; }
            public bool IsHashMismatch { get; init; }

            public static DownloadResult Success(string filePath) => new() { FilePath = filePath };
            public static DownloadResult NetworkError(string message) => new() { ErrorMessage = message, IsNetworkError = true };
            public static DownloadResult HashMismatch(string message) => new() { ErrorMessage = message, IsHashMismatch = true };
            public static DownloadResult Error(string message) => new() { ErrorMessage = message };
        }

        /// <summary>
        /// Downloads the update package to the local Update directory and verifies its integrity.
        /// </summary>
        public async Task<DownloadResult> DownloadUpdateAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.DownloadUrl))
                return DownloadResult.Error("Invalid update information: download URL is missing.");

            // Validate URL again before downloading
            if (!IsValidDownloadUrl(info.DownloadUrl))
            {
                return DownloadResult.Error($"Download URL validation failed. URL is not from a trusted source: {info.DownloadUrl}");
            }

            try
            {
                string updateDir = PathHelper.GetUpdateDirectory();
                string targetPath = Path.Combine(updateDir, info.AssetFileName);

                // Check if directory exists and is writable
                try
                {
                    if (!Directory.Exists(updateDir))
                    {
                        Directory.CreateDirectory(updateDir);
                    }
                }
                catch (Exception ex)
                {
                    return DownloadResult.Error($"Cannot create update directory: {ex.Message}. Check file permissions.");
                }

                // Delete existing file if it exists
                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Delete(targetPath);
                    }
                    catch { }
                }

                using var response = await HttpClient.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var errorMsg = $"HTTP {statusCode}: {response.ReasonPhrase}";
                    
                    if (statusCode == 404)
                        return DownloadResult.Error($"Update file not found on server (404). The release asset may have been removed.");
                    if (statusCode == 403)
                        return DownloadResult.Error($"Access denied (403). GitHub may be rate limiting requests.");
                    if (statusCode >= 500)
                        return DownloadResult.NetworkError($"Server error ({statusCode}). Please try again later.");
                    
                    return DownloadResult.Error($"Download failed: {errorMsg}");
                }

                var contentLength = response.Content.Headers.ContentLength;

                // Download to temporary file first, then rename to avoid file locking issues
                string tempPath = targetPath + ".tmp";
                
                await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;

                    while ((read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        totalRead += read;

                        if (contentLength.HasValue && contentLength.Value > 0 && progress != null)
                        {
                            double percent = (double)totalRead / contentLength.Value * 100.0;
                            progress.Report(percent);
                        }
                    }
                }

                // File stream is now closed, rename temp file to final location
                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Delete(targetPath);
                    }
                    catch { }
                }

                File.Move(tempPath, targetPath);

                // Verify file exists and has content
                if (!File.Exists(targetPath))
                {
                    return DownloadResult.Error("Download completed but file was not found on disk.");
                }

                var fileInfo = new FileInfo(targetPath);
                if (fileInfo.Length == 0)
                {
                    try { File.Delete(targetPath); } catch { }
                    return DownloadResult.Error("Downloaded file is empty (0 bytes). The file may be corrupted.");
                }

                // Verify SHA-256 hash if available
                if (!string.IsNullOrWhiteSpace(info.ExpectedSha256Hash))
                {
                    var hashResult = await VerifyFileHashAsync(targetPath, info.ExpectedSha256Hash);
                    if (!hashResult.IsValid)
                    {
                        try { File.Delete(targetPath); } catch { }
                        return DownloadResult.HashMismatch(
                            $"File integrity check failed. The downloaded file does not match the expected SHA-256 hash.\n\n" +
                            $"Expected: {info.ExpectedSha256Hash}\n" +
                            $"Computed: {hashResult.ComputedHash}\n\n" +
                            $"Please try downloading again.");
                    }
                }

                return DownloadResult.Success(targetPath);
            }
            catch (HttpRequestException httpEx)
            {
                return DownloadResult.NetworkError($"Network error: {httpEx.Message}. Please check your internet connection and try again.");
            }
            catch (TaskCanceledException)
            {
                return DownloadResult.NetworkError("Download timed out. Please check your internet connection and try again.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return DownloadResult.Error($"Permission denied: {ex.Message}. Please run the application with administrator privileges or check file permissions.");
            }
            catch (IOException ioEx)
            {
                return DownloadResult.Error($"File system error: {ioEx.Message}. Check disk space and file permissions.");
            }
            catch (Exception ex)
            {
                return DownloadResult.Error($"Unexpected error: {ex.Message}");
            }
        }

        private sealed class HashVerificationResult
        {
            public bool IsValid { get; init; }
            public string ComputedHash { get; init; } = string.Empty;
        }

        private async Task<HashVerificationResult> VerifyFileHashAsync(string filePath, string expectedHash)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (!File.Exists(filePath))
                        return new HashVerificationResult { IsValid = false, ComputedHash = "FILE_NOT_FOUND" };

                    using var sha256 = SHA256.Create();
                    await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var computedHash = sha256.ComputeHash(fileStream);
                    var computedHashString = BitConverter.ToString(computedHash).Replace("-", "").ToUpperInvariant();

                    var expectedHashUpper = expectedHash.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

                    return new HashVerificationResult
                    {
                        IsValid = string.Equals(computedHashString, expectedHashUpper, StringComparison.OrdinalIgnoreCase),
                        ComputedHash = computedHashString
                    };
                }
                catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process") && attempt < maxRetries)
                {
                    await Task.Delay(retryDelayMs * attempt);
                    continue;
                }
                catch (UnauthorizedAccessException) when (attempt < maxRetries)
                {
                    await Task.Delay(retryDelayMs * attempt);
                    continue;
                }
                catch
                {
                    return new HashVerificationResult { IsValid = false, ComputedHash = "VERIFICATION_ERROR" };
                }
            }

            return new HashVerificationResult { IsValid = false, ComputedHash = "MAX_RETRIES_EXCEEDED" };
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.Add("User-Agent", "ATS_TwoWheeler_Simulator/UpdateChecker");
            return client;
        }

        private async Task<GithubReleaseDto?> GetLatestReleaseAsync(CancellationToken cancellationToken)
        {
            string url = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
            var response = await HttpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GithubReleaseDto>(response);
        }

        private Version? ParseVersionFromTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return null;

            // Remove leading 'v' if present (e.g., "v1.2.3" -> "1.2.3")
            string versionString = tagName.TrimStart('v', 'V');
            
            if (Version.TryParse(versionString, out var version))
                return version;

            return null;
        }

        private string? ExtractSha256FromReleaseNotes(string? releaseNotes)
        {
            if (string.IsNullOrWhiteSpace(releaseNotes))
                return null;

            // Pattern 1: **SHA-256 Hash:** `hash`
            var pattern1 = @"(?i)\*\*SHA-256\s+Hash:\*\*\s*`([a-fA-F0-9]{64})`";
            var match1 = Regex.Match(releaseNotes, pattern1);
            if (match1.Success && match1.Groups.Count > 1)
            {
                return match1.Groups[1].Value;
            }

            // Pattern 2: SHA-256: `hash`
            var pattern2 = @"(?i)SHA-256:\s*`([a-fA-F0-9]{64})`";
            var match2 = Regex.Match(releaseNotes, pattern2);
            if (match2.Success && match2.Groups.Count > 1)
            {
                return match2.Groups[1].Value;
            }

            return null;
        }

        private bool IsValidDownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                var uri = new Uri(url);
                return AllowedDomains.Any(domain => uri.Host.EndsWith(domain, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private Version GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (!string.IsNullOrWhiteSpace(infoAttr?.InformationalVersion)
                    && Version.TryParse(infoAttr.InformationalVersion.Split('+')[0], out var infoVersion))
                {
                    return infoVersion;
                }

                var asmVersion = assembly.GetName().Version;
                if (asmVersion != null)
                    return asmVersion;
            }
            catch
            {
                // Fall through to default
            }

            return new Version(1, 0, 0, 0);
        }

        // DTOs for GitHub API
        private sealed class GithubReleaseDto
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("body")]
            public string? Body { get; set; }

            [JsonPropertyName("assets")]
            public List<GithubAssetDto>? Assets { get; set; }
        }

        private sealed class GithubAssetDto
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}

