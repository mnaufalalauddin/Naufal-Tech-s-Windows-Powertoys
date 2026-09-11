using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class AtomicDownloadFile
{
    // Keep an existing cached package intact until a complete replacement is
    // received. Dispose the exclusive handle BEFORE renaming it on Windows.
    public static async Task<long> SaveAsync(Stream input, string destination,
        long? expectedBytes, long minimumBytes, long maximumBytes,
        Action<long>? report, CancellationToken cancellationToken)
    {
        if (minimumBytes < 0 || maximumBytes < minimumBytes || expectedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedBytes));
        if (expectedBytes > maximumBytes)
            throw new InvalidDataException("The package exceeds the download size limit.");
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string partial = fullPath + "." + Guid.NewGuid().ToString("N") + ".download";
        long received = 0;
        try
        {
            await using (FileStream output = new(partial, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[128 * 1024];
                int count;
                while ((count = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    if (count > maximumBytes - received)
                        throw new InvalidDataException("The package exceeded the download size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    received += count;
                    report?.Invoke(received);
                }
                if (received < minimumBytes || (expectedBytes.HasValue && received != expectedBytes.Value))
                    throw new InvalidDataException($"Incomplete package: received {received} bytes; expected {expectedBytes?.ToString() ?? "unknown"}.");
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, fullPath, overwrite: true);
            return received;
        }
        finally
        {
            // Never mask the download error or remove another operation's file.
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
        }
    }
}
