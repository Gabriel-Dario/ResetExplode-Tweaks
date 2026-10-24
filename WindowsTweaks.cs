using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>
/// Ajuste do Windows para jogos: lê o estado atual, aplica e guarda o valor original (em AppSettings.TweakBackup)
/// na primeira vez que é alterado, para "Restaurar originais" desfazer tudo.
/// Só ajustes documentados e reversíveis; nada que reduza a segurança (Defender, VBS/HVCI, UAC, Windows Update).
/// </summary>
public sealed class Tweak : INotifyPropertyChanged
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Effect { get; init; } = "";
    public string Category { get; init; } = "Jogos e entrada";
    public string Source { get; init; } = "https://support.microsoft.com/windows";
    public string Profiles { get; init; } = "Opcional";
    public bool Advanced { get; init; }
    public bool NeedsRestart { get; init; }
    public Func<bool> Supported { get; init; } = () => true;
    internal Func<bool?> Read { get; init; } = () => null;
    internal Action<bool, AppSettings> Write { get; init; } = (_, _) => { };
    internal Action<AppSettings> Restore { get; init; } = _ => { };

    private bool _isOn;
    public bool IsOn { get => _isOn; set { _isOn = value; Changed(nameof(IsOn)); } }
    private string _status = "";
    public string Status { get => _status; set { _status = value; Changed(nameof(Status)); } }
    public string Badge => NeedsRestart ? "REQUER REINICIAR" : "";

    public void Refresh() => IsOn = Read() ?? false;

    private void Changed(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class WindowsTweaks
{
    public static readonly object Gate = new();
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public static List<Tweak> All() => Legacy().Concat(TweakCatalog.Create(new WindowsTweakStore(), Environment.OSVersion.Version.Build)).ToList();

    private static List<Tweak> Legacy() => new()
    {
        new Tweak
        {
            Id = "power", Title = "Plano de energia: Alto desempenho",
            Category = "Energia", Advanced = true,
            Source = "https://learn.microsoft.com/windows-hardware/design/device-experiences/powercfg-command-line-options",
            Description = "Ativa o plano Alto desempenho, quando disponível. Compare com o plano do fabricante antes de manter.",
            Effect = "Pode elevar consumo, temperatura e ruído; benefício depende da carga e do hardware.",
            Read = () => ActiveScheme()?.Equals(HighPerformance, StringComparison.OrdinalIgnoreCase),
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("power"))
                {
                    s.TweakBackup["power"] = ActiveScheme() ?? throw new InvalidOperationException("Não foi possível identificar o plano atual.");
                    try { s.Save(); } catch { s.TweakBackup.Remove("power"); throw; }
                }
                if (on)
                {
                    // Alguns PCs vêm sem o plano listado: duplicar o modelo padrão o recria com o mesmo GUID.
                    if (!PowerCfg("/list").Contains(HighPerformance, StringComparison.OrdinalIgnoreCase))
                        PowerCfg($"-duplicatescheme {HighPerformance} {HighPerformance}");
                    PowerCfg($"/setactive {HighPerformance}");
                }
                else if (s.TweakBackup["power"] is string prev && !prev.Equals(HighPerformance, StringComparison.OrdinalIgnoreCase))
                    PowerCfg($"/setactive {prev}");
                else PowerCfg("/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); // Equilibrado
            },
            Restore = s => {
                if (s.TweakBackup.GetValueOrDefault("power") is not string prev) throw new IOException("Backup do plano de energia indisponível.");
                PowerCfg($"/setactive {prev}");
                if (ActiveScheme() != prev) throw new IOException("O plano original não foi restaurado.");
            },
        },
        RegTweak("gamemode", "Modo de Jogo",
            "Dá prioridade ao jogo e evita que o Windows Update instale ou reinicie durante a partida.",
            "Recomendado ligado (é o padrão do Windows 11).",
            Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", on: 1, off: 0, absentMeansOn: true),
        new Tweak
        {
            Id = "gamedvr", Title = "Desativar gravação em segundo plano (Game DVR)",
            Profiles = "Jogos", Source = "https://support.xbox.com/help/friends-social-activity/share-socialize/record-game-clips-game-bar-windows-10",
            Description = "Desliga a captura contínua do Xbox Game Bar, que grava os últimos minutos de jogo o tempo todo.",
            Effect = "Pode reduzir a carga da captura quando ela está em uso. Você perde a captura do Game Bar; ganho depende do cenário.",
            Read = () => (ReadDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled") ?? 1) == 0 &&
                         (ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") ?? 1) == 0,
            Write = (on, s) =>
            {
                SetDword(s, "gamedvr.a", Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", on ? 0 : 1);
                SetDword(s, "gamedvr.b", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", on ? 0 : 1);
            },
            Restore = s =>
            {
                RestoreDword(s, "gamedvr.a", Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled");
                RestoreDword(s, "gamedvr.b", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
            },
        },
        RegTweak("hags", "Agendamento de GPU acelerado por hardware",
            "A própria placa de vídeo gerencia sua fila de trabalho, em vez da CPU.",
            "Necessário para o Frame Generation do DLSS 3/4. Pode reduzir latência. Requer GPU e driver compatíveis.",
            Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", on: 2, off: 1,
            absentMeansOn: false, needsRestart: true),
        new Tweak
        {
            Id = "windowed", Title = "Otimizações para jogos em janela",
            Profiles = "Jogos", Source = "https://support.microsoft.com/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11",
            Supported = () => Environment.OSVersion.Version.Build >= 22000,
            Description = "Faz jogos DirectX 10/11 em janela ou \"sem bordas\" usarem o modelo de apresentação moderno (flip).",
            Effect = "Menor latência e suporte a VRR/G-Sync em janela. Recurso do Windows 11.",
            Read = () => DxSettings().GetValueOrDefault("SwapEffectUpgradeEnable") == "1",
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("windowed"))
                {
                    s.TweakBackup["windowed"] = ReadString(Registry.CurrentUser,DxPath,"DirectXUserGlobalSettings") ?? "<ausente>";
                    try { s.Save(); } catch { s.TweakBackup.Remove("windowed"); throw; }
                }
                var d = DxSettings();
                d["SwapEffectUpgradeEnable"] = on ? "1" : "0";
                using var k = Registry.CurrentUser.CreateSubKey(DxPath);
                k.SetValue("DirectXUserGlobalSettings", string.Concat(d.Select(kv => $"{kv.Key}={kv.Value};")));
            },
            Restore = s =>
            {
                if (s.TweakBackup.GetValueOrDefault("windowed") is not string prev) return;
                using var k = Registry.CurrentUser.CreateSubKey(DxPath);
                if (prev == "<ausente>") k.DeleteValue("DirectXUserGlobalSettings", false);
                else k.SetValue("DirectXUserGlobalSettings", prev);
                string actual = k.GetValue("DirectXUserGlobalSettings") as string ?? "<ausente>";
                if (actual != prev) throw new IOException("A restauração das preferências gráficas não foi confirmada.");
            },
        },
        new Tweak
        {
            Id = "mouse", Title = "Desligar aceleração do mouse",
            Source = "https://support.microsoft.com/windows/hardware/input-devices/change-mouse-settings",
            Description = "Desmarca \"Aprimorar precisão do ponteiro\": o cursor anda sempre a mesma distância para o mesmo movimento.",
            Effect = "Não muda FPS, mas deixa a mira consistente em jogos de tiro.",
            Read = () => ReadString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed") == "0",
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("mouse.effective"))
                {
                    var effective = new int[3];
                    if (!SystemParametersInfo(0x0003, 0, effective, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    s.TweakBackup["mouse.effective"] = string.Join(",", effective);
                }
                foreach (var n in new[] { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" })
                    if (!s.TweakBackup.ContainsKey($"mouse.{n}")) s.TweakBackup[$"mouse.{n}"] = ReadString(Registry.CurrentUser, @"Control Panel\Mouse", n);
                s.Save();
                var v = on ? new[] { "0", "0", "0" } : new[] { "1", "6", "10" }; // padrão do Windows
                ApplyMouse(v);
            },
            Restore = s =>
            {
                if (!s.TweakBackup.ContainsKey("mouse.MouseSpeed")) return;
                if (s.TweakBackup.GetValueOrDefault("mouse.effective") is string effective)
                {
                    var values = effective.Split(',').Select(int.Parse).ToArray();
                    if (!SystemParametersInfo(0x0004, 0, values, 0x01 | 0x02)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                else ApplyMouse(new[] { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" }
                    .Select(n => s.TweakBackup.GetValueOrDefault($"mouse.{n}") ?? "0").ToArray());
                using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse");
                foreach (var name in new[] { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" })
                {
                    var previous = s.TweakBackup.GetValueOrDefault($"mouse.{name}");
                    if (previous is null) key.DeleteValue(name, false); else key.SetValue(name, previous);
                    if (key.GetValue(name)?.ToString() != previous) throw new IOException("A restauração do mouse não foi confirmada.");
                }
            },
        },
        UiEffect("animations", "Reduzir animações de interface", "Desliga animações na área de conteúdo. Pode melhorar a sensação de resposta; não é um aumento comprovado de FPS.", 0x1042, 0x1043),
        UiEffect("uieffects", "Reduzir efeitos visuais", "Desliga efeitos de menus e controles. Altera a aparência do Windows; avalie a preferência visual e restaure se desejar.", 0x103e, 0x103f),
    };

    private static Tweak UiEffect(string id, string title, string description, uint get, uint set) => new()
    {
        Id = id, Title = title, Description = description, Effect = "Mudança reversível de interface, sem garantia de desempenho em jogos.",
        Category = "Interface", Source = "https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow",
        Read = () => !ReadUiEffect(get),
        Write = (on, s) => {
            TweakValueBackup.Write(s.TweakBackup,id,()=>ReadUiEffect(get)?"1":"0",value=>WriteUiEffect(set,value=="1"),s.Save,on?"0":"1");
        },
        Restore = s => {
            if (s.TweakBackup.GetValueOrDefault(id) is not string value) throw new IOException("Backup de efeitos ausente.");
            WriteUiEffect(set, value == "1");
            if (ReadUiEffect(get) != (value == "1")) throw new IOException("A restauração do efeito não foi confirmada.");
        }
    };

    private static bool ReadUiEffect(uint action)
    {
        int value = 0;
        if (!GetUiEffect(action, 0, ref value, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return value != 0;
    }
    private static void WriteUiEffect(uint action, bool enabled)
    {
        if (!SetUiEffect(action, 0, enabled ? new IntPtr(1) : IntPtr.Zero, 3)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>Desfaz tudo que foi alterado pelo app (valores guardados antes da primeira alteração).</summary>
    public static void RestoreAll(IEnumerable<Tweak> tweaks, AppSettings s)
    {
        foreach (var t in tweaks)
            RestoreOne(t, s);
    }

    public static bool HasBackup(Tweak t, AppSettings s) =>
        s.TweakBackup.Keys.Any(k => k == t.Id || k.StartsWith(t.Id + ".", StringComparison.Ordinal));

    public static bool RestoreOne(Tweak t, AppSettings s)
    {
        if (!HasBackup(t, s)) return true;
        try
        {
            t.Restore(s);
            var keys = s.TweakBackup.Keys.Where(k => k == t.Id || k.StartsWith(t.Id + ".", StringComparison.Ordinal)).ToArray();
            var previous = keys.ToDictionary(k => k, k => s.TweakBackup[k]);
            foreach (var key in keys) s.TweakBackup.Remove(key);
            try { s.Save(); }
            catch { foreach (var (key, value) in previous) s.TweakBackup[key] = value; throw; }
            t.Status = "Restaurado." + (t.NeedsRestart ? " Reinicie para concluir." : "");
            return true;
        }
        catch (Exception ex) { t.Status = "Falha ao restaurar: " + ex.Message + " Backup preservado."; return false; }
    }

    public static void Apply(Tweak t, bool enabled, AppSettings s)
    {
        if (!t.Supported()) throw new NotSupportedException("Este ajuste não é compatível com a versão atual do Windows.");
        t.Write(enabled, s);
        s.Save();
        if (t.Read() != enabled) throw new IOException("A mudança não foi confirmada. O backup foi preservado para restauração.");
    }

    // ------------------------------------------------------------------ registro

    private static Tweak RegTweak(string id, string title, string desc, string effect, RegistryKey hive, string path, string name,
                                  int on, int off, bool absentMeansOn, bool needsRestart = false) => new()
    {
        Id = id, Title = title, Description = desc, Effect = effect, NeedsRestart = needsRestart,
        Profiles = id == "gamemode" ? "Jogos" : "Opcional", Advanced = id == "hags",
        Source = id == "gamemode" ? "https://support.xbox.com/help/games-apps/game-setup-and-play/use-game-mode-gaming-on-pc" : "https://devblogs.microsoft.com/directx/hardware-accelerated-gpu-scheduling/",
        Read = () => (ReadDword(hive, path, name) is int v) ? v == on : absentMeansOn,
        Write = (enable, s) => SetDword(s, id, hive, path, name, enable ? on : off),
        Restore = s => RestoreDword(s, id, hive, path, name),
    };

    private static int? ReadDword(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        object? value = k?.GetValue(name);
        if (value is not null && value is not int) throw new IOException($"O valor {name} não é DWORD; alteração bloqueada para preservar o tipo original.");
        return value is int v ? v : null;
    }

    private static string? ReadString(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        object? value=k?.GetValue(name);
        if(value is not null && value is not string) throw new IOException($"O valor {name} não é texto; alteração bloqueada para preservar o tipo original.");
        return value as string;
    }

    private static void SetDword(AppSettings s, string backupKey, RegistryKey hive, string path, string name, int value)
    {
        TweakValueBackup.Write(s.TweakBackup,backupKey,
            () => ReadDword(hive,path,name)?.ToString() ?? "<ausente>",
            raw => { using var k=hive.CreateSubKey(path);k.SetValue(name,int.Parse(raw!),RegistryValueKind.DWord); },
            s.Save,value.ToString());
    }

    private static void RestoreDword(AppSettings s, string backupKey, RegistryKey hive, string path, string name)
    {
        TweakValueBackup.Restore(s.TweakBackup,backupKey,
            () => ReadDword(hive,path,name)?.ToString() ?? "<ausente>",
            raw => { using var k=hive.CreateSubKey(path); if(raw=="<ausente>") k.DeleteValue(name,false); else k.SetValue(name,int.Parse(raw??throw new IOException("Backup inválido.")),RegistryValueKind.DWord); });
    }

    private const string DxPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    /// <summary>"Chave=Valor;Chave=Valor;" → dicionário (o valor pode ter outras opções do usuário, que preservamos).</summary>
    private static Dictionary<string, string> DxSettings()
    {
        var d = new Dictionary<string, string>();
        string raw = ReadString(Registry.CurrentUser,DxPath,"DirectXUserGlobalSettings") ?? "";
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2) d[kv[0].Trim()] = kv[1].Trim();
        }
        return d;
    }

    private static void ApplyMouse(string[] v)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse"))
        {
            k.SetValue("MouseSpeed", v[0]);
            k.SetValue("MouseThreshold1", v[1]);
            k.SetValue("MouseThreshold2", v[2]);
        }
        // Aplica na hora, sem precisar sair da conta.
        var p = v.Select(int.Parse).ToArray();
        // SPI_SETMOUSE recebe thresholds primeiro e aceleração por último.
        var effective = new[] { p[1], p[2], p[0] };
        if (!SystemParametersInfo(0x0004 /* SPI_SETMOUSE */, 0, effective, 0x01 | 0x02))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    // ------------------------------------------------------------------ plano de energia

    private static string? ActiveScheme()
    {
        var m = System.Text.RegularExpressions.Regex.Match(PowerCfg("/getactivescheme"), @"[0-9a-fA-F\-]{36}");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    private static string PowerCfg(string args)
    {
        return CheckedSystemCommand.Run(System.IO.Path.Combine(Environment.SystemDirectory,"powercfg.exe"),args);
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint param, int[] data, uint winIni);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] private static extern bool GetUiEffect(uint action, uint param, ref int data, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] private static extern bool SetUiEffect(uint action, uint param, IntPtr data, uint flags);
}
