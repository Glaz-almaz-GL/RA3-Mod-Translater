using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RA3_Mod_Translater
{
    public static partial class TranslateManager
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly Dictionary<string, string> TranslationCache = new();

        public static string GetTotalTime(int phrasesCount)
        {
            int milliseconds = 50 * phrasesCount;

            TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);
            int hours = timeSpan.Hours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;

            return $"На перевод уйдёт ~ {hours}ч. {minutes}мин. и {seconds}сек.";
        }

        public static async Task<string> TranslateAllQuotedTextAsync(string input)
        {
            var matches = Regex.Matches(input, "\"(.*?)\"")
                               .Cast<Match>()
                               .ToList();

            matches.Sort((a, b) => b.Index.CompareTo(a.Index)); // с конца

            string result = input;

            int i = 1;
            int matchesCount = matches.Count;


            string totalTime = GetTotalTime(matchesCount);
            Console.WriteLine($"Обнаружено {matchesCount} фраз для перевода.");
            Console.WriteLine(totalTime);

            foreach (var match in matches)
            {
                string originalText = match.Groups[1].Value;

                if (string.IsNullOrWhiteSpace(originalText) || ProtectedPatterns().IsMatch(originalText))
                {
                    continue;
                }

                string translatedText = await TranslatePreservingTagsAsync(originalText);
                string replacement = $"\"{translatedText}\"";

                Console.WriteLine($"{i}/{matchesCount}) Запрос: {originalText} | Ответ: {replacement}");

                result = result.Substring(0, match.Index) +
                         replacement +
                         result.Substring(match.Index + match.Length);

                Console.Title = $"RA3 Mod Translater - [{i}/{matchesCount}]";
                i++;
            }

            return result;
        }

        public static async Task<string> TranslatePreservingTagsAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string afterUrls = ProtectSpecialPatternsManager.ProtectUrls(text);
            string afterRules = ProtectSpecialPatternsManager.ProtectSpecialPatterns(afterUrls);
            var (protectedText, tagInfos) = ProtectSpecialPatternsManager.ProtectTags(afterRules);
            string afterAmpersand = await TranslateAmpersandTermsAsync(protectedText);
            string translatedText = await TranslateWithDelayAsync(afterAmpersand);
            string restoredTags = ProtectSpecialPatternsManager.RestoreTags(translatedText, tagInfos);
            string restoredRules = ProtectSpecialPatternsManager.RestoreSpecialPatterns(restoredTags);
            string finalResult = ProtectSpecialPatternsManager.RestoreUrls(restoredRules);

            return finalResult;
        }

        public static async Task<string> TranslateAmpersandTermsAsync(string text)
        {
            var matches = Regex.Matches(text, @"&([a-zA-Z][a-zA-Z0-9]*)")
                               .Cast<Match>()
                               .ToList();

            if (matches.Count == 0) return text;

            var keywordsToTranslate = new HashSet<string>();
            foreach (Match m in matches)
            {
                keywordsToTranslate.Add(m.Groups[1].Value);
            }

            var translationTasks = new Dictionary<string, Task<string>>();
            foreach (string kw in keywordsToTranslate)
            {
                if (!TranslationCache.TryGetValue(kw, out _))
                {
                    translationTasks[kw] = TranslateWithDelayAsync(kw);
                }
            }

            if (translationTasks.Count > 0)
            {
                var results = await Task.WhenAll(translationTasks.Values);
                var newTranslations = keywordsToTranslate.Zip(results, (k, v) => new { k, v });
                foreach (var item in newTranslations)
                {
                    TranslationCache[item.k] = item.v;
                }
            }

            string result = text;
            foreach (Match m in matches)
            {
                string keyword = m.Groups[1].Value;
                string replacement = "&" + TranslationCache[keyword];
                result = result.Replace(m.Value, replacement);
            }

            return result;
        }

        public static async Task<string> TranslateWithDelayAsync(string text)
        {
            if (TranslationCache.TryGetValue(text, out string? cached))
                return cached;

            const int MaxRetries = 3;
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await Task.Delay(attempt == 1 ? 50 : 100 * attempt);

                    string url = BuildTranslateUrl(text);
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    string translated = ExtractTranslation(jsonResponse);

                    if (!string.IsNullOrEmpty(translated))
                    {
                        TranslationCache[text] = translated;
                        return translated;
                    }
                }
                catch (Exception ex) when (attempt < MaxRetries)
                {
                    Console.WriteLine($"❌ Ошибка перевода '{text}' (попытка {attempt}): {ex.Message}");
                }
            }

            Console.WriteLine($"❌ Не удалось перевести: {text}");
            TranslationCache[text] = text;
            return text;
        }

        public static string BuildTranslateUrl(string text)
        {
            string encodedText = Uri.EscapeDataString(text);
            return $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q={encodedText}";
        }

        public static string ExtractTranslation(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var first = root[0];
                    if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() > 0)
                    {
                        var sub = first[0];
                        if (sub.ValueKind == JsonValueKind.Array && sub.GetArrayLength() > 0)
                        {
                            var trans = sub[0];
                            if (trans.ValueKind == JsonValueKind.String)
                            {
                                return trans.GetString()!;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка парсинга JSON: {ex.Message}");
            }

            return string.Empty;
        }

        [GeneratedRegex(@"^(\$\w+|&\w+|\[@[^\]]+\]|\{[^\}]+\}|%\w+%)+$")]
        private static partial Regex ProtectedPatterns();
    }
}
