using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Ocr;

namespace Start_Benchmark_Wukong.Models
{
    /// <summary>Результаты одного прогона бенчмарка.</summary>
    public class BenchmarkResult
    {
        /// <summary>Средний FPS за весь тест.</summary>
        public int AverageFps { get; set; }

        /// <summary>Максимальный FPS, зафиксированный за тест.</summary>
        public int MaxFps { get; set; }

        /// <summary>Минимальный FPS, зафиксированный за тест.</summary>
        public int MinFps { get; set; }

        /// <summary>5-й перцентиль FPS — значение, ниже которого оказалось 5% кадров.</summary>
        /// <remarks>Один из ключевых показателей «плавности»: чем выше, тем меньше просадок.</remarks>
        public int Percentile5Fps { get; set; }

        /// <summary>Объём использованной видеопамяти в гигабайтах.</summary>
        public double VideoMemoryGb { get; set; }

        /// <summary>Строковое представление результата для логов и отладки.</summary>
        public override string ToString() =>
            $"Avg: {AverageFps} | Max: {MaxFps} | Min: {MinFps} | 5%: {Percentile5Fps} | VRAM: {VideoMemoryGb} ГБ";
    }

    /// <summary>Извлекает результаты теста со скриншота, полученного BenchmarkRunner.</summary>
    internal static class BenchmarkResultReader
    {
        /// <summary>Читает результаты со скриншота бенчмарка.</summary>
        /// <param name="imagePath">Путь к PNG-файлу со скриншотом окна результатов.</param>
        /// <returns>Объект с распознанными метриками или null при ошибке.</returns>
        public static async Task<BenchmarkResult?> ReadAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return null;

            // 1. Загружаем PNG в WPF BitmapImage
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;   // загружаем сразу, не держим файл открытым
            source.UriSource = new Uri(imagePath);
            source.EndInit();

            // 2. OCR-движок (русский)
            var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("ru-RU"));
            if (engine == null) return null;   // языковой пакет не установлен

            // 3. OCR каждой полосы.
            //    Для средней цифры — отдельный метод с перебором ширины.
            string avgText  = await OcrBigNumberAsync(source, engine, 0.31, 0.38);
            string maxText  = await OcrRegionAsync(source, engine, 0.40, 0.46);
            string minText  = await OcrRegionAsync(source, engine, 0.40, 0.46);
            string p5Text   = await OcrRegionAsync(source, engine, 0.47, 0.53);
            string vramText = await OcrRegionAsync(source, engine, 0.55, 0.61);

            // 4. Парсинг
            var result = new BenchmarkResult
            {
                AverageFps     = ExtractInt(avgText,   @"(\d+)"),
                MaxFps         = ExtractInt(maxText,   @"Максимум[^\d]*(\d+)"),
                MinFps         = ExtractInt(minText,   @"Минимум[^\d]*(\d+)"),
                Percentile5Fps = ExtractInt(p5Text,    @"перцентиль[^\d]*(\d+)"),
                VideoMemoryGb  = ExtractDouble(vramText, @"([\d,\.]+)\s*[Гг]б")
            };

            return result;
        }

        /// <summary>OCR отдельной горизонтальной полосы левой колонки скриншота.</summary>
        /// <param name="source">Исходный скриншот.</param>
        /// <param name="engine">OCR-движок.</param>
        /// <param name="y1">Начало полосы (нормализованное, 0..1).</param>
        /// <param name="y2">Конец полосы (нормализованное, 0..1).</param>
        /// <param name="scale">Множитель увеличения перед OCR. По умолчанию 3.0.</param>
        /// <param name="x1">Начало полосы по X (нормализованное, 0..1). По умолчанию 0.00.</param>
        /// <param name="x2">Конец полосы по X (нормализованное, 0..1). По умолчанию 0.29.</param>
        /// <returns>Распознанный текст полосы.</returns>
        private static async Task<string> OcrRegionAsync(
            BitmapSource source, OcrEngine engine,
            double y1, double y2,
            double scale = 3.0,
            double x1 = 0.00, double x2 = 0.29)
        {
            var rect = new Int32Rect(
                (int)(source.PixelWidth * x1),
                (int)(source.PixelHeight * y1),
                (int)(source.PixelWidth * (x2 - x1)),
                (int)(source.PixelHeight * (y2 - y1)));

            var cropped = new CroppedBitmap(source, rect);
            var scaled = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));

            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(scaled));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;

            // BitmapDecoder — WinRT-версия
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            var ocrResult = await engine.RecognizeAsync(softwareBitmap);
            return ocrResult.Text;
        }

        /// <summary>OCR большой цифры среднего FPS. Перебирает ширину региона, пока не найдёт цифры.</summary>
        /// <remarks>Регион начинается с X = 0.025, ширина плавно растёт от 0.080 до 0.120. Для «22» хватает узкого, для «100» — широкого.</remarks>
        /// <param name="source">Исходный скриншот.</param>
        /// <param name="engine">OCR-движок.</param>
        /// <param name="y1">Начало полосы (нормализованное, 0..1).</param>
        /// <param name="y2">Конец полосы (нормализованное, 0..1).</param>
        /// <returns>Распознанный текст или пустая строка, если ни одна ширина не дала результата.</returns>
        private static async Task<string> OcrBigNumberAsync(
            BitmapSource source, OcrEngine engine, double y1, double y2)
        {
            // Перебираем ширину: 0.080, 0.085, 0.090, ..., 0.115
            for (double x2 = 0.080; x2 < 0.120; x2 += 0.005)
            {
                string text = await OcrRegionAsync(source, engine, y1, y2,
                    scale: 1.5, x1: 0.025, x2: x2);

                // Если в тексте есть хотя бы одна цифра — этого достаточно
                if (Regex.IsMatch(text, @"\d"))
                    return text;
            }

            return string.Empty;
        }

        /// <summary>Извлекает первое целое число по регулярке.</summary>
        /// <param name="text">Текст, в котором ищем число.</param>
        /// <param name="pattern">Регулярное выражение с одной группой захвата — числом.</param>
        /// <returns>Найденное число или 0, если совпадений нет.</returns>
        private static int ExtractInt(string text, string pattern)
        {
            var m = Regex.Match(text, pattern);
            return m.Success && int.TryParse(m.Groups[1].Value, out int v) ? v : 0;
        }

        /// <summary>Извлекает первое вещественное число по регулярке (запятая → точка).</summary>
        /// <param name="text">Текст, в котором ищем число.</param>
        /// <param name="pattern">Регулярное выражение с одной группой захвата — числом.</param>
        /// <returns>Найденное число или 0, если совпадений нет.</returns>
        private static double ExtractDouble(string text, string pattern)
        {
            var m = Regex.Match(text, pattern);
            if (!m.Success) return 0;
            string num = m.Groups[1].Value.Replace(',', '.');
            return double.TryParse(num, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;
        }
    }
}