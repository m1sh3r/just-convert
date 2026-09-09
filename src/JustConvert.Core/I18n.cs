using System.Globalization;
using System.Windows.Markup;

namespace JustConvert.Core;

public static class I18n
{
    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["MenuTitle"] = "Convert to...",
            ["MenuToolTip"] = "Quick file format conversion",
            ["MenuCompress"] = "Compress video (H.264)",
            ["MenuFrames"] = "Extract frames (PNG)",
            ["MenuReencode"] = "Re-encode file",
            ["MenuConvertTo"] = "Convert to {0}",

            ["TitleError"] = "Conversion error",
            ["ErrorDefault"] = "An error occurred while processing the file.",
            ["BtnCopyError"] = "Copy error",
            ["BtnCopied"] = "Copied",
            ["BtnCancel"] = "Cancel",
            ["BtnClose"] = "Close",
            ["BtnYes"] = "Yes",
            ["BtnNo"] = "No",

            ["ImageLoading"] = "Loading image...",
            ["EncodingTo"] = "Encoding to {0}...",
            ["UnsupportedFormat"] = "Unsupported format: {0}",

            ["FfmpegNotFound"] = "FFmpeg was not found. Install it via 'winget install Gyan.FFmpeg'.",
            ["ImageMagickNotFound"] = "ImageMagick was not found. Please install ImageMagick or run the Just Convert installer.",
            ["ImageMagickExitError"] = "ImageMagick exited with error code {0}.",
            ["VideoCompressing"] = "Compressing video...",
            ["VideoExtractingFrames"] = "Extracting frames...",
            ["MediaConverting"] = "Converting media...",
            ["FfmpegProcessing"] = "Processing with FFmpeg...",
            ["FfmpegExitError"] = "FFmpeg exited with error code {0}.",
            ["StatusDone"] = "Done",
            ["StatusSkipped"] = "Skipped",
            ["StatusSkippedAlreadyTarget"] = "Skipped (already in target format)",
            ["StatusSkippedUnsupported"] = "Skipped (unsupported format)",
            ["StatusCancelling"] = "Cancelling...",
            ["StatusCancelled"] = "Conversion cancelled",
            ["StatusItemCancelled"] = "Cancelled",
            ["StatusPreparing"] = "Preparing...",
            ["TitleConverting"] = "Converting...",
            ["TimeLabel"] = "Time: ",

            ["QueueTitle"] = "Conversion Queue",
            ["LabelParallelConversions"] = "Parallel conversions:",
            ["BtnPauseAll"] = "Pause all",
            ["BtnResumeAll"] = "Resume all",
            ["BtnCancelAll"] = "Cancel all",
            ["TooltipPause"] = "Pause",
            ["TooltipResume"] = "Resume",
            ["TooltipCancel"] = "Cancel",
            ["TooltipError"] = "View error",
            ["StatusQueued"] = "Queued",
            ["StatusConverting"] = "Converting...",
            ["StatusPaused"] = "Paused",
            ["StatusAutoPaused"] = "Paused (limit)",
            ["StatusAllDone"] = "All conversions completed",
            ["StatusCompletedWithErrors"] = "Completed: {0}, Errors: {1}",
            ["StatusSummary"] = "Completed {0} of {1}",

            ["FileNotFound"] = "Source file does not exist: {0}",
            ["NoConverterFound"] = "No suitable converter found for '{0}' -> '{1}'",
            ["CliMissingArgs"] = "Error: Specify file path and target format (--to <format>).",

            ["FormatMp4"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H264"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H265"] = "MP4 (H.265, CQ 23)",
            ["FormatWebmVp9"] = "WEBM (VP9, CQ 23)",
            ["FormatWebmAv1"] = "WEBM (AV1, CQ 23)",
            ["FormatMp4H264Nvenc"] = "MP4 (H.264 NVENC, CQ 23)",
            ["FormatMp4H265Nvenc"] = "MP4 (H.265 NVENC, CQ 23)",
            ["FormatWebmAv1Nvenc"] = "WEBM (AV1 NVENC, CQ 23)",
            ["FormatMp4H264Qsv"] = "MP4 (H.264 QSV, CQ 23)",
            ["FormatMp4H265Qsv"] = "MP4 (H.265 QSV, CQ 23)",
            ["FormatWebmVp9Qsv"] = "WEBM (VP9 QSV, CQ 23)",
            ["FormatWebmAv1Qsv"] = "WEBM (AV1 QSV, CQ 23)",
            ["FormatMp4H264Amf"] = "MP4 (H.264 AMF, CQ 23)",
            ["FormatMp4H265Amf"] = "MP4 (H.265 AMF, CQ 23)",
            ["FormatWebmAv1Amf"] = "WEBM (AV1 AMF, CQ 23)",
            ["FormatProRes422"] = "MOV (Apple ProRes 422)",
            ["FormatProRes4444"] = "MOV (Apple ProRes 4444)",
            ["FormatGif"] = "GIF (Animation)",
            ["FormatMp3"] = "MP3 (Audio)",
            ["FormatWav"] = "WAV (Audio)",
            ["FormatFlac"] = "FLAC (Lossless)",
            ["FormatAac"] = "AAC (Audio)",
            ["FormatOgg"] = "OGG (Audio)",
            ["FormatM4a"] = "M4A (Audio)",
            ["FormatBmp"] = "BMP",
            ["FormatPng"] = "PNG",
            ["FormatWebp"] = "WEBP",
            ["FormatTiff"] = "TIFF",
            ["FormatTga"] = "TGA",
            ["FormatIco"] = "ICO (Icon)",
            ["FormatJp2"] = "JPEG 2000",
            ["FormatPcx"] = "PCX",
            ["FormatPpm"] = "PPM",
            ["FormatAvif"] = "AVIF",

            ["SetupTitle"] = "Just Convert Setup",
            ["SetupTitleWithVersion"] = "Just Convert Setup v{0}",
            ["SetupScopeLabel"] = "Install for:",
            ["SetupScopeCurrentUser"] = "Current user (AppData\\Roaming)",
            ["SetupScopeAllUsers"] = "All users (Program Files)",
            ["SetupBtnInstall"] = "Install",
            ["SetupBtnUninstall"] = "Uninstall",
            ["SetupCopying"] = "Copying files...",
            ["SetupRegistering"] = "Registering context menu...",
            ["SetupDownloadingFfmpeg"] = "Downloading FFmpeg...",
            ["SetupDownloadingFfmpegProgress"] = "Downloading FFmpeg: {0:F1} MB / {1:F1} MB ({2:F0}%)",
            ["SetupExtractingFfmpeg"] = "Extracting FFmpeg...",
            ["SetupDownloadingMagick"] = "Downloading ImageMagick...",
            ["SetupDownloadingMagickProgress"] = "Downloading ImageMagick: {0:F1} MB / {1:F1} MB ({2:F0}%)",
            ["SetupExtractingMagick"] = "Extracting ImageMagick...",
            ["SetupUninstalling"] = "Uninstalling files...",
            ["SetupSuccessHeader"] = "Installation Completed",
            ["SetupSuccessText"] = "Just Convert is ready. Right-click any supported file in File Explorer to convert.",
            ["SetupUninstallSuccessHeader"] = "Uninstallation Completed",
            ["SetupUninstallSuccessText"] = "Just Convert has been removed from your system.",
            ["SetupErrorHeader"] = "Setup failed",
            ["SetupCancelConfirmTitle"] = "Cancel Installation",
            ["SetupCancelConfirmText"] = "Are you sure you want to cancel the installation?"
        },
        ["ru"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["MenuTitle"] = "Конвертировать в...",
            ["MenuToolTip"] = "Быстрая конвертация файла в другой формат",
            ["MenuCompress"] = "Сжать видео (H.264)",
            ["MenuFrames"] = "Разбить на кадры (PNG)",
            ["MenuReencode"] = "Перекодировать файл",
            ["MenuConvertTo"] = "Конвертировать в {0}",

            ["TitleError"] = "Ошибка конвертации",
            ["ErrorDefault"] = "Произошла ошибка при обработке файла.",
            ["BtnCopyError"] = "Скопировать ошибку",
            ["BtnCopied"] = "Скопировано",
            ["BtnCancel"] = "Отмена",
            ["BtnClose"] = "Закрыть",
            ["BtnYes"] = "Да",
            ["BtnNo"] = "Нет",

            ["ImageLoading"] = "Загрузка изображения...",
            ["EncodingTo"] = "Кодирование в {0}...",
            ["UnsupportedFormat"] = "Неподдерживаемый формат: {0}",

            ["FfmpegNotFound"] = "FFmpeg не найден. Установите через 'winget install Gyan.FFmpeg'.",
            ["ImageMagickNotFound"] = "ImageMagick не найден. Установите ImageMagick или запустите установщик Just Convert.",
            ["ImageMagickExitError"] = "ImageMagick завершился с кодом ошибки {0}.",
            ["VideoCompressing"] = "Сжатие видео...",
            ["VideoExtractingFrames"] = "Извлечение кадров...",
            ["MediaConverting"] = "Конвертация медиа...",
            ["FfmpegProcessing"] = "Обработка FFmpeg...",
            ["FfmpegExitError"] = "FFmpeg завершился с кодом ошибки {0}.",
            ["StatusDone"] = "Готово",
            ["StatusSkipped"] = "Пропущено",
            ["StatusSkippedAlreadyTarget"] = "Пропущено (уже в целевом формате)",
            ["StatusSkippedUnsupported"] = "Пропущено (неподдерживаемый формат)",
            ["StatusCancelling"] = "Отмена...",
            ["StatusCancelled"] = "Конвертация отменена",
            ["StatusItemCancelled"] = "Отменено",
            ["StatusPreparing"] = "Подготовка...",
            ["TitleConverting"] = "Конвертация...",
            ["TimeLabel"] = "Время: ",

            ["QueueTitle"] = "Очередь конвертации",
            ["LabelParallelConversions"] = "Параллельно:",
            ["BtnPauseAll"] = "Пауза всех",
            ["BtnResumeAll"] = "Возобновить все",
            ["BtnCancelAll"] = "Отменить все",
            ["TooltipPause"] = "Пауза",
            ["TooltipResume"] = "Возобновить",
            ["TooltipCancel"] = "Отменить",
            ["TooltipError"] = "Подробнее об ошибке",
            ["StatusQueued"] = "В очереди",
            ["StatusConverting"] = "Конвертация...",
            ["StatusPaused"] = "На паузе",
            ["StatusAutoPaused"] = "Пауза (лимит)",
            ["StatusAllDone"] = "Все конвертации завершены",
            ["StatusCompletedWithErrors"] = "Завершено: {0}, Ошибок: {1}",
            ["StatusSummary"] = "Выполнено {0} из {1}",

            ["FileNotFound"] = "Исходный файл не существует: {0}",
            ["NoConverterFound"] = "Не найден подходящий конвертер для '{0}' -> '{1}'",
            ["CliMissingArgs"] = "Ошибка: Укажите путь к файлу и целевой формат (--to <формат>).",

            ["FormatMp4"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H264"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H265"] = "MP4 (H.265, CQ 23)",
            ["FormatWebmVp9"] = "WEBM (VP9, CQ 23)",
            ["FormatWebmAv1"] = "WEBM (AV1, CQ 23)",
            ["FormatMp4H264Nvenc"] = "MP4 (H.264 NVENC, CQ 23)",
            ["FormatMp4H265Nvenc"] = "MP4 (H.265 NVENC, CQ 23)",
            ["FormatWebmAv1Nvenc"] = "WEBM (AV1 NVENC, CQ 23)",
            ["FormatMp4H264Qsv"] = "MP4 (H.264 QSV, CQ 23)",
            ["FormatMp4H265Qsv"] = "MP4 (H.265 QSV, CQ 23)",
            ["FormatWebmVp9Qsv"] = "WEBM (VP9 QSV, CQ 23)",
            ["FormatWebmAv1Qsv"] = "WEBM (AV1 QSV, CQ 23)",
            ["FormatMp4H264Amf"] = "MP4 (H.264 AMF, CQ 23)",
            ["FormatMp4H265Amf"] = "MP4 (H.265 AMF, CQ 23)",
            ["FormatWebmAv1Amf"] = "WEBM (AV1 AMF, CQ 23)",
            ["FormatProRes422"] = "MOV (Apple ProRes 422)",
            ["FormatProRes4444"] = "MOV (Apple ProRes 4444)",
            ["FormatGif"] = "GIF (Анимация)",
            ["FormatMp3"] = "MP3 (Аудио)",
            ["FormatWav"] = "WAV (Аудио)",
            ["FormatFlac"] = "FLAC (Без потерь)",
            ["FormatAac"] = "AAC (Аудио)",
            ["FormatOgg"] = "OGG (Аудио)",
            ["FormatM4a"] = "M4A (Аудио)",
            ["FormatBmp"] = "BMP",
            ["FormatPng"] = "PNG",
            ["FormatWebp"] = "WEBP",
            ["FormatTiff"] = "TIFF",
            ["FormatTga"] = "TGA",
            ["FormatIco"] = "ICO (Иконка)",
            ["FormatJp2"] = "JPEG 2000",
            ["FormatPcx"] = "PCX",
            ["FormatPpm"] = "PPM",
            ["FormatAvif"] = "AVIF",

            ["SetupTitle"] = "Установка Just Convert",
            ["SetupTitleWithVersion"] = "Установка Just Convert v{0}",
            ["SetupScopeLabel"] = "Установить для:",
            ["SetupScopeCurrentUser"] = "Текущего пользователя (AppData\\Roaming)",
            ["SetupScopeAllUsers"] = "Всех пользователей (Program Files)",
            ["SetupBtnInstall"] = "Установить",
            ["SetupBtnUninstall"] = "Удалить",
            ["SetupCopying"] = "Копирование файлов...",
            ["SetupRegistering"] = "Регистрация контекстного меню...",
            ["SetupDownloadingFfmpeg"] = "Загрузка FFmpeg...",
            ["SetupDownloadingFfmpegProgress"] = "Загрузка FFmpeg: {0:F1} МБ / {1:F1} МБ ({2:F0}%)",
            ["SetupExtractingFfmpeg"] = "Распаковка FFmpeg...",
            ["SetupDownloadingMagick"] = "Загрузка ImageMagick...",
            ["SetupDownloadingMagickProgress"] = "Загрузка ImageMagick: {0:F1} МБ / {1:F1} МБ ({2:F0}%)",
            ["SetupExtractingMagick"] = "Распаковка ImageMagick...",
            ["SetupUninstalling"] = "Удаление файлов...",
            ["SetupSuccessHeader"] = "Установка завершена",
            ["SetupSuccessText"] = "Just Convert успешно установлен. Для конвертации поддерживаемых файлов откройте контекстное меню и выберите «Конвертировать в...».",
            ["SetupUninstallSuccessHeader"] = "Удаление завершено",
            ["SetupUninstallSuccessText"] = "Just Convert был успешно удален с компьютера.",
            ["SetupErrorHeader"] = "Ошибка установки",
            ["SetupCancelConfirmTitle"] = "Отмена установки",
            ["SetupCancelConfirmText"] = "Вы действительно хотите отменить установку?"
        }
    };

    public static string CurrentLanguage
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (Strings.ContainsKey(culture)) return culture.ToLowerInvariant();

            var installed = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
            if (Strings.ContainsKey(installed)) return installed.ToLowerInvariant();

            return "en";
        }
    }

    public static string T(string key, params object[] args)
    {
        var lang = CurrentLanguage;
        if (!Strings.TryGetValue(lang, out var table) || !table.TryGetValue(key, out var template))
        {
            if (!Strings["en"].TryGetValue(key, out template))
            {
                return key;
            }
        }

        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, template, args) : template;
    }

    public static string MenuTitle => T("MenuTitle");
    public static string MenuToolTip => T("MenuToolTip");

    public static string GetSubMenuTitle(string targetFormat)
    {
        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();
        return fmt switch
        {
            "reencode" => T("MenuReencode"),
            "frames" or "frames-png" or "frames-jpg" => T("MenuFrames"),
            "mp4" or "mp4-h264" or "h264" => T("FormatMp4H264"),
            "mp4-hevc" or "mp4-h265" or "hevc" or "h265" => T("FormatMp4H265"),
            "webm-vp9" or "vp9" or "webm" => T("FormatWebmVp9"),
            "webm-av1" or "av1" or "mp4-av1" => T("FormatWebmAv1"),
            "mp4-h264-nvenc" or "mp4-nvenc-h264" => T("FormatMp4H264Nvenc"),
            "mp4-h265-nvenc" or "mp4-hevc-nvenc" or "mp4-nvenc-hevc" or "mp4-nvenc-h265" => T("FormatMp4H265Nvenc"),
            "webm-av1-nvenc" or "webm-nvenc-av1" or "mp4-av1-nvenc" or "mp4-nvenc-av1" => T("FormatWebmAv1Nvenc"),
            "mp4-h264-qsv" or "mp4-qsv-h264" => T("FormatMp4H264Qsv"),
            "mp4-h265-qsv" or "mp4-hevc-qsv" or "mp4-qsv-hevc" or "mp4-qsv-h265" => T("FormatMp4H265Qsv"),
            "webm-vp9-qsv" or "webm-qsv-vp9" => T("FormatWebmVp9Qsv"),
            "webm-av1-qsv" or "webm-qsv-av1" or "mp4-av1-qsv" or "mp4-qsv-av1" => T("FormatWebmAv1Qsv"),
            "mp4-h264-amf" or "mp4-amf-h264" => T("FormatMp4H264Amf"),
            "mp4-h265-amf" or "mp4-hevc-amf" or "mp4-amf-hevc" or "mp4-amf-h265" => T("FormatMp4H265Amf"),
            "webm-av1-amf" or "webm-amf-av1" or "mp4-av1-amf" or "mp4-amf-av1" => T("FormatWebmAv1Amf"),
            "mov-prores422" or "prores422" or "prores" => T("FormatProRes422"),
            "mov-prores4444" or "prores4444" => T("FormatProRes4444"),
            "gif" => T("FormatGif"),
            "mp3" => T("FormatMp3"),
            "wav" => T("FormatWav"),
            "flac" => T("FormatFlac"),
            "aac" => T("FormatAac"),
            "ogg" => T("FormatOgg"),
            "m4a" => T("FormatM4a"),
            "bmp" => T("FormatBmp"),
            "png" => T("FormatPng"),
            "webp" => T("FormatWebp"),
            "tiff" or "tif" => T("FormatTiff"),
            "tga" => T("FormatTga"),
            "ico" => T("FormatIco"),
            "jp2" or "jpeg2000" => T("FormatJp2"),
            "pcx" => T("FormatPcx"),
            "ppm" => T("FormatPpm"),
            "avif" => T("FormatAvif"),
            _ => targetFormat.ToUpperInvariant()
        };
    }

    public static string GetSubMenuToolTip(string targetFormat)
    {
        if (targetFormat.Equals("reencode", StringComparison.OrdinalIgnoreCase))
        {
            return T("MenuReencode");
        }

        var title = GetSubMenuTitle(targetFormat);
        return T("MenuConvertTo", title);
    }
}

[MarkupExtensionReturnType(typeof(string))]
public class LocExtension : System.Windows.Markup.MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocExtension() { }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key)) return string.Empty;
        return I18n.T(Key);
    }
}
