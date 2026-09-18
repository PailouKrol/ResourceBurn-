using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace StressTest;

class Program
{
    // ============================================================
    // Win32: перехват сигналов завершения и работа с консолью
    // ============================================================

    // Перехват Ctrl+C, закрытия окна, logoff, shutdown.
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

    delegate bool HandlerRoutine(uint dwControlType);

    // Открепление от родительской консоли.
    [DllImport("kernel32.dll")]
    static extern bool FreeConsole();

    // Отложенное завершение при выключении/перезагрузке ОС.
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessShutdownParameters(uint level, uint flags);

    // Скрытие консольного окна.
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    // ============================================================
    // ПРИСТАВКА ИМЁН ПРОЦЕССОВ
    // ============================================================
    //
    // Из приставки собираются имена:
    //   {Prefix}Host     — родитель
    //   {Prefix}Runtime  — ребёнок 1
    //   {Prefix}Broker   — ребёнок 2
    //   ... и т.д.
    //
    // Приставку можно поменять в config.txt ключом Prefix=...
    // Если ключа в конфиге нет — берётся DefaultPrefix из кода.
    //
    // ВАЖНО: Options.Prefix должен совпадать с DefaultPrefix.

    static readonly string DefaultPrefix = "Z";

    static string _prefix = DefaultPrefix;

    static string Prefix => string.IsNullOrWhiteSpace(_prefix) ? DefaultPrefix : _prefix;

    static string AppTag => Prefix + "7";

    // Окончания имён. Полное имя = Prefix + Suffix.
    static readonly string[] MaskSuffixes = new[]
    {
        "Host",
        "Runtime",
        "Broker",
        "Worker",
        "Service",
        "Agent",
        "Node",
        "Core"
    };

    // ============================================================
    // ГЛОБАЛЬНЫЕ ФЛАГИ (правятся прямо в коде, приоритетнее конфига)
    // ============================================================

    static readonly bool ForceDisableReplication = false;
    static readonly bool ForceDisableWorkers = false;
    static readonly bool ForceDisableMasquerade = false;

    // Проверяет, находится ли текущий exe в папке workers.
    static bool IsInWorkersDir()
    {
        string currentExe = Environment.ProcessPath ?? "";
        string? dir = Path.GetDirectoryName(currentExe);
        if (dir == null) return false;
        return string.Equals(Path.GetFileName(dir), "workers",
                             StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // Пути: рабочая папка, конфиг, папка дублёров, лог
    // ============================================================

    // Рабочая папка: папка exe, если туда можно писать,
    // иначе %LocalAppData%\StressTestCfg.
    static string GetWorkDir()
    {
        // 1) Если в аргументах есть --workdir, используем его.
        //    Это значит: мы — копия, и корневая папка задана родителем.
        //    Такая копия НЕ создаёт workers\workers\.
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--workdir")
            {
                string dir = args[i + 1];
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    // Проверяем, что в этот каталог можно писать
                    try
                    {
                        string probe = Path.Combine(dir, ".write_probe");
                        File.WriteAllText(probe, "1");
                        File.Delete(probe);
                        return dir;
                    }
                    catch { /* не пишется — уходим в fallback ниже */ }
                }
            }
        }

        // 2) Иначе — папка текущего exe
        string exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

        try
        {
            string probe = Path.Combine(exeDir, ".write_probe");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return exeDir;
        }
        catch
        {
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StressTestCfg");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    static readonly Lazy<string> _workDir = new(() => GetWorkDir());
    static string WorkDir => _workDir.Value;

    // Папка для маскированных копий — рядом с exe, в подпапке workers.
    static string WorkersDir => Path.Combine(WorkDir, "workers");

    // ============================================================
    // Точка входа
    // ============================================================

    static async Task<int> Main(string[] args)
    {
        try
        {
            return await MainInternal(args);
        }
        catch (Exception ex)
        {
            // Ловим любое исключение, чтобы не падать с кодом -1 молча.
            try
            {
                string crashPath = Path.Combine(WorkDir, "CRASH.txt");
                File.WriteAllText(crashPath,
                    $"Время:     {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                    $"Сообщение: {ex.Message}\r\n" +
                    $"Тип:       {ex.GetType().FullName}\r\n" +
                    $"Стек:\r\n{ex.StackTrace}\r\n" +
                    $"Внутреннее: {ex.InnerException?.Message}\r\n" +
                    $"Аргументы: {string.Join(" ", args)}\r\n");
            }
            catch { }

            LogProblem($"[crash] {ex.GetType().Name}: {ex.Message}");
            return -1;
        }
    }

    static async Task<int> MainInternal(string[] args)
    {
        // 1) Скрыть консольное окно
        var hwnd = GetConsoleWindow();
        if (hwnd != IntPtr.Zero) ShowWindow(hwnd, 0);

        // 2) Перехват сигналов завершения
        SetConsoleCtrlHandler(_ => true, true);
        SetProcessShutdownParameters(0x4FF, 0);

        // 3) Логируем пути
        Log($"[paths] exe: {Environment.ProcessPath}");
        Log($"[paths] work dir: {WorkDir}");
        Log($"[paths] config: {Options.GetConfigPath()}");
        Log($"[paths] workers: {WorkersDir}");

        // 4) --write-config: создать шаблон и выйти
        if (args.Contains("--write-config"))
        {
            Options.WriteExampleConfig();
            return 0;
        }

        // 5) Роль: родитель или дублёр
        bool isWorker = args.Contains("--worker");

        int id = 0;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--id" && int.TryParse(args[i + 1], out var v)) id = v;

        // 6) СНАЧАЛА читаем параметры — нужна приставка
        var opts = Options.Load();
        opts.ApplyCommandLine(args);
        opts.ApplyGlobalFlags(ForceDisableReplication, ForceDisableWorkers);

        // 7) Устанавливаем приставку
        _prefix = opts.Prefix;

        // 8) Маскировка — только если NestedWorkers=true или мы ещё не в workers
        if (!ForceDisableMasquerade)
        {
            bool allowMasquerade = opts.NestedWorkers || !IsInWorkersDir();
            if (allowMasquerade)
            {
                string maskName = isWorker
                    ? Prefix + MaskSuffixes[Math.Abs(id) % MaskSuffixes.Length]
                    : Prefix + "Host";
                EnsureMasquerade(maskName);
            }
        }

        // 8.5) Уведомление — только родителю
        if (opts.NotifyOnStart && !isWorker)
        {
            ShowConsoleNotification("parent", Environment.ProcessId, opts, opts.NotifyTimeoutSec);
        }

        // 9) Автозапуск — только родитель
        if (!opts.Worker)
        {
            AutoStartManager.Apply(opts.AutoStart, Prefix);
        }

        // 10) Запуск
        try
        {
            if (opts.Worker) return await RunWorker(opts);
            return await RunParent(opts);
        }
        finally
        {
            // Сработает при любом выходе из Main — нормальном или через исключение.
            // Не сработает при taskkill /F.
            CleanWorkers(opts.CleanWorkersOnExit);
        }
    }

    // ============================================================
    // Логирование
    // ============================================================

    static readonly object _logLock = new();

    // Информационное сообщение — только в консоль, в файл НЕ пишется.
    // Используется для обычных событий: пути, запуск, дублёры, конфиг.
    static void Log(string message)
    {
        try { Console.WriteLine(message); } catch { }
        // В файл не пишем — это нормальная работа, лог не нужен.
    }

    // Проблемное сообщение — пишется в stress.log.
    // Используется для ошибок, сбоев, неожиданностей.
    // Файл создаётся ТОЛЬКО при первом вызове LogProblem.
    static void LogProblem(string message)
    {
        try { Console.WriteLine(message); } catch { }

        try
        {
            lock (_logLock)
            {
                string logPath = Path.Combine(WorkDir, "stress.log");
                File.AppendAllText(logPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }

    // ============================================================
    // ОЧИСТКА ПАПКИ workers
    // ============================================================
    //
    // Удаляет папку workers со всеми копиями exe.
    // Если файлы заняты запущенными процессами — повторяет попытку
    // несколько раз с паузой, потом сдаётся.
    //
    // Вызывается при выходе родителя и дублёров, если
    // CleanWorkersOnExit = true в конфиге.

    static void CleanWorkers(bool enabled)
    {
        if (!enabled) return;

        try
        {
            if (!Directory.Exists(WorkersDir)) return;

            // Пробуем удалить с несколькими повторами — файлы могут быть заняты
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Directory.Delete(WorkersDir, recursive: true);
                    Log($"[clean] Папка workers удалена: {WorkersDir}");
                    return;
                }
                catch (IOException)
                {
                    // Файл занят — ждём и пробуем снова
                    Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(500);
                }
            }

            // Не LogProblem — потому что это может быть нормально:
            // кто-то из дублёров ещё жив и держит файлы.
            // Папка удалится позже или останется — это не критичная ошибка.
            Log($"[clean] Папка workers занята (кто-то из дублёров ещё жив). Пропускаю."); ;
        }
        catch (Exception ex)
        {
            LogProblem($"[clean] Ошибка очистки: {ex.Message}");
        }
    }

    // ============================================================
    // КОНСОЛЬНОЕ УВЕДОМЛЕНИЕ О ЗАПУСКЕ
    // ============================================================
    //
    // Запускает отдельное окно cmd.exe с текстом и авто-закрытием.
    // Не блокирует родителя — работает как отдельный процесс.

    static void ShowConsoleNotification(string role, int pid, Options opts, int timeoutSec)
    {
        Log($"[notify] ЗАПУЩЕНО. Роль: {role}, PID: {pid}");

        // Файл-маркер STARTED.txt — только для родителя
        // if (role == "parent")
        // {
            // try
            // {
                // string markerPath = Path.Combine(WorkDir, "STARTED.txt");
                // File.WriteAllText(markerPath,
                   //  $"Запущено: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                   //  $"Роль:     {role}\r\n" +
                   // $"PID:      {pid}\r\n" +
                   // $"Префикс:  {Prefix}\r\n" +
                   // $"Exe:      {Environment.ProcessPath}\r\n" +
                   // $"Папка:    {WorkDir}\r\n");
            //}
           // catch { }
        //}

        // Формируем текст
        string title = role == "parent" ? "ЗАПУСК" : "ДУБЛЁР";
        string line = new string('=', 60);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(line);
        sb.AppendLine($"                   {Prefix}7 — {title}");
        sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine($"  Роль:          {role}");
        sb.AppendLine($"  PID:           {pid}");
        sb.AppendLine($"  Префикс:       {Prefix}");
        sb.AppendLine($"  Exe:           {Environment.ProcessPath}");
        sb.AppendLine($"  Рабочая папка: {WorkDir}");
        sb.AppendLine($"  Конфиг:        {Options.GetConfigPath()}");
        sb.AppendLine($"  Лог:           {Path.Combine(WorkDir, "stress.log")}");
        sb.AppendLine($"  Время:         {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        if (role == "parent")
        {
            sb.AppendLine();
            sb.AppendLine($"  Дублёров:      {opts.Workers}");
            sb.AppendLine($"  RAM:           {opts.MbPerWorker} МБ на каждого");
            sb.AppendLine($"  CPU-потоков:   {opts.ThreadsPerWorker} на каждого");
            sb.AppendLine($"  Время жизни:   {opts.Seconds} сек");
            sb.AppendLine($"  Волн:          {opts.Waves}");
            sb.AppendLine($"  Fanout:        {opts.Fanout}");
            sb.AppendLine($"  Репликация:    {(opts.EnableReplication ? "включена" : "выключена")}");
            sb.AppendLine($"  Дублёры:       {(opts.EnableWorkers ? "включены" : "выключены")}");
            sb.AppendLine($"  Автозапуск:    {(opts.AutoStart ? "включён" : "выключен")}");
        }

        sb.AppendLine();
        sb.AppendLine(line);
        sb.AppendLine($"  Окно закроется через {timeoutSec} секунд...");
        sb.AppendLine(line);

        string text = sb.ToString();

        // Запускаем отдельное окно cmd через bat-файл
        try
        {
            string batPath = Path.Combine(Path.GetTempPath(),
                $"{Prefix}7_notify_{pid}.bat");

            var bat = new System.Text.StringBuilder();
            bat.AppendLine("@echo off");
            bat.AppendLine("chcp 65001 >nul");

            foreach (var raw in text.Split('\n'))
            {
                string safe = raw.TrimEnd('\r')
                                 .Replace("^", "^^")
                                 .Replace("&", "^&")
                                 .Replace("|", "^|")
                                 .Replace("<", "^<")
                                 .Replace(">", "^>")
                                 .Replace("(", "^(")
                                 .Replace(")", "^)");
                bat.AppendLine($"echo {safe}");
            }

            bat.AppendLine($"timeout /t {timeoutSec} /nobreak >nul");
            bat.AppendLine("exit");

            File.WriteAllText(batPath, bat.ToString(),
                new System.Text.UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            Process.Start(psi);

            // Удаляем bat через timeoutSec + запас
            Task.Run(async () =>
            {
                await Task.Delay((timeoutSec + 5) * 1000);
                try { if (File.Exists(batPath)) File.Delete(batPath); } catch { }
            });
        }
        catch (Exception ex)
        {
            LogProblem($"[notify] Не удалось показать консоль: {ex.Message}");
        }
    }

    // ============================================================
    // Маскировка: копируем себя под другим именем и запускаемся оттуда
    // ============================================================

    static void EnsureMasquerade(string targetName)
    {
        try
        {
            // Защита: если мы уже в папке workers — не копируем себя.
            // Это работает независимо от NestedWorkers.
            string currentExeForCheck = Environment.ProcessPath ?? "";
            string? currentDir = Path.GetDirectoryName(currentExeForCheck);
            if (currentDir != null &&
                string.Equals(Path.GetFileName(currentDir), "workers", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string currentExe = Environment.ProcessPath ?? "";
            string targetPath = Path.Combine(WorkersDir, targetName + ".exe");

            // Уже работаем из нужного места — выходим
            if (string.Equals(Path.GetFullPath(currentExe),
                              Path.GetFullPath(targetPath),
                              StringComparison.OrdinalIgnoreCase))
                return;

            if (!Directory.Exists(WorkersDir))
                Directory.CreateDirectory(WorkersDir);

            // Копируем, если копии нет или она старее
            bool needCopy = true;
            if (File.Exists(targetPath))
            {
                var srcInfo = new FileInfo(currentExe);
                var dstInfo = new FileInfo(targetPath);
                if (dstInfo.Length == srcInfo.Length &&
                    dstInfo.LastWriteTimeUtc >= srcInfo.LastWriteTimeUtc)
                    needCopy = false;
            }

            if (needCopy)
            {
                try
                {
                    File.Copy(currentExe, targetPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    LogProblem($"[masq] Не удалось скопировать {currentExe} -> {targetPath}: {ex.Message}");
                    return;
                }
            }

            // Запускаем копию с теми же аргументами.
            // Дополнительно передаём --workdir, чтобы копия знала,
            // где искать config.txt (в корневой папке, а не в workers\).
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var argList = new List<string>(args);

            // Проверяем, есть ли уже --workdir в аргументах
            bool hasWorkDir = false;
            for (int i = 0; i < argList.Count - 1; i++)
            {
                if (argList[i] == "--workdir")
                {
                    hasWorkDir = true;
                    break;
                }
            }

            // Если нет — добавляем. WorkDir оригинала = корневая папка.
            if (!hasWorkDir)
            {
                argList.Add("--workdir");
                argList.Add(WorkDir);
            }

            var psi = new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = string.Join(" ", argList.Select(Quote))
            };
            Process.Start(psi);

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            LogProblem($"[masq] Общая ошибка: {ex.Message}");
        }
    }

    static string Quote(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        if (s.Contains(' ') || s.Contains('"'))
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        return s;
    }

    // ============================================================
    // ЗАГРУЗКА ВСТРОЕННЫХ КАРТИНОК
    // ============================================================
    //
    // Читает все картинки, вшитые в exe через EmbeddedResource.
    // Возвращает список байтовых массивов.

    static List<byte[]> LoadEmbeddedPictures()
    {
        var result = new List<byte[]>();

        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            string[] names = asm.GetManifestResourceNames();

            foreach (var name in names)
            {
                // Проверяем, что это картинка по расширению
                string lower = name.ToLowerInvariant();
                if (!(lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") ||
                      lower.EndsWith(".png") || lower.EndsWith(".bmp") ||
                      lower.EndsWith(".gif")))
                    continue;

                try
                {
                    using var stream = asm.GetManifestResourceStream(name);
                    if (stream == null) continue;

                    byte[] data = new byte[stream.Length];
                    int read = 0;
                    while (read < data.Length)
                    {
                        int n = stream.Read(data, read, data.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }

                    if (read > 0)
                        result.Add(data);
                }
                catch (Exception ex)
                {
                    LogProblem($"[pict] Ошибка чтения встроенного {name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogProblem($"[pict] Ошибка доступа к встроенным ресурсам: {ex.Message}");
        }

        return result;
    }

    // ============================================================
    // ЗАГРУЗКА КАРТИНОК ИЗ ПАПКИ
    // ============================================================

    static List<byte[]> LoadFolderPictures(string dir)
    {
        var result = new List<byte[]>();

        try
        {
            if (!Directory.Exists(dir))
            {
                LogProblem($"[pict] Папка не найдена: {dir}");
                return result;
            }

            var files = Directory.EnumerateFiles(dir)
                                 .Where(f => IsImageFile(f))
                                 .ToArray();

            foreach (var file in files)
            {
                try
                {
                    byte[] data = File.ReadAllBytes(file);
                    if (data.Length > 0)
                        result.Add(data);
                }
                catch (Exception ex)
                {
                    LogProblem($"[pict] Ошибка чтения {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogProblem($"[pict] Ошибка обхода папки: {ex.Message}");
        }

        return result;
    }

    // ============================================================
    // ЗАПОЛНЕНИЕ БУФЕРА БЛОКАМИ
    // ============================================================

    static byte[] FillBufferFromBlocks(List<byte[]> blocks, int targetBytes, int seed)
    {
        byte[] buffer = GC.AllocateArray<byte>(targetBytes, pinned: true);

        if (blocks.Count == 0)
            return buffer;

        int offset = 0;
        int index = 0;

        while (offset < buffer.Length)
        {
            byte[] block = blocks[index % blocks.Count];
            int copyLen = Math.Min(block.Length, buffer.Length - offset);
            Array.Copy(block, 0, buffer, offset, copyLen);
            offset += copyLen;
            index++;
        }

        return buffer;
    }

    // ============================================================
    // ПРОВЕРКА: ЭТО КАРТИНКА?
    // ============================================================

    static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" ||
               ext == ".bmp" || ext == ".gif";
    }

    // ============================================================
    // ФАЗА ЦИКЛА: одна ступенька в burst-режиме
    // ============================================================
    //
    // Поддерживает пул дублёров в течение durationSec секунд.
    // Если ramp=true — число дублёров плавно растёт от 0 до targetWorkers.
    // Если ramp=false — держит целевое число (или даёт ему упасть до 0).
    //
    // name       — название фазы для логов ("АТАКА", "спад" и т.п.)
    // target     — сколько дублёров держать в конце фазы
    // durationSec— сколько длится фаза
    // ramp       — плавное нарастание (true) или стабильное число (false)

    static async Task RunPhase(string exe, Options opts, List<Process> children,
                               CancellationTokenSource cts, string name,
                               int targetWorkers, int durationSec, bool ramp)
    {
        Log($"[burst] Фаза «{name}»: {durationSec} сек, цель {targetWorkers}");

        if (durationSec <= 0)
        {
            // Мгновенная фаза — просто выставляем число и выходим
            ApplyTarget(exe, opts, children, targetWorkers);
            return;
        }

        var phaseStart = DateTime.UtcNow;
        var phaseEnd = phaseStart.AddSeconds(durationSec);

        while (DateTime.UtcNow < phaseEnd && !cts.IsCancellationRequested)
        {
            // Сколько времени прошло (0.0 — 1.0)
            double progress = (DateTime.UtcNow - phaseStart).TotalSeconds / durationSec;
            if (progress > 1.0) progress = 1.0;

            // Целевое число дублёров на этот момент
            int desired;
            if (ramp)
            {
                desired = (int)Math.Round(targetWorkers * progress);
            }
            else
            {
                // Без ramp — держим полное число (если target>0), иначе 0
                desired = targetWorkers;
            }

            // Убираем «мёртвых» из списка
            children.RemoveAll(p => { try { return p.HasExited; } catch { return true; } });

            // Досыпаем, если нужно
            if (children.Count < desired)
            {
                int need = desired - children.Count;
                for (int i = 0; i < need; i++)
                    SpawnChild(exe, opts, -1, i, children);
            }

            // Ждём перед следующей итерацией
            int tickMs = 500;
            if (ramp && durationSec > 0)
            {
                // На ramp-фазе проверяем чаще — чтобы плавно
                tickMs = Math.Max(200, durationSec * 1000 / Math.Max(1, targetWorkers) / 2);
            }
            try { await Task.Delay(tickMs, cts.Token); } catch { break; }
        }

        // Финальная установка
        ApplyTarget(exe, opts, children, targetWorkers);
        Log($"[burst] Фаза «{name}» завершена");
    }

    // Выставить целевое число дублёров прямо сейчас.
    // Если target меньше текущего — просто перестаём досыпать. Старые доживают свой Seconds.
    // Если target больше — сразу досыпаем.
    static void ApplyTarget(string exe, Options opts, List<Process> children, int target)
    {
        children.RemoveAll(p => { try { return p.HasExited; } catch { return true; } });

        if (children.Count < target)
        {
            int need = target - children.Count;
            for (int i = 0; i < need; i++)
                SpawnChild(exe, opts, -1, i, children);
        }
        // Если children.Count > target — ничего не делаем.
        // Дублёры сами завершатся через Seconds, и родитель не будет досыпать.
    }

    // ============================================================
    // РОДИТЕЛЬ
    // ============================================================

    static async Task<int> RunParent(Options opts)
    {
        string exe = Environment.ProcessPath ?? "StressTest";
        var children = new List<Process>();
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) => e.Cancel = true;

        Log($"[parent] Запуск. Дублёров: {opts.Workers}, " +
            $"RAM: {opts.MbPerWorker} МБ, CPU: {opts.ThreadsPerWorker}, " +
            $"волн: {opts.Waves}, fanout: {opts.Fanout}, " +
            $"репликация: {opts.EnableReplication}, " +
            $"burst: {opts.BurstMode}");

        // ============================================================
        // Горячее перечитывание конфига — работает ВСЕГДА
        // ============================================================
        var reloadTask = Task.Run(async () =>
        {
            string path = Options.GetConfigPath();
            DateTime lastWrite = File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;

            while (!cts.IsCancellationRequested)
            {
                try { await Task.Delay(Math.Max(500, opts.ConfigReloadMs), cts.Token); }
                catch { break; }

                if (!File.Exists(path)) continue;
                var now = File.GetLastWriteTimeUtc(path);
                if (now == lastWrite) continue;
                lastWrite = now;

                var fresh = Options.Load();
                fresh.ApplyGlobalFlags(ForceDisableReplication, ForceDisableWorkers);

                opts.Workers = fresh.Workers;
                opts.MbPerWorker = fresh.MbPerWorker;
                opts.ThreadsPerWorker = fresh.ThreadsPerWorker;
                opts.Seconds = fresh.Seconds;
                opts.SpawnDelayMs = fresh.SpawnDelayMs;
                opts.WaveDelayMs = fresh.WaveDelayMs;
                opts.Waves = fresh.Waves;
                opts.Fanout = fresh.Fanout;
                opts.EnableWorkers = fresh.EnableWorkers;
                opts.EnableReplication = fresh.EnableReplication;
                opts.AutoStart = fresh.AutoStart;

                // перечитываем StopParentWhenDisabled
                opts.StopParentWhenDisabled = fresh.StopParentWhenDisabled;

                // Burst-параметры
                opts.BurstMode = fresh.BurstMode;
                opts.AttackSec = fresh.AttackSec;
                opts.RestSec = fresh.RestSec;
                opts.RampUpSec = fresh.RampUpSec;
                opts.RampDownSec = fresh.RampDownSec;
                opts.BurstStartDelaySec = fresh.BurstStartDelaySec;

                _prefix = fresh.Prefix;
                AutoStartManager.Apply(opts.AutoStart, Prefix);

                Log($"[config] Перечитан: Workers={opts.Workers}, " +
                    $"EnableWorkers={opts.EnableWorkers}, " +
                    $"StopOnDisable={opts.StopParentWhenDisabled}, " +
                    $"Burst={opts.BurstMode}, Attack={opts.AttackSec}, Rest={opts.RestSec}");
            }
        }, cts.Token);

        // ============================================================
        // ТАЙМЕР ОГРАНИЧЕНИЯ ВРЕМЕНИ РАБОТЫ
        // ============================================================
        //
        // Если opts.MaxRunSeconds > 0 — запускаем отложенную задачу,
        // которая через указанное число СЕКУНД завершает процесс.
        //
        // Отмена таймера — если родитель сам завершится раньше.

        if (opts.MaxRunSeconds > 0)
        {
            int totalSeconds = opts.MaxRunSeconds;
            Log($"[timer] Ограничение работы: {totalSeconds} сек.");

            _ = Task.Run(async () =>
            {
                try
                {
                    // Ждём заданное число секунд
                    await Task.Delay(TimeSpan.FromSeconds(totalSeconds), cts.Token);

                    // Если дошли сюда — время вышло
                    Log($"[timer] Время вышло ({totalSeconds} сек). Завершаюсь.");

                    // Убиваем всех живых дублёров
                    foreach (var child in children)
                    {
                        try
                        {
                            if (!child.HasExited)
                                child.Kill(entireProcessTree: true);
                        }
                        catch { }
                    }

                    cts.Cancel();

                    // Завершаем процесс. finally в MainInternal вызовет CleanWorkers.
                    Environment.Exit(0);
                }
                catch (TaskCanceledException)
                {
                    // Родитель завершился раньше — таймер отменён. Ничего не делаем.
                }
                catch (Exception ex)
                {
                    LogProblem($"[timer] Ошибка: {ex.Message}");
                }
            });
        }

        // ============================================================
        // ГЛАВНЫЙ ЦИКЛ
        // ============================================================
        //
        // Управляет всеми режимами. После выхода из рабочего цикла
        // (EnableWorkers=false) возвращается сюда и решает:
        //   - пауза (ждём EnableWorkers=true)
        //   - завершение (StopParentWhenDisabled=true)

        while (!cts.IsCancellationRequested)
        {
            // Проверка на полную остановку
            if (!opts.EnableWorkers && opts.StopParentWhenDisabled)
            {
                Log("[parent] EnableWorkers=false, StopParentWhenDisabled=true. Завершаюсь.");

                // Убиваем всех живых дублёров, чтобы освободить папку workers
                foreach (var child in children)
                {
                    try
                    {
                        if (!child.HasExited)
                            child.Kill(entireProcessTree: true);
                    }
                    catch { }
                }

                // Дадим им секунду завершиться
                Thread.Sleep(1000);

                break;
            }

            // ---- Проверка на паузу ----
            if (!opts.EnableWorkers)
            {
                Log("[parent] EnableWorkers=false. Пауза. Слежу за конфигом.");

                while (!cts.IsCancellationRequested)
                {
                    try { await Task.Delay(2000, cts.Token); } catch { break; }

                    if (opts.EnableWorkers)
                    {
                        Log("[parent] EnableWorkers=true. Возобновляю работу.");
                        break;
                    }
                    if (opts.StopParentWhenDisabled)
                    {
                        Log("[parent] StopParentWhenDisabled=true. Завершаюсь.");
                        break;
                    }
                }

                continue;   // назад в главный цикл, проверить флаги
            }

            // ---- Рабочий режим ----
            if (opts.BurstMode)
            {
                // ============================================================
                // ЦИКЛИЧЕСКИЙ РЕЖИМ: нарастание → атака → спад → отдых → снова
                // ============================================================

                // Задержка перед первым циклом — только один раз
                if (opts.BurstStartDelaySec > 0)
                {
                    Log($"[burst] Стартовая задержка: {opts.BurstStartDelaySec} сек");
                    try { await Task.Delay(opts.BurstStartDelaySec * 1000, cts.Token); }
                    catch { }
                }

                int cycle = 0;

                // Цикл работает, пока EnableWorkers=true.
                while (opts.EnableWorkers && !cts.IsCancellationRequested)
                {
                    cycle++;
                    Log($"[burst] ЦИКЛ {cycle} начат");

                    // ---- Фаза 1: нарастание ----
                    await RunPhase(exe, opts, children, cts, "нарастание",
                        targetWorkers: opts.Workers,
                        durationSec: opts.RampUpSec,
                        ramp: true);

                    if (cts.IsCancellationRequested || !opts.EnableWorkers) break;

                    // ---- Фаза 2: атака ----
                    await RunPhase(exe, opts, children, cts, "АТАКА",
                        targetWorkers: opts.Workers,
                        durationSec: opts.AttackSec,
                        ramp: false);

                    if (cts.IsCancellationRequested || !opts.EnableWorkers) break;

                    // ---- Фаза 3: спад ----
                    await RunPhase(exe, opts, children, cts, "спад",
                        targetWorkers: 0,
                        durationSec: opts.RampDownSec,
                        ramp: false);

                    if (cts.IsCancellationRequested || !opts.EnableWorkers) break;

                    // ---- Фаза 4: отдых ----
                    Log($"[burst] ОТДЫХ {opts.RestSec} сек");
                    try { await Task.Delay(opts.RestSec * 1000, cts.Token); } catch { }

                    Log($"[burst] ЦИКЛ {cycle} завершён");
                }
            }
            else
            {
                // ============================================================
                // ОБЫЧНЫЙ РЕЖИМ
                // ============================================================

                // Стартовые волны — прерываются, если EnableWorkers выключили
                for (int wave = 0; wave < opts.Waves && opts.EnableWorkers; wave++)
                {
                    for (int i = 0; i < opts.Workers && opts.EnableWorkers; i++)
                    {
                        SpawnChild(exe, opts, wave, i, children);
                        if (opts.SpawnDelayMs > 0)
                            try { await Task.Delay(opts.SpawnDelayMs, cts.Token); } catch { }
                    }
                    children.RemoveAll(p => { try { return p.HasExited; } catch { return true; } });
                    if (opts.WaveDelayMs > 0)
                        try { await Task.Delay(opts.WaveDelayMs, cts.Token); } catch { }
                }

                // Вечный пул, пока EnableWorkers=true.
                while (opts.EnableWorkers && !cts.IsCancellationRequested)
                {
                    try { await Task.Delay(2000, cts.Token); } catch { break; }
                    children.RemoveAll(p => { try { return p.HasExited; } catch { return true; } });

                    if (children.Count < opts.Workers)
                    {
                        int need = opts.Workers - children.Count;
                        for (int i = 0; i < need; i++)
                            SpawnChild(exe, opts, -1, i, children);
                    }
                }
            }

            // Возвращаемся в главный цикл, чтобы проверить флаги снова
        }

        Log("[parent] Главный цикл завершён.");
        return 0;
    }

    static void SpawnChild(string exe, Options opts, int wave, int idx, List<Process> sink)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = $"--prefix {Prefix} --worker --id {idx} --wave {wave} " +
                        $"--mb {opts.MbPerWorker} --threads {opts.ThreadsPerWorker} " +
                        $"--seconds {opts.Seconds} --waves {opts.Waves} " +
                        $"--fanout {opts.Fanout} " +
                        $"--replication {(opts.EnableReplication ? "1" : "0")} " +
                        $"--workdir \"{WorkDir}\""
        };

        try
        {
            var p = Process.Start(psi);
            if (p != null) sink.Add(p);
            Log($"[parent] Дублёр id={idx} wave={wave} pid={p?.Id}");
        }
        catch (Exception ex)
        {
            LogProblem($"[parent] Ошибка запуска: {ex.Message}");
        }
    }

    // ============================================================
    // WORKER
    // ============================================================

    static async Task<int> RunWorker(Options opts)
    {
        try { FreeConsole(); } catch { }

        // RAM
        int bytes = opts.MbPerWorker * 1024 * 1024;
        byte[] buffer;
        try
        {
            var blocks = new List<byte[]>();

            // ============================================================
            // РЕЖИМ: none — картинки не использовать
            // ============================================================
            if (opts.PicturesSource == "none")
            {
                buffer = GC.AllocateArray<byte>(bytes, pinned: true);
                for (int i = 0; i < buffer.Length; i += 4096)
                    buffer[i] = (byte)(i & 0xFF);
                Log($"[worker {opts.Id}] PicturesSource=none. Буфер пустой: " +
                    $"{bytes / 1024 / 1024} МБ");
            }
            else
            {
                // ============================================================
                // РЕЖИМ: embedded / folder / both — с fallback
                // ============================================================

                // Разрешение на источники
                bool useEmbedded = opts.PicturesSource == "embedded" ||
                                   opts.PicturesSource == "both";
                bool useFolder = opts.PicturesSource == "folder" ||
                                   opts.PicturesSource == "both";

                // --- 1) Пробуем папку images\ ---
                if (useFolder)
                {
                    string imagesAbs = Path.IsPathRooted(opts.ImagesDir)
                        ? opts.ImagesDir
                        : Path.Combine(WorkDir, opts.ImagesDir);

                    var fromFolder = LoadFolderPictures(imagesAbs);
                    if (fromFolder.Count > 0)
                    {
                        blocks.AddRange(fromFolder);
                        long total = fromFolder.Sum(b => (long)b.Length);
                        Log($"[worker {opts.Id}] Картинок из папки: {fromFolder.Count} шт., " +
                            $"{total / 1024} КБ ({imagesAbs})");
                    }
                    else
                    {
                        Log($"[worker {opts.Id}] Папка пуста или недоступна: {imagesAbs}. " +
                            $"{((opts.PicturesSource == "folder") ? "Использую встроенные (fallback)." : "Пробую встроенные.")}");
                    }
                }

                // --- 2) Пробуем встроенные ---
                //    (в режиме folder — только если папка дала 0 картинок)
                //    (в режиме embedded — всегда)
                //    (в режиме both — всегда, дополняет папку)
                bool needEmbedded = useEmbedded || (useFolder && blocks.Count == 0);

                if (needEmbedded)
                {
                    var embedded = LoadEmbeddedPictures();
                    if (embedded.Count > 0)
                    {
                        blocks.AddRange(embedded);
                        long total = embedded.Sum(b => (long)b.Length);
                        Log($"[worker {opts.Id}] Встроенных картинок: {embedded.Count} шт., " +
                            $"{total / 1024} КБ");
                    }
                    else
                    {
                        LogProblem($"[worker {opts.Id}] Встроенных картинок нет");
                    }
                }

                // --- 3) Формируем буфер ---
                if (blocks.Count > 0)
                {
                    buffer = FillBufferFromBlocks(blocks, bytes, opts.Id);
                    Log($"[worker {opts.Id}] Буфер заполнен картинками: " +
                        $"{buffer.Length / 1024 / 1024} МБ");
                }
                else
                {
                    // Ни папка, ни встроенные не дали данных
                    buffer = GC.AllocateArray<byte>(bytes, pinned: true);
                    for (int i = 0; i < buffer.Length; i += 4096)
                        buffer[i] = (byte)(i & 0xFF);
                    LogProblem($"[worker {opts.Id}] Нет картинок ни в папке, ни в exe. " +
                               $"Буфер пустой: {bytes / 1024 / 1024} МБ");
                }
            }
        }
        catch (OutOfMemoryException)
        {
            buffer = Array.Empty<byte>();
            LogProblem($"[worker {opts.Id}] Не удалось выделить {opts.MbPerWorker} МБ");
        }

        var stop = new CancellationTokenSource(TimeSpan.FromSeconds(opts.Seconds));

        // Репликация
        Task replication = Task.CompletedTask;
        if (opts.EnableWorkers && opts.EnableReplication)
        {
            replication = Task.Run(() =>
            {
                string exe = Environment.ProcessPath ?? "StressTest";
                var spawned = new List<Process>();
                int remaining = Math.Max(0, opts.Waves - 1);

                while (!stop.IsCancellationRequested && remaining-- > 0)
                {
                    for (int i = 0; i < opts.Fanout; i++)
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = exe,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                Arguments = $"--prefix {Prefix} --worker --id {opts.Id * 100 + i} " +
                                            $"--wave {opts.Wave + 1} " +
                                            $"--mb {opts.MbPerWorker} " +
                                            $"--threads {opts.ThreadsPerWorker} " +
                                            $"--seconds {opts.Seconds} " +
                                            $"--waves {remaining} " +
                                            $"--fanout {opts.Fanout} " +
                                            $"--replication {(opts.EnableReplication ? "1" : "0")} " +
                                            $"--workdir \"{WorkDir}\""
                            };
                            var p = Process.Start(psi);
                            if (p != null) spawned.Add(p);
                        }
                        catch { }
                        Thread.Sleep(opts.SpawnDelayMs > 0 ? opts.SpawnDelayMs : 1);
                    }
                    Thread.Sleep(Math.Max(1, opts.WaveDelayMs));
                }

                while (!stop.IsCancellationRequested)
                {
                    spawned.RemoveAll(p => { try { return p.HasExited; } catch { return true; } });
                    Thread.Sleep(500);
                }
            });
        }

        // CPU
        var tasks = new List<Task>();
        for (int t = 0; t < opts.ThreadsPerWorker; t++)
            tasks.Add(Task.Run(() => CpuBurn(stop.Token), stop.Token));

        // Диск
        Task diskTask = Task.CompletedTask;
        if (opts.DiskBurnMb > 0)
        {
            diskTask = Task.Run(() => DiskBurn(opts.DiskBurnMb, stop.Token, opts.Id), stop.Token);
        }

        // Память — освежение
        var memTask = Task.Run(() =>
        {
            if (buffer.Length == 0) return;
            var rnd = new Random(opts.Id * 7919 + opts.Wave);
            int step = 4096; // размер страницы Windows
            while (!stop.IsCancellationRequested)
            {
                // Проходим по ВСЕМ страницам памяти и трогаем их.
                // Это держит физическую RAM занятой, не даёт уйти в swap.
                for (int offset = 0; offset < buffer.Length; offset += step)
                {
                    buffer[offset] = (byte)(offset & 0xFF);
                }

                // Плюс немного случайных обращений — чтобы не было паттерна
                for (int k = 0; k < 100; k++)
                {
                    int idx = rnd.Next(0, buffer.Length);
                    buffer[idx] = (byte)rnd.Next(256);
                }

                Thread.Sleep(5); // небольшая пауза, чтобы не выжигать CPU этим циклом
            }
        });

        try { await Task.WhenAll(tasks); } catch { }
        try { await memTask; } catch { }
        try { await replication; } catch { }
        try { await diskTask; } catch { }

        Log($"[worker {opts.Id}] Завершён");
        // Дублёр тоже пробует очистить папку — на случай если он последний
        CleanWorkers(opts.CleanWorkersOnExit);
        return 0;
    }

    static void CpuBurn(CancellationToken token)
    {
        // Более тяжёлые операции: sqrt, sin, cos, pow, exp, log
        // FPU нагружается сильнее, чем от простого сложения.
        double x = 0;
        long i = 0;
        while (!token.IsCancellationRequested)
        {
            double a = i % 1000 + 1;
            x += Math.Sqrt(a)
               * Math.Sin(i)
               + Math.Cos(i * 0.5)
               * Math.Pow(a, 1.3)
               + Math.Exp(a * 0.001)
               * Math.Log(a + 1);
            i++;
        }
        if (x == double.MaxValue) Console.WriteLine(x);
    }

    // ============================================================
    // НАГРУЗКА НА ДИСК
    // ============================================================
    //
    // Периодически пишет и читает временный файл в рабочей папке.
    // Файл перезаписывается по кругу. Даёт нагрузку на I/O.
    //
    // sizeMb — размер файла в МБ
    // stop   — токен отмены

    static void DiskBurn(int sizeMb, CancellationToken stop, int workerId)
    {
        try
        {
            string dir = Path.Combine(WorkDir, "diskiotmp");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"io_{workerId}.tmp");

            int bytes = Math.Max(1, sizeMb) * 1024 * 1024;
            byte[] data = new byte[bytes];

            // Заполняем случайными данными — чтобы не сжалось на лету (NTFS не сжимает, но всё же)
            var rnd = new Random(workerId * 31 + 7);
            rnd.NextBytes(data);

            while (!stop.IsCancellationRequested)
            {
                try
                {
                    // Запись
                    File.WriteAllBytes(path, data);

                    // Чтение — чтобы реально прогнать через диск
                    var read = File.ReadAllBytes(path);
                    // Небольшая проверка, чтобы JIT не выкинул чтение
                    if (read.Length > 0 && read[0] == 0xFF) { /* ничего */ }

                    // Опционально: удаление, чтобы не копить мусор
                    // File.Delete(path);
                }
                catch { }

                Thread.Sleep(50);
            }

            // Уборка в конце
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
        catch (Exception ex)
        {
            LogProblem($"[disk] Ошибка: {ex.Message}");
        }
    }

    // ============================================================
    // АВТОЗАПУСК
    // ============================================================
    //
    // Пишет в HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
    // Имя записи привязано к префиксу: {Prefix}AutoRun.

    static class AutoStartManager
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void Apply(bool enable, string prefix)
        {
            try
            {
                string appName = prefix + "AutoRun";

                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null)
                {
                    LogProblem("[autostart] Не удалось открыть ключ Run.");
                    return;
                }

                if (enable)
                {
                    string exe = Environment.ProcessPath ?? "";
                    if (string.IsNullOrEmpty(exe)) return;

                    string value = "\"" + exe + "\"";
                    string? current = key.GetValue(appName) as string;

                    if (!string.Equals(current, value, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(appName, value);
                        Log($"[autostart] Включён ({appName}): {value}");
                    }
                }
                else
                {
                    if (key.GetValue(appName) != null)
                    {
                        key.DeleteValue(appName, throwOnMissingValue: false);
                        Log($"[autostart] Отключён ({appName}).");
                    }
                }
            }
            catch (Exception ex)
            {
                LogProblem($"[autostart] Ошибка: {ex.Message}");
            }
        }
    }

    // ============================================================
    // Параметры
    // ============================================================

    class Options
    {
        // ---- Значения по умолчанию (мягкие, из кода) ----
        public int Workers = 4;
        public int MbPerWorker = 512;
        public int ThreadsPerWorker = 2;
        public int Seconds = 60;
        public int SpawnDelayMs = 20;
        public int WaveDelayMs = 300;
        public int Waves = 3;
        public int Fanout = 2;
        public int ConfigReloadMs = 2000;
        public int DiskBurnMb = 0;   // МБ для диска-нагрузки (0 = выключено)
        public int MaxRunSeconds = 0;   // 0 = без ограничения времени работы

        public bool EnableWorkers = true;
        public bool EnableReplication = false;
        public bool AutoStart = false;
        public bool StopParentWhenDisabled = false;   // завершать родителя при EnableWorkers=false
        public bool NotifyOnStart = true;
        public bool CleanWorkersOnExit = true;   // удалять папку workers при выходе
        public bool NestedWorkers = false;   // вложенные workers\ (старое поведение)
        public int NotifyTimeoutSec = 1;   // сколько секунд висит окно

        // Должен совпадать с Program.DefaultPrefix
        public string Prefix = "Z";

        // ---- Циклический режим (burst) ----
        public bool BurstMode = false;          // включить циклы атака/отдых
        public int AttackSec = 120;             // длительность атаки, сек
        public int RestSec = 180;               // длительность отдыха, сек
        public int RampUpSec = 15;              // плавное нарастание, сек
        public int RampDownSec = 15;            // плавный спад, сек
        public int BurstStartDelaySec = 0;      // задержка перед первым циклом, сек

        // Источник картинок:
        //   "embedded" — только встроенные в exe
        //   "folder"   — только из папки images\
        //   "both"     — и встроенные, и из папки
        //   "none"     — не использовать
        public string PicturesSource = "embedded";

        // Папка с картинками рядом с exe
        public string ImagesDir = "images";

        // ---- Служебное ----
        public bool Worker = false;
        public int Id = 0;
        public int Wave = 0;

        public void ApplyGlobalFlags(bool forceNoReplication, bool forceNoWorkers)
        {
            if (forceNoWorkers) EnableWorkers = false;
            if (forceNoReplication) EnableReplication = false;
        }

        public static string GetConfigPath()
        {
            return Path.Combine(WorkDir, "config.txt");
        }

        // Загрузка: стартуем с дефолтов, потом перекрываем конфигом.
        public static Options Load()
        {
            var o = new Options();
            string path = GetConfigPath();

            if (!File.Exists(path))
            {
                Log($"[config] Файл не найден: {path}");
                Log("[config] Использую параметры по умолчанию из кода.");
                return o;
            }

            Log($"[config] Загружаю: {path}");

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith(";")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "workers":
                        if (int.TryParse(val, out var w)) o.Workers = Math.Max(1, w);
                        break;

                    case "mbperworker":
                        if (int.TryParse(val, out var m)) o.MbPerWorker = Math.Max(1, m);
                        break;

                    case "threadsperworker":
                        if (int.TryParse(val, out var t)) o.ThreadsPerWorker = Math.Max(1, t);
                        break;

                    case "seconds":
                        if (int.TryParse(val, out var s)) o.Seconds = Math.Max(1, s);
                        break;

                    case "spawndelayms":
                        if (int.TryParse(val, out var d)) o.SpawnDelayMs = Math.Max(0, d);
                        break;

                    case "wavedelayms":
                        if (int.TryParse(val, out var wd)) o.WaveDelayMs = Math.Max(0, wd);
                        break;

                    case "waves":
                        if (int.TryParse(val, out var wv)) o.Waves = Math.Max(1, wv);
                        break;

                    case "fanout":
                        if (int.TryParse(val, out var fo)) o.Fanout = Math.Max(1, fo);
                        break;

                    case "configreloadms":
                        if (int.TryParse(val, out var cr)) o.ConfigReloadMs = Math.Max(500, cr);
                        break;

                    case "diskburnmb":
                        if (int.TryParse(val, out var dm)) o.DiskBurnMb = Math.Max(0, dm);
                        break;

                    case "enableworkers":
                        o.EnableWorkers = ParseBool(val);
                        break;

                    case "enablereplication":
                        o.EnableReplication = ParseBool(val);
                        break;

                    case "autostart":
                        o.AutoStart = ParseBool(val);
                        break;

                    case "notifyonstart":
                        o.NotifyOnStart = ParseBool(val);
                        break;

                    case "cleanworkersonexit":
                        o.CleanWorkersOnExit = ParseBool(val);
                        break;

                    case "prefix":
                        if (!string.IsNullOrWhiteSpace(val))
                            o.Prefix = val.Trim();
                        break;

                    case "burstmode":
                        o.BurstMode = ParseBool(val);
                        break;

                    case "attacksec":
                        if (int.TryParse(val, out var atk)) o.AttackSec = Math.Max(1, atk);
                        break;

                    case "restsec":
                        if (int.TryParse(val, out var rst)) o.RestSec = Math.Max(1, rst);
                        break;

                    case "rampupsec":
                        if (int.TryParse(val, out var ru)) o.RampUpSec = Math.Max(0, ru);
                        break;

                    case "rampdownsec":
                        if (int.TryParse(val, out var rd)) o.RampDownSec = Math.Max(0, rd);
                        break;

                    case "burststartdelaysec":
                        if (int.TryParse(val, out var bsd)) o.BurstStartDelaySec = Math.Max(0, bsd);
                        break;

                    case "nestedworkers":
                        o.NestedWorkers = ParseBool(val);
                        break;

                    case "notifytimeoutsec":
                        if (int.TryParse(val, out var nt)) o.NotifyTimeoutSec = Math.Max(0, nt);
                        break;

                    case "stopparentwhendisabled":
                        o.StopParentWhenDisabled = ParseBool(val);
                        break;

                    case "picturessource":
                        o.PicturesSource = ParsePicturesSource(val);
                        break;

                    case "imagesdir":
                        if (!string.IsNullOrWhiteSpace(val))
                            o.ImagesDir = val.Trim();
                        break;

                    case "maxrunseconds":
                        if (int.TryParse(val, out var mrs)) o.MaxRunSeconds = Math.Max(0, mrs);
                        break;

                    default:
                        LogProblem($"[config] Неизвестный ключ: {key}");
                        break;
                }
            }

            return o;
        }

        static bool ParseBool(string val)
        {
            var v = val.Trim().ToLowerInvariant();
            return v == "true" || v == "yes" || v == "1" || v == "on" || v == "вкл";
        }

        // Разбор аргументов командной строки
        public void ApplyCommandLine(string[] a)
        {
            for (int i = 0; i < a.Length; i++)
            {
                switch (a[i])
                {
                    case "--worker": Worker = true; break;
                    case "--workers": Workers = int.Parse(a[++i]); break;
                    case "--mb": MbPerWorker = int.Parse(a[++i]); break;
                    case "--threads": ThreadsPerWorker = int.Parse(a[++i]); break;
                    case "--seconds": Seconds = int.Parse(a[++i]); break;
                    case "--id": Id = int.Parse(a[++i]); break;
                    case "--wave": Wave = int.Parse(a[++i]); break;
                    case "--waves": Waves = int.Parse(a[++i]); break;
                    case "--fanout": Fanout = int.Parse(a[++i]); break;
                    case "--replication":
                        EnableReplication = a[++i] == "1";
                        break;
                    case "--prefix":
                        Prefix = a[++i];
                        break;
                    case "--workdir":
                        // Значение используется в GetWorkDir через Environment.GetCommandLineArgs().
                        // Просто пропускаем его при разборе.
                        i++;
                        break;
                }
            }
        }

        // Распознаёт значение ключа PicturesSource.
        // Возвращает: "embedded", "folder", "both" или "none".
        static string ParsePicturesSource(string val)
        {
            var v = val.Trim().ToLowerInvariant();

            // true / yes / 1 / on / вкл → embedded
            if (v == "true" || v == "yes" || v == "1" || v == "on" || v == "вкл")
                return "embedded";

            // false / no / 0 / off / выкл → folder
            if (v == "false" || v == "no" || v == "0" || v == "off" || v == "выкл")
                return "folder";

            // maybetrue / both / maybe → both
            if (v == "maybetrue" || v == "both" || v == "maybe" ||
                v == "оба" || v == "все")
                return "both";

            // none / no_pictures → none
            if (v == "none" || v == "нет")
                return "none";

            // Не распознали — embedded по умолчанию
            return "embedded";
        }

        // Записать шаблон config.txt. Только по --write-config.
        public static void WriteExampleConfig()
        {
            string path = GetConfigPath();

            if (File.Exists(path))
            {
                Log($"[config] Уже существует, не перезаписываю: {path}");
                return;
            }

            string template =
                "# ============================================================\n" +
                "# StressTest config. Формат: Ключ=Значение. Комментарии: # или ;\n" +
                "# Файл НЕ создаётся автоматически. Только по флагу --write-config.\n" +
                "# Ключи, которых нет в этом файле, берутся из кода программы.\n" +
                "# ============================================================\n" +
                "\n" +
                "# ---- Приставка имён процессов ----\n" +
                "# Из неё собираются имена: Prefix+Host, Prefix+Runtime и т.д.\n" +
                "# Тег в командной строке: --tag Prefix7\n" +
                "# Если ключа нет — берётся значение из кода (ZX).\n" +
                "Prefix=Z\n" +
                "\n" +
                "# ---- Пул дублёров ----\n" +
                "# Сколько живых дублёров держать одновременно\n" +
                "Workers=2\n" +
                "\n" +
                "# Пауза между запусками соседних дублёров, мс\n" +
                "SpawnDelayMs=20\n" +
                "\n" +
                "# Пауза между волнами репликации, мс\n" +
                "WaveDelayMs=300\n" +
                "\n" +
                "# ---- Нагрузка на железо ----\n" +
                "# Сколько МБ RAM выделяет и пиннит один дублёр\n" +
                "MbPerWorker=128\n" +
                "\n" +
                "# Сколько CPU-потоков крутит один дублёр\n" +
                "ThreadsPerWorker=1\n" +
                "\n" +
                "# Сколько секунд живёт один дублёр до самозавершения\n" +
                "Seconds=15\n" +
                "\n" +
                "# ---- Репликация ----\n" +
                "# Глубина размножения вглубь (поколения)\n" +
                "Waves=1\n" +
                "\n" +
                "# Сколько детей создаёт один дублёр за одну волну\n" +
                "Fanout=1\n" +
                "\n" +
                "# ---- Управление дублёрами ----\n" +
                "# Создавать ли дублёров вообще (true/false, 1/0, yes/no)\n" +
                "EnableWorkers=true\n" +
                "\n" +
                "# Разрешить ли репликацию (дети порождают детей)\n" +
                "EnableReplication=false\n" +
                "\n" +
                "# ---- Поведение родителя при EnableWorkers=false ----\n" +
                "# true  — родитель завершается. Сработает CleanWorkersOnExit.\n" +
                "# false — родитель уходит в вечный сон, нагрузка останавливается,\n" +
                "#         но процесс остаётся. По умолчанию false.\n" +
                "StopParentWhenDisabled=false\n" +
                "\n" +
                "# ---- Автозапуск ----\n" +
                "# Запускать ли программу при входе в систему\n" +
                "# Пишется в HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\n" +
                "# Имя записи: {Prefix}AutoRun\n" +
                "AutoStart=false\n" +
                "\n" +
                "# ---- Уведомление о запуске ----\n" +
                "# Показывать окно cmd при старте (true/false, 1/0, yes/no)\n" +
                "NotifyOnStart=true\n" +
                "\n" +
                "# ---- Очистка папки workers ----\n" +
                "# Удалять папку workers со всеми копиями при выходе\n" +
                "# true  — папка удаляется после завершения всех процессов\n" +
                "# false — папка остаётся, копии переиспользуются при следующем запуске\n" +
                "CleanWorkersOnExit=true\n" +
                "\n" +
                "# ---- Вложенные папки workers ----\n" +
                "# false — одна папка workers рядом с exe (по умолчанию)\n" +
                "# true  — копии создают свои workers внутри workers (много папок)\n" +
                "NestedWorkers=false\n" +
                "\n" +
                "# ---- Циклический режим (burst) ----\n" +
                "# Включить циклы: нарастание -> атака -> спад -> отдых -> снова\n" +
                "# Если false — работает обычный режим, без циклов.\n" +
                "BurstMode=false\n" +
                "\n" +
                "# Длительность атаки (полная нагрузка), сек\n" +
                "AttackSec=120\n" +
                "\n" +
                "# Длительность отдыха (нет нагрузки), сек\n" +
                "RestSec=180\n" +
                "\n" +
                "# Плавное нарастание: за сколько секунд выйти с 0 до Workers\n" +
                "# 0 — мгновенный старт, 60+ — очень плавно.\n" +
                "RampUpSec=15\n" +
                "\n" +
                "# Плавный спад: за сколько секунд сойти с Workers до 0\n" +
                "# 0 — резкое прекращение, 60+ — долгий хвост.\n" +
                "RampDownSec=15\n" +
                "\n" +
                "# Задержка перед первым циклом, сек\n" +
                "BurstStartDelaySec=0\n" +
                "\n" +
                "# ---- Таймаут окна уведомления ----\n" +
                "# Сколько секунд окно висит на экране (0 = закрыть сразу)\n" +
                "NotifyTimeoutSec=1\n" +
                "\n" +
                "# ---- Заполнение памяти картинками ----\n" +
                "# Откуда брать картинки для заполнения RAM:\n" +
                "#   true      — только встроенные в exe картинки\n" +
                "#   false     — только картинки из папки images\\ рядом с exe.\n" +
                "#                Если папки нет — используются встроенные.\n" +
                "#   maybetrue — и встроенные, и из папки (если папка есть)\n" +
                "#   none      — не использовать картинки, заполнять пустым массивом\n" +
                "PicturesSource=true\n" +
                "\n" +
                "# Папка с картинками. Лежит рядом с exe (в рабочей папке).\n" +
                "# Используется, если PicturesSource=false или maybetrue.\n" +
                "ImagesDir=images\n" +
                "\n" +
                "# ---- Ограничение времени работы ----\n" +
                "# Через сколько СЕКУНД родитель завершается сам.\n" +
                "# 0 — без ограничения (по умолчанию).\n" +
                "# >0 — через это время родитель выходит, срабатывает CleanWorkersOnExit.\n" +
                "# Примеры: 30 = 30 сек, 300 = 5 мин, 3600 = 1 час, 86400 = 24 часа.\n" +
                "MaxRunSeconds=0\n" +
                "\n" +
                "# ---- Служебное ----\n" +
                "# Как часто родитель перечитывает этот файл, мс (мин. 500)\n" +
                "ConfigReloadMs=2000\n";

            try
            {
                File.WriteAllText(path, template);
                Log($"[config] Шаблон записан: {path}");
            }
            catch (Exception ex)
            {
                LogProblem($"[config] Не удалось записать шаблон: {ex.Message}");
            }
        }
    }
}