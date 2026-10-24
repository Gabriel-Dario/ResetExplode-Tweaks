# Plano técnico — otimizações por hardware

## Contrato do produto

Uma aba integrada ao ResetExplode identifica hardware e limitações, orienta um teste, gera recomendações explicadas e permite aplicar/restaurar ações compatíveis. Perfis Jogos, Produtividade e Equilíbrio. BIOS/OC são avançados guiados, conforme decisão de Dário. Nenhuma promessa universal de ganho ou de segurança de overclock.

Fluxo: analisar PC → escolher objetivo → medir referência → revisar recomendações → aplicar individualmente → reiniciar quando necessário → medir novamente → manter ou restaurar. Um diagnóstico por amostragem não deve ser apresentado como benchmark; um teste sintético não deve ser apresentado como FPS real em jogos.

## Arquitetura proposta

- **Snapshot de hardware/capacidades:** dados tipados com origem, horário e ausência explícita. CPU/plataforma, GPU/driver, placa-mãe/BIOS, RAM/configuração, armazenamento, Windows/build e condição de alimentação. Reaproveitar coletores/sensores existentes; evitar WMI pesado a cada atualização da UI.
- **Catálogo de ações:** identificador estável, categoria, descrição, fonte, pré-requisitos, compatibilidade, efeitos colaterais, necessidade de admin/reboot, tipo (aplicável, link para configuração, orientação BIOS), detecção, aplicação, validação e reversão.
- **Regras de recomendação:** lógica separada da UI e dos acessos ao sistema, testável com fixtures. Considerar objetivo, capacidade detectada e medição; dados insuficientes geram orientação/estado desconhecido, não conclusão inventada.
- **Execução com histórico:** salvar estado anterior antes da primeira escrita; ações idempotentes; execução fora da thread da UI; timeout/cancelamento; checagem de resultado e estado; persistir falhas parciais; permitir desfazer uma ação ou conjunto preservando backups que falharam.
- **Sessão de medição:** reutilizar testes e sensores existentes; impedir concorrência entre testes; registrar carga/resolução/preset/duração, temperaturas disponíveis, versão/hardware e reinício pendente. Comparar apenas sessões equivalentes e explicar variação.
- **Apresentação WPF:** preservar tema e padrões existentes. Adicionar aba de recomendações, cartões com estado/evidência/ação e filtros; integrar os controles de tweaks/OC existentes para não haver estados divergentes.

As classes/nomes finais serão escolhidos após traçar as dependências da primeira fatia; isso evita refatorar a aplicação inteira antes de entregar valor.

## Marcos e critérios de aceite

### 1. Fundação e restauração
- Testes de round-trip de valores existentes/ausentes e falha parcial, com armazenamento/comandos simulados.
- Falha ao aplicar ou restaurar nunca anuncia sucesso nem elimina a única cópia do estado anterior.
- Verificar timeout/exit code dos comandos e leitura após escrita.
- Build continua aprovado. Não executar mudanças reais no Windows durante os testes automatizados.

### 2. Diagnóstico e perfis
- Identificar desktop/notebook quando possível; tratar GPU múltipla e informação faltante.
- Perfis Jogos, Produtividade e Equilíbrio mudam prioridades, não fabricam compatibilidade.
- Sensores desconhecidos ou indisponíveis aparecem como tal. Frequência de RAM menor que informação nominal, sozinha, não prova EXPO/XMP desativado.
- Fixtures de Windows 10/11, Intel/AMD, notebook na bateria, GPU integrada/dedicada e ausência de sensores.

### 3. Aba e catálogo inicial de tweaks
- Exibir motivos, estado, aplicabilidade, fonte, reinício necessário e reversão por ação.
- Reutilizar Modo de Jogo, captura em segundo plano, energia e demais ações existentes depois de validar suporte e semântica em documentação atual.
- Revisar HAGS e otimização de jogos em janela por versão do Windows e capacidade; oferecer abrir Configurações quando o suporte não puder ser comprovado.
- Incluir caminhos de inicialização, drivers, energia e armazenamento já presentes no produto; adicionar ajustes úteis somente com efeito/pré-requisitos verificáveis.
- Sem pacote genérico que desative segurança, atualizações, pagefile, timers ou serviços indiscriminadamente para prometer FPS.

### 4. Testes e comparação
- Reutilizar CPU/GPU/memória/disco e captura de sessão existente conforme objetivo; usuário escolhe/inicia carga relevante.
- Linha de base e pós-mudança têm configuração comparável. Cancelamento, falha de sensor e reinício pendente precisam de estados próprios.
- Mostrar métrica, delta e condições; se variação entre passadas puder explicar a diferença, não declarar melhora comprovada.
- Manter teste de estabilidade separado de medição de desempenho e de saúde do hardware.

### 5. BIOS e OC guiados
- Revisar o guia existente: compatibilidade CPU/placa/BIOS, documentação do fabricante e limites de observação.
- Abordar memória XMP/EXPO, boost/energia, térmica e undervolt quando suportados. Não prescrever CO, tensão, clock, TDP ou frequência de monitor universais.
- Botões abrem instruções, ferramentas oficiais ou teste apropriado; não fingem que um clique no Windows alterou firmware.
- Preservar ajustes de GPU existentes, revendo pré-requisitos/erros e restauração; não ampliar OC automático nesta entrega sem nova decisão.

### 6. Revisão e entrega
- Revisar regressões em navegação, persistência e funções já existentes; compilar e publicar em pasta separada.
- Validar interface e estados de operação, inclusive máquina sem sensores/admin/recursos suportados.
- Entregar lista concreta do implementado, testes aprovados, limites e como iniciar/restaurar.
- Não substituir o executável original nem publicar em GitHub automaticamente.

## Pontos de revisão

Ao terminar cada marco: requisito → evidência/teste → resultado → lacuna → próximo passo. Manter continuidade curta. Usar testes proporcionais para regras e efeitos no sistema; evitar testes que apenas repetem texto da UI.

## Handoff de modelo

Pesquisa inicial e arquitetura registradas. **Parar aqui e trocar para GPT-6.1 Sol com raciocínio alto antes do marco 1**, conforme pedido de Dário. A troca ocorre no seletor do aplicativo; não alterar configurações globais como substituto. Novas pesquisas específicas de ações/compatibilidade fazem parte da implementação e devem consultar fontes primárias.
