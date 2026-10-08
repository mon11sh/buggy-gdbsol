using System.Text.Json;
using System.Text.Encodings.Web;
using NotificationService.Config;
using NotificationService.DTOs;

namespace NotificationService.Services;

public class NotificationStorageService
{
    private readonly Settings _settings;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private const int MaxMessageLength = 2000;
    private const int MaxRecipientLength = 255;

    public NotificationStorageService(Settings settings)
    {
        _settings = settings;
        EnsureFileSync();
    }

    private void EnsureFileSync()
    {
        var directory = Path.GetDirectoryName(_settings.DataFile);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_settings.DataFile))!);
        if (!File.Exists(_settings.DataFile))
        {
            File.WriteAllText(_settings.DataFile, "[]");
        }
    }

    private async Task<List<NotificationModel>> ReadDataAsync()
    {
        try
        {
            using var stream = new FileStream(_settings.DataFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<List<NotificationModel>>(content) ?? new List<NotificationModel>();
        }
        catch (Exception)
        {
            return new List<NotificationModel>();
        }
    }

    private async Task WriteDataAsync(List<NotificationModel> data)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var serialized = JsonSerializer.Serialize(data, options);
        var tmpPath = $"{_settings.DataFile}.{Environment.ProcessId}.tmp";

        using (var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(serialized);
            await writer.FlushAsync();
        }

        // Atomically replace
        File.Move(tmpPath, _settings.DataFile, overwrite: true);
    }

    private string SanitizeMessage(string? message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;
        var escaped = HtmlEncoder.Default.Encode(message);
        return escaped.Length <= MaxMessageLength ? escaped : escaped[..MaxMessageLength];
    }

    public async Task AddNotificationAsync(string recipient, string message, string type, string? mode = null)
    {
        var sanitizedMessage = SanitizeMessage(message);
        var safeRecipient = recipient?.Length > MaxRecipientLength ? recipient[..MaxRecipientLength] : recipient ?? string.Empty;

        await _semaphore.WaitAsync();
        try
        {
            var data = await ReadDataAsync();
            var nextId = data.Count > 0 ? data.Max(n => n.Id) + 1 : 1;

            var newNote = new NotificationModel
            {
                Id = nextId,
                Recipient = safeRecipient,
                Message = sanitizedMessage,
                Type = type,
                Mode = mode,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"),
                Read = false
            };

            data.Add(newNote);
            await WriteDataAsync(data);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<NotificationModel>> GetNotificationsAsync(string recipient)
    {
        await _semaphore.WaitAsync();
        try
        {
            var data = await ReadDataAsync();
            return data.Where(n => n.Recipient == recipient).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task MarkAllAsReadAsync(string recipient)
    {
        await _semaphore.WaitAsync();
        try
        {
            var data = await ReadDataAsync();
            bool changed = false;
            foreach (var n in data)
            {
                if (n.Recipient == recipient && !n.Read)
                {
                    n.Read = true;
                    changed = true;
                }
            }

            if (changed)
            {
                await WriteDataAsync(data);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearNotificationsAsync(string recipient)
    {
        await _semaphore.WaitAsync();
        try
        {
            var data = await ReadDataAsync();
            var filteredData = data.Where(n => n.Recipient != recipient).ToList();
            
            if (filteredData.Count != data.Count)
            {
                await WriteDataAsync(filteredData);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
