using System.Text.Json;
using System.IO;

namespace ResetExplode;

public enum SettingKind { UiFlag, UiNumber, RegistryDword, PowerAc }
public sealed record TweakSetting(string Id, SettingKind Kind, string Address, string Name, uint Get = 0, uint Set = 0, bool UiParam = false);
public interface ITweakStore
{
    bool Supports(TweakSetting setting);
    string Scope(TweakSetting setting);
    string? Read(TweakSetting setting, string scope);
    void Write(TweakSetting setting, string scope, string? value);
}

/// <summary>Declarative actions; all I/O is injectable. A single durable snapshot owns value and power scheme.</summary>
public static class TweakCatalog
{
    private sealed record Snapshot(string Scope, string? Value);
    public const string SpiSource = "https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow";
    private const string PowerDocs = "https://learn.microsoft.com/windows-hardware/customize/power-settings/";
    private const string Processor = "54533251-82be-4824-96c1-47b60b740d00";
    public static List<Tweak> Create(ITweakStore store, int build)
    {
        var actions = new List<Tweak>();
        void Add(TweakSetting spec, string title, string description, string effect, string category, string source,
            string enabled, string disabled, bool advanced = false, string profiles = "Opcional", int minBuild = 19041, bool restart = false)
        {
            actions.Add(new Tweak {
                Id = spec.Id, Title = title, Description = description, Effect = effect, Category = category,
                Source = source, Advanced = advanced, Profiles = profiles, NeedsRestart = restart,
                Supported = () => build >= minBuild && store.Supports(spec),
                Read = () => store.Read(spec, store.Scope(spec)) is string v ? v == enabled : null,
                Write = (on, settings) => {
                    string scope = store.Scope(spec);
                    // A power setting must not silently lose the original from a different scheme.
                    if (settings.TweakBackup.TryGetValue(spec.Id, out var previous) && previous != null &&
                        JsonSerializer.Deserialize<Snapshot>(previous)?.Scope != scope)
                        throw new InvalidOperationException("O plano de energia mudou. Restaure este ajuste antes de alterar outro plano.");
                    TweakValueBackup.Write(settings.TweakBackup, spec.Id,
                        () => JsonSerializer.Serialize(new Snapshot(scope, store.Read(spec, scope))),
                        raw => { var value = Decode(raw); store.Write(spec, value.Scope, value.Value); },
                        settings.Save, JsonSerializer.Serialize(new Snapshot(scope, on ? enabled : disabled)));
                },
                Restore = settings => TweakValueBackup.Restore(settings.TweakBackup, spec.Id,
                    () => { var original = Decode(settings.TweakBackup[spec.Id]); return JsonSerializer.Serialize(new Snapshot(original.Scope, store.Read(spec, original.Scope))); },
                    raw => { var original = Decode(raw); store.Write(spec, original.Scope, original.Value); })
            });
        }
        void Ui(string id, string title, uint get, uint set, string effect, bool uiParam = false) =>
            Add(new(id, SettingKind.UiFlag, "", "", get, set, uiParam), title,
                "Desativa este efeito pela API do Windows e avisa os aplicativos da mudança.", effect,
                "Interface", SpiSource, "0", "1", profiles: "Equilíbrio");
        Ui("menu-animation", "Desativar animações de menus", 0x1002, 0x1003, "Menus aparecem sem animação. Pode reduzir trabalho visual; não altera o renderizador do jogo.");
        Ui("combo-animation", "Desativar animação de listas suspensas", 0x1004, 0x1005, "Listas de seleção deixam de deslizar; aplicativos com interface própria podem ignorar.");
        Ui("list-smooth", "Desativar rolagem animada de listas", 0x1006, 0x1007, "Reduz transições em controles de lista do Windows; rolagem fica menos suave.");
        Ui("caption-gradient", "Desativar gradiente de títulos clássicos", 0x1008, 0x1009, "Simplifica barras de título clássicas; pode não afetar janelas modernas.");
        Ui("hot-tracking", "Desativar realce animado de controles", 0x100e, 0x100f, "Remove realce de seleção em controles compatíveis; altera a aparência.");
        Ui("menu-fade", "Desativar fade de menus", 0x1012, 0x1013, "Menus podem usar deslizamento se a animação principal continuar ligada. Desative animações de menus para remover ambas.");
        Ui("selection-fade", "Desativar fade após selecionar menus", 0x1014, 0x1015, "Itens selecionados desaparecem sem transição em controles compatíveis.");
        Ui("tooltip-animation", "Desativar animação de dicas", 0x1016, 0x1017, "Dicas de controles aparecem sem transição; textos de ajuda são preservados.");
        Ui("tooltip-fade", "Desativar fade de dicas", 0x1018, 0x1019, "Desativa o fade; desative também animação de dicas para remover o deslizamento.");
        Ui("cursor-shadow", "Desativar sombra do ponteiro", 0x101a, 0x101b, "Simplifica o ponteiro; pode reduzir sua visibilidade em certos fundos.");
        Ui("drop-shadow", "Desativar sombras de menus compatíveis", 0x1024, 0x1025, "Afeta classes de janela que usam CS_DROPSHADOW; não remove todas as sombras do desktop.");
        Ui("drag-outline", "Arrastar janelas pelo contorno", 0x0026, 0x0025, "Exibe somente a moldura durante o arraste; útil em sessões remotas ou PCs limitados.", true);
        Add(new("menu-delay", SettingKind.UiNumber, "", "", 0x006a, 0x006b, true),
            "Remover espera de abertura de submenus", "Define em 0 ms a espera de submenus compatíveis ao passar o mouse.",
            "Pode abrir submenus involuntariamente. Desligar usa 400 ms; Restaurar recupera seu intervalo exato.",
            "Interface", SpiSource, "0", "400", profiles: "Produtividade");
        Add(new("transparency", SettingKind.RegistryDword, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency"),
            "Desativar transparência do Windows", "Desativa transparência das superfícies compatíveis do sistema.",
            "Reduz efeitos de composição; saia e entre na conta para atualizar todas as superfícies. Sem ganho de FPS garantido.",
            "Interface", "https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/develop/settings/settings-common.md", "0", "1", profiles: "Equilíbrio", restart: true);

        void Power(string id, string title, string group, string guid, string on, string off, string description, string effect, string source) =>
            Add(new(id, SettingKind.PowerAc, group, guid), title, description + " Apenas na tomada e no plano atual.",
                effect + " O firmware pode limitar o efeito. Restaurar recupera o valor no plano original, inclusive após trocar de plano.",
                "Energia", source, on, off, advanced: true);
        string epp = PowerDocs + "options-for-perf-state-engine-perfenergypreference";
        Power("cpu-epp", "CPU: priorizar desempenho (EPP 0)", Processor, "36687f9e-e3a5-4dbf-b1dc-15eb381c6863", "0", "50",
            "Solicita preferência por desempenho no controle autônomo da CPU.", "Pode aumentar calor e consumo. Depende de CPPC/HWP; leitura do valor não comprova ganho.", epp);
        Power("cpu-epp1", "CPU híbrida: EPP da classe 1 em 0", Processor, "36687f9e-e3a5-4dbf-b1dc-15eb381c6864", "0", "50",
            "Ajusta a preferência de energia da classe 1 quando exposta pelo Windows.", "A classe não identifica universalmente P-cores/E-cores; confirme a plataforma. Pode aumentar consumo.", epp);
        Power("cpu-boost", "Permitir boost automático da CPU", Processor, "be337238-0d82-4146-a960-4f3749d470c7", "1", "0",
            "Permite boost dentro dos limites definidos pelo fabricante, sem editar tensão ou multiplicador.", "Pode aumentar temperatura, ruído e consumo; desligar desativa boost. Não é overclock de BIOS.", PowerDocs + "options-for-perf-state-engine-perfboostmode");
        Power("cpu-max", "CPU: limite máximo de desempenho em 100%", Processor, "bc5038f7-23e0-4960-96da-33abaf5935ec", "100", "99",
            "Remove um limite percentual inferior a 100% no plano.", "Se já estiver em 100%, não traz mudança. Desligar limita a 99%, o que pode afetar boost; prefira Restaurar.", PowerDocs + "options-for-perf-state-engine-maxperformance");
        Power("cpu-idle", "CPU: permitir redução em repouso (mínimo 5%)", Processor, "893dee8e-2bef-41e0-89c6-b55d0929964c", "5", "100",
            "Evita manter o mínimo da CPU em 100% quando não há trabalho.", "Pode reduzir calor em repouso. Desligar solicita mínimo de 100%, elevando consumo; prefira Restaurar.", PowerDocs + "options-for-perf-state-engine-minperformance");
        Power("pcie-aspm", "PCIe: desativar economia do link na tomada", "501a4d13-42af-4429-9fd1-a8218c268e20", "ee12f906-d277-404b-b6da-e5fa1a576df5", "0", "1",
            "Solicita que links PCIe não usem a economia ASPM do plano.", "Útil somente para investigar transições de energia; aumenta consumo e não garante menor latência.", PowerDocs + "pci-express-settings-link-state-power-management");
        Power("cooling-active", "Resfriamento: preferir ventilação ativa", Processor, "94d3a615-a899-4ac5-ae2b-e4d8f634367f", "1", "0",
            "Solicita resfriamento ativo antes de resfriamento passivo em plataformas compatíveis.", "Pode aumentar ruído; não controla diretamente a ventoinha e muitos firmwares ignoram esta preferência.", "https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/winnt.h");
        Power("usb-suspend", "USB: permitir suspensão seletiva", "2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", "1", "0",
            "Permite economizar energia de portas USB ociosas sem suspender todas as portas.", "Recomendado pela Microsoft; não é ajuste de polling rate. Investigue dispositivos defeituosos individualmente.", "https://learn.microsoft.com/windows-hardware/drivers/usbcon/usb-selective-suspend");

        void Edge(string id, string name, string title, string description, string effect, string on, string off, string profiles = "Jogos,Produtividade,Equilíbrio") =>
            Add(new(id, SettingKind.RegistryDword, @"Software\Policies\Microsoft\Edge", name), title, description,
                effect + " Define política local: o Edge pode mostrar 'Gerenciado'. Políticas de organização podem prevalecer. Reinicie o navegador; confira edge://policy.",
                "Navegador em segundo plano", "https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/" + name,
                on, off, profiles: profiles);
        Edge("edge-startup", "StartupBoostEnabled", "Edge: desativar inicialização acelerada", "Impede a pré-inicialização de processos do Edge ao entrar no Windows.", "Pode reduzir processos em repouso; a primeira abertura do navegador pode demorar mais.", "0", "1");
        Edge("edge-background", "BackgroundModeEnabled", "Edge: parar apps após fechar o navegador", "Desativa o modo de execução em segundo plano do Edge.", "Pode interromper extensões e notificações após fechar as janelas; não encerra janelas abertas.", "0", "1");
        Edge("edge-sleep", "SleepingTabsEnabled", "Edge: ativar suspensão de abas inativas", "Permite liberar CPU e memória de abas inativas elegíveis.", "Sites podem precisar retomar ao voltar à aba. Política não se aplica a perfis com conta Microsoft pessoal.", "1", "0");
        Edge("edge-sleep-timeout", "SleepingTabsTimeout", "Edge: suspender abas após 5 minutos", "Define 300 segundos para abas elegíveis; requer suspensão de abas ligada.", "Pode reduzir retenção de recursos. Política não se aplica a perfis com conta Microsoft pessoal.", "300", "900", "Opcional");
        Edge("edge-preload", "NewTabPagePrerenderEnabled", "Edge: desativar pré-carga da nova aba", "Evita pré-renderizar a nova aba antes de abri-la.", "A nova aba pode abrir mais devagar. Política não se aplica a perfis com conta Microsoft pessoal.", "0", "1");
        Edge("edge-prefetch", "NetworkPredictionOptions", "Edge: desativar previsão de rede", "Define a previsão de rede como 'nunca' (2).",
            "Pode reduzir tráfego antecipado; a navegação pode ficar mais lenta. Política não se aplica a perfis com conta Microsoft pessoal.", "2", "0", "Opcional");
        return actions;
    }
    private static Snapshot Decode(string? raw) => raw is null ? throw new IOException("Backup ausente.") :
        JsonSerializer.Deserialize<Snapshot>(raw) ?? throw new IOException("Backup inválido.");
}
