using System.Diagnostics;

namespace UnityFontReplacer.CLI;

/// <summary>
/// 콘솔 하단 고정 상태바.
/// 왼쪽: Mem, workers (안정적)
/// 오른쪽: progress, 현재 파일명 (자주 변경)
/// </summary>
public class StatusBar : IDisposable
{
    private readonly int _maxWorkers;
    private readonly Process _process;
    private int _current;
    private int _total;
    private string _currentFile = "";
    private bool _disposed;
    private int _statusLineRow = -1;
    private readonly object _lock = new();

    public StatusBar(int maxWorkers = 1)
    {
        _maxWorkers = maxWorkers;
        _process = Process.GetCurrentProcess();
    }

    public void SetTotal(int total)
    {
        _total = total;
    }

    public void Update(int current, string fileName)
    {
        lock (_lock)
        {
            _current = current;
            _currentFile = fileName;
            Render();
        }
    }

    public void Increment(string fileName)
    {
        lock (_lock)
        {
            _current++;
            _currentFile = fileName;
            Render();
        }
    }

    /// <summary>
    /// 상태바 위에 로그 라인을 출력한다. 상태바는 항상 마지막 줄을 유지.
    /// </summary>
    public void Log(string message)
    {
        lock (_lock)
        {
            ClearStatusLine();
            Console.WriteLine(message);
            Render();
        }
    }

    private void Render()
    {
        try
        {
            _process.Refresh();
            var memMB = _process.WorkingSet64 / (1024.0 * 1024.0);
            string memStr = memMB >= 1024
                ? $"{memMB / 1024.0:F1}GB"
                : $"{memMB:F0}MB";

            float pct = _total > 0 ? _current * 100f / _total : 0f;
            string left = $" Mem: {memStr} | workers: {_maxWorkers}";

            int consoleWidth;
            try { consoleWidth = Console.WindowWidth; }
            catch { consoleWidth = 120; }

            // 파일명 (오른쪽 끝)
            int maxFileLen = Math.Max(10, consoleWidth / 3);
            string fileName = _currentFile.Length > maxFileLen
                ? ".." + _currentFile[^(maxFileLen - 2)..]
                : _currentFile;
            string right = $" {_current}/{_total} ({pct:F0}%) | {fileName} ";

            // 진행률 바 (중간)
            int barSpace = consoleWidth - left.Length - right.Length;
            string bar = "";
            if (barSpace > 4)
            {
                int filled = (int)(pct / 100f * (barSpace - 2));
                filled = Math.Clamp(filled, 0, barSpace - 2);
                bar = "[" + new string('#', filled) + new string('-', barSpace - 2 - filled) + "]";
            }

            string line = left + bar + right;
            if (line.Length > consoleWidth)
                line = line[..consoleWidth];
            else if (line.Length < consoleWidth)
                line += new string(' ', consoleWidth - line.Length);

            // 상태바 줄로 이동해서 덮어쓰기
            int row;
            try { row = Console.CursorTop; }
            catch { return; }

            Console.SetCursorPosition(0, row);
            Console.Write("\x1b[7m"); // 반전 색상
            Console.Write(line);
            Console.Write("\x1b[0m"); // 리셋

            _statusLineRow = row;
        }
        catch
        {
            // 콘솔 접근 불가 시 무시
        }
    }

    private void ClearStatusLine()
    {
        try
        {
            if (_statusLineRow >= 0)
            {
                Console.SetCursorPosition(0, _statusLineRow);
                int width;
                try { width = Console.WindowWidth; }
                catch { width = 120; }
                Console.Write(new string(' ', width));
                Console.SetCursorPosition(0, _statusLineRow);
            }
        }
        catch { }
    }

    public void Clear()
    {
        lock (_lock)
        {
            ClearStatusLine();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
