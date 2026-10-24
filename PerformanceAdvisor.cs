namespace ResetExplode;

public enum AdvisorProfile { Jogos, Produtividade, Equilibrio }
public enum AdvisorActionKind { Tweak, Navigate, Settings, Guide }
public sealed record AdvisorHardware
{
    public string Cpu { get; init; } = "Não identificado";
    public string[] Gpus { get; init; } = Array.Empty<string>();
    public string Board { get; init; } = "Não identificada";
    public string Bios { get; init; } = "Não identificada";
    public string BootId { get; init; } = "";
    public DateTime? BootUtc { get; init; }
    public string SelectedGpu { get; init; } = "";
    public int WindowsBuild { get; init; }
    public double? RamGb { get; init; }
    public uint? ConfiguredMemoryMhz { get; init; }
    public bool? Portable { get; init; }
    public bool? OnAc { get; init; }
    public double? SystemDriveFreePct { get; init; }
    public bool RestartPending { get; init; }
    public string[] Missing { get; init; } = Array.Empty<string>();
    public string Identity => $"{Cpu}|{string.Join(";", Gpus)}|{SelectedGpu}|{Board}|{Bios}|{RamGb}|{ConfiguredMemoryMhz}|{WindowsBuild}";
}
public sealed record AdvisorRecommendation(string Id, string Category, string Title, string Reason, string Tradeoff,
    AdvisorActionKind Kind, string Target, string Source, int Priority = 50, bool? Enable = null, string? Guide = null);
public sealed record AdvisorReading(double? Cpu, double? Gpu, double? Ram, double? CpuTemp, double? GpuTemp,
    double? Vram, bool Thermal, double? Fps, string? Process, double? Low1 = null);
public sealed record AdvisorMeasurement
{
    public DateTime Date { get; init; } = DateTime.Now;
    public string Hardware { get; init; } = "";
    public AdvisorProfile Profile { get; init; }
    public string Workload { get; init; } = "";
    public bool? OnAc { get; init; }
    public bool RestartPending { get; init; }
    public string? Process { get; init; }
    public int Samples { get; init; }
    public double DurationSeconds { get; init; }
    public double? Cpu { get; init; }
    public double? Gpu { get; init; }
    public double? Ram { get; init; }
    public double? CpuTempMax { get; init; }
    public double? GpuTempMax { get; init; }
    public double? Vram { get; init; }
    public double? Fps { get; init; }
    public double? Low1 { get; init; }
    public double? FpsVariationPct { get; init; }
    public int FrameSamples { get; init; }
    public bool Thermal { get; init; }
    public bool Complete => Samples >= 25 && DurationSeconds >= 28;

    public static AdvisorMeasurement From(AdvisorHardware hardware, AdvisorProfile profile, string workload,
        IReadOnlyList<AdvisorReading> rows, double duration)
    {
        static double? Average(IEnumerable<double?> values) {
            var known = values.Where(v => v.HasValue && double.IsFinite(v.Value)).Select(v => v!.Value).ToArray();
            return known.Length == 0 ? null : known.Average();
        }
        static double? Max(IEnumerable<double?> values) {
            var known = values.Where(v => v.HasValue && double.IsFinite(v.Value)).Select(v => v!.Value).ToArray();
            return known.Length == 0 ? null : known.Max();
        }
        var processes = rows.Where(r => r.Fps is double f && double.IsFinite(f) && f>0 && r.Process != null).Select(r => r.Process).Distinct().ToArray();
        var frames = processes.Length == 1 ? rows.Where(r => r.Fps is double f && double.IsFinite(f) && f>0 && r.Process == processes[0]).ToArray() : Array.Empty<AdvisorReading>();
        var fps = Average(frames.Select(r => r.Fps));
        var buckets = frames.Chunk(10).Where(b => b.Length >= 5).Select(b => b.Average(r => r.Fps!.Value)).ToArray();
        return new() {
            Hardware = hardware.Identity, Profile = profile, Workload = workload.Trim(), OnAc = hardware.OnAc, RestartPending = hardware.RestartPending,
            Samples = rows.Count, DurationSeconds = duration, Process = processes.Length == 1 ? processes[0] : null,
            Cpu = Average(rows.Select(r => r.Cpu)), Gpu = Average(rows.Select(r => r.Gpu)), Ram = Average(rows.Select(r => r.Ram)),
            CpuTempMax = Max(rows.Select(r => r.CpuTemp)), GpuTempMax = Max(rows.Select(r => r.GpuTemp)), Vram = Max(rows.Select(r => r.Vram)),
            Fps = fps, Low1 = Average(frames.Select(r => r.Low1)), FrameSamples = frames.Length,
            FpsVariationPct = buckets.Length >= 2 && fps > 0 ? 100 * (buckets.Max() - buckets.Min()) / fps.Value : null,
            Thermal = rows.Any(r => r.Thermal)
        };
    }
}

public static class PerformanceAdvisor
{
    public const string MicrosoftPerformance = "https://support.microsoft.com/pt-br/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows";
    public const string MicrosoftGraphics = "https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11";
    public const string PowerSource = "https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options";
    public const string IntelGuide = "https://www.intel.com/content/www/us/en/gaming/resources/overclocking-xtu-guide.html";
    public const string AmdGuide = "https://www.amd.com/en/products/software/ryzen-master.html";

    public static IReadOnlyList<AdvisorRecommendation> Recommend(AdvisorHardware h, AdvisorProfile profile, AdvisorMeasurement? run)
    {
        var r = new List<AdvisorRecommendation>();
        bool gaming = profile == AdvisorProfile.Jogos;
        void Add(string id, string category, string title, string reason, string cost, AdvisorActionKind kind, string target, string source, int priority = 50, bool? enable = null, string? guide = null) =>
            r.Add(new(id, category, title, reason, cost, kind, target, source, priority, enable, guide));
        if (h.RestartPending)
            Add("reboot", "Diagnóstico", "Concluir a reinicialização pendente", "O Windows informa alterações pendentes. Finalize-as antes de comparar resultados.", "Salve seu trabalho e reinicie pelo Windows quando estiver pronto.", AdvisorActionKind.Settings, "ms-settings:windowsupdate", MicrosoftPerformance, 0);
        if (run != null && (run.Hardware != h.Identity || run.Profile != profile || !run.Complete)) run = null;
        if (run?.Thermal == true || run?.CpuTempMax >= 90 || run?.GpuTempMax >= 85)
            Add("thermal", "Térmica", "Verificar refrigeração antes de aumentar potência", "A medição encontrou temperatura elevada ou sinal térmico do driver. Temperatura isolada não confirma throttling da CPU.", "Compare com os limites do modelo; confira poeira, fluxo de ar, montagem e ventoinhas. Não aplique OC para tentar corrigir aquecimento.", AdvisorActionKind.Navigate, "sensors", MicrosoftPerformance, 1);
        if (run?.Ram >= 85 || h.RamGb is > 0 and <= 8)
            Add("memory", "Memória", "Revisar a pressão de RAM", run?.Ram >= 85 ? $"Uso médio observado: {run.Ram:0}% da RAM." : $"Foram identificados {h.RamGb:0.#} GB de RAM; avalie o consumo na sua carga.", "Feche somente apps escolhidos por você. RAM em cache é útil; esvaziá-la periodicamente não garante mais FPS.", AdvisorActionKind.Navigate, "optimize", MicrosoftPerformance, 8);
        if (h.SystemDriveFreePct is >= 0 and < 15)
            Add("diskspace", "Armazenamento", "Revisar o espaço da unidade do Windows", $"A unidade do sistema tem {h.SystemDriveFreePct:0.#}% livre.", "Revise o que será apagado. O aplicativo apenas abre a limpeza do Windows.", AdvisorActionKind.Settings, "ms-settings:storagesense", MicrosoftPerformance, 10);
        Add("startup", "Sistema", "Enxugar a inicialização com critério", "Aplicativos desnecessários podem disputar RAM, CPU e disco após o login.", "Mantenha segurança, drivers e sincronizações de que precisa; a aba existente permite escolher e desfazer.", AdvisorActionKind.Navigate, "startup", MicrosoftPerformance, 30);
        if (gaming && h.WindowsBuild >= 10240) {
            Add("gamemode", "Jogos", "Ativar o Modo de Jogo", "Perfil Jogos: use a política do próprio Windows durante partidas.", "Não altera gráficos nem garante ganho em toda máquina.", AdvisorActionKind.Tweak, "gamemode", "https://support.xbox.com/en-US/help/games-apps/game-setup-and-play/use-game-mode-gaming-on-pc", 20, true);
            Add("gamedvr", "Jogos", "Desligar captura do Game Bar", "Útil se você não utiliza captura/clipes do Game Bar.", "A gravação/captura do Game Bar será desativada. OBS e outras ferramentas têm suas próprias configurações.", AdvisorActionKind.Tweak, "gamedvr", "https://support.xbox.com/help/friends-social-activity/share-socialize/record-game-clips-game-bar-windows-10", 35, true);
            Add("mouse", "Entrada", "Desligar aceleração do ponteiro", "Pode tornar o movimento do cursor mais previsível; jogos com entrada bruta podem ignorar esse ajuste.", "Muda a sensação do mouse no desktop. Não aumenta FPS nem mede input lag.", AdvisorActionKind.Tweak, "mouse", "https://support.microsoft.com/en-us/windows/hardware/input-devices/change-mouse-settings", 60, true);
        }
        if (h.WindowsBuild >= 22000 && gaming)
            Add("windowed", "Gráficos", "Otimizar jogos DX10/11 em janela", "Recurso do Windows 11 para jogos compatíveis em janela/sem bordas.", "Reabra o jogo após ajustar. A opção pode ser controlada por jogo nas Configurações.", AdvisorActionKind.Tweak, "windowed", MicrosoftGraphics, 40, true);
        Add("graphics", "Gráficos", "Revisar HAGS e GPU por aplicativo", h.Gpus.Length > 1 ? "Múltiplas GPUs detectadas. Escolha a GPU adequada para cada aplicativo nas Configurações." : "O suporte ao agendamento de GPU depende de hardware e driver; um valor no registro não comprova suporte.", "Compare HAGS ligado/desligado na mesma carga quando a opção existir. Não aplicar automaticamente.", AdvisorActionKind.Settings, "ms-settings:display-advancedgraphics", MicrosoftGraphics, gaming ? 45 : 70);
        bool hot = run?.Thermal == true || run?.CpuTempMax >= 90 || run?.GpuTempMax >= 85;
        if (h.OnAc == true && h.Portable == false && profile != AdvisorProfile.Equilibrio && !hot)
            Add("power", "Energia", "Experimentar Alto desempenho", "Desktop conectado à energia: compare este plano sob carga antes de decidir mantê-lo.", "Pode aumentar consumo, temperatura e ruído. Aplique uma mudança por vez e restaure se não houver benefício.", AdvisorActionKind.Tweak, "power", PowerSource, 65, true);
        else
            Add("power-review", "Energia", "Revisar o modo de energia", h.OnAc == false ? "O computador está na bateria; priorize autonomia ou conecte a fonte para comparar desempenho." : "Equilíbrio, condição térmica ou alimentação não confirmada: selecione o modo de energia conforme o uso.", "Máximo desempenho pode consumir mais e aquecer. Não forçamos esse plano neste cenário.", AdvisorActionKind.Settings, "ms-settings:powersleep", PowerSource, 25);
        Add("animations", "Interface", "Reduzir animações de conteúdo", "Opção para quem prefere transições mais imediatas, especialmente em PCs limitados.", "Altera a aparência; não é ganho comprovado de FPS.", AdvisorActionKind.Tweak, "animations", "https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow", profile == AdvisorProfile.Produtividade ? 40 : 80, true);
        Add("uieffects", "Interface", "Reduzir efeitos de menus e controles", "Diminui efeitos visuais usando a configuração de interface do Windows.", "Preferência visual; mantenha se gostar do resultado e restaure quando quiser.", AdvisorActionKind.Tweak, "uieffects", MicrosoftPerformance, 85, true);
        if (run?.Gpu >= 90 || run?.Vram >= 90)
            Add("game-quality", "Teste", "Ajustar a carga gráfica", "Uso alto de GPU/VRAM foi observado. GPU ocupada também pode ser o comportamento esperado.", "Experimente reduzir texturas/resolução ou usar upscaling no próprio jogo. Compare qualidade e fluidez.", AdvisorActionKind.Navigate, "games", MicrosoftGraphics, 15);
        if(run?.Cpu >= 85 && run?.Gpu is < 80)
            Add("cpu-load", "Teste", "Investigar a carga de CPU", "A observação encontrou uso alto de CPU com GPU menos ocupada. Isso é uma indicação para investigar, não uma prova isolada de gargalo.", "Confira tarefas de fundo e repita o teste de CPU/sessão de gargalo; não use overclock como correção automática.", AdvisorActionKind.Navigate, "bottleneck", MicrosoftPerformance, 16);
        Add("drivers", "Manutenção", "Conferir drivers e atualizações", "Use fontes oficiais e avalie correções específicas para seu hardware.", "Driver mais novo não implica ganho garantido; não instale pacotes indiscriminados.", AdvisorActionKind.Navigate, "drivers", MicrosoftPerformance, 75);
        Add("storage", "Armazenamento", "Analisar saúde e desempenho dos discos", "O aplicativo já possui diagnóstico e benchmark por unidade.", "Teste de escrita cria arquivos temporários e gera atividade no disco; escolha quando executar.", AdvisorActionKind.Navigate, "disks", MicrosoftPerformance, 80);
        Add("memory-bios", "BIOS · guiado", "Conferir XMP/EXPO e configuração da memória", h.ConfiguredMemoryMhz is uint speed ? $"WMI reportou configuração de memória de {speed} MHz. Isso sozinho não identifica perfil ou canal ativo." : "Não foi possível confirmar a configuração da RAM pelo sistema.", "Perfis de memória são overclock e dependem de CPU, módulos, placa e BIOS.", AdvisorActionKind.Guide, "memory", AmdGuide, 90, guide: MemoryGuide(h));
        Add("cpu-bios", "BIOS · guiado", "Boost, energia e undervolt por plataforma", $"CPU: {h.Cpu}. Placa: {h.Board}. BIOS: {h.Bios}.", "Sem valores universais de tensão, frequência ou Curve Optimizer. Requer validação e documentação específica.", AdvisorActionKind.Guide, "cpu", h.Cpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) || h.Cpu.Contains("Ryzen", StringComparison.OrdinalIgnoreCase) ? AmdGuide : IntelGuide, 95, guide: CpuGuide(h));
        Add("oc-check", "Estabilidade", "Abrir verificação de estabilidade", "Reutilize os testes e o histórico de erros WHEA/driver existentes após mudanças avançadas.", "Passar em um teste curto não garante estabilidade permanente. Teste de carga exige atenção à temperatura.", AdvisorActionKind.Navigate, "oc", IntelGuide, 96);
        Add("display", "Tela", "Conferir taxa de atualização e VRR", "Confira se o Windows usa a frequência oficialmente suportada pelo monitor.", "Não forçamos overclock de painel ou timings. Compatibilidade de VRR depende do monitor, conexão e GPU.", AdvisorActionKind.Settings, "ms-settings:display-advanced", MicrosoftGraphics, 75);
        return r.OrderBy(a => a.Priority).ToArray();
    }

    public static string MemoryGuide(AdvisorHardware h) =>
        $"Placa: {h.Board}\nBIOS: {h.Bios}\n\n1. Consulte o manual exato da placa ou do notebook e a lista de memória suportada. Salve o perfil atual e as instruções de recuperação do fabricante.\n2. Verifique se CPU, BIOS e módulos suportam XMP/EXPO. A presença de DDR4/DDR5 ou o nome Ryzen não comprova suporte.\n3. Se optar pelo perfil, use o perfil documentado pelo kit/fabricante; evite tensão e timings copiados de outro PC.\n4. Teste CPU/RAM e seu uso real, observando erros e temperaturas. Se houver falhas, restaure o perfil anterior. Não declare estabilidade com base só em 15 minutos.\n\nEsta orientação não altera a BIOS. Frequência WMI não prova perfil ativo nem topologia de canais.";
    public static string CpuGuide(AdvisorHardware h) =>
        $"CPU: {h.Cpu}\nPlaca: {h.Board}\nBIOS: {h.Bios}\n\n1. Confirme suporte no manual do modelo e nas ferramentas oficiais Intel XTU ou AMD Ryzen Master; notebooks/OEM podem bloquear controles.\n2. Meça a referência em configuração atual, conectado à fonte, com refrigeração adequada.\n3. Confira limites de energia/boost e curvas de ventoinha recomendados pelo fabricante. Menus e limites variam por versão da BIOS.\n4. PBO/Curve Optimizer, multiplicador e undervolt só entram se a plataforma suportar. Não usamos um valor negativo, tensão ou potência universal. Uma mudança por vez, com possibilidade de restaurar.\n5. Verifique carga pesada, uso leve, suspensão e seus aplicativos. Registre WHEA, falhas de driver e temperatura. Um teste aprovado é evidência daquele teste, não garantia vitalícia.\n\nO aplicativo não escreve firmware nem desativa proteções. Guarde a chave de recuperação de criptografia em local seguro antes de alterações de firmware; siga o procedimento do fabricante.";

    public static string Compare(AdvisorMeasurement baseline, AdvisorMeasurement current)
    {
        if (!baseline.Complete || !current.Complete) return "Medição incompleta: repita as duas sessões de 30 s.";
        if (baseline.Hardware != current.Hardware || baseline.Profile != current.Profile || baseline.Workload != current.Workload || baseline.OnAc != current.OnAc)
            return "Sessões diferentes: mantenha hardware/BIOS, perfil, fonte de energia e descrição da carga iguais.";
        if (baseline.RestartPending || current.RestartPending) return "Conclua a reinicialização pendente e refaça a referência antes de comparar.";
        if (baseline.OnAc==null || current.OnAc==null) return "Alimentação não confirmada: registre as duas sessões com a condição de energia conhecida.";
        if (baseline.Process != current.Process || baseline.FrameSamples < 25 || current.FrameSamples < 25 || baseline.Fps is not > 0 || current.Fps is not > 0)
            return "Sessões registradas, mas sem FPS suficiente do mesmo processo. Compare os benchmarks existentes para medir desempenho; uso de CPU/RAM não é pontuação.";
        double delta = 100 * (current.Fps.Value / baseline.Fps.Value - 1);
        double variation = Math.Max(baseline.FpsVariationPct ?? 100, current.FpsVariationPct ?? 100);
        return $"FPS observado: {baseline.Fps:0.0} → {current.Fps:0.0} ({delta:+0.0;-0.0;0}%). Variação entre blocos: até {variation:0.0}%. " +
            (Math.Abs(delta) <= Math.Max(3, variation) ? "A diferença pode ser explicada pela variação medida. " : "Repita as passadas antes de atribuir a diferença ao ajuste. ") +
            "Amostragem ETW de apresentação; não mede latência de entrada nem comprova ganho causal.";
    }
}
