# ResetExplode Tweaks · v0.18

Evolução independente do [ResetExplode de GustavoReset](https://github.com/GustavoReset/ResetExplode), sob [MIT](LICENSE). Preserva histórico/créditos; não é uma versão oficial do autor original. Documentação original em [README-UPSTREAM.md](README-UPSTREAM.md).

Aplicativo Windows x64 WPF/.NET 8 com diagnóstico e **36 ações executáveis, 28 novas nesta versão**.

| Categoria | Ações | Exemplos |
|---|---:|---|
| Energia | 9 | EPP, boost, limites da CPU, resfriamento, PCIe e USB |
| Jogos e entrada | 5 | Modo de Jogo, Game DVR, janela, HAGS e mouse |
| Interface | 16 | Animações, transparência, sombras, arraste e menus |
| Navegador em segundo plano | 6 | Edge: inicialização, background, suspensão de abas, preload e rede |

Abra **RESET FERRAMENTAS → Tweaks · catálogo**. Busque ou filtre categoria; cada ação mostra estado, efeito colateral e documentação, com **Aplicar ajuste** e **Restaurar original**. **Selecionar sugeridos** apenas marca candidatos do perfil Jogos/Produtividade/Equilíbrio; **Revisar e aplicar seleção** mostra todos os itens antes da execução. Energia avançada não entra automaticamente nas sugestões. Também há restauração de todos os itens com backup.

Para inventário e comparação de uso/temperatura/FPS, use **Seu PC · ajustes**. BIOS/OC ficam nas ferramentas/guias existentes, sem valores universais de clock/tensão.

## Limites e recuperação

- Original salvo antes de escrever; verificação por releitura. Falha de restauração mantém o backup. Dados em `%AppData%\ResetExplode\settings.json`; não use duas versões simultaneamente.
- Energia altera valores **AC (tomada)** do plano atual. O snapshot inclui GUID e valor; restaurar outro plano não o ativa. Se trocar de plano, restaure antes de reaplicar o mesmo item.
- Firmware, driver, Windows e políticas de organização podem limitar o efeito. HAGS exige suporte no Windows/driver. Escrever uma preferência não prova ganho de desempenho.
- Edge pode mostrar “Gerenciado”; algumas políticas não se aplicam à conta Microsoft pessoal, conforme descrito nos cartões. Reinicie o navegador e confira `edge://policy`.
- Efeitos visuais alteram a interface, não o renderizador dos jogos. Mais RAM livre não comprova mais FPS. Compare a mesma carga antes/depois.
- Ajustes novos não desativam Defender, UAC, VBS ou Windows Update. Suspensão seletiva USB segue a recomendação Microsoft de mantê-la habilitada.
- Aplicação/reversão testadas em armazenamento simulado; validação nativa nesta máquina foi somente de leitura. Não houve medição de ganho nem aplicação de tweaks no PC de desenvolvimento.

## Compilar e verificar

Windows com SDK .NET 8 ou superior:

```powershell
dotnet build ResetExplode.csproj -c Release
dotnet run --project tests/ResetExplode.Advisor.Tests.csproj -c Release
dotnet run --project tests/UiSmoke/ResetExplode.UiSmoke.csproj -c Release
dotnet publish ResetExplode.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o publish
```

Teste WPF fora da tela, sem iniciar sensores/drivers nem aplicar tweaks. Inventários, logs e capturas locais não são versionados. A base ainda emite avisos preexistentes em OC/DiskHealth.

Referências: [pesquisa do catálogo](work/PESQUISA-TWEAKS.md), [arquitetura/medição](work/PESQUISA.md), [continuidade](work/CONTINUITY.md).
