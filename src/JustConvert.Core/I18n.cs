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
            ["VideoCompressing"] = "Compressing video...",
            ["VideoExtractingFrames"] = "Extracting frames...",
            ["MediaConverting"] = "Converting media...",
            ["FfmpegProcessing"] = "Processing with FFmpeg...",
            ["FfmpegExitError"] = "FFmpeg exited with error code {0}.",
            ["StatusDone"] = "Done",
            ["TimeLabel"] = "Time: ",

            ["FileNotFound"] = "Source file does not exist: {0}",
            ["NoConverterFound"] = "No suitable converter found for '{0}' -> '{1}'",
            ["CliMissingArgs"] = "Error: Specify file path and target format (--to <format>).",

            ["FormatMp4"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H264"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H265"] = "MP4 (H.265, CQ 23)",
            ["FormatProRes422"] = "MOV (Apple ProRes 422)",
            ["FormatProRes4444"] = "MOV (Apple ProRes 4444)",
            ["FormatGif"] = "GIF (Animation)",
            ["FormatMp3"] = "MP3 (Audio)",
            ["FormatWav"] = "WAV (Audio)",
            ["FormatFlac"] = "FLAC (Lossless)",
            ["FormatAac"] = "AAC (Audio)",
            ["FormatOgg"] = "OGG (Audio)",
            ["FormatM4a"] = "M4A (Audio)",

            ["SetupTitle"] = "Just Convert Setup",
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
            ["VideoCompressing"] = "Сжатие видео...",
            ["VideoExtractingFrames"] = "Извлечение кадров...",
            ["MediaConverting"] = "Конвертация медиа...",
            ["FfmpegProcessing"] = "Обработка FFmpeg...",
            ["FfmpegExitError"] = "FFmpeg завершился с кодом ошибки {0}.",
            ["StatusDone"] = "Готово",
            ["TimeLabel"] = "Время: ",

            ["FileNotFound"] = "Исходный файл не существует: {0}",
            ["NoConverterFound"] = "Не найден подходящий конвертер для '{0}' -> '{1}'",
            ["CliMissingArgs"] = "Ошибка: Укажите путь к файлу и целевой формат (--to <формат>).",

            ["FormatMp4"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H264"] = "MP4 (H.264, CQ 23)",
            ["FormatMp4H265"] = "MP4 (H.265, CQ 23)",
            ["FormatProRes422"] = "MOV (Apple ProRes 422)",
            ["FormatProRes4444"] = "MOV (Apple ProRes 4444)",
            ["FormatGif"] = "GIF (Анимация)",
            ["FormatMp3"] = "MP3 (Аудио)",
            ["FormatWav"] = "WAV (Аудио)",
            ["FormatFlac"] = "FLAC (Без потерь)",
            ["FormatAac"] = "AAC (Аудио)",
            ["FormatOgg"] = "OGG (Аудио)",
            ["FormatM4a"] = "M4A (Аудио)",

            ["SetupTitle"] = "Установка Just Convert",
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
            "frames" or "frames-png" or "frames-jpg" => T("MenuFrames"),
            "mp4" or "mp4-h264" or "h264" => T("FormatMp4H264"),
            "mp4-hevc" or "mp4-h265" or "hevc" or "h265" => T("FormatMp4H265"),
            "mov-prores422" or "prores422" or "prores" => T("FormatProRes422"),
            "mov-prores4444" or "prores4444" => T("FormatProRes4444"),
            "gif" => T("FormatGif"),
            "mp3" => T("FormatMp3"),
            "wav" => T("FormatWav"),
            "flac" => T("FormatFlac"),
            "aac" => T("FormatAac"),
            "ogg" => T("FormatOgg"),
            "m4a" => T("FormatM4a"),
            _ => targetFormat.ToUpperInvariant()
        };
    }

    public static string GetSubMenuToolTip(string targetFormat)
    {
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