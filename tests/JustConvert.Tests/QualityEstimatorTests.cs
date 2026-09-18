using JustConvert.Core.Converters.Tools;

namespace JustConvert.Tests;

public class QualityEstimatorTests
{
    [Fact]
    public void EstimateVideoBitrate_ProRes422_CalculatesCorrectTargetBitrate()
    {
        var bitrate = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "prores422");
        Assert.InRange(bitrate, 140_000, 150_000);
    }

    [Fact]
    public void EstimateVideoBitrate_ProRes4444_CalculatesCorrectTargetBitrate()
    {
        var bitrate = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "prores4444");
        Assert.InRange(bitrate, 320_000, 340_000);
    }

    [Fact]
    public void EstimateVideoBitrate_H264VsHevc_HevcIsMoreCompact()
    {
        var h264Bitrate = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "h264");
        var hevcBitrate = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "hevc");
        Assert.True(hevcBitrate < h264Bitrate);
    }

    [Fact]
    public void EstimateVideoBitrate_NvencEncoder_HigherBitrateThanCpu()
    {
        var cpuBitrate = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "h264", "cpu");
        var nvencBitrate = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "h264", "nvenc");
        Assert.True(nvencBitrate > cpuBitrate);
    }

    [Fact]
    public void EstimateVideoBitrate_SourceBitrateCapping_CapsBitrateAppropriately()
    {
        var uncapped = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "h264");
        var capped = QualityEstimator.EstimateVideoBitrateKbps(1920, 1080, 30.0, 23, "h264", sourceBitrateKbps: 2000);
        Assert.True(capped <= 2300);
        Assert.True(capped < uncapped);
    }

    [Fact]
    public void EstimateVideoFileSize_Remux_MatchesSourceSizeWithOverhead()
    {
        var size = QualityEstimator.EstimateVideoFileSize(
            1920, 1080, 30.0, 60.0, 23, "remux",
            sourceFileSizeBytes: 100_000_000L);
        Assert.Equal(101_000_000L, size);
    }

    [Fact]
    public void EstimateAudioFileSize_Wav_MatchesDeterministicPcmFormula()
    {
        var size = QualityEstimator.EstimateAudioFileSize(10.0, 0, "wav", true, 44100, 2, 16);
        var expectedPcm = 44100L * 2 * 2 * 10 + 44L;
        Assert.Equal(expectedPcm, size);
    }

    [Fact]
    public void EstimateAudioFileSize_Flac_CompressedRelativeToWav()
    {
        var wavSize = QualityEstimator.EstimateAudioFileSize(60.0, 0, "wav", true, 44100, 2, 16);
        var flacSize = QualityEstimator.EstimateAudioFileSize(60.0, 0, "flac", true, 44100, 2, 16);
        Assert.True(flacSize < wavSize);
        Assert.True(flacSize > wavSize / 2);
    }

    [Fact]
    public void EstimateAudioFileSize_Mp3_ScalesWithBitrate()
    {
        var size128 = QualityEstimator.EstimateAudioFileSize(60.0, 128, "mp3");
        var size320 = QualityEstimator.EstimateAudioFileSize(60.0, 320, "mp3");
        Assert.True(size320 > size128 * 2);
    }

    [Fact]
    public void EstimateImageFileSize_Bmp_MatchesExactRawDimensions()
    {
        var size = QualityEstimator.EstimateImageFileSize(1920, 1080, 0, 90, "bmp");
        Assert.Equal(1920L * 1080L * 3L + 54L, size);
    }

    [Fact]
    public void EstimateImageFileSize_JpegVsWebpVsAvif_ModernFormatsSmaller()
    {
        var jpeg = QualityEstimator.EstimateImageFileSize(1920, 1080, 0, 85, "jpg");
        var webp = QualityEstimator.EstimateImageFileSize(1920, 1080, 0, 85, "webp");
        var avif = QualityEstimator.EstimateImageFileSize(1920, 1080, 0, 85, "avif");
        Assert.True(webp < jpeg);
        Assert.True(avif < webp);
    }

    [Fact]
    public void EstimateGifFileSize_ScalesWithDuration()
    {
        var shortGif = QualityEstimator.EstimateGifFileSize(1920, 1080, 3.0);
        var longGif = QualityEstimator.EstimateGifFileSize(1920, 1080, 10.0);
        Assert.True(longGif > shortGif * 3);
    }

    [Fact]
    public void EstimateFramesFolderSize_ReflectsTotalFramesAndResolution()
    {
        var size1080p = QualityEstimator.EstimateFramesFolderSize(1920, 1080, 30.0, 5.0, "jpg");
        var size4k = QualityEstimator.EstimateFramesFolderSize(3840, 2160, 30.0, 5.0, "jpg");
        Assert.True(size4k > size1080p * 3);
    }

    [Fact]
    public void EstimateVideoFileSize_VbrAndCbr_UsesDirectBitrate()
    {
        var sizeVbr = QualityEstimator.EstimateVideoFileSize(
            1920, 1080, 30.0, 60.0, 23, "h264",
            audioBitrateKbps: 192,
            rateControl: "vbr",
            videoBitrateKbps: 8000);

        var sizeCbr = QualityEstimator.EstimateVideoFileSize(
            1920, 1080, 30.0, 60.0, 23, "h264",
            audioBitrateKbps: 192,
            rateControl: "cbr",
            videoBitrateKbps: 8000);

        Assert.Equal(sizeVbr, sizeCbr);
        var expectedBytes = (long)(((8000 + 192) * 1000.0 / 8.0) * 60.0 * 1.015);
        Assert.Equal(expectedBytes, sizeVbr);
    }
}
