using System.Globalization;
using System.Text.Json;

using static CtxSignlib.Functions;
namespace Mywebext.Language
{
    /// <summary>
    /// Global language translation service.
    /// Layout:
    /// Language/
    ///   Packs/
    ///     en.json
    ///     fr.json
    ///   Service/
    ///     LanguageService.cs
    /// </summary>
    public static class LanguageService
    {
        private static readonly object Sync = new();

        private static readonly Dictionary<string, Dictionary<string, string>> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        private static string _defaultLanguage = "en";
        private static string _currentLanguage = "en";

        /// <summary>
        /// Root path to Language/Packs
        /// </summary>
        private static string _packsPath = Path.Combine(AppContext.BaseDirectory, "Language", "Packs");

        /// <summary>
        /// Gets the currently active language code.
        /// </summary>
        public static string CurrentLanguage => _currentLanguage;

        /// <summary>
        /// Gets the default fallback language code.
        /// </summary>
        public static string DefaultLanguage => _defaultLanguage;

        /// <summary>
        /// Gets the path where language packs are stored.
        /// </summary>
        public static string PacksPath => _packsPath;

        /// <summary>
        /// Initializes the language service.
        /// Call once at startup.
        /// </summary>
        public static void Init(
            string? languageCode = null,
            string? defaultLanguage = "en",
            string? packsPath = null,
            bool useCurrentUICulture = false)
        {
            lock (Sync)
            {
                if (!string.IsNullOrWhiteSpace(packsPath))
                    _packsPath = packsPath;

                if (!string.IsNullOrWhiteSpace(defaultLanguage))
                    _defaultLanguage = NormalizeLanguage(defaultLanguage);

                if (useCurrentUICulture)
                    _currentLanguage = NormalizeLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                else if (!string.IsNullOrWhiteSpace(languageCode))
                    _currentLanguage = NormalizeLanguage(languageCode);
                else
                    _currentLanguage = _defaultLanguage;

                Cache.Clear();

                EnsureLoaded(_defaultLanguage);
                EnsureLoaded(_currentLanguage);
            }
        }

        /// <summary>
        /// Sets the active language.
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return;

            lock (Sync)
            {
                _currentLanguage = NormalizeLanguage(languageCode);
                EnsureLoaded(_defaultLanguage);
                EnsureLoaded(_currentLanguage);
            }
        }

        /// <summary>
        /// Sets the default fallback language.
        /// </summary>
        public static void SetDefaultLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return;

            lock (Sync)
            {
                _defaultLanguage = NormalizeLanguage(languageCode);
                EnsureLoaded(_defaultLanguage);
            }
        }

        /// <summary>
        /// Sets the packs folder directly.
        /// </summary>
        public static void SetPacksPath(string packsPath)
        {
            if (string.IsNullOrWhiteSpace(packsPath))
                return;

            lock (Sync)
            {
                _packsPath = packsPath;
                Cache.Clear();
                EnsureLoaded(_defaultLanguage);
                EnsureLoaded(_currentLanguage);
            }
        }

        /// <summary>
        /// Uses the current UI culture to set the active language.
        /// Example: en-US -> en
        /// </summary>
        public static void UseCurrentUICulture()
        {
            SetLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }

        /// <summary>
        /// Translate by key, falling back to:
        /// current language -> default language -> fallback -> key
        /// </summary>
        public static string T(string key, string fallback, params object[] args)
        {
            if (Null(key))
                return FormatSafe(fallback, args);

            string value = GetValue(key) ?? fallback ?? key;
            return FormatSafe(value, args);
        }

        /// <summary>
        /// Translate by key, falling back to the key itself.
        /// </summary>
        public static string T(string key, params object[] args)
        {
            if (Null(key))
                return string.Empty;

            string value = GetValue(key) ?? key;
            return FormatSafe(value, args);
        }

        /// <summary>
        /// Tries to get a translated value.
        /// </summary>
        public static bool TryT(string key, out string value)
        {
            value = string.Empty;

            if (Null(key))
                return false;

            string? result = GetValue(key);
            if (Null(result))
                return false;

            value = result!;
            return true;
        }

        /// <summary>
        /// Returns true if the key exists in either current or default language.
        /// </summary>
        public static bool Has(string key)
        {
            if (Null(key))
                return false;

            lock (Sync)
            {
                EnsureLoaded(_defaultLanguage);
                EnsureLoaded(_currentLanguage);

                return
                    (Cache.TryGetValue(_currentLanguage, out var current) && current.ContainsKey(key)) ||
                    (Cache.TryGetValue(_defaultLanguage, out var fallback) && fallback.ContainsKey(key));
            }
        }

        /// <summary>
        /// Reloads all language packs from disk.
        /// </summary>
        public static void Reload()
        {
            lock (Sync)
            {
                Cache.Clear();
                EnsureLoaded(_defaultLanguage);
                EnsureLoaded(_currentLanguage);
            }
        }

        private static string? GetValue(string key)
        {
            lock (Sync)
            {
                EnsureLoaded(_defaultLanguage);
                EnsureLoaded(_currentLanguage);

                if (Cache.TryGetValue(_currentLanguage, out var current) &&
                    current.TryGetValue(key, out var currentValue) &&
                    !Null(currentValue))
                {
                    return currentValue;
                }

                if (Cache.TryGetValue(_defaultLanguage, out var fallback) &&
                    fallback.TryGetValue(key, out var fallbackValue) &&
                    !Null(fallbackValue))
                {
                    return fallbackValue;
                }

                return null;
            }
        }

        private static void EnsureLoaded(string languageCode)
        {
            if (Cache.ContainsKey(languageCode))
                return;

            Cache[languageCode] = LoadLanguageFile(languageCode);
        }

        private static Dictionary<string, string> LoadLanguageFile(string languageCode)
        {
            try
            {
                string filePath = Path.Combine(_packsPath, $"{languageCode}.json");

                if (!File.Exists(filePath))
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                string json = File.ReadAllText(filePath);

                using JsonDocument doc = JsonDocument.Parse(json);

                Dictionary<string, string> flat = new(StringComparer.OrdinalIgnoreCase);
                FlattenJson(doc.RootElement, null, flat);

                return flat;
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void FlattenJson(JsonElement element, string? prefix, Dictionary<string, string> output)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty prop in element.EnumerateObject())
                    {
                        string nextKey = Null(prefix)
                            ? prop.Name
                            : $"{prefix}.{prop.Name}";

                        FlattenJson(prop.Value, nextKey, output);
                    }
                    break;

                case JsonValueKind.String:
                    if (!Null(prefix))
                        output[prefix!] = element.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (!Null(prefix))
                        output[prefix!] = element.ToString();
                    break;
            }
        }

        private static string NormalizeLanguage(string languageCode)
        {
            if (Null(languageCode))
                return "en";

            languageCode = languageCode.Trim();

            try
            {
                return CultureInfo.GetCultureInfo(languageCode)
                    .TwoLetterISOLanguageName
                    .ToLowerInvariant();
            }
            catch
            {
                int dash = languageCode.IndexOf('-');
                if (dash > 0)
                    languageCode = languageCode[..dash];

                return languageCode.Trim().ToLowerInvariant();
            }
        }

        private static string FormatSafe(string? text, params object[] args)
        {
            if (Null(text))
                return string.Empty;

            if (args == null || args.Length == 0)
                return text!;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, text!, args);
            }
            catch
            {
                return text!;
            }
        }
    }
}
