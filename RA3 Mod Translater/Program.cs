using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RA3_Mod_Translater
{
    static class Program
    {
        private static string currentResultText = "";
        private static bool isTranslationComplete = false;

        private static string? currentOutputPath;

        static async Task Main()
        {
            Console.CancelKeyPress += OnProcessExit;

            try
            {
                string currentInputText;

                Console.Write("Путь до mod.str файла: ");
                string? inputFilePath = Console.ReadLine()?.Replace("\"", "").Trim();

                if (string.IsNullOrEmpty(inputFilePath) || !File.Exists(inputFilePath))
                {
                    Console.WriteLine("Файл не существует или пуст.");
                    Console.ReadKey();
                    return;
                }

                currentInputText = await File.ReadAllTextAsync(inputFilePath);
                currentOutputPath = Path.ChangeExtension(inputFilePath, ".txt");
                currentResultText = currentInputText;

                Console.WriteLine("Начинаем перевод...");

                string result = await TranslateManager.TranslateAllQuotedTextAsync(currentInputText);
                currentResultText = result;
                isTranslationComplete = true;

                await File.WriteAllTextAsync(currentOutputPath, result);
                Console.WriteLine($"Готово! Результат сохранён в: {currentOutputPath}");
            }
            finally
            {
                Console.CancelKeyPress -= OnProcessExit;
                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();
            }
        }

        static void OnProcessExit(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            Console.WriteLine("\n\n⚠️  Прерывание получено. Сохраняем текущее состояние...");

            try
            {
                if (!string.IsNullOrEmpty(currentOutputPath))
                {
                    string finalContent = currentResultText;

                    if (!isTranslationComplete)
                    {
                        string header = $"# ⚠️ ЧАСТИЧНЫЙ ПЕРЕВОД\n# {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
                        finalContent = header + currentResultText;
                    }

                    File.WriteAllText(currentOutputPath, finalContent);
                    Console.WriteLine($"✅ Частичный результат сохранён:\n   {currentOutputPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}