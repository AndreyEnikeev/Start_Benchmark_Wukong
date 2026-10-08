using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Start_Benchmark_Wukong.Models
{
    /// <summary>Готовит GameUserSettings.ini под конкретный тип теста.</summary>
    internal static class BenchmarkConfigurator
    {
        /// <summary>Режимы тестирования</summary>
        public enum TestMode { Cpu, Gpu }

        /// <summary>Набор параметров теста, которые отображаются в UI</summary>
        public class TestParameters
        {
            /// <summary>Уровень качества для всех sg.*Quality (0 = Low, 4 = Cinematic).</summary>
            public int Level { get; init; }

            /// <summary>Масштаб разрешения в процентах (sg.ResolutionQuality).</summary>
            public double ResolutionQuality { get; init; }

            /// <summary>Ширина окна рендера в пикселях.</summary>
            public int Width { get; init; }

            /// <summary>Высота окна рендера в пикселях.</summary>
            public int Height { get; init; }
        }

        /// <summary>Параметры по умолчанию для режима.</summary>
        public static TestParameters GetDefaultParameters(TestMode mode)
        {
            var (w, h) = mode == TestMode.Gpu ? PickGpuResolution() : PickCpuResolution();
            return new TestParameters
            {
                Level = mode == TestMode.Gpu ? 4 : 0,
                ResolutionQuality = mode == TestMode.Gpu ? 100 : 33,
                Width = w,
                Height = h
            };
        }

        /// <summary>Применяет настройки под нужный режим.</summary>
        public static bool ApplySettings(TestMode mode, TestParameters? custom = null)
        {
            string path = ComponentsBenchmark.ConfigFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            var p = custom ?? GetDefaultParameters(mode);

            ClearReadOnly(path);

            try
            {
                string content = File.ReadAllText(path);
                content = ApplyScalabilityGroups(content, mode, p);
                content = ApplyUiSettings(content, mode, p); // ← теперь принимает p
                content = ApplySingleKeys(content, mode, p);
                File.WriteAllText(path, content);
            }
            catch
            {
                return false;
            }
            finally
            {
                // Возвращаем ReadOnly, чтобы бенчмарк не перезаписал наши правки
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            }

            return true;
        }

        /// <summary>Снимает ReadOnly.</summary>
        public static void ClearReadOnly(string? path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = ComponentsBenchmark.ConfigFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            FileAttributes attrs = File.GetAttributes(path);
            if ((attrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
        }

        /// <summary>Возвращает словарь параметров UISettingData для режима.</summary>
        /// <param name="mode">Режим теста (CPU или GPU).</param>
        /// <param name="p">Параметры теста (используются Dx12 и др.).</param>
        public static Dictionary<string, string> GetUiValues(TestMode mode, TestParameters p)
        {
            // Общие для обоих режимов настройки:
            //   InsertFrame = 0     — генерация кадров ВСЕГДА выключена
            //   SuperResolutionSampling = 0 — апскейл выключен (честный FPS)
            //   Vsync = 0           — вертикальная синхронизация выключена
            //   LockFrameRate = 0   — ограничение FPS выключено
            //   MotionBlur = 0      — размытие движения выключено
            var common = new Dictionary<string, string>
            {
                ["InsertFrame"]             = "0",
                ["SuperResolutionSampling"] = "0",
                ["Vsync"]                   = "0",
                ["LockFrameRate"]           = "0",
                ["MotionBlur"]              = "0",
                ["Dx12"]                    = "1",
            };

            if (mode == TestMode.Gpu)
            {
                // GPU-тест: максимальная нагрузка на видеокарту.
                var gpu = new Dictionary<string, string>(common)
                {
                    // Общий уровень = 6 (максимум, "Cinematic")
                    ["QualityLevel"]       = "6",
                    // Все детальные настройки = 4 (Cinematic)
                    ["ViewDistance"]       = "4",
                    ["AntiAliasing"]       = "4",
                    ["PostProcessing"]     = "4",
                    ["ShadowQuality"]      = "4",
                    ["TextureQuality"]     = "4",
                    ["FxQuality"]          = "4",
                    ["MaterialQuality"]    = "4",
                    ["VegetationQuality"]  = "4",
                    ["GlobalIllumination"] = "4",
                    ["ReflectionQuality"]  = "4",
                    // 1 = включить RTX, уровень RT = 3 (высокий)
                    ["Rtx"]                = "1",
                    ["RtxLevel"]           = "3",
                    // 0 = DLSS выключен, чтобы получить «честный» FPS
                    ["Dlss"]               = "0",
                };
                return gpu;
            }

            // CPU-тест: всё по минимуму, чтобы бутылочным горлышком стал процессор.
            var cpu = new Dictionary<string, string>(common)
            {
                ["QualityLevel"]       = "0", // 0 = Low
                ["ViewDistance"]       = "0",
                ["AntiAliasing"]       = "0",
                ["PostProcessing"]     = "0",
                ["ShadowQuality"]      = "0",
                ["TextureQuality"]     = "0",
                ["FxQuality"]          = "0",
                ["MaterialQuality"]    = "0",
                ["VegetationQuality"]  = "0",
                ["GlobalIllumination"] = "0",
                ["ReflectionQuality"]  = "0",
                ["Rtx"]                = "0",  // RTX выключена
                ["RtxLevel"]           = "0",
                ["Dlss"]               = "1",  // DLSS включён — облегчаем GPU
            };
            return cpu;
        }

        /// <summary>Человекочитаемое значение UISettingData.</summary>
        public static string GetUiValueDisplay(string key, string rawValue)
        {
            bool isOn = rawValue == "1";
            switch (key)
            {
                case "Rtx": return isOn ? "Включена" : "Выключена";
                case "Dlss": return isOn ? "Включён" : "Выключен";
                case "Vsync": return isOn ? "Включена" : "Выключена";
                case "LockFrameRate": return isOn ? "Включено" : "Выключено";
                case "MotionBlur": return isOn ? "Включено" : "Выключено";
                case "InsertFrame": return isOn ? "Включена" : "Выключена";
                case "SuperResolutionSampling": return isOn ? "Включено" : "Выключено";
                case "Dx12": return "DirectX 12";
                case "QualityLevel":
                    return GetQualityLevelName(ParseInt(rawValue));
                default:
                    return $"{GetQualityLevelName(ParseInt(rawValue))} (код {rawValue})";
            }
        }

        private static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;

        /// <summary>Правит секцию [ScalabilityGroups]: sg.ResolutionQuality и sg.*Quality.</summary>
        /// <param name="content">Содержимое ini-файла.</param>
        /// <param name="mode">Режим теста (CPU или GPU).</param>
        /// <param name="p">Параметры теста.</param>
        /// <returns>Отредактированное содержимое ini-файла.</returns>
        private static string ApplyScalabilityGroups(string content, TestMode mode, TestParameters p)
        {
            string resValue = p.ResolutionQuality.ToString(CultureInfo.InvariantCulture);

            // sg.ResolutionQuality — масштаб рендера в процентах
            content = Regex.Replace(content,
                @"^sg\.ResolutionQuality\s*=\s*[\d.,]+",
                $"sg.ResolutionQuality={resValue}",
                RegexOptions.Multiline);

            // sg.*Quality — уровень качества (0..4)
            foreach (string key in ScalabilityGroupKeys)
            {
                content = Regex.Replace(content,
                    $@"^sg\.{key}\s*=\s*\d+",
                    $"sg.{key}={p.Level}",
                    RegexOptions.Multiline);
            }

            return content;
        }

        /// <summary>Правит строку UISettingData=(...) — там, где настройки хранятся</summary>
        /// <param name="content">Содержимое ini-файла.</param>
        /// <param name="mode">Режим теста (CPU или GPU).</param>
        /// <param name="p">Параметры теста.</param>
        /// <returns>Отредактированное содержимое ini-файла.</returns>
        private static string ApplyUiSettings(string content, TestMode mode, TestParameters p)
        {
            foreach (var kvp in GetUiValues(mode, p))
            {
                content = Regex.Replace(content,
                    $@"\(""{Regex.Escape(kvp.Key)}"",\s*""[^""]*""\)",
                    $"(\"{kvp.Key}\", \"{kvp.Value}\")");
            }
            return content;
        }

        /// <summary>=Одиночные ключи верхнего уровня=</summary>
        /// <param name="content">Содержимое ini-файла.</param>
        /// <param name="mode">Режим теста (CPU или GPU).</param>
        /// <param name="p">Параметры теста.</param>
        /// <returns>Отредактированное содержимое ini-файла.</returns>
        private static string ApplySingleKeys(string content, TestMode mode, TestParameters p)
        {
            // bUseVSync=False — вертикальная синхронизация выключена.
            // bUseDynamicResolution=False — фиксированный масштаб рендера,
            //   чтобы sg.ResolutionQuality не переопределялся движком.
            // FrameRateLimit=0 — без ограничения FPS.
            content = SetBoolValue(content, "bUseVSync", false);
            content = SetBoolValue(content, "bUseDynamicResolution", false);
            content = Regex.Replace(content, @"^FrameRateLimit=[\d.,]+",
                "FrameRateLimit=0.000000", RegexOptions.Multiline);

            // Согласия и первый запуск — чтобы не было диалогов при старте.
            content = SetIntValue(content, "PrivacyAgreement", 1);
            content = SetIntValue(content, "AgreementReaded", 1);
            content = SetBoolValue(content, "FirstSettingFinish", true);

            // Разрешение (см. PickCpuResolution / PickGpuResolution).
            int w = p.Width;
            int h = p.Height;

            content = SetIntValue(content, "DesiredScreenWidth", w);
            content = SetIntValue(content, "DesiredScreenHeight", h);
            content = SetIntValue(content, "ResolutionSizeX", w);
            content = SetIntValue(content, "ResolutionSizeY", h);
            content = SetIntValue(content, "LastUserConfirmedResolutionSizeX", w);
            content = SetIntValue(content, "LastUserConfirmedResolutionSizeY", h);
            content = SetIntValue(content, "LastUserConfirmedDesiredScreenWidth", w);
            content = SetIntValue(content, "LastUserConfirmedDesiredScreenHeight", h);

            return content;
        }

        /// <summary>Разрешение для CPU-теста: минимум 1280x720</summary>
        /// <remarks>V\Минимальный видеорежим монитора, но не ниже 720p.</remarks>
        private static (int W, int H) PickCpuResolution()
        {
            const int MinCpuWidth = 1280;
            const int MinCpuHeight = 720;
            var min = ComputerSpecifications.SystemInfo.Display.MinResolutionMode;
            if (min.Width >= MinCpuWidth && min.Height >= MinCpuHeight)
                return (min.Width, min.Height);
            return (MinCpuWidth, MinCpuHeight);
        }

        /// <summary>Разрешение для GPU-теста: максимальный видеорежим монитора.</summary>
        private static (int W, int H) PickGpuResolution()
        {
            var max = ComputerSpecifications.SystemInfo.Display.MaxResolutionMode;
            if (max.Width <= 0 || max.Height <= 0)
                return (1920, 1080);
            return (max.Width, max.Height);
        }

        /// <summary>Установка целочисленного ключа вида целочисленого значения.</summary>
        /// <param name="content">Содержимое ini-файла.</param>
        /// <param name="key">Имя ключа (без пробелов).</param>
        /// <param name="value">Целое значение.</param>
        /// <returns>Отредактированное содержимое ini-файла.</returns>
        private static string SetIntValue(string content, string key, int value)
        {
            return Regex.Replace(content,
                $@"^{Regex.Escape(key)}=\d+",
                $"{key}={value}",
                RegexOptions.Multiline);
        }

        /// <summary>Установка булева ключа вида True/False.</summary>
        /// <param name="content">Содержимое ini-файла.</param>
        /// <param name="key">Имя ключа (без пробелов).</param>
        /// <param name="value">true или false.</param>
        /// <returns>Отредактированное содержимое ini-файла.</returns>
        private static string SetBoolValue(string content, string key, bool value)
        {
            string v = value ? "True" : "False";
            return Regex.Replace(content,
                $@"^{Regex.Escape(key)}=(True|False)",
                $"{key}={v}",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }
        /// <summary>Человекочитаемое название уровня качества.</summary>
        public static string GetQualityLevelName(int level) => level switch
        {
            0 => "Низкое (Low)",
            4 => "Высокое (High)",
            6 => "Максимум (Max)",
            _ => $"Уровень {level}"
        };

        /// <summary>Список sg.*Quality — параметров (без sg.ResolutionQuality).</summary>
        public static readonly string[] ScalabilityGroupKeys =
        {
            "ViewDistanceQuality",         // Дальность прорисовки
            "AntiAliasingQuality",         // Сглаживание
            "ShadowQuality",               // Тени
            "GlobalIlluminationQuality",   // Глобальное освещение
            "RayTracingQuality",           // Трассировка лучей (в sg)
            "ReflectionQuality",           // Отражения
            "PostProcessQuality",          // Постобработка
            "TextureQuality",              // Текстуры
            "EffectsQuality",              // Эффекты
            "FoliageQuality",              // Растительность
            "ShadingQuality"               // Шейдинг
        };

        /// <summary>Человекочитаемое описание sg.*Quality.</summary>
        public static string GetSgKeyDescription(string key) => key switch
        {
            "ViewDistanceQuality" => "Дальность прорисовки",
            "AntiAliasingQuality" => "Сглаживание",
            "ShadowQuality" => "Тени",
            "GlobalIlluminationQuality" => "Глобальное освещение",
            "RayTracingQuality" => "Трассировка лучей",
            "ReflectionQuality" => "Отражения",
            "PostProcessQuality" => "Постобработка",
            "TextureQuality" => "Текстуры",
            "EffectsQuality" => "Эффекты",
            "FoliageQuality" => "Растительность",
            "ShadingQuality" => "Шейдинг",
            _ => key
        };
    }
}