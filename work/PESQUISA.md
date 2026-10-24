# Pesquisa de base — 24/10/2026

## Evidência do projeto

Fonte: [ResetExplode original](https://github.com/GustavoReset/ResetExplode), checkout local main em `ce1d9b3`. App WPF .NET 8, MIT, versão 0.16.0. README e código já incluem sensores, testes, otimização, ajustes Windows, estabilidade/OC e guia de BIOS. A direção é personalização, validação e integração dessas funções, não duplicá-las.

Fonte visual/comercial fornecida: [Hyper Tweaks](https://hyper-tweaks.vercel.app/), página lida no navegador. Ela anuncia FPS, latência, ajustes de registro, térmica e overclock de monitor, mas não apresentou dados brutos/configurações suficientes para reproduzir os ganhos. Inferência: pode inspirar categorias de produto, mas suas alegações não são evidência de benefício dos ajustes.

## Fontes e decisões

| Fonte primária | O que sustenta | Consequência para o projeto |
|---|---|---|
| [Microsoft — powercfg](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options) | Consulta, alteração e exportação de planos de energia; diagnósticos têm condições próprias | Ler estado/capacidade, persistir anterior e verificar retorno. `/energy` é diagnóstico energético, não benchmark de FPS |
| [Microsoft — jogos em janela](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11) | Recurso Windows 11 para jogos DX10/11 compatíveis; muda apresentação para flip model | Não recomendar universalmente no Windows 10 ou confundir com todos os tipos de jogo |
| [Microsoft — working set](https://learn.microsoft.com/windows/win32/memory/working-set) e [EmptyWorkingSet](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset) | Remover páginas do working set não elimina a necessidade futura delas; referência posterior gera page fault | Inferência: reduzir RAM exibida não comprova maior desempenho. Evitar limpeza periódica como recomendação padrão |
| [Intel — guia XTU](https://www.intel.com/content/www/us/en/gaming/resources/overclocking-xtu-guide.html) | Compatibilidade específica; linha de base, mudanças graduais, medição e estabilidade; riscos dependem da plataforma | BIOS/OC guiados e condicionais; sem parâmetros universais ou promessa de estabilidade por teste curto |
| [AMD — Ryzen Master](https://www.amd.com/en/products/software/ryzen-master.html) | Ferramenta oficial de ajuste em hardware suportado; OC pode afetar garantia e operação | Verificar suporte exato e documentação por CPU antes de recomendar PBO/CO/undervolt |
| [WinUtil — SPEC](https://github.com/ChrisTitusTech/winutil/blob/main/SPEC.md) e [guia de tweaks](https://github.com/ChrisTitusTech/winutil/blob/main/docs/src/content/docs/guides/tweaks.mdx) | Exemplo de catálogo declarativo, presets e operações de desfazer; documentação explicita limites de restauração de serviços | Inspirar organização e reversão, sem executar o instalador ou copiar ajustes como se fossem todos adequados. Preservar estado exato é melhor que voltar apenas ao default |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | Sensores de temperatura, carga, clocks, ventoinhas e tensão | Já é dependência do ResetExplode; reaproveitar, mantendo ausência de sensor como ausência de informação |
| [Intel PresentMon](https://github.com/GameTechDev/PresentMon/blob/main/README.md) | Ferramentas de apresentação de frames e medição | Referência para evolução da medição; o app já usa ETW. Não adicionar nova dependência antes de avaliar a medição existente |

## Limites ainda a pesquisar durante implementação

### Fontes adicionais utilizadas na implementação
- [Microsoft — recomendações de desempenho](https://support.microsoft.com/pt-br/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows): base para orientar inicialização, espaço livre, efeitos visuais, energia e manutenção; não é comprovação de ganho numérico.
- [Microsoft — SystemParametersInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow): flags GET/SETCLIENTAREAANIMATION e GET/SETUIEFFECTS usadas nos novos controles reversíveis. GETMOUSE/SETMOUSE também definem a ordem thresholds/aceleração, corrigida no caminho de aplicação do mouse.
- [Microsoft — mouse](https://support.microsoft.com/en-us/windows/hardware/input-devices/change-mouse-settings): função da precisão do ponteiro e configurações do mouse; isso não comprova aumento de FPS.
- [Xbox — captura de clipes](https://support.xbox.com/help/friends-social-activity/share-socialize/record-game-clips-game-bar-windows-10): documenta a função perdida ao desabilitar captura. A rota foi confirmada pelo link de [depreciação do Steps Recorder](https://support.microsoft.com/en-us/windows/apps/steps-recorder-deprecation).

- Cada novo tweak terá fonte e matriz de compatibilidade próprias. Esta pesquisa define a arquitetura; não valida um catálogo ilimitado de ajustes.
- HAGS, ReBAR, perfis de GPU, XMP/EXPO e firmware dependem de SO, driver e plataforma. Informações WMI isoladas podem não provar o estado de um recurso.
- Métricas de latência não são intercambiáveis: frametime, latência de apresentação, input-to-photon e ping medem coisas diferentes.
- Resultados sintéticos não substituem testes do jogo/carga real; controle de variáveis e repetição são necessários para atribuir ganho a uma mudança.
- Overclock de monitor não será aplicado genericamente: não há suporte universal nem ganho garantido inferível só do modelo informado pelo Windows.

## Seleção de modelo

[Documentação oficial de seleção OpenAI](https://developers.openai.com/api/docs/guides/model-selection) diferencia Astra para análise/entregas exigentes e Sol para trabalho técnico com gestão de tempo/custo. Recomendação contextual: Astra alto em pesquisa/arquitetura/revisão; Sol alto na implementação. Não há conversão confiável do percentual de cota em uma garantia de concluir este escopo.
