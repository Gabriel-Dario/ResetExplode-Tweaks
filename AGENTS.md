# ResetExplode — desenvolvimento local

- Aplicativo WPF, C#, `net8.0-windows`; preservar tema, navegação e funções existentes. O executável instalado fica na pasta pai; não sobrescrevê-lo durante desenvolvimento.
- Fonte original MIT em `https://github.com/GustavoReset/ResetExplode`. Preservar licença/créditos. Upstream não aceita PRs. Dário autorizou criar repositório próprio ResetExplode-Tweaks; nunca enviar alterações ao upstream por engano.
- Build validado nesta máquina: `dotnet build ResetExplode.csproj -c Release --nologo -v minimal`. Baseline compila com avisos preexistentes em `MainWindow.Oc.cs` e `DiskHealth.cs`.
- Testes de regras/recuperação: `dotnet run --project tests/ResetExplode.Advisor.Tests.csproj -c Release`. Teste WPF: `dotnet run --project tests/UiSmoke/ResetExplode.UiSmoke.csproj -c Release -- --inventory`. Ambos executados; detalhes na continuidade.
- Regras puras em `PerformanceAdvisor.cs`; inventário em `AdvisorHardwareCollector.cs`; nova UI em `PerformanceAdvisorView.xaml/.cs`. `MainWindow(preview: true)` só existe para construir a janela sem iniciar hardware/timers em testes; produção usa o construtor padrão.
- UI principal em `MainWindow.xaml`, navegação/ações em `MainWindow.xaml.cs` e partials. Tweaks legados em `WindowsTweaks.cs`; novos em `TweakCatalog.cs`, I/O nativo em `WindowsTweakStore.cs`, catálogo WPF em `TweakCatalogView.xaml/.cs`. Persistência em `AppSettings.cs`, sensores em `SensorMonitor.cs`.
- Já existem otimização, benchmarks, gargalo e OC/BIOS. Reutilizar essas capacidades; não criar motores concorrentes sem necessidade demonstrada.
- O app exige administrador e pode alterar o PC. Testes de regras/rollback devem usar adaptadores e dados simulados, sem aplicar tweaks, encerrar processos, fazer OC ou carregar stress tests no computador de desenvolvimento por padrão.
- Cada ação nova precisa de compatibilidade, estado atual, efeito colateral, fonte, estado anterior persistido, verificação e reversão quando executável. Falha de rollback deve preservar backup e informar o erro.
- Não tratar ausência de sensor ou de informação de BIOS como confirmação. Não apresentar ganho de FPS, latência ou estabilidade sem medição adequada.
- Trabalho atual e escolhas aceitas: `work/CONTINUITY.md`; plano técnico e critérios: `work/PLANO.md`; pesquisa: `work/PESQUISA.md`. Ler trechos pertinentes ao retomar, sem recarregar todos os documentos a cada ação.
