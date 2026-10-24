using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Native Windows adapter. No scripts, shell interpolation or downloaded commands.</summary>
public sealed class WindowsTweakStore : ITweakStore
{
    public bool Supports(TweakSetting s)
    {
        try {
            if (s.Kind == SettingKind.RegistryDword) {
                if (s.Address.Contains(@"Policies\Microsoft\Edge")) {
                    using var edge = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");
                    using var perUser = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");
                    if (edge?.GetValue("") is not string && perUser?.GetValue("") is not string) return false;
                }
            }
            return Read(s, Scope(s)) != null || s.Kind == SettingKind.RegistryDword;
        } catch { return false; }
    }
    public string Scope(TweakSetting s)
    {
        if (s.Kind != SettingKind.PowerAc) return "";
        uint result = PowerGetActiveScheme(IntPtr.Zero, out IntPtr pointer);
        Error(result);
        try { return Marshal.PtrToStructure<Guid>(pointer).ToString(); }
        finally { LocalFree(pointer); }
    }
    public string? Read(TweakSetting s, string scope)
    {
        if (s.Kind == SettingKind.RegistryDword) {
            using var key = Registry.CurrentUser.OpenSubKey(s.Address);
            var value = key?.GetValue(s.Name);
            if (value == null) return null;
            if (key!.GetValueKind(s.Name) != RegistryValueKind.DWord || value is not int number)
                throw new IOException("Tipo do registro inesperado; preserve o valor original: " + s.Name);
            return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (s.Kind == SettingKind.PowerAc) {
            var scheme = Guid.Parse(scope); var group = Guid.Parse(s.Address); var setting = Guid.Parse(s.Name);
            Error(PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, out uint value));
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        uint flag = 0;
        if (!GetParameter(s.Get, 0, ref flag, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return flag.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public void Write(TweakSetting s, string scope, string? value)
    {
        if (s.Kind == SettingKind.RegistryDword) {
            using var key = Registry.CurrentUser.CreateSubKey(s.Address);
            if (value == null) key.DeleteValue(s.Name, false);
            else key.SetValue(s.Name, int.Parse(value, System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.DWord);
            return;
        }
        uint number = uint.Parse(value ?? throw new IOException("Backup nativo inválido."), System.Globalization.CultureInfo.InvariantCulture);
        if (s.Kind == SettingKind.PowerAc) {
            var scheme = Guid.Parse(scope); var group = Guid.Parse(s.Address); var setting = Guid.Parse(s.Name);
            Error(PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, number));
            // Refresh only the scheme already active. Restoring another scheme must not activate it.
            if (Scope(s) == scope) Error(PowerSetActiveScheme(IntPtr.Zero, ref scheme));
            return;
        }
        if (!SetParameter(s.Set, s.UiParam ? number : 0, s.UiParam ? IntPtr.Zero : new IntPtr(number), 3))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private static void Error(uint status) { if (status != 0) throw new Win32Exception((int)status); }
    [DllImport("user32.dll", EntryPoint="SystemParametersInfoW", SetLastError=true)]
    private static extern bool GetParameter(uint action, uint param, ref uint value, uint flags);
    [DllImport("user32.dll", EntryPoint="SystemParametersInfoW", SetLastError=true)]
    private static extern bool SetParameter(uint action, uint param, IntPtr value, uint flags);
    [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
    [DllImport("powrprof.dll")] private static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, out uint value);
    [DllImport("powrprof.dll")] private static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, uint value);
    [DllImport("powrprof.dll")] private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
