using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.Telephony;

public class RecordingService : IRecordingService
{
    private readonly ICrmDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecordingService> _logger;
    private readonly string _recordingsRootDirectory;

    public RecordingService(
        ICrmDbContext dbContext,
        IConfiguration configuration,
        ILogger<RecordingService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;

        var configuredPath = _configuration["Telephony:RecordingsPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            _recordingsRootDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recordings"));
        }
        else
        {
            _recordingsRootDirectory = Path.GetFullPath(configuredPath);
        }

        if (!Directory.Exists(_recordingsRootDirectory))
        {
            Directory.CreateDirectory(_recordingsRootDirectory);
        }
    }

    public string GetRecordingsDirectory() => _recordingsRootDirectory;

    public bool ValidateRecordingPath(string filePathOrName, out string sanitizedPath)
    {
        sanitizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(filePathOrName))
        {
            return false;
        }

        // Security: Path traversal checks
        if (filePathOrName.Contains("..") || filePathOrName.Contains('\0') || filePathOrName.Contains('/') || filePathOrName.Contains('\\'))
        {
            _logger.LogWarning("Potential path traversal attempt detected in recording filename: {Input}", filePathOrName);
            return false;
        }

        // Only allow safe extension types (wav, mp3, gsm)
        var ext = Path.GetExtension(filePathOrName).ToLowerInvariant();
        if (ext != ".wav" && ext != ".mp3" && ext != ".gsm" && ext != ".ogg")
        {
            _logger.LogWarning("Unsupported or prohibited audio format requested: {Extension}", ext);
            return false;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_recordingsRootDirectory, filePathOrName));

        // Ensure full path is strictly located within the root directory
        var rootWithSlash = _recordingsRootDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? _recordingsRootDirectory
            : _recordingsRootDirectory + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSlash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal evasion attempt: {FullPath} is outside {Root}", fullPath, rootWithSlash);
            return false;
        }

        sanitizedPath = fullPath;
        return true;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> GetCallAudioStreamAsync(Guid callId)
    {
        var call = await _dbContext.CallRecords.FirstOrDefaultAsync(c => c.Id == callId);
        if (call == null || string.IsNullOrWhiteSpace(call.RecordingFileName) || !call.HasRecording)
        {
            _logger.LogInformation("No recording associated with CallId: {CallId}", callId);
            return null;
        }

        if (!ValidateRecordingPath(call.RecordingFileName, out var fullPath))
        {
            _logger.LogWarning("Recording filename failed security validation: {FileName}", call.RecordingFileName);
            return null;
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Recording file does not exist on disk: {Path}", fullPath);
            return null;
        }

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".gsm" => "audio/x-gsm",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };

        var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return (fileStream, contentType, call.RecordingFileName);
    }

    public async Task<bool> LinkRecordingToCallAsync(string uniqueId, string recordingFileName, long? fileSize = null, int? durationSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(uniqueId) || string.IsNullOrWhiteSpace(recordingFileName))
            return false;

        if (!ValidateRecordingPath(recordingFileName, out var sanitizedPath))
            return false;

        var call = await _dbContext.CallRecords.FirstOrDefaultAsync(c => c.UniqueId == uniqueId);
        if (call == null)
            return false;

        call.RecordingFileName = recordingFileName;
        call.RecordingFilePath = sanitizedPath;
        call.HasRecording = File.Exists(sanitizedPath);

        if (fileSize.HasValue)
        {
            call.RecordingFileSize = fileSize.Value;
        }
        else if (File.Exists(sanitizedPath))
        {
            call.RecordingFileSize = new FileInfo(sanitizedPath).Length;
        }

        if (durationSeconds.HasValue)
        {
            call.RecordingDurationSeconds = durationSeconds.Value;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }
}
