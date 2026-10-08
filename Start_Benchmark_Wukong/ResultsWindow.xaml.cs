using Start_Benchmark_Wukong.Models;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Start_Benchmark_Wukong
{
    /// <summary>Окно собранных результатов</summary>
    public partial class ResultsWindow : Window
    {
        /// <summary>Путь до скрина теста CPU</summary>
        private readonly string _cpuScreenshot;
        /// <summary>Путь до скрина теста GPU</summary>
        private readonly string _gpuScreenshot;

        /// <summary>Окно собранных результатов</summary>
        /// <param name="cpu">Результаты теста CPU</param>
        /// <param name="cpuShot">Путь до скрина теста CPU</param>
        /// <param name="gpu">Результаты теста GPU</param>
        /// <param name="gpuShot">Путь до скрина теста GPU</param>
        public ResultsWindow(BenchmarkResult cpu, string cpuShot,
                             BenchmarkResult gpu, string gpuShot)
        {
            InitializeComponent();

            _cpuScreenshot = cpuShot;
            _gpuScreenshot = gpuShot;

            // Характеристики ПК — просто проставляем значения в существующие поля
            FillSpecs();

            // Результаты тестов
            FillResult(CpuAvgBox, CpuMaxBox, CpuMinBox, CpuP5Box, CpuVramBox, cpu);
            FillResult(GpuAvgBox, GpuMaxBox, GpuMinBox, GpuP5Box, GpuVramBox, gpu);

            // Параметры тестов (значения по умолчанию)
            var cpuParams = BenchmarkConfigurator.GetDefaultParameters(BenchmarkConfigurator.TestMode.Cpu);
            var gpuParams = BenchmarkConfigurator.GetDefaultParameters(BenchmarkConfigurator.TestMode.Gpu);

            CpuLevelText.Text = cpuParams.Level.ToString();
            CpuQualityText.Text = cpuParams.ResolutionQuality.ToString();
            CpuWidthText.Text = cpuParams.Width.ToString();
            CpuHeightText.Text = cpuParams.Height.ToString();

            GpuLevelText.Text = gpuParams.Level.ToString();
            GpuQualityText.Text = gpuParams.ResolutionQuality.ToString();
            GpuWidthText.Text = gpuParams.Width.ToString();
            GpuHeightText.Text = gpuParams.Height.ToString();

            // sg.* — все ключи имеют один и тот же уровень
            string sgLevelName = BenchmarkConfigurator.GetQualityLevelName(cpuParams.Level);
            string sgLevelNameGpu = BenchmarkConfigurator.GetQualityLevelName(gpuParams.Level);
            string cpuResQuality = $"{cpuParams.ResolutionQuality.ToString(System.Globalization.CultureInfo.InvariantCulture)} %";
            string gpuResQuality = $"{gpuParams.ResolutionQuality.ToString(System.Globalization.CultureInfo.InvariantCulture)} %";

            CpuSgResQualityText.Text = cpuResQuality;
            CpuSgViewDistanceText.Text = sgLevelName;
            CpuSgAntiAliasingText.Text = sgLevelName;
            CpuSgShadowText.Text = sgLevelName;
            CpuSgGlobalIlluminationText.Text = sgLevelName;
            CpuSgRayTracingText.Text = sgLevelName;
            CpuSgReflectionText.Text = sgLevelName;
            CpuSgPostProcessText.Text = sgLevelName;
            CpuSgTextureText.Text = sgLevelName;
            CpuSgEffectsText.Text = sgLevelName;
            CpuSgFoliageText.Text = sgLevelName;
            CpuSgShadingText.Text = sgLevelName;

            GpuSgResQualityText.Text = gpuResQuality;
            GpuSgViewDistanceText.Text = sgLevelNameGpu;
            GpuSgAntiAliasingText.Text = sgLevelNameGpu;
            GpuSgShadowText.Text = sgLevelNameGpu;
            GpuSgGlobalIlluminationText.Text = sgLevelNameGpu;
            GpuSgRayTracingText.Text = sgLevelNameGpu;
            GpuSgReflectionText.Text = sgLevelNameGpu;
            GpuSgPostProcessText.Text = sgLevelNameGpu;
            GpuSgTextureText.Text = sgLevelNameGpu;
            GpuSgEffectsText.Text = sgLevelNameGpu;
            GpuSgFoliageText.Text = sgLevelNameGpu;
            GpuSgShadingText.Text = sgLevelNameGpu;

            // UISettingData — значения зависят от режима
            var cpuUiValues = BenchmarkConfigurator.GetUiValues(BenchmarkConfigurator.TestMode.Cpu, cpuParams);
            var gpuUiValues = BenchmarkConfigurator.GetUiValues(BenchmarkConfigurator.TestMode.Gpu, gpuParams);

            var cpuUiBoxes = new Dictionary<string, TextBlock>
            {
                ["QualityLevel"]            = CpuUiQualityLevelText,
                ["ViewDistance"]            = CpuUiViewDistanceText,
                ["AntiAliasing"]            = CpuUiAntiAliasingText,
                ["PostProcessing"]          = CpuUiPostProcessingText,
                ["ShadowQuality"]           = CpuUiShadowQualityText,
                ["TextureQuality"]          = CpuUiTextureQualityText,
                ["FxQuality"]               = CpuUiFxQualityText,
                ["MaterialQuality"]         = CpuUiMaterialQualityText,
                ["VegetationQuality"]       = CpuUiVegetationQualityText,
                ["GlobalIllumination"]      = CpuUiGlobalIlluminationText,
                ["ReflectionQuality"]       = CpuUiReflectionQualityText,
                ["Rtx"]                     = CpuUiRtxText,
                ["RtxLevel"]                = CpuUiRtxLevelText,
                ["Dlss"]                    = CpuUiDlssText,
                ["Dx12"]                    = CpuUiDx12Text,
                ["InsertFrame"]             = CpuUiInsertFrameText,
                ["SuperResolutionSampling"] = CpuUiSuperResolutionSamplingText,
                ["Vsync"]                   = CpuUiVsyncText,
                ["LockFrameRate"]           = CpuUiLockFrameRateText,
                ["MotionBlur"]              = CpuUiMotionBlurText,
            };

            var gpuUiBoxes = new Dictionary<string, TextBlock>
            {
                ["QualityLevel"]            = GpuUiQualityLevelText,
                ["ViewDistance"]            = GpuUiViewDistanceText,
                ["AntiAliasing"]            = GpuUiAntiAliasingText,
                ["PostProcessing"]          = GpuUiPostProcessingText,
                ["ShadowQuality"]           = GpuUiShadowQualityText,
                ["TextureQuality"]          = GpuUiTextureQualityText,
                ["FxQuality"]               = GpuUiFxQualityText,
                ["MaterialQuality"]         = GpuUiMaterialQualityText,
                ["VegetationQuality"]       = GpuUiVegetationQualityText,
                ["GlobalIllumination"]      = GpuUiGlobalIlluminationText,
                ["ReflectionQuality"]       = GpuUiReflectionQualityText,
                ["Rtx"]                     = GpuUiRtxText,
                ["RtxLevel"]                = GpuUiRtxLevelText,
                ["Dlss"]                    = GpuUiDlssText,
                ["Dx12"]                    = GpuUiDx12Text,
                ["InsertFrame"]             = GpuUiInsertFrameText,
                ["SuperResolutionSampling"] = GpuUiSuperResolutionSamplingText,
                ["Vsync"]                   = GpuUiVsyncText,
                ["LockFrameRate"]           = GpuUiLockFrameRateText,
                ["MotionBlur"]              = GpuUiMotionBlurText,
            };

            foreach (var kv in cpuUiBoxes)
                kv.Value.Text = BenchmarkConfigurator.GetUiValueDisplay(kv.Key, cpuUiValues[kv.Key]);

            foreach (var kv in gpuUiBoxes)
                kv.Value.Text = BenchmarkConfigurator.GetUiValueDisplay(kv.Key, gpuUiValues[kv.Key]);
        }

        /// <summary>Заполняет блок характеристик ПК.</summary>
        private void FillSpecs()
        {

            // CPU
            CpuNameBox.Text = ComputerSpecifications.SystemInfo.Cpu.Name;
            CpuCoresBox.Text = ComputerSpecifications.SystemInfo.Cpu.Cores.ToString();
            CpuThreadsBox.Text = ComputerSpecifications.SystemInfo.Cpu.LogicalProcessors.ToString();
            CpuFreqBox.Text = $"{ComputerSpecifications.SystemInfo.Cpu.MaxClockMhz} МГц";

            // GPU
            if (ComputerSpecifications.SystemInfo.Gpus.Count == 0)
            {
                GpuBlock1.Visibility = Visibility.Collapsed;
                GpuBlock2.Visibility = Visibility.Collapsed;
                GpuNotFoundText.Visibility = Visibility.Visible;
            }
            else
            {
                var g1 = ComputerSpecifications.SystemInfo.Gpus[0];
                Gpu1NameBox.Text = g1.Name;
                Gpu1DriverBox.Text = g1.DriverVersion;
                Gpu1VramBox.Text = $"{g1.VramGb:F1} GB";

                if (ComputerSpecifications.SystemInfo.Gpus.Count >= 2)
                {
                    var g2 = ComputerSpecifications.SystemInfo.Gpus[1];
                    Gpu2NameBox.Text = g2.Name;
                    Gpu2DriverBox.Text = g2.DriverVersion;
                    Gpu2VramBox.Text = $"{g2.VramGb:F1} GB";
                }
                else
                {
                    GpuBlock2.Visibility = Visibility.Collapsed;
                }
            }

            // RAM
            RamTotalBox.Text = $"{ComputerSpecifications.SystemInfo.Ram.TotalGb:F2} GB";

            // Экран
            DispCurrentBox.Text = ComputerSpecifications.SystemInfo.Display.Current.ToString();
            DispMaxBox.Text = ComputerSpecifications.SystemInfo.Display.MaxResolutionMode.ToString();
            DispMinBox.Text = ComputerSpecifications.SystemInfo.Display.MinResolutionMode.ToString();

            // ОС
            OsCaptionBox.Text = ComputerSpecifications.SystemInfo.Os.Caption;
            OsVersionBox.Text = ComputerSpecifications.SystemInfo.Os.Version;
            OsArchBox.Text = ComputerSpecifications.SystemInfo.Os.Architecture;
            Os64BitBox.Text = ComputerSpecifications.SystemInfo.Os.Is64BitProcess ? "Да" : "Нет";

            // BIOS
            BiosVendorBox.Text = ComputerSpecifications.SystemInfo.Bios.Vendor;
            BiosVersionBox.Text = ComputerSpecifications.SystemInfo.Bios.Version;
            BiosDateBox.Text = ComputerSpecifications.SystemInfo.Bios.ReleaseDate;
        }

        /// <summary>Заполняет блок результатов одного теста.</summary>
        private static void FillResult(TextBox avg, TextBox max, TextBox min,
                                       TextBox p5, TextBox vram, BenchmarkResult r)
        {
            avg.Text = r.AverageFps.ToString();
            max.Text = r.MaxFps.ToString();
            min.Text = r.MinFps.ToString();
            p5.Text = r.Percentile5Fps.ToString();
            vram.Text = $"{r.VideoMemoryGb} ГБ";
        }

        /// <summary>Открытия скринов с результатами CPU</summary>
        private void OpenCpuScreenshot_Click(object sender, RoutedEventArgs e) => OpenFile(_cpuScreenshot);
        /// <summary>Открытия скринов с результатами GPU</summary>
        private void OpenGpuScreenshot_Click(object sender, RoutedEventArgs e) => OpenFile(_gpuScreenshot);

        /// <summary>Открытия скринов с результатами</summary>
        /// <param name="path">Путь до скрина</param>
        private static void OpenFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("Файл не найден.");
                return;
            }
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { }
        }
    }
}