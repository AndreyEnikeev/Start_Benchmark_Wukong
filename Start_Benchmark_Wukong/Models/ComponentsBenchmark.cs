using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace Start_Benchmark_Wukong.Models
{
    /// <summary>Класс для проверки установленных компонентов</summary>
    internal static class ComponentsBenchmark
    {
        /// <summary>Путь до папки Бенчмарка</summary>
        public static string BenchmarkInstallPath { get; private set; } = string.Empty;
        /// <summary>Путь до exe файла Бенчмарка</summary>
        public static string ExeFilePath { get; private set; } = string.Empty;
        /// <summary>Путь до файла настройки Бенчмарка</summary>
        public static string ConfigFilePath { get; private set; } = string.Empty;

        /// <summary>Коды проверки нужных файлов</summary>
        public enum BenchmarkCheckResult
        {
            /// <summary>Всё установленно и готово</summary>
            Ready = 0,
            /// <summary>Steam не установлен / путь не найден</summary>
            SteamNotFound = 1,
            /// <summary>Steam есть, но бенчмарка нет</summary>
            BenchmarkNotFound = 2,
            /// <summary>Бенчмарк есть, но нету файла с настройками</summary>
            BenchmarkNotConfigFile = 3
        }


        /// <summary>Проверяет, установлен ли бенчмарк в любой из библиотек Steam.</summary>
        public static BenchmarkCheckResult GetBenchmarkStatus()
        {
            string steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath)) return BenchmarkCheckResult.SteamNotFound;

            foreach (string libraryPath in GetLibraryFolders(steamPath))
            {
                string manifestPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_3132990.acf");
                if (!File.Exists(manifestPath)) continue;

                string installDir = ParseInstallDir(manifestPath);
                if (string.IsNullOrEmpty(installDir)) continue;

                string fullPath = Path.Combine(libraryPath, "steamapps", "common", installDir);
                if (!Directory.Exists(fullPath)) continue;

                // Нашли — запоминаем пути
                BenchmarkInstallPath = fullPath;
                ExeFilePath = Path.Combine(fullPath, "b1", "Binaries", "Win64", "b1-Win64-Shipping.exe");
                if (!File.Exists(ExeFilePath))
                {
                    BenchmarkInstallPath = string.Empty;
                    ExeFilePath = string.Empty;
                    continue;
                }

                ConfigFilePath = Path.Combine(fullPath, "b1", "Saved", "Config", "Windows", "GameUserSettings.ini");

                if (!File.Exists(ConfigFilePath))
                    return BenchmarkCheckResult.BenchmarkNotConfigFile;

                return BenchmarkCheckResult.Ready;
            }

            return BenchmarkCheckResult.BenchmarkNotFound;
        }

        /// <summary>Извлекает значение "installdir" из файла манифеста.</summary>
        private static string ParseInstallDir(string manifestPath)
        {
            try
            {
                string content = File.ReadAllText(manifestPath);
                var match = Regex.Match(content, @"""installdir""\s+""([^""]+)""");
                return match.Success ? match.Groups[1].Value : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        /// <summary>Поиск путь установки Steam. Сначала в HKLM (обе ветки), потом в HKCU.</summary>
        private static string GetSteamInstallPath()
        {
            string[] registryPaths =
            {
                @"SOFTWARE\Wow6432Node\Valve\Steam", // 64-битная Windows, 32-битный процесс
                @"SOFTWARE\Valve\Steam",             // 32-битная Windows или 64-битный процесс
            };

            foreach (string path in registryPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key?.GetValue("InstallPath") is string installPath &&
                    !string.IsNullOrEmpty(installPath))
                {
                    return installPath;
                }
            }

            // Фолбэк: путь может храниться в HKCU
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                if (key?.GetValue("SteamPath") is string steamPath &&
                    !string.IsNullOrEmpty(steamPath))
                {
                    return steamPath.Replace('/', '\\');
                }
            }

            return string.Empty;
        }

        /// <summary>Возвращает список всех папок-библиотек Steam (включая основную).</summary>
        private static IEnumerable<string> GetLibraryFolders(string steamPath)
        {
            yield return steamPath; // основная библиотека

            string libraryFoldersFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersFile)) yield break;

            string content;
            try
            {
                content = File.ReadAllText(libraryFoldersFile);
            }
            catch
            {
                yield break;
            }

            // Простой парсинг: ищем строки вида "path"    "D:\\SteamLibrary"
            var matches = Regex.Matches(content, @"""path""\s+""([^""]+)""");
            foreach (Match match in matches)
            {
                string path = match.Groups[1].Value.Replace(@"\\", @"\");
                if (!string.IsNullOrEmpty(path)) yield return path;
            }
        }
    }
}