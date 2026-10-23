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
                FullName = "Alex Tan (Poly Student)",
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

        // 2. Seed 6 Playable Singapore-Themed Tracks if collection is empty
        if (tracksCol.Count() == 0)
        {
            var seedTracksData = new[]
            {
                new {
                    Title = "Majulah Beat",
                    Artist = "Lion City Sound",
                    Album = "Red Dot Anthems",
                    FileName = "majulah_beat.wav",
                    Mood = "Energetic",
                    Bpm = 128,
                    Duration = 180,
                    Freq = 523.25, // C5 note
                    Cover = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=500"
                },
                new {
                    Title = "Orchard Road Chill",
                    Artist = "Somerset Vibes",
                    Album = "City Lights SG",
                    FileName = "orchard_road_chill.wav",
                    Mood = "Chill",
                    Bpm = 95,
                    Duration = 210,
                    Freq = 440.00, // A4 note
                    Cover = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=500"
                },
                new {
                    Title = "Marina Bay Sunrise",
                    Artist = "Sentosa Waves",
                    Album = "Island State of Mind",
                    FileName = "marina_bay_sunrise.wav",
                    Mood = "Ambient",
                    Bpm = 80,
                    Duration = 240,
                    Freq = 329.63, // E4 note
                    Cover = "https://images.unsplash.com/photo-1508700115892-45ecd05ae2ad?w=500"
                },
                new {
                    Title = "Changi Airport Rain",
                    Artist = "Terminal Melodies",
                    Album = "Jewel Rain Vortex",
                    FileName = "changi_airport_rain.wav",
                    Mood = "Lo-Fi",
                    Bpm = 75,
                    Duration = 195,
                    Freq = 392.00, // G4 note
                    Cover = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=500"
                },
                new {
                    Title = "Tiong Bahru Cafe",
                    Artist = "Haji Lane Groove",
                    Album = "Heritage Rhythms",
                    FileName = "tiong_bahru_cafe.wav",
                    Mood = "Study",
                    Bpm = 110,
                    Duration = 165,
                    Freq = 587.33, // D5 note
                    Cover = "https://images.unsplash.com/photo-1447752875215-b2761acb3c5d?w=500"
                },
                new {
                    Title = "Sentosa Sunset Groove",
                    Artist = "East Coast Collective",
                    Album = "Coastline Sunset",
                    FileName = "sentosa_sunset_groove.wav",
                    Mood = "Groove",
                    Bpm = 118,
                    Duration = 220,
                    Freq = 659.25, // E5 note
                    Cover = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=500"
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
