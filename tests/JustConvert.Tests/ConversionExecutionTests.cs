using System.Diagnostics;
using System.IO;
using JustConvert.Core;
using JustConvert.Core.Converters;
using JustConvert.Core.Converters.Tools;

namespace JustConvert.Tests;

public class ConversionExecutionTests : IDisposable
{
    private readonly ConverterRegistry _registry = new();
    private readonly string _tempDir;

    public ConversionExecutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jc_exec_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        AppSettings.CustomSettingsFilePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        AppSettings.CustomSettingsFilePath = null;
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("aac")]
    [InlineData("m4a")]
    [InlineData("wav")]
    [InlineData("flac")]
    [InlineData("ogg")]
    [InlineData("opus")]
    [InlineData("aiff")]
    [InlineData("reencode")]
    public async Task ConvertAudio_AllAudioFormats_Succeeds(string targetFormat)
    {
        var ffmpeg = ToolLocator.FindFfmpegPath();
        if (ffmpeg == null) return;

        var inputWav = Path.Combine(_tempDir, $"input_{targetFormat}.wav");
        CreateSyntheticWavFile(inputWav);

        var result = await _registry.ConvertFileAsync(inputWav, targetFormat);

        Assert.True(result.Success, $"Conversion to {targetFormat} failed: {result.ErrorMessage}");
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        Assert.True(new FileInfo(result.OutputPath).Length > 0);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("webp")]
    [InlineData("bmp")]
    [InlineData("ico")]
    [InlineData("gif")]
    [InlineData("tiff")]
    [InlineData("tga")]
    [InlineData("pcx")]
    [InlineData("ppm")]
    [InlineData("jp2")]
    public async Task ConvertImage_AllImageFormats_Succeeds(string targetFormat)
    {
        var magick = ToolLocator.FindMagickPath();
        if (magick == null) return;

        var inputBmp = Path.Combine(_tempDir, $"input_{targetFormat}.bmp");
        CreateSyntheticBmpFile(inputBmp);

        var result = await _registry.ConvertFileAsync(inputBmp, targetFormat);

        Assert.True(result.Success, $"Conversion to {targetFormat} failed: {result.ErrorMessage}");
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        Assert.True(new FileInfo(result.OutputPath).Length > 0);
    }

    [Theory]
    [InlineData("mp4-h264")]
    [InlineData("remux-mp4")]
    [InlineData("remux-mkv")]
    [InlineData("mov-prores422")]
    [InlineData("frames")]
    [InlineData("mp3")]
    [InlineData("wav")]
    [InlineData("flac")]
    [InlineData("aac")]
    [InlineData("reencode")]
    public async Task ConvertVideo_AllCommonVideoFormats_Succeeds(string targetFormat)
    {
        var ffmpeg = ToolLocator.FindFfmpegPath();
        if (ffmpeg == null) return;

        var inputMp4 = Path.Combine(_tempDir, $"input_vid_{targetFormat}.mp4");
        if (!CreateSyntheticMp4File(ffmpeg, inputMp4)) return;

        var result = await _registry.ConvertFileAsync(inputMp4, targetFormat);

        Assert.True(result.Success, $"Conversion to {targetFormat} failed: {result.ErrorMessage}");
        Assert.NotNull(result.OutputPath);

        if (targetFormat == "frames")
        {
            Assert.True(Directory.Exists(result.OutputPath));
            Assert.NotEmpty(Directory.GetFiles(result.OutputPath, "*.png"));
        }
        else
        {
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(new FileInfo(result.OutputPath).Length > 0);
        }
    }

    private static void CreateSyntheticWavFile(string filePath)
    {
        const int sampleRate = 44100;
        const short bitsPerSample = 16;
        const short channels = 1;
        const int durationSeconds = 1;
        const int sampleCount = sampleRate * durationSeconds;
        const int dataChunkSize = sampleCount * channels * (bitsPerSample / 8);

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataChunkSize);
        writer.Write("WAVE"u8.ToArray());

        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);

        writer.Write("data"u8.ToArray());
        writer.Write(dataChunkSize);

        for (int i = 0; i < sampleCount; i++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 16000);
            writer.Write(sample);
        }
    }

    private static void CreateSyntheticBmpFile(string filePath)
    {
        const int width = 32;
        const int height = 32;
        const int rowStride = (width * 3 + 3) & ~3;
        const int imageSize = rowStride * height;
        const int fileSize = 54 + imageSize;

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var row = new byte[rowStride];
        for (int x = 0; x < width; x++)
        {
            row[x * 3 + 0] = 255;
            row[x * 3 + 1] = 128;
            row[x * 3 + 2] = 64;
        }

        for (int y = 0; y < height; y++)
        {
            writer.Write(row);
        }
    }

    private static bool CreateSyntheticMp4File(string ffmpegPath, string destination)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-f lavfi -i testsrc=duration=1:size=320x240:rate=10 -f lavfi -i sine=frequency=440:duration=1 -c:v libx264 -pix_fmt yuv420p -c:a aac -y \"{destination}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10000);
            return File.Exists(destination) && new FileInfo(destination).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void MediaConverter_ExtractDiagnosticMessage_ExtractsSpecificErrorLine()
    {
        var sampleLog = """
            ffmpeg version 6.0 Copyright (c) 2000-2023 the FFmpeg developers
              libavutil      58.  2.100 / 58.  2.100
              libavcodec     60.  3.100 / 60.  3.100
            [mp4 @ 000001878f8b3600] Could not find tag for codec pcm_s16le in stream #1, codec not currently supported in container
            Could not write header for output file #0 (incorrect codec parameters ?): Invalid argument
            Conversion failed!
            """;

        var result = MediaProbe.ExtractDiagnosticMessage(sampleLog, -22);
        Assert.Equal("Could not write header for output file #0 (incorrect codec parameters ?): Invalid argument", result);
    }

    [Fact]
    public void MediaConverter_ExtractDiagnosticMessage_FallsBackWhenEmpty()
    {
        var result = MediaProbe.ExtractDiagnosticMessage("", -22);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void ImageConverter_ExtractDiagnosticMessage_ExtractsMagickError()
    {
        var sampleLog = """
            magick: unable to open image 'non_existing.png': No such file or directory @ error/blob.c/OpenBlob/3571.
            """;

        var result = ImageConverter.ExtractDiagnosticMessage(sampleLog, 1);
        Assert.Equal("magick: unable to open image 'non_existing.png': No such file or directory @ error/blob.c/OpenBlob/3571.", result);
    }

    [Fact]
    public void BuildAudioArguments_WhenCustomBitrateNull_ResolvesProbeBitrate()
    {
        var info = new AudioStreamInfo(128, false, "mp3", false, 16, 44100, 2);
        var args = AudioConverter.BuildAudioArguments("in.wav", "out.mp3", "mp3", info, false, null);
        Assert.Contains("-b:a 128k", args);
    }

    [Theory]
    [InlineData("frames", "frame_%04d.png")]
    [InlineData("frames-png", "frame_%04d.png")]
    [InlineData("frames-jpg", "frame_%04d.jpg")]
    [InlineData("frames-webp", "frame_%04d.webp")]
    [InlineData("frames-bmp", "frame_%04d.bmp")]
    [InlineData("frames-tiff", "frame_%04d.tiff")]
    public void BuildVideoArguments_FramesFormats_ProducesExpectedOutputPattern(string targetExt, string expectedPattern)
    {
        var args = VideoConverter.BuildVideoArguments("in.mp4", "outDir", targetExt);
        Assert.Contains(expectedPattern, args);
        Assert.DoesNotContain("fps=1", args);
    }

    [Fact]
    public void BuildVideoArguments_WhenAudioBitrateZero_ResolvesFromAudioInfo()
    {
        var audioInfo = new AudioStreamInfo(128, false, "aac", false, 16, 44100, 2);
        var setting = new VideoQualitySetting
        {
            VideoCodec = "h264",
            AudioCodec = "aac",
            AudioBitrateKbps = 0
        };

        var args = VideoConverter.BuildVideoArguments("in.mkv", "out.mp4", "mp4", audioInfo, false, null, setting);
        Assert.Contains("-b:a 128k", args);
    }
}
