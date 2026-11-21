using System.Security.Cryptography;
using System.Text.Json;

namespace Rfkunck.Desktop;

internal sealed record TelegramSettings(int Port, bool DisableCfTls, string Secret)
{
    public static TelegramSettings Load(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                using var file = File.OpenRead(path);
                var saved = JsonSerializer.Deserialize<TelegramSettings>(file);
                if (saved is { Port: >= 1024 and <= 65535, Secret.Length: 32 } &&
                    saved.Secret.All(Uri.IsHexDigit))
                {
                    return saved;
                }
            }
            catch (JsonException)
            {
                // Regenerate invalid settings. Access and disk errors are reported to the user.
            }
        }

        return CreateDefault();
    }

    public static TelegramSettings CreateDefault() =>
        new(1443, false, RandomNumberGenerator.GetHexString(32, lowercase: true));

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string staged = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(staged, JsonSerializer.Serialize(this));
            File.Move(staged, path, overwrite: true);
        }
        finally
        {
            File.Delete(staged);
        }
    }
}
