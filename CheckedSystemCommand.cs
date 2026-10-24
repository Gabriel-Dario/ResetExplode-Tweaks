using System.Diagnostics;
using System.IO;

namespace ResetExplode;

public static class CheckedSystemCommand
{
    public static string Run(string executable,string arguments,int timeoutMs=5000)
    {
        var info=new ProcessStartInfo(executable,arguments) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using var process=Process.Start(info)??throw new IOException("Não foi possível iniciar o comando.");
        var stdout=process.StandardOutput.ReadToEndAsync();
        var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(timeoutMs)) {
            try {process.Kill(true);process.WaitForExit(2000);} catch { }
            throw new TimeoutException("O comando excedeu o tempo permitido.");
        }
        if(process.ExitCode!=0) throw new IOException($"O comando falhou ({process.ExitCode}): {stderr.GetAwaiter().GetResult().Trim()}");
        return stdout.GetAwaiter().GetResult();
    }
}
