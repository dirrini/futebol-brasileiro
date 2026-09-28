# Entregas incrementais

Revisão: 28/09/2026. A fundação de importação de clubes/jogadores está implementada.
As etapas podem ser revisadas com evidência da implementação. O editor externo,
o campeonato e a importação de skins ainda não estão implementados. Consulte
[ARCHITECTURE.md](ARCHITECTURE.md), [DATA-FORMAT.md](DATA-FORMAT.md) e
[README-DATABASE.md](README-DATABASE.md).

## A. Base arquitetural

| Etapa | Entrega | Aceite |
| --- | --- | --- |
| A1 | Arquitetura, contratos propostos, roteiro e regras no AGENTS.md | Dependências, propriedade de dados, integração e requisito de skins explícitos; revisão documental e build vigente |
| A2 | Contrato v1 executável: schemas, exemplos e validação | Exemplo mínimo válido; erros claros para IDs/referências/versões inválidos; fonte visual separada do core |
| A3 | Domain/Application em C# puro e adaptadores de importação | Quatro clubes e seus jogadores carregados de base externa; nenhuma dependência de Unity/JSON no core |

A1 está concluída. A2 está implementada no recorte JSON v1 de clubes, jogadores,
vínculos e referências visuais; schemas de competições, pacotes e skins ficam nas
respectivas etapas futuras. A3 está implementada com Domain/Application isolados,
importador e bootstrap que carrega quatro clubes e 44 jogadores no player.
Nenhum resultado ou temporada foi acrescentado ao catálogo. A ponte para usar
esses dados no amistoso existente é a próxima entrega de integração.
Não criar assemblies vazias ou serviços fictícios somente para marcar uma etapa.

## B. Primeiro marco: competição jogável

| Etapa | Entrega | Aceite |
| --- | --- | --- |
| B1 | Temporada em memória e catálogo importado | IDs estáveis, quatro clubes, regras copiadas da revisão; assets de origem preservados |
| B2 | Calendário de turno único | Três rodadas e seis jogos; cada par uma vez; sem clube duplicado na rodada |
| B3 | Resultado e classificação | Pontos, gols e desempates verificados; resultados inválidos/conflitantes rejeitados; duplicidade inofensiva |
| B4 | Adaptador do motor 3D | Início e fim associados a FixtureId/ExecutionId; placar copiado antes da limpeza; abandono/falha permite tentar novamente |
| B5 | Interface editável no Unity | Calendário -> partida -> retorno à classificação atualizada; sessão sobrevive à troca de telas |
| B6 | Campeonato completo no navegador | Seis jogos concluídos; encerramento correto; verificados repetição, abandono e retorno |

Para o protótipo, propor vitória/empate/derrota com 3/1/0 pontos e ordenar por
pontos, vitórias, saldo e gols marcados. Empate completo permanece empate
esportivo; ClubId pode estabilizar a apresentação, sem inventar um campeão único.
Registrar esses parâmetros na base de exemplo. Regulamentos oficiais são conteúdo
posterior, com regras verificadas para a edição correspondente.

Os seis jogos deste marco usam o motor 3D, controlando um lado ou assistindo à IA.
Ao recarregar o navegador, o protótipo perde a temporada; a interface deve informar
isso até a entrega de persistência. Simulação rápida pertence ao marco seguinte.

## C. Segundo marco: temporada persistente

| Etapa | Entrega | Aceite |
| --- | --- | --- |
| C1 | Save/load versionado no navegador | Retomar após recarga, preservando base/regras e referências de mídia; falha não corrompe save anterior |
| C2 | Simulação rápida separada do motor 3D | Mesmo contrato de resultado; pontos registrados uma vez; teste reproduzível para a simulação com seed |
| C3 | Fluxo misto | Jogar um confronto e simular outros; salvar e retomar sem perder ou duplicar resultados |

Reprodutibilidade de C2 não implica determinismo da física/animação da partida 3D.

## D. Editor externo e skins da comunidade

Esta frente usa o contrato de A2. O editor é uma aplicação separada; a tecnologia
da interface será escolhida na sua implementação. D1 pode começar após A3. A prova
de skins D2 deve preceder a interface completa de upload, para confirmar o contrato
visual com o motor real.

| Etapa | Entrega | Aceite |
| --- | --- | --- |
| D1 | Cadastro e exportação de clubes, jogadores, competições e regras suportadas | Alterar dado no editor, exportar e importar no jogo sem recompilar; IDs preservados |
| D2 | Template de autoria e prova técnica de skin completa | Geometria e texturas próprias no rig do jogo; rosto preservado com kits de dois clubes; corrida, condução, passe, chute, lateral e goleiro verificados |
| D3 | Perfil e worker de processamento local | Receber FBX/texturas do template, validar e compilar pacote WebGL com Unity ativado; erros localizáveis; nova tentativa preserva revisão válida |
| D4 | Upload, prévia processada e associação pelo editor | Autor envia fonte compatível, acompanha processamento, revisa animações e associa SkinId/revisão a PlayerId sem configurar a skin manualmente no Unity |
| D5 | Exportação e importação dos recursos gráficos | Base e skin transferidas para uma instalação limpa compatível; testar dependência ausente, revisão antiga e fallback |
| D6 | Teste da comunidade no WebGL | Exemplo de skin personalizada, como uma representação de Messi, utilizável em partida; atributos e física permanecem os do cadastro; medir 22 skins distintas e confirmar limites do perfil |

**O primeiro editor utilizável precisa concluir D1 a D6.** Um seletor de presets,
um retrato PNG ou uma textura sobre o modelo padrão não substituem o suporte a
modelos personalizados compatíveis solicitado pelo usuário. A prova técnica e
os limites do perfil em D2/D3 orientam a validação; D6 confirma os limites antes
da liberação do fluxo para a comunidade. D2 verifica também referências/offsets
de posicionamento da bola e a fase dos eventos de contato nas animações.

Modelos precisam seguir o template/perfil. GLB e outros rigs podem ganhar novas
rotas após sua validação. Um arquivo rejeitado deve receber diagnóstico no editor;
não prometer converter automaticamente qualquer personagem ou criar um modelo
3D a partir de uma fotografia.

Uma galeria pública com contas e worker hospedado é uma etapa posterior. A rota
local deve permitir importar, preparar e compartilhar pacotes como arquivos.

## Conclusão de cada entrega

1. Implementar apenas o escopo da etapa e atualizar contratos/documentos afetados.
2. Executar verificações significativas para as regras ou integração alteradas.
3. Gerar WebGL novo com `./scripts/webgl.ps1` e confirmar `soccer-web` saudável.
4. Testar no navegador o comportamento afetado e descrever a cobertura real.
5. Informar arquivos alterados e comandos Git restritos à entrega; não executar
   comandos Git sem pedido explícito do usuário.

Pontos de atenção já conhecidos: captura de resultado dependente do ciclo do
motor, troca de UI, material principal substituído pelo uniforme, dependência do
prefab/Avatar atual e erro preexistente de layout XInputController em um fluxo de
controle virtual. Corrigir um problema quando bloquear a etapa, com escopo e
validação explícitos; não assumir que um build comprova ausência de falhas runtime.
