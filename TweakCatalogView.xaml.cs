using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ResetExplode;

public sealed class TweakChoice : INotifyPropertyChanged
{
    public required Tweak Tweak { get; init; }
    private bool _selected;
    public bool Selected { get => _selected; set { _selected = value; Changed(nameof(Selected)); } }
    public bool Available { get; private set; }
    public bool HasBackup { get; private set; }
    public string StateText { get; private set; } = "Estado não lido";
    public void Refresh(AppSettings settings, bool preview)
    {
        HasBackup = WindowsTweaks.HasBackup(Tweak, settings);
        try {
            Available = !preview && Tweak.Supported();
            bool? current = Available ? Tweak.Read() : null;
            // Unknown is explicit; absent registry policy is writable, and does not imply enabled.
            StateText = preview ? "Prévia · nenhuma alteração permitida" : !Available ? "Indisponível nesta configuração / leitura bloqueada" :
                current is null ? "Não configurado explicitamente" : current == true ? "Ajuste já configurado" : "Ajuste não configurado";
            if (current.HasValue) Tweak.IsOn = current.Value;
            if (Tweak.Advanced) StateText += " · AVANÇADO";
            if (Tweak.NeedsRestart) StateText += " · requer reinício / nova sessão";
            if (HasBackup) StateText += " · original salvo";
        } catch (Exception ex) { Available = false; StateText = "Falha de leitura: " + ex.Message; }
        if (!Available) Selected = false;
        Changed(nameof(Available)); Changed(nameof(HasBackup)); Changed(nameof(StateText));
    }
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class TweakCatalogView : UserControl
{
    private AppSettings? _settings;
    private List<TweakChoice> _choices = new();
    private Func<bool> _externalBusy = () => false;
    private bool _preview;
    public bool IsBusy { get; private set; }
    public TweakCatalogView() { InitializeComponent(); }
    public void Configure(AppSettings settings, IEnumerable<Tweak> tweaks, Func<bool> busy)
    {
        _settings = settings; _externalBusy = busy;
        _choices = tweaks.Select(t => new TweakChoice { Tweak = t }).ToList();
        CategoryBox.ItemsSource = new[] { "Todas as categorias" }.Concat(_choices.Select(c => c.Tweak.Category).Distinct().Order());
        CategoryBox.SelectedIndex = 0;
        CatalogSummary.Text = $"{_choices.Count} ações reais disponíveis no catálogo · compatibilidade conferida ao abrir";
        Filter();
    }
    public void LoadPreview(IEnumerable<Tweak> tweaks)
    {
        _preview = true; Configure(new AppSettings(), tweaks, () => false); Refresh();
        ApplySelectedButton.IsEnabled = RestoreSavedButton.IsEnabled = SuggestButton.IsEnabled = RefreshButton.IsEnabled = false;
    }
    public void OnOpen() { if (!IsBusy) Refresh(); }
    private void Refresh()
    {
        if (_settings == null) return;
        foreach (var item in _choices) item.Refresh(_settings, _preview);
        CatalogSummary.Text = $"{_choices.Count} ações executáveis · {_choices.Count(c => c.Available)} disponíveis neste PC · {_choices.Count(c => c.HasBackup)} com original salvo";
        Filter();
    }
    private static string SearchKey(string text) => new(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    private void Filter()
    {
        if (CatalogItems == null) return;
        string query = SearchKey(SearchBox.Text.Trim());
        string? category = CategoryBox.SelectedItem as string;
        CatalogItems.ItemsSource = _choices.Where(c =>
            (category == null || category == "Todas as categorias" || c.Tweak.Category == category) &&
            SearchKey(c.Tweak.Title + " " + c.Tweak.Description + " " + c.Tweak.Effect + " " + c.Tweak.Category).Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    private void Filter_Changed(object sender, RoutedEventArgs e) => Filter();
    private void Refresh_Click(object sender, RoutedEventArgs e) { if (!IsBusy) Refresh(); }
    private void Suggest_Click(object sender, RoutedEventArgs e)
    {
        if (_preview || IsBusy) return;
        string profile = (ProfileBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Jogos";
        foreach (var c in _choices) {
            try { c.Selected = c.Available && !c.Tweak.Advanced && c.Tweak.Profiles.Split(',').Contains(profile) && c.Tweak.Read() != true; }
            catch (Exception ex) { c.Selected = false; c.Tweak.Status = "Falha ao selecionar: " + ex.Message; }
        }
        OperationText.Text = $"{_choices.Count(c => c.Selected)} sugestões selecionadas para {profile} no catálogo completo (inclusive fora do filtro). Revise antes de aplicar.";
        // Clear filters so a selected action can never be hidden from the review.
        SearchBox.Text = ""; CategoryBox.SelectedIndex = 0; Filter();
    }
    private async void ApplySelected_Click(object sender, RoutedEventArgs e) => await Run(_choices.Where(c => c.Selected).ToArray(), false);
    private async void ApplyOne_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: TweakChoice c }) await Run(new[] { c }, false); }
    private async void RestoreOne_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: TweakChoice c }) await Run(new[] { c }, true); }
    private async void RestoreSaved_Click(object sender, RoutedEventArgs e) => await Run(_choices.Where(c => c.HasBackup).ToArray(), true);
    private void Source_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TweakChoice c }) return;
        try { Process.Start(new ProcessStartInfo(c.Tweak.Source) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show("Não foi possível abrir a documentação: " + ex.Message); }
    }
    private async Task Run(TweakChoice[] choices, bool restore)
    {
        if (_preview || IsBusy || _settings == null) return;
        if (_externalBusy()) { OperationText.Text = "Conclua o teste ou ajuste em andamento primeiro."; return; }
        var targets = choices.Where(c => restore ? c.HasBackup : c.Available).ToArray();
        if (targets.Length == 0) { OperationText.Text = "Nenhum ajuste elegível selecionado."; return; }
        if (!Review(targets, restore)) return;
        if (targets.Any(c => c.Tweak.Id == "hags") && !restore && MessageBox.Show(
            "HAGS exige GPU e driver compatíveis. A chave de registro não comprova suporte. Confirme a opção nas Configurações gráficas do Windows antes de continuar.",
            "HAGS · compatibilidade", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        IsBusy = true; CatalogItems.IsEnabled = false;
        ApplySelectedButton.IsEnabled = RestoreSavedButton.IsEnabled = SuggestButton.IsEnabled = RefreshButton.IsEnabled = false;
        OperationText.Text = restore ? "Restaurando originais…" : "Salvando originais e aplicando…";
        var results = new List<(TweakChoice Item, bool Success, string Message)>();
        try {
            await Task.Run(() => { lock (WindowsTweaks.Gate) {
                foreach (var item in targets) {
                    try {
                        bool ok;
                        if (restore) ok = WindowsTweaks.RestoreOne(item.Tweak, _settings);
                        else { WindowsTweaks.Apply(item.Tweak, true, _settings); ok = true; }
                        results.Add((item, ok, ok ? restore ? "Original restaurado." : "Valor aplicado e relido. Confira o efeito no cenário de uso." : item.Tweak.Status));
                    } catch (Exception ex) { results.Add((item, false, "Falha: " + ex.Message + " Backup mantido quando existente.")); }
                }
                foreach (var result in results) _settings.AdvisorChanges.Insert(0, $"{DateTime.Now:g} · catálogo · {(restore ? "restaurar" : "aplicar")} · {result.Item.Tweak.Title} · {(result.Success ? "OK" : "FALHA")}");
                _settings.AdvisorChanges = _settings.AdvisorChanges.Take(50).ToList();
                if (results.Any(r => r.Item.Tweak.NeedsRestart)) {
                    _settings.AdvisorRestartBoot = AdvisorHardwareCollector.Collect().BootId;
                    _settings.AdvisorRestartRequestedUtc = DateTime.UtcNow;
                }
                _settings.Save();
            }});
            foreach (var r in results) { r.Item.Tweak.Status = r.Message; r.Item.Selected = false; }
            OperationText.Text = $"{results.Count(r => r.Success)} concluídos · {results.Count(r => !r.Success)} falhas. " +
                (results.Any(r => r.Item.Tweak.NeedsRestart) ? "Reinicie / entre novamente na conta para concluir os itens indicados." : "Compare a mesma carga antes e depois.");
        } catch (Exception ex) { OperationText.Text = "Falha ao concluir o histórico: " + ex.Message + ". Confira os estados e backups antes de repetir."; }
        finally {
            IsBusy = false; CatalogItems.IsEnabled = true;
            ApplySelectedButton.IsEnabled = RestoreSavedButton.IsEnabled = SuggestButton.IsEnabled = RefreshButton.IsEnabled = true;
            Refresh();
        }
    }
    private bool Review(TweakChoice[] targets, bool restore)
    {
        var dialog = new Window { Title = "Revisar tweaks", Width = 760, Height = 600, MinWidth = 580, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(this),
            Background = (System.Windows.Media.Brush)FindResource("Bg"), Foreground = (System.Windows.Media.Brush)FindResource("Fg") };
        var layout = new DockPanel { Margin = new Thickness(22) };
        var title = new TextBlock { Text = (restore ? "RESTAURAR ORIGINAIS" : "APLICAR SELEÇÃO") + $" · {targets.Length} itens",
            FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,15) };
        DockPanel.SetDock(title, Dock.Top); layout.Children.Add(title);
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom);
        footer.Children.Add(new TextBlock { Text = "Execução em sequência. Falhas mantêm os backups. Alterações de energia podem aumentar consumo e temperatura.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,10) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "CANCELAR", IsCancel = true, Style = (Style)FindResource("OutlineButton"), Padding = new Thickness(18,9,18,9), Margin = new Thickness(0,0,10,0) };
        var apply = new Button { Content = restore ? "RESTAURAR" : "APLICAR", Style = (Style)FindResource("PrimaryButton"), Padding = new Thickness(18,9,18,9) };
        apply.Click += (_,_) => dialog.DialogResult = true;
        buttons.Children.Add(cancel); buttons.Children.Add(apply); footer.Children.Add(buttons); layout.Children.Add(footer);
        var contents = new StackPanel();
        foreach (var item in targets) {
            contents.Children.Add(new TextBlock { Text = item.Tweak.Title + (item.Tweak.Advanced ? " · AVANÇADO" : ""), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,5) });
            contents.Children.Add(new TextBlock { Text = item.Tweak.Description + "\n" + item.Tweak.Effect, TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("Muted") });
        }
        layout.Children.Add(new ScrollViewer { Content = contents, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = layout;
        return dialog.ShowDialog() == true;
    }
}
