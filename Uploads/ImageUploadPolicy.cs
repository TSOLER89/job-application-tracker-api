namespace job_application_tracker_api.Uploads;

public static class ImageUploadPolicy
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 1024 * 1024;

    // Check the bytes as well as the extension; the supplied MIME type isn't trusted.
    public static string? GetExtension(ReadOnlySpan<byte> header, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" &&
            header.Length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
        {
            return ".jpg";
        }

        if (extension == ".png" && header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return ".png";
        }

        if (extension == ".webp" && header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }
}
