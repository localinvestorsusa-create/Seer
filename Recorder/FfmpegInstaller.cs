using System.IO.Compression;
using System.Security.Cryptography;

namespace Recorder;

// Downloads the Windows essentials build directly from its publisher when the
// user clicks Install. The archive is checked against the publisher's SHA-256.
internal static class FfmpegInstaller
{
    private const string ArchiveUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private const string ChecksumUrl = ArchiveUrl + ".sha256";

    public static async Task<string> InstallAsync(IProgress<int> progress)
    {
        string work = Path.Combine(Path.GetTempPath(), "Recorder", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        string archivePath = Path.Combine(work, "ffmpeg.zip");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            string expected = (await client.GetStringAsync(ChecksumUrl)).Trim().Split(' ', '\t', '\r', '\n')[0];
            if (expected.Length != 64 || !expected.All(Uri.IsHexDigit))
                throw new IOException("The FFmpeg checksum was not valid.");

            using (var response = await client.GetAsync(ArchiveUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var destination = File.Create(archivePath);
                byte[] buffer = new byte[128 * 1024];
                long copied = 0;
                int count;
                while ((count = await source.ReadAsync(buffer)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, count));
                    copied += count;
                    if (total is > 0) progress.Report((int)(copied * 100 / total.Value));
                }
            }

            await using (var stream = File.OpenRead(archivePath))
            {
                byte[] actual = await SHA256.HashDataAsync(stream);
                if (!Convert.ToHexString(actual).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The FFmpeg download failed its SHA-256 check.");
            }

            using var archive = ZipFile.OpenRead(archivePath);
            var executable = archive.Entries.SingleOrDefault(entry =>
                entry.FullName.Replace('\\', '/').EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
            if (executable is null) throw new IOException("The FFmpeg archive did not contain ffmpeg.exe.");

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Recorder", "tools");
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, "ffmpeg.exe");
            string temporary = Path.Combine(folder, "ffmpeg-new.exe");
            await using (var input = executable.Open())
            await using (var output = File.Create(temporary))
                await input.CopyToAsync(output);
            File.Move(temporary, target, true);
            progress.Report(100);
            return target;
        }
        finally
        {
            if (Directory.Exists(work)) Directory.Delete(work, true);
        }
    }
}
