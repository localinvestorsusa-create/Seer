using System.Runtime.InteropServices;

namespace Recorder;

internal static class DownloadsFolder
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    public static string GetPath()
    {
        var id = DownloadsId;
        int result = SHGetKnownFolderPath(in id, 0, 0, out nint pointer);
        Marshal.ThrowExceptionForHR(result);
        try
        {
            return Marshal.PtrToStringUni(pointer)
                   ?? throw new IOException("Windows did not return a Downloads folder.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(in Guid folderId, uint flags, nint token, out nint path);
}
