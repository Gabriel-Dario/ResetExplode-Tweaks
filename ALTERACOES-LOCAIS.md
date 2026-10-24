# ResetExplode v0.18 — tweaks e otimizações por hardware

## Catálogo v0.18

Agora há 36 ações executáveis (28 novas): 9 de energia, 5 de jogos/entrada, 16 de interface e 6 de atividade do Edge. Em **RESET FERRAMENTAS → Tweaks · catálogo**, aplique/restaure cada item ou selecione sugestões e revise o conjunto antes de executar. A revisão em lote tem rolagem e efeitos colaterais; os ajustes avançados de energia ficam fora das sugestões automáticas.

`TweakCatalog.cs` define os novos ajustes; `WindowsTweakStore.cs` contém APIs/registro; armazenamento injetável testa persistência e recuperação sem alterar o Windows. Originais de energia incluem GUID do plano. As fontes estão no aplicativo e em `work/PESQUISA-TWEAKS.md`. Build separado em `build-otimizacoes-v0.18/ResetExplode.exe`.

O restante desta página registra o diagnóstico entregue na v0.17. A limitação antiga de ações apenas individuais aplica-se à aba de diagnóstico; o catálogo v0.18 também oferece lote revisado.

Cópia local modificada do projeto MIT de GustavoReset. O executável original na pasta pai foi preservado. Esta versão fica em `build-otimizacoes-v0.17/ResetExplode.exe`.

## Usar

1. Feche a outra versão do ResetExplode e abra o executável desta pasta. O app mantém a exigência de administrador da base original.
2. Abra **RESET FERRAMENTAS → Seu PC · ajustes** e clique em **Analisar PC**.
3. Escolha **Jogos**, **Produtividade** ou **Equilíbrio**. Cada cartão explica motivo, efeito colateral, fonte e ação. Algumas recomendações abrem uma ferramenta/configuração ou orientação; outras aplicam um ajuste reversível.
4. Para comparar, conclua reinícios pendentes, descreva a carga com aplicativo/cena/resolução/preset e use **Observar 30 s**. Não há carga de stress criada por esse botão. Para observar jogo, deixe-o em primeiro plano durante a captura.
5. Salve a referência, aplique uma mudança e repita a mesma carga. Use os benchmarks e testes de estabilidade já existentes quando forem mais adequados ao objetivo.
6. Use **Restaurar** no cartão ou **Restaurar ajustes** para voltar ao estado salvo. Se uma reversão falhar, o backup permanece e a mensagem indica a falha.

**Aplicar** é uma ação imediata e individual. Não há botão que aplique um conjunto inteiro de tweaks automaticamente. Guias de BIOS não escrevem firmware. A aba OC existente pede confirmação antes dos ajustes avançados de GPU.

## O que mudou

- Inventário de CPU, GPUs, RAM/configuração, placa/BIOS, Windows, alimentação e espaço disponível. Dados ausentes ficam explícitos.
- Recomendador por perfil e condições medidas: energia, gráficos/VRAM, RAM, térmica, inicialização, drivers, armazenamento, tela e BIOS.
- Reutilização dos ajustes existentes; novos controles reversíveis para animações de conteúdo e efeitos de interface.
- HAGS e seleção de GPU tratados como orientação condicionada a suporte no Windows/driver, sem forçar HAGS pelo recomendador.
- Observação de 30 s com sensores e FPS ETW quando disponíveis; referência, comparação, histórico e exportação JSON local.
- Restauração corrigida: falhas parciais conservam o backup; estado anterior salvo antes da escrita; tipos inesperados do registro bloqueiam a mudança.
- Execução de `powercfg` verifica saída e timeout; preferências são salvas por substituição de arquivo temporário.
- Guia BIOS/OC revisado sem valores universais de tensão/clock/Curve Optimizer. Teste sem erros não é chamado de garantia de estabilidade.
- Bloqueio de mudanças de ajuste/OC durante medições ou outros testes e sincronização dos controles de tweaks antigos/novos.

## Como interpretar resultados

- Uso de CPU/RAM é diagnóstico, não pontuação de desempenho. Mais memória exibida como livre não comprova melhora.
- FPS vem do mecanismo ETW existente do app. Depende de aplicação/API e pode ficar indisponível. Jogos sem apresentação DXGI/D3D9 compatível podem não produzir dados.
- As sessões precisam do mesmo hardware/BIOS, perfil, alimentação, processo e descrição de carga. Reinício pendente, poucos dados ou sessões diferentes impedem a comparação de FPS.
- Variação entre blocos é apresentada como referência descritiva; não é um teste estatístico de significância. Mesmo uma diferença grande precisa de repetição e controle de variáveis antes de ser atribuída ao tweak.
- Nomes/configurações informados por WMI não comprovam XMP/EXPO, canal de memória ou compatibilidade de OC. Guias exigem o manual do modelo e validação prática.
- Nenhuma promessa de ganho de FPS ou latência acompanha os ajustes. Overclock de monitor e parâmetros universais de BIOS não são aplicados automaticamente.

## Persistência e privacidade

A versão usa a pasta de configurações já existente `%AppData%\ResetExplode`, preservando preferências anteriores. Não use duas versões simultaneamente. Os relatórios são salvos onde você escolher, sem envio automático. Contêm nomes de hardware e a descrição da carga digitada; revise antes de compartilhar.

## Verificação de desenvolvimento

```powershell
dotnet run --project tests/ResetExplode.Advisor.Tests.csproj -c Release
dotnet run --project tests/UiSmoke/ResetExplode.UiSmoke.csproj -c Release -- --inventory
dotnet build ResetExplode.csproj -c Release --nologo -v minimal
```

Os testes de escrita usam adaptadores em memória e subprocessos de teste. O teste WPF renderiza a interface e valida navegação/perfil sem iniciar sensores/drivers nem aplicar mudanças do sistema. `--inventory` acrescenta leitura real de hardware. Testes completos de OC, estabilidade e ganhos reais em diversos PCs não foram realizados nesta entrega.

Fontes e decisões em `work/PESQUISA.md`; plano em `work/PLANO.md`; estado e verificações em `work/CONTINUITY.md`.
