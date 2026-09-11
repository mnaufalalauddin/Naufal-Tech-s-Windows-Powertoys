using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct UpdateCacheBackup(string Source, string? Backup);

internal static class UpdateCacheReset
{
    internal static async Task<UpdateCacheBackup> RunAsync(string windowsDirectory, string relativeCache,
        Func<CancellationToken, Task> ensureServicesStopped, Action<string> log, CancellationToken cancellationToken,
        Action<string, string>? move = null, Func<CancellationToken, Task>? retryDelay = null)
    {
        // Never rename Windows/SoftwareDistribution itself, follow junctions,
        // delete old backups, or broaden this to arbitrary supplied directories.
        if (relativeCache is not (@"SoftwareDistribution\DataStore" or @"SoftwareDistribution\Download" or @"System32\catroot2"))
            throw new ArgumentException("The requested directory is not an allowed Windows Update cache.", nameof(relativeCache));
        string root = Path.GetFullPath(windowsDirectory);
        string source = Path.GetFullPath(Path.Combine(root, relativeCache));
        string backup = source + ".bak-wpt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        move ??= Directory.Move;
        retryDelay ??= token => Task.Delay(500, token);
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ensureServicesStopped(cancellationToken);
            if (!ValidateDirectoryChain(root, relativeCache))
            {
                log($"Cache not present; nothing to rename: {source}");
                return new(source, null);
            }
            if (Path.Exists(backup)) throw new IOException("The unique cache backup destination already exists: " + backup);
            try
            {
                move(source, backup);
                if (!Directory.Exists(backup) || (File.GetAttributes(backup) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Cache rename did not create a verifiable backup: " + backup);
                log($"Preserved cache backup: {source} -> {backup}");
                return new(source, backup);
            }
            catch (Exception exception) when (IsLockedOrDenied(exception))
            {
                log($"Cache rename attempt {attempt}/3: {source}; HRESULT 0x{exception.HResult:X8}: {exception.Message}");
                if (attempt == 3)
                    throw new IOException($"Cannot reset {source}: Windows still denies the rename or holds the cache open after three attempts. " +
                        "Existing backups were preserved; folder ownership and security permissions were not changed. " +
                        "Restart Windows, let any active update finish, then retry. If this persists, inspect update/security software locks and folder permissions. " +
                        $"Original error 0x{exception.HResult:X8}: {exception.Message}", exception);
                await retryDelay(cancellationToken);
            }
        }
        throw new InvalidOperationException("Cache reset did not reach a terminal state.");
    }

    private static bool IsLockedOrDenied(Exception exception) => exception is UnauthorizedAccessException ||
        (exception is IOException && (exception.HResult & 0xffff) is 5 or 32 or 33);

    private static bool ValidateDirectoryChain(string root, string relativeCache)
    {
        string current = root;
        // Reject a reparse point anywhere up to the volume, including a linked
        // Windows root. Do not silently treat access denied as 'not present'.
        for (DirectoryInfo? ancestor = new(root); ancestor is not null; ancestor = ancestor.Parent)
            ValidateAttributes(File.GetAttributes(ancestor.FullName), ancestor.FullName);
        foreach (string segment in relativeCache.Split('\\'))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            ValidateAttributes(attributes, current);
        }
        return true;
    }

    internal static void ValidateAttributes(FileAttributes attributes, string path)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
            throw new InvalidOperationException("Cache reset refused a non-directory or reparse point: " + path);
    }
}
