using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Start_Benchmark_Wukong.Models
{
    internal static class BenchmarkRunner
    {
        /// <summary>Код нажатия ЛКМ (используется в mouse_event).</summary>
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        /// <summary>Код отпускания ЛКМ (используется в mouse_event).</summary>
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        /// <summary>Код растровой операции BitBlt: прямая побитовая копия пикселей источника.</summary>
        private const uint SRCCOPY = 0x00CC0020;
        /// <summary>Виртуальный код клавиши Enter.</summary>
        private const byte VK_RETURN = 0x0D;
        /// <summary>Флаг keybd_event: событие «клавиша отпущена» (без него — нажатие).</summary>
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>до экрана "Press any key"</summary>
        private const int StartupWaitSec = 55;
        /// <summary>от клика до меню</summary>
        private const int AfterPressWaitSec = 20;
        /// <summary>длительность теста + запас</summary>
        private const int TestDurationSec = 220;

        /// <summary>
        /// Запускает бенчмарк, проходит сценарий, возвращает путь к PNG с результатами (или null).
        /// </summary>
        public static async Task<string?> RunAsync()
        {
            string exePath = ComponentsBenchmark.ExeFilePath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

            try
            {
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false
                });
            }
            catch { return null; }

            // 1. Ждём загрузку до "Press any key"
            await Task.Delay(StartupWaitSec * 1000);

            // Ждём процесс и его видимое окно
            Process? process = null;
            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 60; i++)
            {
                var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ComponentsBenchmark.ExeFilePath));
                if (procs.Length > 0)
                {
                    process = procs[0];
                    hwnd = FindWindowByProcess(process);
                    if (hwnd != IntPtr.Zero) break;
                }
                Thread.Sleep(500);
            }

            if (process == null || hwnd == IntPtr.Zero)
                return null;

            try
            {

                // 2. Клик "любая кнопка"
                ClickAt(50, 50);

                // 3. Ждём меню
                await Task.Delay(AfterPressWaitSec * 1000);


                const double StartButtonNx = 0.1354;
                const double StartButtonNy = 0.4509;
                // 4. Клик по кнопке Start
                ClickAt((int)(ComputerSpecifications.SystemInfo.Display.Current.Width * StartButtonNx), (int)(ComputerSpecifications.SystemInfo.Display.Current.Height * StartButtonNy));
                // 4.1. Прячу мышку в самый низ справа
                SetCursorPos(ComputerSpecifications.SystemInfo.Display.Current.Width, 0);
                // 4.2. Ждём появления плашки подтверждения и жмём Enter
                await Task.Delay(1500);
                keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                // 6. Ждём прохождения теста
                await Task.Delay(TestDurationSec * 1000);

                // 7. Скриншот результатов
                string resultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenBenchmark", $"benchmark_result_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                var bmp = CaptureWindow(hwnd);
                if
                    (bmp != null) SavePng(bmp, resultPath);
                else
                    return null;

                return resultPath;
            }
            finally
            {
                // 8. Убиваем процесс
                try {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch { }
            }
        }/// <summary>
         /// Запускает бенчмарк в первый раз, чтобы он создал GameUserSettings.ini,
         /// ждёт появления файла (учитывая время компиляции шейдеров) и закрывает процесс.
         /// Используется, когда <see cref="ComponentsBenchmark.GetBenchmarkStatus"/> вернул
         /// <see cref="ComponentsBenchmark.BenchmarkCheckResult.BenchmarkNotConfigFile"/>.
         /// </summary>
         /// <param name="shaderCompileTimeoutSec">
         /// Сколько секунд максимум ждать появления конфига.
         /// По умолчанию 600 (10 минут) — с запасом на компиляцию шейдеров,
         /// которая на слабых системах может занимать до 10–15 минут.
         /// </param>
         /// <param name="progress">
         /// Необязательный callback для логирования прогресса.
         /// Принимает строку сообщения и число прошедших секунд.
         /// </param>
         /// <returns>true, если конфиг появился до истечения таймаута.</returns>
        public static async Task<bool> GenerateConfigAsync(
            int shaderCompileTimeoutSec = 600,
            Action<string, int>? progress = null)
        {
            string exePath = ComponentsBenchmark.ExeFilePath;
            string configPath = ComponentsBenchmark.ConfigFilePath;

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return false;
            if (string.IsNullOrEmpty(configPath))
                return false;

            // Если файл вдруг уже есть (гонка/повторный вызов) — сразу выходим
            if (File.Exists(configPath))
                return true;

            Process? process;
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false
                });
            }
            catch
            {
                return false;
            }

            if (process == null)
                return false;

            try
            {
                var startedAt = DateTime.UtcNow;
                int lastReportedSec = -10; // чтобы первый отчёт был сразу

                while ((DateTime.UtcNow - startedAt).TotalSeconds < shaderCompileTimeoutSec)
                {
                    if (process.HasExited)
                    {
                        // Бенчмарк упал до создания конфига. Проверим ещё раз на всякий случай:
                        // иногда процесс завершается штатно сразу после создания файла.
                        if (File.Exists(configPath))
                            return true;
                        return false;
                    }

                    if (File.Exists(configPath))
                    {
                        // Конфиг появился. Даём бенчмарку мгновение, чтобы дописать его
                        // (файл может появиться пустым и наполняться ещё доли секунды),
                        // но не ждём завершения компиляции шейдеров — она идёт параллельно
                        // и на самом деле не нужна для генерации конфига.
                        await Task.Delay(1500);
                        return File.Exists(configPath);
                    }

                    // Периодически сообщаем о прогрессе (раз в 10 секунд)
                    int elapsedSec = (int)(DateTime.UtcNow - startedAt).TotalSeconds;
                    if (elapsedSec - lastReportedSec >= 10)
                    {
                        lastReportedSec = elapsedSec;
                        progress?.Invoke(
                            $"Ожидание генерации конфига... прошло {elapsedSec} сек. " +
                            $"(компиляция шейдеров может занять несколько минут)",
                            elapsedSec);
                    }

                    await Task.Delay(1000);
                }

                // Таймаут. Последняя проверка — вдруг файл всё-таки появился.
                return File.Exists(configPath);
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        // Даём ОС время прибить процесс, чтобы не оставить «висяк»
                        process.WaitForExit(5000);
                    }
                }
                catch { /* ignore */ }
            }
        }

        // === Вспомогательные ===
        /// <summary>Нажатие мышки в определённом месте</summary>
        /// <param name="screenX">Координаты на экране по Х</param>
        /// <param name="screenY">Координаты на экране по Y</param>
        private static void ClickAt(int screenX, int screenY)
        {
            SetCursorPos(screenX, screenY);
            Thread.Sleep(200);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(140);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(200);
        }

        /// <summary>Поиск главного окна по процессу</summary>
        /// <param name="process">Процесс программы</param>
        /// <returns>Указатель на окно</returns>
        private static IntPtr FindWindowByProcess(Process process)
        {
            IntPtr found = IntPtr.Zero;
            uint targetPid = (uint)process.Id;

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != targetPid || !IsWindowVisible(hWnd))
                    return true;

                GetWindowRect(hWnd, out RECT r);
                int w = r.Right - r.Left;
                int h = r.Bottom - r.Top;
                // Игнорируем мелкие служебные окна
                if (w < 400 || h < 300)
                    return true;

                found = hWnd;
                return false;
            }, IntPtr.Zero);

            return found;
        }
        /// <summary>Создание изображения в памяти</summary>
        /// <remarks>Для сохранения результатов теста</remarks>
        /// <param name="hwnd">Указатель на главное окно</param>
        /// <returns>Изображение в памяти</returns>
        private static BitmapSource? CaptureWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            GetWindowRect(hwnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return null;

            IntPtr hdcSrc = IntPtr.Zero, hdcDest = IntPtr.Zero, hBitmap = IntPtr.Zero, hOld = IntPtr.Zero;
            try
            {
                hdcSrc = GetWindowDC(hwnd);
                hdcDest = CreateCompatibleDC(hdcSrc);
                hBitmap = CreateCompatibleBitmap(hdcSrc, width, height);
                hOld = SelectObject(hdcDest, hBitmap);
                BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, SRCCOPY);

                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                if (hOld != IntPtr.Zero) SelectObject(hdcDest, hOld);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (hdcDest != IntPtr.Zero) DeleteDC(hdcDest);
                if (hdcSrc != IntPtr.Zero) ReleaseDC(hwnd, hdcSrc);
            }
        }
        /// <summary>Сохранения изображения</summary>
        /// <param name="bitmap">Изображения для сохранения</param>
        /// <param name="path">Путь до файла сохранения</param>
        private static void SavePng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        #region Импортируемы функции
        /// <summary>Получает координаты и размеры окна по его дескриптору.</summary>
        /// <param name="hWnd">Дескриптор окна, у которого запрашиваются размеры.</param>
        /// <param name="lpRect">Выходной параметр: структура RECT с координатами (Left, Top, Right, Bottom) в экранных пикселях.</param>
        /// <returns>true, если операция успешна; иначе false.</returns>
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>Выводит указанное окно на передний план и активирует его (передаёт фокус ввода).</summary>
        /// <param name="hWnd">Дескриптор окна, которое нужно активировать.</param>
        /// <returns>true, если окно удалось вывести на передний план; иначе false.</returns>
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>Перемещает курсор мыши в указанную точку экрана.</summary>
        /// <param name="X">Координата X в экранных пикселях (0 — левый край экрана).</param>
        /// <param name="Y">Координата Y в экранных пикселях (0 — верхний край экрана).</param>
        /// <returns>true, если курсор удалось переместить; иначе false.</returns>
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);

        /// <summary>Синтезирует событие мыши (движение, нажатие, отпускание).</summary>
        /// <param name="dwFlags">Флаги, определяющие тип события (например, MOUSEEVENTF_LEFTDOWN).</param>
        /// <param name="dx">Смещение по X. Для абсолютных координат игнорируется (мы используем SetCursorPos отдельно).</param>
        /// <param name="dy">Смещение по Y. Аналогично dx.</param>
        /// <param name="dwData">Дополнительные данные (для колёсика — величина прокрутки, для остальных — 0).</param>
        /// <param name="dwExtraInfo">Указатель на дополнительные данные приложения (обычно IntPtr.Zero).</param>
        [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

        /// <summary>Возвращает контекст устройства (DC) для указанного окна — используется для захвата пикселей через BitBlt.</summary>
        /// <param name="hWnd">Дескриптор окна, чей DC нужно получить.</param>
        /// <returns>Дескриптор DC или IntPtr.Zero при ошибке.</returns>
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);

        /// <summary>Освобождает контекст устройства, полученный через GetWindowDC.</summary>
        /// <param name="hWnd">Дескриптор окна, которому принадлежал DC.</param>
        /// <param name="hDC">Дескриптор DC, который нужно освободить.</param>
        /// <returns>Результат освобождения (зависит от типа DC).</returns>
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        /// <summary>Создаёт контекст устройства в памяти, совместимый с указанным DC.</summary>
        /// <param name="hdc">Исходный DC, с которым должен быть совместим новый.</param>
        /// <returns>Дескриптор нового DC в памяти или IntPtr.Zero при ошибке.</returns>
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        /// <summary>Создаёт bitmap в памяти, совместимый с указанным DC.</summary>
        /// <param name="hdc">DC, с которым должен быть совместим bitmap.</param>
        /// <param name="nWidth">Ширина bitmap в пикселях.</param>
        /// <param name="nHeight">Высота bitmap в пикселях.</param>
        /// <returns>Дескриптор bitmap или IntPtr.Zero при ошибке.</returns>
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        /// <summary>Выбирает объект (например, bitmap) в указанный контекст устройства.</summary>
        /// <param name="hdc">DC, в который выбирается объект.</param>
        /// <param name="hgdiobj">Дескриптор выбираемого объекта.</param>
        /// <returns>Дескриптор предыдущего объекта в DC (нужно вернуть перед удалением DC).</returns>
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        /// <summary>Копирует блок пикселей из одного DC в другой (используется для захвата содержимого окна).</summary>
        /// <param name="hdcDest">DC-приёмник (наш bitmap в памяти).</param>
        /// <param name="nXDest">X-координата верхнего левого угла области назначения.</param>
        /// <param name="nYDest">Y-координата верхнего левого угла области назначения.</param>
        /// <param name="nWidth">Ширина копируемой области.</param>
        /// <param name="nHeight">Высота копируемой области.</param>
        /// <param name="hdcSrc">DC-источник (контекст окна бенчмарка).</param>
        /// <param name="nXSrc">X-координата верхнего левого угла области источника.</param>
        /// <param name="nYSrc">Y-координата верхнего левого угла области источника.</param>
        /// <param name="dwRop">Код растровой операции (SRCCOPY — прямая копия пикселей).</param>
        /// <returns>true при успехе; иначе false.</returns>
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        /// <summary>Удаляет GDI-объект (bitmap, DC и т.п.) и освобождает связанные ресурсы.</summary>
        /// <param name="hObject">Дескриптор удаляемого объекта.</param>
        /// <returns>true при успехе; иначе false.</returns>
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>Удаляет контекст устройства, созданный через CreateCompatibleDC.</summary>
        /// <param name="hdc">Дескриптор DC для удаления.</param>
        /// <returns>true при успехе; иначе false.</returns>
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);

        /// <summary>Синтезирует нажатие или отпускание клавиши клавиатуры.</summary>
        /// <param name="bVk">Виртуальный код клавиши (например, VK_RETURN = 0x0D для Enter).</param>
        /// <param name="bScan">Аппаратный скан-код клавиши (обычно 0 — определяется системой).</param>
        /// <param name="dwFlags">Флаги: 0 — нажатие, KEYEVENTF_KEYUP — отпускание.</param>
        /// <param name="dwExtraInfo">Дополнительные данные (обычно UIntPtr.Zero).</param>
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        /// <summary>Перечисляет все окна верхнего уровня на экране, вызывая для каждого callback-функцию.</summary>
        /// <param name="lpEnumFunc">Делегат, вызываемый для каждого окна. Возврат false останавливает перебор.</param>
        /// <param name="lParam">Произвольный параметр, передаваемый в callback.</param>
        /// <returns>true, если перечисление прошло успешно; иначе false.</returns>
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        /// <summary>Возвращает идентификатор процесса, которому принадлежит указанное окно.</summary>
        /// <param name="hWnd">Дескриптор окна.</param>
        /// <param name="processId">Выходной параметр: PID процесса-владельца.</param>
        /// <returns>Идентификатор потока, создавшего окно.</returns>
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        /// <summary>Проверяет, видимо ли окно в данный момент (не скрыто и не свёрнуто в трей).</summary>
        /// <param name="hWnd">Дескриптор проверяемого окна.</param>
        /// <returns>true, если окно видимо; иначе false.</returns>
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>Делегат для callback-функции, вызываемой EnumWindows для каждого окна.</summary>
        /// <param name="hWnd">Дескриптор текущего окна.</param>
        /// <param name="lParam">Пользовательский параметр, переданный в EnumWindows.</param>
        /// <returns>true — продолжить перебор; false — остановить.</returns>
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>Структура для получения размеров и позиции окна приложения (Win32 RECT).</summary>
        /// <remarks>Координаты задаются в экранных пикселях от верхнего левого угла экрана.</remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        #endregion
    }
}