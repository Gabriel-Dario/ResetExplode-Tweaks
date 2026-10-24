# Pesquisa aplicada — catálogo v0.18

Consultado em 24/10/2026. Pergunta: quais ajustes podem ser aplicados e revertidos com estado observável e benefício contextual?

## Exemplos examinados

[Winutil, fonte do catálogo](https://github.com/ChrisTitusTech/winutil/blob/main/config/tweaks.json): referência declarativa; nenhum script executado ou importado. Vários undos retornam ao padrão, enquanto este aplicativo guarda o valor efetivamente capturado, incluindo ausência. Hyper Tweaks foi referência de produto; suas promessas de marketing não comprovam ganhos.

## Implementação e evidência

| Família | Decisão e fonte primária |
|---|---|
| 13 efeitos/tempos novos | [SystemParametersInfoW](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow): getters e setters correspondentes. DragFullWindows/MenuShowDelay escrevem em uiParam; demais flags em pvParam. Fades dependem dos controles principais. Aplicativos modernos podem ignorar preferências. |
| Transparência | `EnableTransparency` DWORD 0/1, HKCU Themes\Personalize. [Documentação Microsoft em fonte](https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/develop/settings/settings-common.md). Requer nova sessão para atualização completa; sem FPS medido. |
| EPP e classe 1 | AC 0/50: [PerfEnergyPreference](https://learn.microsoft.com/windows-hardware/customize/power-settings/options-for-perf-state-engine-perfenergypreference). 0 favorece desempenho em modo autônomo; classe 1 não identifica universalmente P/E-cores. |
| Boost | AC 1/0: [PERFBOOSTMODE](https://learn.microsoft.com/windows-hardware/customize/power-settings/options-for-perf-state-engine-perfboostmode). Permite boost dentro dos limites existentes; não escreve tensão/multiplicador. |
| Limites CPU | Máximo 100/99, mínimo 5/100: [máximo](https://learn.microsoft.com/windows-hardware/customize/power-settings/options-for-perf-state-engine-maxperformance) e [mínimo](https://learn.microsoft.com/windows-hardware/customize/power-settings/options-for-perf-state-engine-minperformance). Cartões explicam ambos; Restaurar recupera percentual exato. Não são presets universais. |
| ASPM e resfriamento | AC 0/1 para ASPM; 1/0 para ativo/passivo. [Header oficial WinNT](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/winnt.h) confirma GUIDs/semântica. [Página ASPM](https://learn.microsoft.com/windows-hardware/customize/power-settings/pci-express-settings-link-state-power-management) contém texto genérico inconsistente sobre personality; foi corroborada com o header. Firmware pode ignorar. |
| USB | Suspensão seletiva AC 1/0. [Microsoft recomenda mantê-la habilitada](https://learn.microsoft.com/windows-hardware/drivers/usbcon/usb-selective-suspend); não desabilitamos como suposto ganho universal. Não é polling rate. |
| Energia/backup | [PowerReadACValueIndex](https://learn.microsoft.com/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex) e [PowerWriteACValueIndex](https://learn.microsoft.com/windows/win32/api/powrprof/nf-powrprof-powerwriteacvalueindex). Snapshot inclui plano e valor; restauração não ativa outro plano. |
| Edge startup/background | [StartupBoostEnabled](https://learn.microsoft.com/deployedge/microsoft-edge-policies/StartupBoostEnabled) e [BackgroundModeEnabled](https://learn.microsoft.com/deployedge/microsoft-edge-policies/BackgroundModeEnabled), ambas 0. Pode reduzir processos se antes ativos; pode atrasar abertura/interromper notificações. |
| Edge abas/preload/rede | [SleepingTabsEnabled](https://learn.microsoft.com/deployedge/microsoft-edge-policies/SleepingTabsEnabled)=1, [SleepingTabsTimeout](https://learn.microsoft.com/deployedge/microsoft-edge-policies/SleepingTabsTimeout)=300, [NewTabPagePrerenderEnabled](https://learn.microsoft.com/deployedge/microsoft-edge-policies/NewTabPagePrerenderEnabled)=0, [NetworkPredictionOptions](https://learn.microsoft.com/deployedge/microsoft-edge-policies/NetworkPredictionOptions)=2. Limitações para conta Microsoft pessoal descritas no aplicativo; conferir edge://policy. |

## Conclusão e lacunas

Fontes sustentam alterações de preferências e efeitos específicos, não ganho universal. A hipótese de benefício quando há contenção precisa de teste no PC/carga. [Dicas Microsoft de desempenho](https://support.microsoft.com/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows) apoiam avaliar inicialização, aparência e energia conforme o cenário.

Não foram adicionados HPET/timers genéricos, desligamento de proteções/atualizações, core parking forçado, remoção de serviços em massa ou parâmetros universais de BIOS.

23 verificações incluem ida/volta de todas as 28 novas ações em armazenamento simulado, falha de persistência sem escrita, falha de rollback com backup e mudança de plano. WPF cobre 36 itens, navegação, busca sem acentos e categoria. Leitura nativa identificou 28/28 novas configurações nesta máquina; disponibilidade de um valor não comprova suporte/efeito no hardware. Nenhum setter nativo executado no PC de desenvolvimento; ganho/efeito real por hardware continua sem validação.
