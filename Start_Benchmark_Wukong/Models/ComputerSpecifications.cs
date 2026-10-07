using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Start_Benchmark_Wukong.Models
{
    /// <summary>Класс сбора информации о системе</summary>
    internal class ComputerSpecifications
    {
        /// <summary>Информация о ЦП</summary>
        public class CpuInfo
        {
            /// <summary>Наименование</summary>
            public string Name { get; set; } = "Unknown";
            /// <summary>Количество физических ядер</summary>
            public uint Cores { get; set; }
            /// <summary>Количество потоков</summary>
            public uint LogicalProcessors { get; set; }
            /// <summary>Номинальная частота в МГц (не Boost)</summary>
            public uint MaxClockMhz { get; set; }
        }

        /// <summary>Информация о видеокарте</summary>
        public class GpuInfo
        {
            /// <summary>Наименование</summary>
            public string Name { get; set; } = "Unknown";
            /// <summary>Версия драйвера</summary>
            public string DriverVersion { get; set; } = "н/д";
            /// <summary>Видео память в байтах</summary>
            public ulong VramBytes { get; set; }
            /// <summary>Видео память в гигабайтах</summary>
            public double VramGb => Math.Round(VramBytes / (1024.0 * 1024 * 1024), 2);
        }

        /// <summary>Информация о оперативной памяти</summary>
        public class RamInfo
        {
            /// <summary>Память в байтах</summary>
            public ulong TotalBytes { get; set; }
            /// <summary>Память в гигабайтах</summary>
            public double TotalGb => Math.Round(TotalBytes / (1024.0 * 1024 * 1024), 2);
        }

        /// <summary>Информация о дисплеи</summary>
        public class DisplayInfo
        {
            /// <summary>Текущий видеорежим (то, что сейчас выставлено в системе)</summary>
            public DisplayMode Current { get; set; } = new();

            /// <summary>Режим с максимальным разрешением (по площади кадра).</summary>
            /// <remarks>Если таких несколько — берётся с наибольшей частотой обновления.</remarks>
            public DisplayMode MaxResolutionMode { get; set; } = new();
            /// <summary>Режим с минимальным разрешением (по площади кадра).</summary>
            /// <remarks>Если таких несколько — берётся с наибольшей частотой обновления.</remarks>
            public DisplayMode MinResolutionMode { get; set; } = new();
        }
        /// <summary>Один видеорежим: разрешение + частота обновления</summary>
        public class DisplayMode
        {
            /// <summary>Ширина в пикселях</summary>
            public int Width { get; set; } = 0;

            /// <summary>Высота в пикселях</summary>
            public int Height { get; set; } = 0;

            /// <summary>Частота обновления в Гц</summary>
            public int RefreshRate { get; set; } = 0;

            /// <summary>Удобное представление, например "1920x1080 @ 60 Гц"</summary>
            public override string ToString() => $"{Width}x{Height} @ {RefreshRate} Гц";
        }

        /// <summary>Информация об ОС</summary>
        public class OsInfo
        {
            /// <summary>Наименование версии</summary>
            public string Caption { get; set; } = "Unknown";
            /// <summary>Версия</summary>
            public string Version { get; set; } = "н/д";
            /// <summary>Архитиктура</summary>
            public string Architecture { get; set; } = "н/д";
            /// <summary>64 битная версия или нет</summary>
            public bool Is64BitProcess { get; set; }
        }
        /// <summary>Информация о БИОСе</summary>
        public class BiosInfo
        {
            /// <summary>Производитель</summary>
            public string Vendor { get; set; } = "Unknown";
            /// <summary>Версия</summary>
            public string Version { get; set; } = "н/д";
            /// <summary>Дата версии</summary>
            public string ReleaseDate { get; set; } = "н/д";
        }
        /// <summary></summary>
        public class SystemInfo
        {
            /// <summary>ЦП</summary>
            public CpuInfo Cpu { get; set; } = new();
            /// <summary>Список видеокарт</summary>
            public List<GpuInfo> Gpus { get; set; } = new();
            /// <summary>Оперативная память</summary>
            public RamInfo Ram { get; set; } = new();
            /// <summary>Монитор</summary>
            public DisplayInfo Display { get; set; } = new();
            /// <summary>ОС</summary>
            public OsInfo Os { get; set; } = new();
            /// <summary>БОИС</summary>
            public BiosInfo Bios { get; set; } = new();
            /// <summary>Вывод информации</summary>
            public string ToDisplayString()
            {
                var sb = new StringBuilder();

                sb.AppendLine("═══ ХАРАКТЕРИСТИКИ СИСТЕМЫ ═══");
                sb.AppendLine();

                sb.AppendLine("▸ CPU");
                sb.AppendLine($"    Модель            : {Cpu.Name}");
                sb.AppendLine($"    Ядра (физические) : {Cpu.Cores}");
                sb.AppendLine($"    Потоки (лог.)     : {Cpu.LogicalProcessors}");
                sb.AppendLine($"    Макс. частота     : {Cpu.MaxClockMhz} МГц");
                sb.AppendLine();

                sb.AppendLine("▸ GPU");
                if (Gpus.Count == 0)
                {
                    sb.AppendLine("    Не обнаружено");
                }
                else
                {
                    for (int i = 0; i < Gpus.Count; i++)
                    {
                        var g = Gpus[i];
                        sb.AppendLine($"    [{i + 1}] {g.Name}");
                        sb.AppendLine($"         Драйвер       : {g.DriverVersion}");
                        sb.AppendLine($"         VRAM          : {g.VramGb:F1} GB");
                    }
                }
                sb.AppendLine();

                sb.AppendLine("▸ RAM");
                sb.AppendLine($"    Общий объём       : {Ram.TotalGb:F2} GB");
                sb.AppendLine();

                sb.AppendLine("▸ Экран");
                sb.AppendLine($"    Текущий режим     : {Display.Current.Width} × {Display.Current.Height} @ {Display.Current.RefreshRate} Гц");
                sb.AppendLine($"    Максимум          : {Display.MaxResolutionMode}");
                sb.AppendLine($"    Минимум           : {Display.MinResolutionMode}");
                sb.AppendLine();

                sb.AppendLine("▸ Операционная система");
                sb.AppendLine($"    {Os.Caption}");
                sb.AppendLine($"    Версия            : {Os.Version}");
                sb.AppendLine($"    Разрядность ОС    : {Os.Architecture}");
                sb.AppendLine($"    Процесс 64-бит    : {(Os.Is64BitProcess ? "Да" : "Нет")}");
                sb.AppendLine();

                sb.AppendLine("▸ BIOS");
                sb.AppendLine($"    Производитель     : {Bios.Vendor}");
                sb.AppendLine($"    Версия            : {Bios.Version}");
                sb.AppendLine($"    Дата              : {Bios.ReleaseDate}");

                return sb.ToString();
            }
        }




        // ═════════════════════════════════════════════
        //  P/Invoke — определение физических ядер CPU
        //  через GetLogicalProcessorInformation
        // ═════════════════════════════════════════════

        /// <summary>
        /// Структура, которую возвращает GetLogicalProcessorInformation.
        /// Одна запись = одна единица информации (ядро, кэш, NUMA-узел).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
        {
            /// <summary>маска логических процессоров</summary>
            public UIntPtr ProcessorMask;
            /// <summary>тип связи (ядро / кэш / NUMA)</summary>
            public int Relationship;
            // не используем, но нужны для корректного размера структуры
            public long Reserved1;
            public long Reserved2;
        }

        /// <summary>Функция для взятия информации о ЦП</summary>
        /// <param name="Buffer">Указатель на обьект в памяти</param>
        /// <param name="ReturnLength">Возвращаемая длинна</param>
        /// <returns>Удалось взять или нет</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformation(IntPtr Buffer, ref uint ReturnLength);

        /// <summary>
        /// Значение поля Relationship = 0, обозначающее «это физическое ядро».
        /// Другие значения (1 = NUMA, 2 = кэш) нам не нужны.
        /// </summary>
        private const int RelationProcessorCore = 0;

        // ═════════════════════════════════════════════
        //  P/Invoke — объём оперативной памяти
        // ═════════════════════════════════════════════

        /// <summary>
        /// Структура для GlobalMemoryStatusEx. Возвращает общий объём
        /// физической памяти в ullTotalPhys (в байтах, ulong).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            /// <summary>обязательное поле — размер структуры</summary>
            public uint dwLength;
            /// <summary>% использования памяти</summary>
            public uint dwMemoryLoad;
            /// <summary>Общий объём RAM в байтах</summary>
            public ulong ullTotalPhys; // 
            // не используем, но нужны для корректного размера структуры
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
        /// <summary>Функция для взятия информация об RAM</summary>
        /// <param name="lpBuffer">Класс с полями. Первое поле указывает размер и читается, а другие заполняет функция</param>
        /// <returns>Удалось взять информацию или нет</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        // ═════════════════════════════════════════════
        //  P/Invoke — разрешение и частота экрана
        //  через GDI-контекст основного монитора
        // ═════════════════════════════════════════════

        /// <summary>
        /// Структура DEVMODE из wingdi.h. Содержит описание одного видеорежима:
        /// разрешение, частоту, глубину цвета и десятки других полей.
        /// Нам нужны только dmPelsWidth, dmPelsHeight, dmDisplayFrequency и dmSize.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            /// <summary>обязательное поле: размер структуры</summary>
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;         // глубина цвета (бит на пиксель)
            /// <summary>ширина в пикселях</summary>
            public int dmPelsWidth;
            /// <summary>высота в пикселях</summary>
            public int dmPelsHeight;
            public int dmDisplayFlags;
            /// <summary>частота обновления в Гц</summary>
            public int dmDisplayFrequency;

            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        /// <summary>
        /// Перечисляет видеорежимы дисплея. При iModeNum = 0..N возвращает
        /// режимы по одному, пока не вернёт false. При iModeNum = -1 возвращает
        /// текущий режим.
        /// </summary>
        /// <param name="lpszDeviceName">Имя устройства; null = основной дисплей</param>
        /// <param name="iModeNum">Номер режима (0, 1, 2, ...) или ENUM_CURRENT_SETTINGS = -1</param>
        /// <param name="lpDevMode">Структура DEVMODE для заполнения</param>
        /// <returns>true при успехе, false когда режимы закончились</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);


        // ═════════════════════════════════════════════
        //  Точка входа
        // ═════════════════════════════════════════════
        /// <summary>Запуск сбора информации</summary>
        /// <returns>Класс с собраной информацией</returns>
        public static SystemInfo Collect()
        {
            SystemInfo info = new SystemInfo();
            CollectCpu(info);
            CollectGpus(info);
            CollectRam(info);
            CollectDisplay(info);
            CollectOs(info);
            CollectBios(info);
            return info;
        }

        /// <summary>Сбор информации о ЦП</summary>
        /// <param name="info">Класс собираемой информацией</param>
        private static void CollectCpu(SystemInfo info)
        {
            try
            {
                // Путь в HKLM, где Windows хранит паспорт первого CPU.
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

                if (key != null)
                {
                    // ProcessorNameString = REG_SZ, человекочитаемое имя CPU
                    info.Cpu.Name = GetString(key, "ProcessorNameString", "Unknown");

                    // ~MHz = REG_DWORD, номинальная частота в МГц (не Boost!)
                    info.Cpu.MaxClockMhz = key.GetValue("~MHz") switch
                    {
                        int i => (uint)i,        // REG_DWORD
                        uint u => u,
                        long l when l >= 0 => (uint)l,
                        _ => 0
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Registry] Ошибка чтения CPU: {ex.Message}");
            }

            // Environment.ProcessorCount — это количество ЛОГИЧЕСКИХ процессоров,
            // включая Hyper-Threading (у 8-ядерного с HT будет 16)
            info.Cpu.LogicalProcessors = (uint)Environment.ProcessorCount;

            // Физические ядра считаем через P/Invoke — Environment этого не знает
            info.Cpu.Cores = (uint)GetPhysicalCoreCount();
        }
        /// <summary>Расчёт количество физических ядер в ЦП</summary>
        /// <returns>Кол-во ядер</returns>
        private static int GetPhysicalCoreCount()
        {
            // Первый вызов — с нулевым буфером, чтобы узнать требуемый размер
            uint length = 0;
            GetLogicalProcessorInformation(IntPtr.Zero, ref length);
            if (length == 0) return 0;

            IntPtr buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                // Второй вызов — с выделенным буфером, получаем данные
                if (!GetLogicalProcessorInformation(buffer, ref length))
                    return 0;

                int structSize = Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>();
                int count = (int)(length / structSize);
                int cores = 0;

                for (int i = 0; i < count; i++)
                {
                    // Сдвигаемся на i * sizeof(структуры) байт от начала буфера
                    IntPtr ptr = IntPtr.Add(buffer, i * structSize);
                    var entry = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>(ptr);

                    // Считаем только записи с Relationship = 0 (физическое ядро)
                    if (entry.Relationship == RelationProcessorCore)
                        cores++;
                }
                return cores;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>Сбор информации о видеокарта</summary>
        /// <param name="info">Класс собираемой информацией</param>
        private static void CollectGpus(SystemInfo info)
        {
            // GUID класса «Display adapters» (видеоадаптеры).
            // Это стандартный идентификатор Windows, он одинаков на всех системах.
            // По нему реестр хранит по подразделу на каждую установленную видеокарту.
            const string displayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";

            try
            {
                // Внутри этого класса лежат подразделы 0000, 0001, ... — по одному на GPU
                using var classKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\" + displayClassGuid);
                if (classKey == null) return;

                foreach (var subName in classKey.GetSubKeyNames())
                {
                    // Нас интересуют только имена из 4 цифр: 0000, 0001, ...
                    // Другие подразделы (например, "Properties") пропускаем
                    if (subName.Length != 4 || !int.TryParse(subName, out _))
                        continue;

                    using var sub = classKey.OpenSubKey(subName);
                    if (sub == null) continue;

                    // DriverDesc = REG_SZ, отображаемое имя GPU (например, "NVIDIA GeForce RTX 4070")
                    string name = GetString(sub, "DriverDesc", string.Empty);
                    if (string.IsNullOrEmpty(name)) continue;

                    var gpu = new GpuInfo
                    {
                        Name = name,

                        // DriverVersion = REG_SZ, версия драйвера (например, "31.0.15.5123")
                        DriverVersion = GetString(sub, "DriverVersion", "н/д"),
                    };

                    // VRAM лежит в подразделе HardwareInformation
                    using var hwKey = sub.OpenSubKey("HardwareInformation");
                    if (hwKey != null)
                    {
                        // qwMemorySize = REG_QWORD, объём VRAM в БАЙТАХ
                        gpu.VramBytes = hwKey.GetValue("qwMemorySize") switch
                        {
                            long l when l >= 0 => (ulong)l, // REG_QWORD
                            int i when i >= 0 => (ulong)i, // иногда 32-битная система отдаёт int
                            _ => 0
                        };
                    }

                    info.Gpus.Add(gpu);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Registry] Ошибка чтения GPU: {ex.Message}");
            }
        }

        /// <summary>Сбор информации о оперативной памяти</summary>
        /// <param name="info">Класс собираемой информацией</param>
        private static void CollectRam(SystemInfo info)
        {
            try
            {
                var mem = new MEMORYSTATUSEX
                {
                    // dwLength обязателен — Windows проверяет его для совместимости версий
                    dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
                };

                if (GlobalMemoryStatusEx(mem))
                    info.Ram.TotalBytes = mem.ullTotalPhys;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[P/Invoke] Ошибка чтения RAM: {ex.Message}");
            }
        }

        /// <summary>Сбор информации о мониторе</summary>
        /// <param name="info">Класс собираемой информацией</param>
        private static void CollectDisplay(SystemInfo info)
        {
            var modes = new List<DisplayMode>();
            var seen = new HashSet<(int, int, int)>();

            DEVMODE devMode = new DEVMODE
            {
                // dmSize обязательно должен быть установлен до вызова —
                // Windows проверяет его для совместимости версий структуры
                dmSize = (short)Marshal.SizeOf<DEVMODE>()
            };

            int modeNum = 0;

            // EnumDisplaySettings возвращает режимы по одному, пока не вернёт false.
            // null в первом параметре = основной монитор.
            while (EnumDisplaySettings(null, modeNum, ref devMode))
            {
                int w = devMode.dmPelsWidth;
                int h = devMode.dmPelsHeight;
                int r = devMode.dmDisplayFrequency;

                // Фильтруем мусорные записи:
                // - 0 или 1 в частоте означает "использовать по умолчанию"
                // - разрешения ниже 640x480 не имеют практического смысла
                // - частоты выше 500 Гц не существуют
                if (r > 1 && r < 500 && w >= 640 && h >= 480)
                {
                    // HashSet защищает от дубликатов (EnumDisplaySettings иногда их отдаёт)
                    if (seen.Add((w, h, r)))
                        modes.Add(new DisplayMode { Width = w, Height = h, RefreshRate = r });
                }

                modeNum++;
            }

            if(modes.Count == 0)
            {
                return;
            }

            // Сортируем для удобства: сначала по разрешению, потом по частоте
            modes.Sort((a, b) =>
            {
                int byArea = ((long)a.Width * a.Height).CompareTo((long)b.Width * b.Height);
                return byArea != 0 ? byArea : a.RefreshRate.CompareTo(b.RefreshRate);
            });
            info.Display.MaxResolutionMode = modes.OrderByDescending(m => (long)m.Width * m.Height).ThenByDescending(m => m.RefreshRate).First();
            info.Display.MinResolutionMode = modes.OrderBy(m => (long)m.Width * m.Height).ThenByDescending(m => m.RefreshRate).First();
            if (EnumDisplaySettings(null, -1, ref devMode))
            {
                info.Display.Current = new DisplayMode { Width = devMode.dmPelsWidth, Height = devMode.dmPelsHeight, RefreshRate = devMode.dmDisplayFrequency };
            }
            else
            {
                info.Display.Current = info.Display.MinResolutionMode;
            }
        }

        /// <summary>Сбор информации об ОС</summary>
        /// <param name="info">Класс собираемой информацией</param>
        private static void CollectOs(SystemInfo info)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

                if (key != null)
                {
                    // ProductName = REG_SZ, например "Windows 10 Pro" или "Windows 11 Pro"
                    string product = GetString(key, "ProductName", "Unknown");

                    // DisplayVersion = REG_SZ, например "23H2" (для Windows 10/11)
                    string display = GetString(key, "DisplayVersion", string.Empty);

                    Version osVer = Environment.OSVersion.Version;
                    info.Os.Version = $"{osVer.Major}.{osVer.Minor}.{osVer.Build}";

                    info.Os.Caption = product;
                    if (!string.IsNullOrEmpty(display))
                        info.Os.Caption += $" ({display})";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Registry] Ошибка чтения ОС: {ex.Message}");
            }

            // Fallback — если реестр не дал версию, берём из Environment.OSVersion
            if (string.IsNullOrEmpty(info.Os.Version) || info.Os.Version == "н/д")
            {
                var v = Environment.OSVersion.Version;
                info.Os.Version = $"{v.Major}.{v.Minor}.{v.Build}";
            }

            // RuntimeInformation.OSArchitecture — "X64", "Arm64", "X86"
            info.Os.Architecture = RuntimeInformation.OSArchitecture.ToString();

            // Environment.Is64BitProcess — true, если наше приложение 64-битное
            info.Os.Is64BitProcess = Environment.Is64BitProcess;
        }

        /// <summary>Сбор информации о БИОС</summary>
        /// <param name="info">Класс собираемой информацией</param>
        private static void CollectBios(SystemInfo info)
        {
            try
            {
                // HKLM\HARDWARE\DESCRIPTION\System\BIOS — ключ, где Windows хранит
                // данные SMBIOS, которые BIOS/UEFI передала при загрузке
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (key == null) return;

                // BIOSVendor = REG_SZ, например "American Megatrends Inc."
                info.Bios.Vendor = GetString(key, "BIOSVendor", "Unknown");

                // BIOSVersion — капризный параметр: может быть REG_SZ или REG_MULTI_SZ
                info.Bios.Version = key.GetValue("BIOSVersion") switch
                {
                    string[] ver => ver.Length == 0 ? "н/д" : string.Join(" ", ver).Trim(),
                    string ver when !string.IsNullOrWhiteSpace(ver) => ver.Trim(),
                    _ => "н/д"
                };

                // BIOSReleaseDate = REG_SZ, формат обычно "MM/DD/YYYY"
                info.Bios.ReleaseDate = GetString(key, "BIOSReleaseDate", "н/д");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Registry] Ошибка чтения BIOS: {ex.Message}");
            }
        }

        /// <summary>Читает REG_SZ как string. Возвращает fallback, если значения нет.</summary>
        /// <param name="key">Ключ регистра</param>
        /// <param name="valueName">Имя параметра, который берём</param>
        /// <param name="fallback">Если не удалсоь взять. Значение по умолчанию</param>
        private static string GetString(RegistryKey key, string valueName, string fallback)
        {
            return key.GetValue(valueName) as string ?? fallback;
        }
    }
}
