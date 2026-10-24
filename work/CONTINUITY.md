# Continuidade — ResetExplode / otimização por hardware

Atualizado em 24/10/2026.

## Pedido atual e entrega v0.18
- Dário informou que v0.17 tinha poucas ações novas e pediu catálogo real mais amplo, pesquisa e repositório próprio GitHub. A publicação desse repositório está autorizada; não publicar no upstream.
- Implementadas 36 ações executáveis (8 anteriores + 28 novas): energia 9, jogos/entrada 5, interface 16, Edge 6. Catálogo pesquisável com filtros, estado, fonte, seleção sugerida por perfil, revisão rolável antes do lote e restauração individual/conjunta.
- Novos arquivos: `TweakCatalog.cs`, `WindowsTweakStore.cs`, `TweakCatalogView.xaml/.cs`; integração em MainWindow e testes. Fonte original preservada em README-UPSTREAM e licença MIT intacta.
- Snapshot declarativo inclui escopo/plano e valor; persistência antes da escrita. Mudança de plano não sobrescreve snapshot; restauração de plano inativo não o ativa. Estado de política escrito não equivale a efetividade no hardware/browser.
- 23 testes de regras/recuperação aprovados; todos os 28 novos ajustes percorrem aplicar/desligar/restaurar no adaptador simulado. WPF: catálogo 36, navegação, busca sem acento, categoria, prévia sem mutações. Leitura nativa: 28/28 configurações novas disponíveis nesta máquina. Nenhum setter/OC/stress aplicado no PC.
- Pesquisa em `work/PESQUISA-TWEAKS.md`. Publicação automática upstream substituída por disparo manual; CI verifica código/testes. Inventário, capturas e logs locais ignorados no Git.
- Pacote v0.18 gerado em `C:\Users\Dário\ResetExplode\build-otimizacoes-v0.18\ResetExplode.exe`, 197158743 bytes; LICENSE, LEIA-ME-TWEAKS.md e SHA256SUMS.txt. Hash e verificações em `work/VALIDACAO-TWEAKS.json` (local, ignorado). Originais anteriores preservados.
- Criado e enviado `https://github.com/Gabriel-Dario/ResetExplode-Tweaks`, privado (pergunta opcional sem resposta). Commit funcional `cdcad8bdadfc8c038755692d1b209052f9fc5d8e` confirmado remotamente na main; branch local rastreia origin/main. Remoto original preservado como upstream. A cópia era shallow e o primeiro push falhou por objeto ancestral ausente; `git fetch --unshallow upstream main` recuperou o histórico e o envio seguinte passou.
- CI adicional **aprovada** em `https://github.com/Gabriel-Dario/ResetExplode-Tweaks/actions/runs/40142965094`: build, 23 testes e WPF passaram no Windows hospedado do GitHub. Testes locais/pacote também aprovados. Não houve release remota automática; publicação futura é manual e versionada. Próximo uso: abrir build v0.18 → RESET FERRAMENTAS → Tweaks · catálogo; escolher/aplicar após revisão e medir uma mudança por vez para atribuir efeito.

## Objetivo e decisões aceitas
- Evoluir ResetExplode com tweaks e uma aba de otimizações baseada em hardware e testes, preservando a UI/funcionalidades existentes.
- Suportar diferentes PCs, não apenas o computador de Dário.
- Perfis: jogos, produtividade e equilíbrio.
- Mudanças reversíveis por padrão; BIOS e overclock avançados guiados, com pré-requisitos e confirmação no momento pertinente.
- Referência fornecida: `https://hyper-tweaks.vercel.app/`; basear-se no mercado de tweaks de modo mais amplo e verificar alegações.
- Ser conciso, pesquisar fontes primárias, trabalhar por etapas e revisar evidência e regressões.

## Pedido mais recente sobre modelos
- Dário pediu: indicar o ponto de trocar para Sol e parar nesse ponto.
- Ponto escolhido: após pesquisa inicial/arquitetura e antes de implementar comportamento do aplicativo.
- Recomendação: GPT-6 Astra alto para decisões exigentes e revisão; GPT-6.1 Sol alto para implementação/testes. O agente não controla o seletor desta conversa e não alterou o modelo nem a configuração global.
- Pausa encerrada: Dário respondeu “ok, pd começar”; a implementação abaixo foi autorizada e concluída.

## Estado observado
- Pasta `C:\Users\Dário\ResetExplode` continha `ResetExplode.exe` e `LEIA-ME.txt`; não havia checkout Git.
- Fonte clonada sem alterar esses arquivos para `C:\Users\Dário\ResetExplode\source`, de `https://github.com/GustavoReset/ResetExplode`, branch main, HEAD abreviado `ce1d9b3`.
- WPF .NET 8, versão declarada 0.16.0, licença MIT. Upstream informa que não aceita PRs; trabalho local autorizado.
- SDKs 8.0.425, 9.0.303 e 10.0.401 disponíveis.
- Build baseline `dotnet build ResetExplode.csproj -c Release --nologo -v minimal`: aprovado, 0 erros, 16 avisos emitidos (alguns repetidos pelo projeto temporário WPF). Nenhum tweak, benchmark de carga ou OC foi executado.
- Inspecionados manifesto, README, licença, fluxo de navegação, catálogo de tweaks, persistência, otimizador, sensores e guia OC. Não é necessário REA para ler o código-fonte completo disponível.
- Site Hyper Tweaks lido no navegador: página de marketing com promessas de ganhos e segurança; sem metodologia suficiente apresentada na página para validar essas promessas.

## Achados que afetam a implementação
- `WindowsTweaks.RestoreAll` captura falhas e depois apaga todo `TweakBackup`: pode perder o estado original de uma restauração que falhou. Corrigir antes de ampliar o catálogo.
- `PowerCfg` espera com prazo, mas requer revisão de timeout/exit code e confirmação do efeito antes de anunciar sucesso.
- `Optimizer.CleanMemory` esvazia working sets/standby. Mais RAM indicada como livre não comprova melhor desempenho; não virar regra universal do recomendador.
- `BuildOcGuide` usa sugestões genéricas de EXPO/PBO/Curve Optimizer e limites sem validar plataforma/modelo/BIOS de forma suficiente para um produto multi-PC. Revisar textos e critérios, sem preset universal de tensão/clock/CO.
- Já há WindowsTweaks, RESET OTIMIZA, RESET OC, sensores, WHEA, benchmarks CPU/GPU/RAM/disco, overlay e gargalo: evoluir/reutilizar.

## Arquivos criados nesta etapa
- `AGENTS.md`, `work/PLANO.md`, `work/PESQUISA.md`, este registro.
- Na etapa inicial de planejamento, nenhum arquivo funcional foi modificado. A implementação posterior está registrada abaixo; o executável original foi preservado.

## Marcos executados após a troca / autorização
1. Ler PLANO e os trechos de código do primeiro marco.
2. Proteger com testes a restauração parcial e a preservação do backup; corrigir execução/verificação de ações.
3. Implementar modelos de diagnóstico/recomendação e a aba conforme o plano, em fatias verificáveis.
4. Entregar build separado e evidência de testes; revisão crítica final pode voltar a Astra se Dário desejar.

## Implementação autorizada e progresso
- Dário respondeu “ok, pd começar” após a pausa de modelo. A pausa anterior está revogada; implementação e verificações em andamento nesta sessão.
- Implementados inventário, regras por perfil, nova aba WPF, observação de 30 s, referência/comparação/histórico/exportação, novas ações de efeitos visuais e navegação para recursos existentes.
- Corrigidos backup/rollback parcial, ordem de persistência antes da escrita, retry após falha de persistência, validação de tipos do registro, ordem SPI_SETMOUSE e timeout/exit code do powercfg.
- Catálogo/controles antigos compartilham estado; guarda de concorrência evita ajustes/OC durante medições. Guia BIOS/OC sem presets universais. Recursos de logo usam URI de assembly explícita, validada no teste de janela hospedada.
- Suíte de regras com 17 verificações aprovada, sem escrita no registro real. WPF: carregamento, troca de perfil, construção da janela principal e navegação aprovados; imagens em `work/`, inventário real lido sem iniciar sensores/drivers.
- Publicação v0.17 autocontida/single-file concluída em `C:\Users\Dário\ResetExplode\build-otimizacoes-v0.17\ResetExplode.exe`, 197109591 bytes. Versão e SHA-256 verificados; LICENSE, LEIA-ME-OTIMIZACOES.md e SHA256SUMS.txt acompanham o executável.
- Verificação final após os últimos ajustes: 17 testes de regras/recuperação aprovados, WPF/perfis/navegação aprovados, imagens revisadas, publish aprovado e diff sem erros. Detalhes em `work/VALIDACAO.json`, `tests-final.log`, `ui-smoke.log` e `publish-final.log`.
- Não foram aplicados tweaks/OC reais nem executados stress tests neste PC. Não há medição de ganho real de FPS ou validação exaustiva de hardware/BIOS. Regras usam compatibilidade, dados ausentes explícitos e orientação guiada.
- Próximo uso: fechar a versão antiga, abrir o novo EXE, acessar RESET FERRAMENTAS → Seu PC · ajustes, analisar e escolher perfil. Concluir reinícios pendentes antes de salvar referência. Não há pendência de implementação necessária para usar esta entrega; novas ampliações devem partir deste estado.
