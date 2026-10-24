using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ResetExplode;

public partial class PerformanceAdvisorView : UserControl
{
    private AppSettings? _settings;
    private List<Tweak> _tweaks = new();
    private AdvisorHardware? _hardware;
    private AdvisorMeasurement? _last;
    private AdvisorProfile _profile = AdvisorProfile.Jogos;
    private Action<string>? _navigate;
    private Func<bool> _busy = () => false;
    private Func<LoadSample>? _sample;
    private Func<FrameStats?>? _frames;
    private Func<string> _selectedGpu = () => "";
    private CancellationTokenSource? _measurement;
    private bool _operation;
    private bool _preview;
    public bool IsBusy => _operation || _measurement != null;

    public PerformanceAdvisorView() { InitializeComponent(); }

    public void Configure(AppSettings settings, List<Tweak> tweaks, Action<string> navigate, Func<bool> busy,
        Func<LoadSample> sample, Func<FrameStats?> frames, Func<string> selectedGpu)
    {
        _tweaks=tweaks; _navigate=navigate; _busy=busy; _sample=sample; _frames=frames; _selectedGpu=selectedGpu;
        _last=settings.AdvisorMeasurements.FirstOrDefault();
        if (Enum.TryParse<AdvisorProfile>(settings.AdvisorProfile,out var profile)) _profile=profile;
        foreach (var rb in ProfileButtons) if (rb.Tag?.ToString()==_profile.ToString()) rb.IsChecked=true;
        _settings=settings;
        ShowHistory(); ShowMeasurement();
    }

    // Render/interaction verification without inventory, drivers, admin, or Windows changes.
    public void LoadPreview(AdvisorHardware hardware, AdvisorMeasurement? measurement = null)
    {
        _preview=true; _hardware=hardware; _last=measurement;
        RenderHardware(); RefreshCards(); ShowMeasurement();
        ScanButton.IsEnabled=false; MeasureButton.IsEnabled=false; RestoreButton.IsEnabled=false;
    }

    public async void OnOpen()
    {
        if (_hardware == null && !IsBusy) await ScanAsync();
        else if (!IsBusy) RefreshCards();
    }
    public void CancelObservation() => _measurement?.Cancel();
    private RadioButton[] ProfileButtons => new[] {ProfileGaming, ProfileProductivity, ProfileBalanced};

    private static IEnumerable<T> FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
            var child=VisualTreeHelper.GetChild(root,i);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisual<T>(child)) yield return descendant;
        }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await ScanAsync();
    private async Task ScanAsync()
    {
        if (_preview || IsBusy || _busy()) return;
        SetOperation(true); OperationText.Text="Lendo hardware e compatibilidade…";
        try {
            string gpu=_selectedGpu();
            _hardware=await Task.Run(()=>AdvisorHardwareCollector.Collect(gpu));
            if (_settings?.AdvisorRestartBoot is string boot) {
                bool changedBoot=!string.IsNullOrEmpty(_hardware.BootId) && boot != _hardware.BootId && !string.IsNullOrEmpty(boot) && boot != "unknown";
                bool rebootAfterRequest=_hardware.BootUtc is DateTime bootTime && _settings.AdvisorRestartRequestedUtc is DateTime request && bootTime>request;
                if (changedBoot || rebootAfterRequest) {
                    _settings.AdvisorRestartBoot=null; _settings.AdvisorRestartRequestedUtc=null; _settings.Save();
                } else _hardware=_hardware with { RestartPending=true };
            }
            RenderHardware(); RefreshCards(); OperationText.Text="Análise concluída. As ações abaixo não foram aplicadas automaticamente.";
        } catch (Exception ex) { OperationText.Text="Não foi possível concluir a análise: "+ex.Message; }
        finally { SetOperation(false); RefreshCards(); }
    }

    private void RenderHardware()
    {
        if (_hardware is not { } h) return;
        HardwareText.Text=$"{h.Cpu}\n{(h.Gpus.Length>0?string.Join(" · ",h.Gpus):"GPU não identificada")}\n"+
            $"{(h.RamGb is double ram?$"{ram:0.#} GB RAM":"RAM não identificada")} · {h.Board}\nBIOS {h.Bios} · Windows build {h.WindowsBuild}";
        InventoryNote.Text=$"{(h.Portable==true?"Portátil":h.Portable==false?"Desktop":"Tipo não confirmado")} · "+
            $"{(h.OnAc==true?"Fonte conectada":h.OnAc==false?"Na bateria":"Alimentação desconhecida")} · "+
            $"{(h.ConfiguredMemoryMhz is uint mhz?$"RAM configurada: {mhz} MHz (WMI)":"Clock da RAM não confirmado")}"+
            (h.RestartPending?" · REINICIALIZAÇÃO PENDENTE":"")+
            (h.Missing.Length>0?"\nLeituras indisponíveis: "+string.Join(", ",h.Missing):"");
    }

    private void Profile_Checked(object sender, RoutedEventArgs e)
    {
        if (Cards == null || sender is not RadioButton {Tag:string tag} || !Enum.TryParse<AdvisorProfile>(tag,out var profile)) return;
        if (IsBusy) return;
        _profile=profile;
        if (_settings!=null) { _settings.AdvisorProfile=tag; try { _settings.Save(); } catch(Exception ex) { OperationText.Text=ex.Message; } }
        RefreshCards(); ShowMeasurement();
    }

    private void RefreshCards()
    {
        Cards.Children.Clear();
        if (_hardware==null) return;
        var recommendations=PerformanceAdvisor.Recommend(_hardware,_profile,_last);
        RecommendationsTitle.Text=$"{recommendations.Count} RECOMENDAÇÕES · {_profile.ToString().ToUpperInvariant()}";
        foreach (var r in recommendations) {
            var content=new StackPanel();
            content.Children.Add(Text(r.Category.ToUpperInvariant(),"Label"));
            var title=Text(r.Title); title.FontSize=17; title.FontWeight=FontWeights.SemiBold; content.Children.Add(title);
            var reason=Text(r.Reason,"Sub"); reason.Margin=new Thickness(0,8,0,0); content.Children.Add(reason);
            content.Children.Add(Text(r.Tradeoff,"Sub"));
            var row=new WrapPanel {Margin=new Thickness(0,14,0,0)};
            Tweak? tweak=r.Kind==AdvisorActionKind.Tweak?_tweaks.FirstOrDefault(t=>t.Id==r.Target):null;
            bool? state=null; string error="";
            if (!_preview && tweak!=null) { try { state=tweak.Read(); } catch(Exception ex) { error=ex.Message; } }
            string label=r.Kind switch {AdvisorActionKind.Tweak=>state==r.Enable?"APLICADO":"APLICAR",AdvisorActionKind.Guide=>"VER ORIENTAÇÃO",AdvisorActionKind.Settings=>"ABRIR CONFIGURAÇÃO",_=>"ABRIR FERRAMENTA"};
            var action=Button(label,()=>Execute(r),true);
            action.IsEnabled=!_preview && !IsBusy && (tweak==null || (error=="" && tweak.Supported() && state!=r.Enable));
            row.Children.Add(action);
            if (tweak!=null && _settings!=null && WindowsTweaks.HasBackup(tweak,_settings)) {
                var restore=Button("RESTAURAR",()=>Restore(tweak)); restore.IsEnabled=!IsBusy; row.Children.Add(restore);
            }
            var source=Button("FONTE",()=>Open(r.Source)); source.IsEnabled=!_preview; row.Children.Add(source);
            content.Children.Add(row);
            if (error!="") content.Children.Add(Text("Estado não confirmado: "+error,"Sub"));
            if (tweak?.NeedsRestart==true) content.Children.Add(Text("Requer reiniciar após aplicar ou restaurar.","Label"));
            Cards.Children.Add(new Border {Style=(Style)FindResource("CardBorder"),Margin=new Thickness(0,0,0,10),Child=content});
        }
        RestoreButton.IsEnabled=!_preview && !IsBusy && _settings?.TweakBackup.Count>0;
    }
    private TextBlock Text(string text,string? style=null) {
        var block=new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap};
        block.SetResourceReference(TextBlock.ForegroundProperty,"Fg");
        if (style!=null) block.Style=(Style)FindResource(style);
        return block;
    }
    private Button Button(string label,Action action,bool primary=false) {
        var button=new Button {Content=label,Style=(Style)FindResource(primary?"PrimaryButton":"OutlineButton"),Margin=new Thickness(0,0,8,6)};
        button.Click+=(_,_)=>action(); return button;
    }

    private async void Execute(AdvisorRecommendation r)
    {
        if (_preview || IsBusy || _busy()) { OperationText.Text="Conclua o teste ou a operação em andamento antes de ajustar."; return; }
        try {
            if (r.Kind==AdvisorActionKind.Tweak) {
                var t=_tweaks.First(x=>x.Id==r.Target);
                SetOperation(true);
                await Task.Run(()=> { lock(WindowsTweaks.Gate) WindowsTweaks.Apply(t,r.Enable==true,_settings!); });
                LogChange(t.Title+" · aplicado"); MarkRestart(t); t.Refresh();
                OperationText.Text=t.Title+": configuração confirmada."+(t.NeedsRestart?" Reinicie antes da próxima medição.":"");
            } else if (r.Kind==AdvisorActionKind.Guide) ShowGuide(r);
            else if (r.Kind==AdvisorActionKind.Settings) Open(r.Target);
            else _navigate?.Invoke(r.Target);
        } catch (Exception ex) { OperationText.Text="Falha: "+ex.Message+" Se houver alteração parcial, use Restaurar."; }
        finally { SetOperation(false); RefreshCards(); }
    }

    private async void Restore(Tweak tweak)
    {
        if (_preview || IsBusy || _busy() || _settings==null) return;
        if (MessageBox.Show("Restaurar o estado salvo de "+tweak.Title+"?","Restaurar",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
        SetOperation(true);
        try {
            bool success=await Task.Run(()=> { lock(WindowsTweaks.Gate) return WindowsTweaks.RestoreOne(tweak,_settings); });
            OperationText.Text=tweak.Status; LogChange(tweak.Title+(success?" · restaurado":" · falha ao restaurar"));
            if (success) MarkRestart(tweak);
        } catch (Exception ex) { OperationText.Text=ex.Message; }
        finally { SetOperation(false); RefreshCards(); }
    }
    private async void RestoreAll_Click(object sender,RoutedEventArgs e)
    {
        if (_preview || IsBusy || _busy() || _settings==null) return;
        if (MessageBox.Show("Restaurar os ajustes com backup? Falhas manterão o backup para nova tentativa.","Restaurar ajustes",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
        SetOperation(true);
        try {
            bool restart=_tweaks.Any(t=>t.NeedsRestart&&WindowsTweaks.HasBackup(t,_settings));
            await Task.Run(()=> {lock(WindowsTweaks.Gate) WindowsTweaks.RestoreAll(_tweaks,_settings);});
            OperationText.Text=string.Join("\n",_tweaks.Where(t=>!string.IsNullOrEmpty(t.Status)).Select(t=>t.Title+": "+t.Status));
            if (restart) MarkRestart(new Tweak {NeedsRestart=true});
            LogChange("Restauração solicitada · backups restantes: "+_settings.TweakBackup.Count);
        } catch(Exception ex) { OperationText.Text=ex.Message; }
        finally { SetOperation(false); RefreshCards(); }
    }
    private void MarkRestart(Tweak t) {
        if (!t.NeedsRestart || _settings==null) return;
        _settings.AdvisorRestartBoot=_hardware?.BootId is {Length:>0} id?id:"unknown";
        _settings.AdvisorRestartRequestedUtc=DateTime.UtcNow;
        _settings.Save();
        if (_hardware!=null) _hardware=_hardware with {RestartPending=true}; RenderHardware();
    }
    private void LogChange(string message) {
        if (_settings==null) return;
        _settings.AdvisorChanges.Insert(0,$"{DateTime.Now:g} · {message}");
        _settings.AdvisorChanges=_settings.AdvisorChanges.Take(50).ToList(); _settings.Save(); ShowHistory();
    }
    private void ShowGuide(AdvisorRecommendation r) {
        var text=Text(r.Guide??r.Reason); text.Margin=new Thickness(24); text.FontSize=14;
        new Window {Title=r.Title,Width=730,Height=620,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Owner=Window.GetWindow(this),Background=(Brush)FindResource("Bg"),Content=new ScrollViewer {Content=text,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}}.ShowDialog();
    }
    private static void Open(string target) {
        if (Uri.TryCreate(target,UriKind.Absolute,out var uri) && uri.Scheme is "https" or "ms-settings")
            Process.Start(new ProcessStartInfo(target) {UseShellExecute=true});
    }
    private void Navigate_Click(object sender,RoutedEventArgs e) {
        if (!_preview && sender is Button {Tag:string target}) _navigate?.Invoke(target);
    }

    private async void Measure_Click(object sender,RoutedEventArgs e)
    {
        if (_measurement!=null) { _measurement.Cancel(); return; }
        if (_preview || IsBusy || _busy()) { OperationText.Text="Conclua o outro teste primeiro."; return; }
        string workload=WorkloadText.Text.Trim();
        if (workload.Length<6 || workload.StartsWith("Minha carga ·")) { OperationText.Text="Descreva a carga, cena e configurações que pretende repetir antes e depois."; WorkloadText.Focus(); return; }
        await ScanAsync(); if (_hardware==null) return;
        if (_hardware.RestartPending) {OperationText.Text="Conclua a reinicialização pendente antes de medir uma referência.";return;}
        var rows=new List<AdvisorReading>(); var clock=Stopwatch.StartNew();
        var hardware=_hardware; var profile=_profile;
        using var cancellation=new CancellationTokenSource(); _measurement=cancellation;
        MeasureButton.Content="PARAR OBSERVAÇÃO"; WorkloadText.IsEnabled=false; ScanButton.IsEnabled=false; BaselineButton.IsEnabled=false;
        foreach(var rb in ProfileButtons) rb.IsEnabled=false;
        RefreshCards(); OperationText.Text="Observando. Alterne para o aplicativo/jogo e mantenha a mesma carga por 30 s.";
        try {
            while(clock.Elapsed.TotalSeconds<30) {
                await Task.Delay(1000,cancellation.Token);
                var s=_sample?.Invoke(); FrameStats? frames=null;
                try { frames=_frames?.Invoke(); if(frames?.ProcessId==Environment.ProcessId) frames=null; } catch { }
                var memory=Optimizer.MemoryStatus();
                double? ram=(double?)s?.RamPct ?? (memory.Total>0 ? 100d*(memory.Total-memory.Available)/memory.Total : null);
                rows.Add(new(s?.CpuTotal,s?.GpuLoad,ram,s?.CpuTemp,s?.GpuTemp,s?.VramPct,s?.GpuThermalCap==true,
                    frames?.Fps,frames?.ProcessName,frames?.Low1Fps));
                MeasureProgress.Value=Math.Min(30,clock.Elapsed.TotalSeconds);
                MeasurementText.Text=$"{rows.Count} amostras · {clock.Elapsed.TotalSeconds:0}/30 s · CPU {Number(s?.CpuTotal,"% ")} · GPU {Number(s?.GpuLoad,"%")} · FPS {Number(frames?.Fps,"")}";
            }
            // Refresh power/reboot facts: a change during capture invalidates a before/after comparison.
            string selectedGpu=_selectedGpu();
            var finalHardware=await Task.Run(()=>AdvisorHardwareCollector.Collect(selectedGpu));
            if(finalHardware.Identity!=hardware.Identity || finalHardware.OnAc!=hardware.OnAc || finalHardware.RestartPending) throw new InvalidOperationException("Hardware, alimentação ou reinicialização mudou durante a observação. Refaça a sessão.");
            _last=AdvisorMeasurement.From(hardware,profile,workload,rows,clock.Elapsed.TotalSeconds);
            if (_settings!=null) { _settings.AdvisorMeasurements.Insert(0,_last); _settings.AdvisorMeasurements=_settings.AdvisorMeasurements.Take(20).ToList();_settings.Save(); }
            OperationText.Text="Observação concluída. Não é um teste de estabilidade nem uma prova isolada de ganho."; ShowMeasurement();ShowHistory();
        } catch(OperationCanceledException) {OperationText.Text="Observação cancelada; nenhum resultado parcial foi salvo.";ShowMeasurement();}
        catch(Exception ex) {OperationText.Text="Medição não salva: "+ex.Message;ShowMeasurement();}
        finally {
            _measurement=null; MeasureButton.Content="OBSERVAR 30 S"; WorkloadText.IsEnabled=true;ScanButton.IsEnabled=true;
            foreach(var rb in ProfileButtons) rb.IsEnabled=true;
            BaselineButton.IsEnabled=_last?.Complete==true; RefreshCards();
        }
    }

    private static string Number(double? value,string unit) => value is double v?$"{v:0.#}{unit}":"não medido";
    private void ShowMeasurement() {
        if (_last==null) return;
        MeasurementText.Text=$"{_last.Date:g} · {_last.Profile} · {_last.Workload}\n"+
            $"CPU média {Number(_last.Cpu,"%")} · GPU média {Number(_last.Gpu,"%")} · RAM média {Number(_last.Ram,"%")}\n"+
            $"Máxima CPU {Number(_last.CpuTempMax," °C")} · GPU {Number(_last.GpuTempMax," °C")} · FPS {Number(_last.Fps,"")} ({_last.Process??"processo não identificado"})";
        BaselineButton.IsEnabled=!_preview && _last.Complete && !IsBusy;
        ComparisonText.Text=_settings?.AdvisorBaseline is { } baseline?PerformanceAdvisor.Compare(baseline,_last):"Salve uma referência antes de aplicar um ajuste e repita a mesma observação depois.";
    }
    private void Baseline_Click(object sender,RoutedEventArgs e) {
        if(_preview||IsBusy||_last?.Complete!=true||_settings==null) return;
        if(_last.Profile!=_profile) { OperationText.Text="Faça uma medição no perfil selecionado antes de salvar a referência.";return; }
        try {_settings.AdvisorBaseline=_last;_settings.Save();OperationText.Text="Referência salva. Aplique uma mudança e repita exatamente a mesma carga.";ShowMeasurement();}
        catch(Exception ex) {OperationText.Text="Não foi possível salvar: "+ex.Message;}
    }
    private void ShowHistory() {
        if(_settings==null) return;
        HistoryText.Text=string.Join("\n",_settings.AdvisorChanges.Take(10))+
            "\n\n"+string.Join("\n",_settings.AdvisorMeasurements.Take(6).Select(m=>$"{m.Date:g} · {m.Profile} · {m.Workload} · FPS {Number(m.Fps,"")}"));
    }
    private void Export_Click(object sender,RoutedEventArgs e) {
        if(_preview||IsBusy||_hardware==null) return;
        var dialog=new SaveFileDialog {Filter="Relatório JSON|*.json",FileName="ResetExplode-otimizacoes.json"};
        if(dialog.ShowDialog()!=true) return;
        try {
            var report=new {Version="0.17-advisor",Hardware=_hardware,Profile=_profile,Last=_last,Baseline=_settings?.AdvisorBaseline,
                Recommendations=PerformanceAdvisor.Recommend(_hardware,_profile,_last),Changes=_settings?.AdvisorChanges,
                Limits="Sem seriais/chaves/contas; contém nomes de hardware e a descrição de carga digitada. Nenhum envio automático."};
            File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
            OperationText.Text="Relatório salvo localmente.";
        } catch(Exception ex) {OperationText.Text="Falha ao exportar: "+ex.Message;}
    }
    private void SetOperation(bool active) {
        _operation=active;
        ScanButton.IsEnabled=!active&&!_preview; MeasureButton.IsEnabled=!active&&!_preview;
        RestoreButton.IsEnabled=!active&&!_preview;
        foreach(var rb in ProfileButtons) rb.IsEnabled=!active;
        if(active) foreach(var b in FindVisual<Button>(Cards)) b.IsEnabled=false;
    }
}
