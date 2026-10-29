using Backend.Models;
using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.Data;

// [EDUCATIONAL NOTE - Lab 14 S1]:
// Database Seeding & Mock Audio Generation:
// For local development and polytechnic labs, an empty database creates a confusing experience.
// DbSeeder ensures the system is ready immediately upon 'dotnet run' by:
// 1. Creating default student user 'student@kkaudiolab.sg' with BCrypt salted password.
// 2. Synthesizing 6 playable PCM .wav audio files in 'AudioLibrary/' using pure C# binary I/O,
//    ensuring students can immediately test HTTP 206 Partial Content byte streaming without
//    needing manual file downloads.
// 3. Generating sample Playlists with LiteDB [BsonRef] associations.
public static class DbSeeder
{
    public static void Seed(ILiteDatabase db, IConfiguration config, ILogger logger)
    {
        var usersCol = db.GetCollection<User>("users");
        var tracksCol = db.GetCollection<Track>("tracks");
        var playlistsCol = db.GetCollection<Playlist>("playlists");

        usersCol.EnsureIndex(u => u.Email, unique: true);
        tracksCol.EnsureIndex(t => t.Title);
        tracksCol.EnsureIndex(t => t.FileName);

        // 1. Seed Student User
        const string studentEmail = "student@kkaudiolab.sg";
        var student = usersCol.FindOne(u => u.Email == studentEmail);
        if (student == null)
        {
            student = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = studentEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 11),
                FullName = "KK Audio Lab",
                Role = "Student",
                CreatedAtUtc = DateTime.UtcNow
            };
            usersCol.Insert(student);
            logger.LogInformation("Seeded default user: {Email} (Password: Password123!)", studentEmail);
        }

        // Resolve AudioLibrary path
        var configuredPath = config["AudioLibraryPath"] ?? "AudioLibrary";
        var audioDirPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(Directory.GetCurrentDirectory(), configuredPath);

        if (!Directory.Exists(audioDirPath))
        {
            Directory.CreateDirectory(audioDirPath);
        }

        // 2. Seed 6 Playable Tracks if collection is empty
        if (tracksCol.Count() == 0)
        {
            var seedTracksData = new[]
            {
                new {
                    Title = "The Beauty of Letting Go",
                    Artist = "KK Audio Lab",
                    Album = "KK Audio Lab Greatest Hits",
                    FileName = "The Beauty of Letting Go.mp3",
                    Mood = "Energetic",
                    Bpm = 128,
                    // [SYSTEM NOTE]: High-precision audio requires Duration to be a double (209.5).
                    // If compilation fails, update Track.DurationSeconds and all DTOs to double.
                    Duration = 209.5,
                    Freq = 523.25, // C5 note
                    Cover = "https://cdn2.suno.ai/55d1a380-630d-4e5b-aaa6-02685563b7b2.jpeg"
                },
                new {
                    Title = "蒲公英的告别",
                    Artist = "KK Audio Lab",
                    Album = "KK Audio Lab Greatest Hits",
                    FileName = "蒲公英的告别.mp3",
                    Mood = "Chill",
                    Bpm = 95,
                    Duration = 241,
                    Freq = 440.00, // A4 note
                    Cover = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=500"
                },
                new {
                    Title = "擁抱這場雨",
                    Artist = "KK Audio Lab",
                    Album = "KK Audio Lab Greatest Hits",
                    FileName = "擁抱這場雨.mp3",
                    Mood = "Ambient",
                    Bpm = 80,
                    Duration = 154,
                    Freq = 329.63, // E4 note
                    Cover = "https://cdn2.suno.ai/09d5247e-9f8a-4ee4-881f-7c7ce0a17061.jpeg"
                },
                new {
                    Title = "Serendipity_ Tied to You",
                    Artist = "KK Audio Lab",
                    Album = "KK Audio Lab Greatest Hits",
                    FileName = "Serendipity_ Tied to You.mp3",
                    Mood = "Lo-Fi",
                    Bpm = 75,
                    Duration = 252,
                    Freq = 392.00, // G4 note
                    Cover = "https://cdn2.suno.ai/fd1d0a2a-d8cc-4fd2-a88a-0091f12d6cd5.jpeg"
                },
                new {
                    Title = "独家花期",
                    Artist = "KK Audio Lab",
                    Album = "KK Audio Lab Greatest Hits",
                    FileName = "独家花期.mp3",
                    Mood = "Study",
                    Bpm = 110,
                    Duration = 191,
                    Freq = 587.33, // D5 note
                    Cover = "https://cdn2.suno.ai/57b5cac5-5f0d-4e2f-9d34-8c9f5b36dd55.jpeg"
                }
            };

            var createdTracks = new List<Track>();

            foreach (var item in seedTracksData)
            {
                var filePath = Path.Combine(audioDirPath, item.FileName);

                // Generate genuine, playable PCM WAV file on disk if not already present
                if (!File.Exists(filePath))
                {
                    GeneratePcmWavFile(filePath, item.Freq, durationSeconds: 3.5f);
                }

                var fileInfo = new FileInfo(filePath);

                var track = new Track
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = item.Title,
                    Artist = item.Artist,
                    Album = item.Album,
                    FileName = item.FileName,
                    FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 308744,
                    DurationSeconds = item.Duration,
                    CoverArtUrl = item.Cover,
                    UploadedByUserId = student.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                    AudioMetadata = new AudioMetadata
                    {
                        MoodTag = item.Mood,
                        Bpm = item.Bpm,
                        BitrateKbps = 320,
                        Format = "wav"
                    }
                };

                tracksCol.Insert(track);
                createdTracks.Add(track);
                logger.LogInformation("Seeded track: {Title} by {Artist} ({FileName})", track.Title, track.Artist, track.FileName);
            }

            // 3. Seed 2 Playlists referencing the created tracks
            if (playlistsCol.Count() == 0 && createdTracks.Count >= 6)
            {
                var studyPlaylist = new Playlist
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = "Study Beats SG",
                    Description = "Low-tempo, high-focus tunes for Singapore exam prep and lab assignments.",
                    UserId = student.Id,
                    Owner = student,
                    Tracks = new List<Track> { createdTracks[0], createdTracks[3], createdTracks[4] },
                    CreatedAtUtc = DateTime.UtcNow
                };

                var weekendPlaylist = new Playlist
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = "KK Audio Lab Weekend Vibes",
                    Description = "Chillout melodic vibes for walking around Marina Bay and Sentosa.",
                    UserId = student.Id,
                    Owner = student,
                    Tracks = new List<Track> { createdTracks[1], createdTracks[2], createdTracks[5] },
                    CreatedAtUtc = DateTime.UtcNow
                };

                playlistsCol.Insert(studyPlaylist);
                playlistsCol.Insert(weekendPlaylist);
                logger.LogInformation("Seeded 2 default playlists for student: '{P1}' and '{P2}'", studyPlaylist.Title, weekendPlaylist.Title);
            }
        }
    }

    /// <summary>
    /// Generates a valid RIFF/WAVE PCM audio file containing a pleasant harmonic tone with an ADSR envelope.
    /// This file can be streamed and played directly by HTML5 &lt;audio&gt; tags and VLC/Windows Media Player.
    /// </summary>
    public static void GeneratePcmWavFile(string outputPath, double baseFrequency, float durationSeconds)
    {
        const int sampleRate = 44100;
        const short bitsPerSample = 16;
        const short channels = 1;

        var totalSamples = (int)(sampleRate * durationSeconds);
        var subchunk2Size = totalSamples * channels * (bitsPerSample / 8);
        var chunkSize = 36 + subchunk2Size;

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs);

        // RIFF chunk descriptor
        bw.Write("RIFF"u8.ToArray());
        bw.Write(chunkSize);
        bw.Write("WAVE"u8.ToArray());

        // "fmt " sub-chunk
        bw.Write("fmt "u8.ToArray());
        bw.Write(16); // Subchunk1Size for PCM
        bw.Write((short)1); // AudioFormat: 1 = PCM
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * (bitsPerSample / 8)); // ByteRate
        bw.Write((short)(channels * (bitsPerSample / 8))); // BlockAlign
        bw.Write(bitsPerSample);

        // "data" sub-chunk
        bw.Write("data"u8.ToArray());
        bw.Write(subchunk2Size);

        // Audio synthesis: Warm harmonic tone with gentle fade-in and fade-out to prevent audio pops
        for (var i = 0; i < totalSamples; i++)
        {
            var t = (double)i / sampleRate;

            // Multi-harmonic overtone (fundamental + 2nd harmonic + 3rd harmonic)
            var sample = 0.65 * Math.Sin(2.0 * Math.PI * baseFrequency * t)
                       + 0.25 * Math.Sin(2.0 * Math.PI * (baseFrequency * 1.5) * t)
                       + 0.10 * Math.Sin(2.0 * Math.PI * (baseFrequency * 2.0) * t);

            // Smooth envelope (0.2s fade in, 0.4s fade out)
            var envelope = 1.0;
            if (t < 0.2)
            {
                envelope = t / 0.2;
            }
            else if (t > durationSeconds - 0.4)
            {
                envelope = (durationSeconds - t) / 0.4;
            }

            var sampleValue = (short)(sample * envelope * 24000.0);
            bw.Write(sampleValue);
        }
    }
}
