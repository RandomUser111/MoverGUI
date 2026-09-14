using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace MoverGUI;

internal sealed class PscpService
{
    private readonly string _pscpPath;

    public PscpService(string dataDirectory)
    {
        var binDir = Path.Combine(dataDirectory, "bin");
        Directory.CreateDirectory(binDir);
        _pscpPath = Path.Combine(binDir, "pscp.exe");
        EnsurePscpExtracted();
    }

    public async Task<bool> CanReachAsync(string host, int port, int timeoutMs, CancellationToken token)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TransferResult> SendAsync(
        CashRegister cash,
        IReadOnlyList<string> files,
        string remotePath,
        string userName,
        int port,
        IReadOnlyList<string> passwords,
        IProgress<string>? progress,
        CancellationToken token)
    {
        if (!await CanReachAsync(cash.Ip, port, 2500, token))
            return Fail(cash, "Нет связи", $"TCP-порт {port} недоступен");

        var order = BuildPasswordOrder(cash.PreferredPasswordIndex, passwords.Count);
        var hostKeyPrepared = false;

        foreach (var index in order)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report($"{cash.Ip}: проверка пароля {index + 1} из {passwords.Count}");

            var attempt = await RunPscpAsync(cash.Ip, port, userName, remotePath, files, passwords[index], batch: true, acceptHostKey: false, token);

            if (IsUnknownHostKey(attempt.Output))
            {
                progress?.Report($"{cash.Ip}: добавление ключа SSH-хоста в кэш PuTTY");
                var accept = await RunPscpAsync(cash.Ip, port, userName, remotePath, files, passwords[index], batch: false, acceptHostKey: true, token);
                hostKeyPrepared = true;

                if (accept.ExitCode == 0)
                    return Ok(cash, index, accept.Output);

                if (!IsAuthenticationError(accept.Output))
                    return Fail(cash, "Ошибка копирования", Compact(accept.Output));

                continue;
            }

            if (attempt.ExitCode == 0)
                return Ok(cash, index, attempt.Output);

            if (IsAuthenticationError(attempt.Output))
                continue;

            if (IsUnknownHostKey(attempt.Output) && !hostKeyPrepared)
                continue;

            return Fail(cash, "Ошибка копирования", Compact(attempt.Output));
        }

        return Fail(cash, "Пароль не подошел", "Ни один пароль из списка не прошел авторизацию");
    }

    private async Task<ProcessResult> RunPscpAsync(
        string ip,
        int port,
        string user,
        string remotePath,
        IReadOnlyList<string> files,
        string password,
        bool batch,
        bool acceptHostKey,
        CancellationToken token)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pscpPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = acceptHostKey,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        if (batch)
            psi.ArgumentList.Add("-batch");
        psi.ArgumentList.Add("-pw");
        psi.ArgumentList.Add(password);
        psi.ArgumentList.Add("-scp");
        psi.ArgumentList.Add("-P");
        psi.ArgumentList.Add(port.ToString());

        foreach (var file in files)
            psi.ArgumentList.Add(file);

        psi.ArgumentList.Add($"{user}@{ip}:{remotePath}");

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        if (!process.Start())
            return new ProcessResult(-1, "Не удалось запустить pscp.exe");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (acceptHostKey)
        {
            await process.StandardInput.WriteLineAsync("y");
            process.StandardInput.Close();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            if (token.IsCancellationRequested)
                throw;
            return new ProcessResult(-2, "Таймаут выполнения pscp.exe");
        }

        var output = (stdout.ToString() + Environment.NewLine + stderr).Trim();
        return new ProcessResult(process.ExitCode, output);
    }

    private void EnsurePscpExtracted()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var input = asm.GetManifestResourceStream("MoverGUI.pscp.exe")
                          ?? throw new InvalidOperationException("В приложение не встроен pscp.exe");

        var shouldWrite = !File.Exists(_pscpPath) || new FileInfo(_pscpPath).Length != input.Length;
        if (!shouldWrite)
            return;

        using var output = File.Create(_pscpPath);
        input.CopyTo(output);
    }

    private static List<int> BuildPasswordOrder(int? preferred, int count)
    {
        var result = new List<int>(count);
        if (preferred is >= 0 && preferred < count)
            result.Add(preferred.Value);
        for (var i = 0; i < count; i++)
            if (!result.Contains(i))
                result.Add(i);
        return result;
    }

    private static bool IsAuthenticationError(string output)
    {
        var s = output.ToLowerInvariant();
        return s.Contains("access denied") ||
               s.Contains("authentication failed") ||
               s.Contains("password:") ||
               s.Contains("no supported authentication methods") ||
               s.Contains("unable to authenticate");
    }

    private static bool IsUnknownHostKey(string output)
    {
        var s = output.ToLowerInvariant();
        return s.Contains("host key is not cached") ||
               s.Contains("server's host key is not cached") ||
               s.Contains("cannot confirm a host key in batch mode") ||
               s.Contains("store key in cache");
    }

    private static string Compact(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Неизвестная ошибка pscp.exe";
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" | ", lines.TakeLast(3));
    }

    private static TransferResult Ok(CashRegister cash, int passwordIndex, string details) => new()
    {
        Cash = cash,
        Success = true,
        Status = "Готово",
        Details = Compact(details),
        WorkingPasswordIndex = passwordIndex
    };

    private static TransferResult Fail(CashRegister cash, string status, string details) => new()
    {
        Cash = cash,
        Success = false,
        Status = status,
        Details = details
    };

    private sealed record ProcessResult(int ExitCode, string Output);
}
