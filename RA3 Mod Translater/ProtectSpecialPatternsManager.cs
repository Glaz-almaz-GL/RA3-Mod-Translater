using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RA3_Mod_Translater
{
    public static class ProtectSpecialPatternsManager
    {
        // Для URL
        private static readonly List<string> UrlPlaceholders = new();
        private static readonly List<string> RulePlaceholders = new();

        // Префикс для плейсхолдеров
        private static readonly string UrlPlaceholderFormat = "___URL_{0}___";
        private static readonly string RulePlaceholderFormat = "___RULE_{0}___";
        public static string ProtectUrls(string text)
        {
            UrlPlaceholders.Clear();
            // Упрощённый паттерн для URL
            var pattern = @"https?://[^\s\""<>\[\]{}|\\^`]+|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/[^\s\""<>\[\]{}|\\^`]*";
            var matches = Regex.Matches(text, pattern);

            string result = text;
            int shift = 0;

            foreach (Match match in matches)
            {
                string url = match.Value;
                UrlPlaceholders.Add(url);
                string placeholder = string.Format(UrlPlaceholderFormat, UrlPlaceholders.Count - 1);

                result = result.Remove(match.Index + shift, url.Length)
                              .Insert(match.Index + shift, placeholder);
                shift += placeholder.Length - url.Length;
            }

            return result;
        }

        public static string RestoreUrls(string text)
        {
            string result = text;
            for (int i = 0; i < UrlPlaceholders.Count; i++)
            {
                result = result.Replace(string.Format(UrlPlaceholderFormat, i), UrlPlaceholders[i]);
            }
            return result;
        }

        public static string ProtectSpecialPatterns(string text)
        {
            RulePlaceholders.Clear();

            // Пример правила: Cost \d+ Power → 150 ед. энергии
            var costPowerPattern = @"Cost\s+(\d+)\s+Power";

            return Regex.Replace(text, costPowerPattern, match =>
            {
                string value = match.Groups[1].Value;
                string replacement = $"{value} ед. энергии";
                RulePlaceholders.Add(replacement);
                return string.Format(RulePlaceholderFormat, RulePlaceholders.Count - 1);
            });
        }

        public static string RestoreSpecialPatterns(string text)
        {
            string result = text;
            for (int i = 0; i < RulePlaceholders.Count; i++)
            {
                result = result.Replace(string.Format(RulePlaceholderFormat, i), RulePlaceholders[i]);
            }
            return result;
        }

        public class TagInfo
        {
            public string Original { get; set; } = "";
            public string Placeholder { get; set; } = "";
        }

        public static (string protectedText, List<TagInfo> tagInfos) ProtectTags(string text)
        {
            var tagInfos = new List<TagInfo>();
            var pattern = @"\$[a-zA-Z_]\w*|\[@[^\]]+\]|\{[^\}]+\}|%[a-zA-Z0-9_]+%";

            var matches = Regex.Matches(text, pattern);
            string result = text;
            int shift = 0;

            foreach (Match match in matches)
            {
                string tag = match.Value;
                string token = $"__NT{Guid.NewGuid().ToString("N")[..8]}__";

                tagInfos.Add(new TagInfo { Original = tag, Placeholder = token });

                result = result.Remove(match.Index + shift, tag.Length)
                              .Insert(match.Index + shift, token);
                shift += token.Length - tag.Length;
            }

            return (result, tagInfos);
        }

        public static string RestoreTags(string text, List<TagInfo> tagInfos)
        {
            string result = text;
            foreach (var info in tagInfos)
            {
                result = result.Replace(info.Placeholder, info.Original);
            }
            return result;
        }
    }
}
