# Entregas incrementais

Revisão: 29/09/2026. O catálogo v4 tem os 16 clubes do Paulistão e 363 relacionados
nas súmulas de 10–11/01/2026, com cadastros de países e estádios principais. Ele
alimenta o amistoso; as competições estão vazias até a implementação do regulamento
oficial. Saves do campeonato demonstrativo conservam a própria base. O menu oferece
Quick match, Championship, Career e Options; Career
cria um perfil, com simulação de calendário ainda pendente. Um editor local
intermediário está disponível em `/editor/`. As etapas podem ser revisadas com
evidência da implementação. O editor completo e a importação de skins continuam
pendentes. Consulte
[ARCHITECTURE.md](ARCHITECTURE.md), [DATA-FORMAT.md](DATA-FORMAT.md) e
[README-DATABASE.md](README-DATABASE.md).

## A. Base arquitetural

| Etapa | Entrega | Aceite |
| --- | --- | --- |
| A1 | Arquitetura, contratos propostos, roteiro e regras no AGENTS.md | Dependências, propriedade de dados, integração e requisito de skins explícitos; revisão documental e build vigente |
| A2 | Contratos executáveis: schemas, exemplos e validação | Exemplo mínimo válido; erros claros para IDs/referências/versões inválidos; fonte visual separada do core |
| A3 | Domain/Application em C# puro e adaptadores de importação | Quatro clubes e seus jogadores carregados de base externa; nenhuma dependência de Unity/JSON no core |
| A4 | Catálogo na seleção e no amistoso | Seleção por ClubId; escalação de onze sem truncar elenco; dados importados na partida; cancelamento e retorno com liberação dos clones |

A1 está concluída. A2 está implementada no recorte JSON v1 de clubes, jogadores,
vínculos e referências visuais, extensão v2 de sete presets de aparência e v3 de
competições/edições round-robin e v4 com referência histórica, países, estádios,
cadastro dos clubes e biografias/apelidos. Pacotes e skins ficam nas respectivas etapas
futuras. A3 está implementada com Domain/Application isolados,
importador e bootstrap que carrega a base externa no player. A amostra atual tem
16 clubes e 363 jogadores relacionados na abertura, incluindo 23 do São Paulo FC;
não corresponde aos elencos completos. Consulte [HISTORICAL-DATA.md](HISTORICAL-DATA.md).
O JSON é editável por refresh no
Compose, sem recompilar. Os visuais locais do São Paulo continuam compilados.
Nenhum resultado ou temporada foi acrescentado ao catálogo. A4 está implementada
com LineupPlanner, bindings visuais locais e uma sessão que fixa a revisão de cada
amistoso. A integração de resultados identificados pertence a B4.
Não criar assemblies vazias ou serviços fictícios somente para marcar uma etapa.

O editor atual permite cadastrar/editar clubes e jogadores, transferir vínculos,
ajustar posições, altura/peso, atributos e presets, validar, salvar e exportar JSON.
Na v4 inclui apelido, nome completo, nascimento, pé, nacionalidade, metadados de
clubes, países, estádios e referência histórica com fontes. Seleção de equipes
é filtrada por país em Quick match, Championship e Career.
O salvamento incrementa a revisão e o jogo lê a nova base após refresh. Sua prévia
é ilustrativa e suas opções usam o personagem compilado; não incluem upload de
imagens/modelos. Esse recorte adianta parte de D1 sem concluir o marco do editor.

## B. Primeiro marco: competição jogável

| Etapa | Entrega | Aceite |
| --- | --- | --- |
| B1 | Temporada em memória e catálogo importado | IDs estáveis, quatro clubes, regras copiadas da revisão; assets de origem preservados |
| B2 | Calendário de turno único | Três rodadas e seis jogos; cada par uma vez; sem clube duplicado na rodada |
| B3 | Resultado e classificação | Pontos, gols e desempates verificados; resultados inválidos/conflitantes rejeitados; duplicidade inofensiva |
| B4 | Adaptador do motor 3D | Início e fim associados a FixtureId/ExecutionId; placar copiado antes da limpeza; abandono/falha permite tentar novamente |
| B5 | Interface editável no Unity | Calendário -> partida -> retorno à classificação atualizada; sessão sobrevive à troca de telas |
| B6 | Campeonato completo no navegador | Seis jogos concluídos; encerramento correto; verificados repetição, abandono e retorno |

O exemplo usa vitória/empate/derrota com 3/1/0 pontos e ordena por
pontos, vitórias, saldo e gols marcados. Empate completo permanece empate
esportivo; ClubId pode estabilizar a apresentação, sem inventar um campeão único.
Esses parâmetros estão na base de exemplo. Regulamentos oficiais são conteúdo
posterior, com regras verificadas para a edição correspondente.

O escopo escolhido para o menu antecipa C1–C3: o usuário assume um dos quatro
clubes, joga seus três confrontos no motor 3D e os demais jogos são simulados.
O progresso é salvo no navegador, incluindo a revisão da base. Há seis resultados
ao concluir a liga. A implementação de B1–B5 e C1–C3 deve ser validada em conjunto;
testes de regras não substituem o aceite B6 de um campeonato completo no player.

## C. Segundo marco: temporada persistente

| Etapa | Entrega | Aceite |
| --- | --- | --- |
| C1 | Save/load versionado no navegador | Retomar após recarga, preservando base/regras e referências de mídia; falha não corrompe save anterior |
| C2 | Simulação rápida separada do motor 3D | Mesmo contrato de resultado; pontos registrados uma vez; teste reproduzível para a simulação com seed |
| C3 | Fluxo misto | Jogar um confronto e simular outros; salvar e retomar sem perder ou duplicar resultados |

Reprodutibilidade de C2 não implica determinismo da física/animação da partida 3D.

## D. Editor externo e skins da comunidade

Esta frente usa o contrato de A2. O editor local usa ES modules no navegador e
backend Node 22/Ajv 8, separado do Unity e do core, com schemas portáteis. D1 começou
pelo cadastro de clubes/jogadores; competições e regras ainda faltam. A prova
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

Próximo passo recomendado nessa frente: adicionar uma prévia 3D real ao fluxo de
edição e executar D2 com uma única skin de referência compatível. Isso deve
confirmar rig, materiais, troca de uniforme e animações antes de construir o
processamento e a interface de upload. Não substituir D2 por mais opções de
presets. A competição permanece separada do editor e das imagens. D1 continua
parcial até os formulários de cadastro das competições e
regras suportadas.

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

## E. Carreira e conteúdo temporal

O primeiro recorte cria e salva nome/avatar do treinador, clube e mês/ano inicial.
Os avatares são retratos gráficos locais, sem criação de personagem 3D. A data é
registrada como intenção de início; ainda não altera os elencos nem escolhe uma
revisão histórica automaticamente.

O próximo recorte precisa definir vigências de vínculos, equipes e regras, resolver
o conteúdo válido na data escolhida, compor calendários de várias competições e
permitir avançar o tempo. Datas sem cobertura devem ter diagnóstico explícito.
Não copiar elencos atuais para épocas diferentes como se fossem dados históricos.

### Próximos recortes da carreira paulista

1. Completar a cobertura dos elencos de janeiro além dos relacionados da estreia,
   verificar posições e dados biográficos pendentes e criar vigências explícitas.
   `snapshot.date` documenta a observação; não substitui esse resolvedor temporal.
2. Implementar a edição oficial de 2026 com os 16 participantes, formato e datas
   verificados. O motor atual aceita somente ligas round-robin. Antes de habilitar
   a edição, adequar a persistência: o envelope com esta base completa excede o
   limite atual de 384 KiB, mesmo após remover a formatação do JSON.
3. Introduzir o relógio da carreira e agenda diária, com notícias de veículos
   fictícios e resultados vinculados às partidas, sem modificar o catálogo fonte.
4. Acrescentar finanças, propostas, treino e amistosos como estado da carreira.
   Orçamento, reputação e patrocínio cadastrais não executam essas regras hoje.

### Estádio principal e mandos temporários

O cadastro guarda o estádio oficial principal (`club.stadiumId`): Allianz Parque
para Palmeiras e Nabi Abi Chedid para Bragantino. A indisponibilidade não substitui
esse vínculo. A etapa futura introduzirá períodos de reforma e reservas para
eventos; estes poderão gerar receita conforme regras financeiras explícitas.
Quando houver conflito com uma partida, a carreira exigirá escolher outro estádio
disponível. O estádio efetivo será associado ao confronto/save, com validação dos
conflitos de agenda, mantendo o principal no catálogo. Estádios alternativos terão
seus próprios IDs e registros. Nenhuma dessas indisponibilidades é simulada hoje.

Modelos 3D e pacotes visuais de estádios são outra entrega: o ID cadastral precisará
ser associado a um recurso compatível, preservando dimensões lógicas do campo,
câmeras e posições da partida. Os 16 registros atuais usam o estádio 3D genérico.

## Conclusão de cada entrega

1. Implementar apenas o escopo da etapa e atualizar contratos/documentos afetados.
2. Executar verificações significativas para as regras ou integração alteradas.
3. Para código ou recursos compilados, gerar WebGL novo com `./scripts/webgl.ps1`
   e confirmar `soccer-web` saudável. Edições apenas no JSON externo compatível
   seguem a exceção de refresh do AGENTS.md, com validação e teste no navegador.
   Mudanças somente no editor, mantendo o contrato Unity, exigem `npm test`,
   publicação das imagens Compose e verificação do editor e dos serviços saudáveis.
4. Testar no navegador o comportamento afetado e descrever a cobertura real.
5. Informar arquivos alterados e comandos Git restritos à entrega; não executar
   comandos Git sem pedido explícito do usuário.

Pontos de atenção já conhecidos: captura de resultado dependente do ciclo do
motor, troca de UI, material principal substituído pelo uniforme, dependência do
prefab/Avatar atual e erro preexistente de layout XInputController em um fluxo de
controle virtual. Corrigir um problema quando bloquear a etapa, com escopo e
validação explícitos; não assumir que um build comprova ausência de falhas runtime.
