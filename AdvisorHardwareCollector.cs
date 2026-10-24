using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Bounded hardware inventory only. No serials, account data, or system changes.</summary>
public static class AdvisorHardwareCollector
{
    public static AdvisorHardware Collect(string selectedGpu = "")
    {
        var missing = new List<string>();
        var cpu = Read("Win32_Processor", "Name", missing).FirstOrDefault()?.ToString() ?? "Não identificado";
        var board = Read("Win32_BaseBoard", "Manufacturer,Product", missing);
        var bios = Read("Win32_BIOS", "SMBIOSBIOSVersion", missing).FirstOrDefault()?.ToString() ?? "Não identificada";
        var gpu = Read("Win32_VideoController", "Name", missing).Select(v => v?.ToString()).Where(v => !string.IsNullOrWhiteSpace(v)).Cast<string>().ToArray();
        double? ram = Read("Win32_ComputerSystem", "TotalPhysicalMemory", missing).FirstOrDefault() is object total && double.TryParse(total.ToString(), out double bytes) ? bytes / (1024 * 1024 * 1024) : null;
        uint[] speeds = Read("Win32_PhysicalMemory", "ConfiguredClockSpeed", missing).Select(v => uint.TryParse(v?.ToString(),out uint speed) ? speed : 0).Where(v => v > 0).ToArray();
        var kind = Read("Win32_ComputerSystem", "PCSystemType", missing).FirstOrDefault()?.ToString();
        bool? portable = kind switch { "2" => true, "1" or "3" => false, _ => null };
        bool? ac = GetSystemPowerStatus(out var power) ? power.Ac switch { 0 => false, 1 => true, _ => null } : null;
        double? free = null;
        try {
            var disk = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            if (disk.IsReady && disk.TotalSize > 0) free = 100d * disk.AvailableFreeSpace / disk.TotalSize;
        } catch { missing.Add("espaço do disco"); }
        string bootId=Read("Win32_OperatingSystem","LastBootUpTime",missing).FirstOrDefault()?.ToString() ?? "";
        DateTime? bootUtc=null;
        try {if(bootId.Length>0) bootUtc=ManagementDateTimeConverter.ToDateTime(bootId).ToUniversalTime();} catch { }
        return new() {
            Cpu=cpu, Board=board.Length == 0 ? "Não identificada" : string.Join(" ",board.Where(x=>x!=null)), Bios=bios, Gpus=gpu,
            BootId=bootId, BootUtc=bootUtc, SelectedGpu=selectedGpu,
            WindowsBuild=Environment.OSVersion.Version.Build, RamGb=ram, ConfiguredMemoryMhz=speeds.Length>0?speeds.Min():null,
            Portable=portable, OnAc=ac, SystemDriveFreePct=free, RestartPending=IsRestartPending(), Missing=missing.Distinct().ToArray()
        };
    }

    private static object?[] Read(string table, string fields, List<string> missing)
    {
        try {
            using var query = new ManagementObjectSearcher("SELECT " + fields + " FROM " + table);
            query.Options.Timeout = TimeSpan.FromSeconds(5);
            using var rows = query.Get();
            var values = new List<object?>();
            foreach (ManagementObject row in rows) {
                using (row) foreach (var field in fields.Split(',')) values.Add(row[field]);
                if (values.Count >= 32) break;
            }
            return values.ToArray();
        } catch {
            missing.Add(table switch {"Win32_Processor"=>"processador","Win32_BaseBoard"=>"placa-mãe","Win32_BIOS"=>"BIOS","Win32_VideoController"=>"placa de vídeo","Win32_PhysicalMemory"=>"módulos de RAM","Win32_OperatingSystem"=>"inicialização do Windows",_=>"configuração do sistema"});
            return Array.Empty<object?>();
        }
    }

    public static bool IsRestartPending()
    {
        try {
            using var cbs=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            using var update=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            using var session=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            return cbs!=null || update!=null || session?.GetValue("PendingFileRenameOperations")!=null;
        } catch { return false; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PowerStatus { public byte Ac, Flags, Percent, Saver; public uint Life, Full; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
