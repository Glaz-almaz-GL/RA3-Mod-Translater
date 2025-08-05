using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Threading.Tasks;

namespace RA3_Mod_Translater
{
    public class GoogleTranslateSelenium : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly Task initialization;

        public GoogleTranslateSelenium()
        {
            var options = new ChromeOptions();
            //options.AddArgument("--headless"); // Убери, если хочешь видеть окно
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--lang=ru");

            driver = new ChromeDriver(options);
            initialization = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                driver.Navigate().GoToUrl("https://translate.google.com/?sl=de&tl=ru&op=translate");

                Task.Delay(2000).Wait();
            });
        }

        public async Task<string> TranslateAsync(string text)
        {
            await initialization; // Гарантируем, что страница загружена

            return await Task.Run(() =>
            {
                try
                {
                    // Очищаем и вводим текст
                    var inputField = driver.FindElement(By.ClassName("er8xn"));
                    inputField.Clear();
                    inputField.SendKeys(text);

                    // Ждём немного, пока появится результат (можно улучшить через WebDriverWait)
                    Task.Delay(3000).Wait(); // Да, это не идеально, но работает для простых случаев

                    // Находим поле с переводом
                    var outputField = driver.FindElement(By.ClassName("ryNqvb")); // Это div с результатом
                    string translated = outputField.Text;

                    // Очищаем поле после перевода
                    inputField.Clear();

                    return string.IsNullOrWhiteSpace(translated) ? text : translated;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при переводе '{text}': {ex.Message}");
                    return text;
                }
            });
        }

        public void Dispose()
        {
            driver?.Quit();
            driver?.Dispose();
        }
    }
}
