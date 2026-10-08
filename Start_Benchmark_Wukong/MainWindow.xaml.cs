using Start_Benchmark_Wukong.Models;
using System.Windows;

namespace Start_Benchmark_Wukong
{
    /// <summary>
    /// Главное окно приложения: управляет сценарием автоматического запуска
    /// бенчмарка (проверка компонентов, сбор характеристик ПК, CPU-тест,
    /// GPU-тест, формирование отчёта).
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Источник отмены текущего сценария (используется кнопкой «Отмена»).</summary>
        private CancellationTokenSource? _cts;

        /// <summary>Признак того, что сценарий сейчас выполняется.</summary>
        private bool _running;

        /// <summary>
        /// Конструктор окна. Инициализирует XAML-разметку и подписывается
        /// на событие загрузки окна, чтобы запустить сценарий.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Обработчик события загрузки окна. Запускает основной сценарий.
        /// </summary>
        /// <param name="sender">Источник события.</param>
        /// <param name="e">Аргументы события.</param>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RunSequenceAsync();
        }

        /// <summary>
        /// Добавляет строку в лог с меткой времени и прокручивает его вниз.
        /// </summary>
        /// <param name="msg">Сообщение для добавления в лог.</param>
        private void Log(string msg)
        {
            LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            // Автопрокрутка вниз
            if (LogText.Parent is System.Windows.Controls.ScrollViewer sv)
                sv.ScrollToEnd();
        }

        /// <summary>
        /// Обновляет текущий этап, прогресс-бар и пишет сообщение в лог.
        /// </summary>
        /// <param name="text">Текст текущего этапа.</param>
        /// <param name="progress">Значение прогресса (0..100).</param>
        private void SetStage(string text, double progress)
        {
            StageText.Text = text;
            Progress.Value = progress;
            Log(text);
        }

        /// <summary>
        /// Основной сценарий: проверка компонентов, сбор характеристик,
        /// CPU-тест, GPU-тест и открытие окна результатов.
        /// </summary>
        private async Task RunSequenceAsync()
        {
            _running = true;
            _cts = new CancellationTokenSource();

            try
            {
                // ─── ШАГ 1: проверка компонентов ───────────────────────────
                SetStage("Шаг 1/5: Проверка компонентов Benchmark...", 5);
                var status = ComponentsBenchmark.GetBenchmarkStatus();
                switch (status)
                {
                    case ComponentsBenchmark.BenchmarkCheckResult.SteamNotFound:
                    {
                        ShowError("Steam не установлен.\nУстановите Steam и повторите запуск.");
                        return;
                    }
                    case ComponentsBenchmark.BenchmarkCheckResult.BenchmarkNotFound:
                    {
                        ShowError("Бенчмарк Black Myth: Wukong не найден ни в одной библиотеке Steam.\n" +
                                  "Установите бенчмарк (AppID 3132990) через Steam.");
                        return;
                    }
                    case ComponentsBenchmark.BenchmarkCheckResult.BenchmarkNotConfigFile:
                    {
                        Log("Файл GameUserSettings.ini не найден. Запускаю бенчмарк для его генерации...");
                        SetStage("Шаг 1/5: Первый запуск бенчмарка (генерация конфига)...", 5);

                        bool created = await BenchmarkRunner.GenerateConfigAsync(
                            shaderCompileTimeoutSec: 600, // 10 минут с запасом на компиляцию шейдеров
                            progress: (msg, elapsed) =>
                            {
                                // Вызывается из фонового потока — маршалим в UI
                                Dispatcher.Invoke(() =>
                                {
                                    // Обновляем текст этапа, но прогресс-бар не двигаем слишком агрессивно:
                                    // компиляция может занять разное время, и «угадать» процент нельзя.
                                    StageText.Text = msg;
                                    Log(msg);
                                });
                            });

                        if (!created)
                        {
                            ShowError("Не удалось сгенерировать GameUserSettings.ini за отведённое время.\n" +
                                      "Запустите бенчмарк вручную, дождитесь завершения компиляции шейдеров, " +
                                      "закройте его и запустите это приложение снова.");
                            return;
                        }

                        Log("✓ GameUserSettings.ini создан.");
                        Progress.Value = 15;
                        break;
                    }
                }
                Log($"✓ Бенчмарк найден: {ComponentsBenchmark.ExeFilePath}");
                Progress.Value = 15;

                // ─── ШАГ 2: характеристики ПК ──────────────────────────────
                SetStage("Шаг 2/5: Сбор характеристик ПК...", 20);
                await Task.Run(() => ComputerSpecifications.Collect());
                Log($"CPU : {ComputerSpecifications.SystemInfo.Cpu.Name}");
                Log($"GPU : {string.Join("; ", ComputerSpecifications.SystemInfo.Gpus.Select(g => g.Name))}");
                Log($"RAM : {ComputerSpecifications.SystemInfo.Ram.TotalGb:F1} GB");
                Log($"Экран: {ComputerSpecifications.SystemInfo.Display.Current}");
                Progress.Value = 30;

                // ─── ШАГ 3: CPU-тест ───────────────────────────────────────
                SetStage("Шаг 3/5: Настройка и запуск CPU-теста...", 35);
                if (!BenchmarkConfigurator.ApplySettings(BenchmarkConfigurator.TestMode.Cpu))
                {
                    ShowError("Не удалось применить настройки для CPU-теста (GameUserSettings.ini).");
                    return;
                }
                Log("✓ Параметры CPU-теста применены.");
                Progress.Value = 40;

                Log("▶ Запуск бенчмарка (CPU). Это занимает ~4–5 минут...");
                string? cpuShot = await BenchmarkRunner.RunAsync();
                if (string.IsNullOrEmpty(cpuShot))
                {
                    ShowError("Не удалось запустить или дождаться завершения CPU-теста.");
                    return;
                }
                Log($"✓ Скриншот CPU: {cpuShot}");
                Progress.Value = 55;

                Log("Распознавание результатов CPU (OCR)...");
                var cpuResult = await BenchmarkResultReader.ReadAsync(cpuShot);
                if (cpuResult == null)
                {
                    ShowError("Не удалось распознать результаты CPU-теста.\n" +
                              "Проверьте, установлен ли русский языковой пакет для OCR.");
                    return;
                }
                Log($"✓ CPU: {cpuResult}");
                Progress.Value = 60;

                await Task.Delay(5000); //Один раз поймал ошибку с синхронизацией steam, поэтому добавил ожидание

                // ─── ШАГ 4: GPU-тест ───────────────────────────────────────
                SetStage("Шаг 4/5: Настройка и запуск GPU-теста...", 65);
                if (!BenchmarkConfigurator.ApplySettings(BenchmarkConfigurator.TestMode.Gpu))
                {
                    ShowError("Не удалось применить настройки для GPU-теста.");
                    return;
                }
                Log("✓ Параметры GPU-теста применены.");
                Progress.Value = 70;

                Log("▶ Запуск бенчмарка (GPU). Это занимает ~4–5 минут...");
                string? gpuShot = await BenchmarkRunner.RunAsync();
                if (string.IsNullOrEmpty(gpuShot))
                {
                    ShowError("Не удалось запустить или дождаться завершения GPU-теста.");
                    return;
                }
                Log($"✓ Скриншот GPU: {gpuShot}");
                Progress.Value = 85;

                Log("Распознавание результатов GPU (OCR)...");
                var gpuResult = await BenchmarkResultReader.ReadAsync(gpuShot);
                if (gpuResult == null)
                {
                    ShowError("Не удалось распознать результаты GPU-теста.");
                    return;
                }
                Log($"✓ GPU: {gpuResult}");
                Progress.Value = 95;

                // ─── ШАГ 5: окно результатов ───────────────────────────────
                SetStage("Шаг 5/5: Формирование отчёта...", 100);
                await Task.Delay(300);

                var win = new ResultsWindow(cpuResult, cpuShot, gpuResult, gpuShot);
                win.Show();
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Непредвиденная ошибка:\n{ex.Message}");
                Log(ex.ToString());
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>
        /// Показывает сообщение об ошибке в логе и в диалоговом окне.
        /// </summary>
        /// <param name="message">Текст ошибки.</param>
        private void ShowError(string message)
        {
            Log("✗ ОШИБКА: " + message);
            MessageBox.Show(this, message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Обработчик кнопки «Отмена». Спрашивает подтверждение и,
        /// если пользователь согласен, отменяет сценарий и закрывает окно.
        /// </summary>
        /// <param name="sender">Источник события.</param>
        /// <param name="e">Аргументы события.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                var res = MessageBox.Show(this,
                    "Прервать выполнение бенчмарка?", "Отмена",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
                _cts?.Cancel();
            }
            Close();
        }
    }
}