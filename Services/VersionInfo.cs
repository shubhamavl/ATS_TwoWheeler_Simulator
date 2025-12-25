using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace ATS_TwoWheeler_Simulator.Services
{
    /// <summary>
    /// Version information service - provides application version and Git information
    /// </summary>
    public class VersionInfo
    {
        private static VersionInfo? _instance;
        private static readonly object _lock = new object();

        private string? _gitCommitHash;
        private string? _gitBranch;
        private DateTime? _buildDate;
        private string? _informationalVersion;

        private VersionInfo()
        {
            LoadVersionInfo();
        }

        public static VersionInfo Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new VersionInfo();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Get application version (e.g., "3.1.0")
        /// </summary>
        public string Version
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "Unknown";
            }
        }

        /// <summary>
        /// Get full version with build number (e.g., "3.1.0.0")
        /// </summary>
        public string FullVersion
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version != null ? version.ToString() : "Unknown";
            }
        }

        /// <summary>
        /// Get Git commit hash (short, 7 characters)
        /// </summary>
        public string GitCommitHash => _gitCommitHash ?? "Unknown";

        /// <summary>
        /// Get Git branch name
        /// </summary>
        public string GitBranch => _gitBranch ?? "Unknown";

        /// <summary>
        /// Get build date
        /// </summary>
        public DateTime BuildDate => _buildDate ?? DateTime.MinValue;

        /// <summary>
        /// Get informational version (includes Git info if available)
        /// </summary>
        public string InformationalVersion => _informationalVersion ?? FullVersion;

        /// <summary>
        /// Get formatted version string for display
        /// </summary>
        public string DisplayVersion
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append($"Version {FullVersion}");
                
                if (!string.IsNullOrEmpty(_gitCommitHash) && _gitCommitHash != "Unknown")
                {
                    sb.Append($" (Commit: {_gitCommitHash.Substring(0, Math.Min(7, _gitCommitHash.Length))})");
                }
                
                if (!string.IsNullOrEmpty(_gitBranch) && _gitBranch != "Unknown")
                {
                    sb.Append($" [{_gitBranch}]");
                }
                
                return sb.ToString();
            }
        }

        /// <summary>
        /// Get detailed version information
        /// </summary>
        public string DetailedInfo
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Application Version: {FullVersion}");
                sb.AppendLine($"Build Date: {BuildDate:yyyy-MM-dd HH:mm:ss}");
                
                if (!string.IsNullOrEmpty(_gitCommitHash) && _gitCommitHash != "Unknown")
                {
                    sb.AppendLine($"Git Commit: {_gitCommitHash}");
                }
                
                if (!string.IsNullOrEmpty(_gitBranch) && _gitBranch != "Unknown")
                {
                    sb.AppendLine($"Git Branch: {_gitBranch}");
                }
                
                var baseDirectory = AppContext.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDirectory))
                {
                    sb.AppendLine($"Build Location: {baseDirectory}");
                }
                
                return sb.ToString();
            }
        }

        private void LoadVersionInfo()
        {
            try
            {
                // Get informational version from assembly
                var assembly = Assembly.GetExecutingAssembly();
                var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoVersion != null)
                {
                    _informationalVersion = infoVersion.InformationalVersion;
                }

                // Try to get build date from assembly
                var buildDateAttr = assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
                if (buildDateAttr != null && buildDateAttr.Key == "BuildDate")
                {
                    if (DateTime.TryParse(buildDateAttr.Value, out var parsedDate))
                    {
                        _buildDate = parsedDate;
                    }
                }

                // Try to get Git info from assembly metadata
                var gitCommitAttr = assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
                if (gitCommitAttr != null && gitCommitAttr.Key == "GitCommitHash")
                {
                    _gitCommitHash = gitCommitAttr.Value;
                }

                var gitBranchAttr = assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
                if (gitBranchAttr != null && gitBranchAttr.Key == "GitBranch")
                {
                    _gitBranch = gitBranchAttr.Value;
                }

                // If not found in assembly, try to get from Git directly
                if (string.IsNullOrEmpty(_gitCommitHash) || _gitCommitHash == "Unknown")
                {
                    _gitCommitHash = GetGitCommitHash();
                }

                if (string.IsNullOrEmpty(_gitBranch) || _gitBranch == "Unknown")
                {
                    _gitBranch = GetGitBranch();
                }

                // If build date not found, use base directory date
                if (!_buildDate.HasValue || _buildDate == DateTime.MinValue)
                {
                    var baseDirectory = AppContext.BaseDirectory;
                    if (!string.IsNullOrEmpty(baseDirectory) && Directory.Exists(baseDirectory))
                    {
                        var dirInfo = new DirectoryInfo(baseDirectory);
                        _buildDate = dirInfo.LastWriteTime;
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail - use defaults
            }
        }

        private string? GetGitCommitHash()
        {
            try
            {
                var baseDirectory = AppContext.BaseDirectory;
                var projectDir = Path.GetDirectoryName(baseDirectory);
                var gitDir = FindGitDirectory(projectDir);
                
                if (string.IsNullOrEmpty(gitDir))
                    return null;

                var headFile = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(headFile))
                    return null;

                var headContent = File.ReadAllText(headFile).Trim();
                
                // Handle detached HEAD
                if (headContent.StartsWith("ref: "))
                {
                    var refPath = headContent.Substring(5);
                    var refFile = Path.Combine(gitDir, refPath);
                    if (File.Exists(refFile))
                    {
                        return File.ReadAllText(refFile).Trim();
                    }
                }
                else
                {
                    // Detached HEAD - return the commit hash directly
                    return headContent.Length >= 40 ? headContent : null;
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return null;
        }

        private string? GetGitBranch()
        {
            try
            {
                var baseDirectory = AppContext.BaseDirectory;
                var projectDir = Path.GetDirectoryName(baseDirectory);
                var gitDir = FindGitDirectory(projectDir);
                
                if (string.IsNullOrEmpty(gitDir))
                    return null;

                var headFile = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(headFile))
                    return null;

                var headContent = File.ReadAllText(headFile).Trim();
                
                if (headContent.StartsWith("ref: refs/heads/"))
                {
                    return headContent.Substring(16);
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return null;
        }

        private string? FindGitDirectory(string? startPath)
        {
            if (string.IsNullOrEmpty(startPath))
                return null;

            var current = new DirectoryInfo(startPath);
            while (current != null)
            {
                var gitDir = Path.Combine(current.FullName, ".git");
                if (Directory.Exists(gitDir))
                {
                    return gitDir;
                }
                current = current.Parent;
            }
            
            return null;
        }
    }
}

