# Editor de base — contrato de interação

Esta aplicação local tem público de autoria/teste do Futebol Brasileiro, idioma
pt-BR e uso principal em desktop. É um editor de rascunho da base inteira. Consulte
`../../ARCHITECTURE.md`, `../../DATA-FORMAT.md` e `../../DESIGN.md` para as
fronteiras de domínio, versão portátil e referência visual. A API do editor valida
o documento integral e exige ETag; o jogo importa a base por uma fronteira própria.

## Proprietários canônicos

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
| --- | --- | --- | --- | --- |
| Form | `ui.js` field/select/textarea + model.js validateDocument + history-model.js validateHistory + competition-rules.js validateCompetitions | DATA-FORMAT.md e este contrato | Fichas de jogador, clube, estádio, país, referência, campeonato e edição | Erro textual associado e foco no primeiro campo inválido |
| Select/Listbox | `ui.js` select | Este contrato | Nativo; geometria do popup do SO aceita, labels pt-BR | Teclado e popup |
| Scrollbar | `styles.css` baseline global | DESIGN.md | Gutters na lista | Track/thumb/hover/active e forced-colors |
| Toast | `app.js` renderFeedback/renderStatus + ui.js announce | Este contrato | Success/warning/error/info persistentes | Região live e mesma linguagem nos dois cadastros |
| CRUD | `app.js` createRecord/removeRecord/save + records.js | API ETag e este contrato | Rascunho da base; publicação explícita única | CRUD, referências, conflito, sucesso mantém seleção |
| Dialog | `ui.js` showDialog + dialog HTML nativo modal | Este contrato | Criação e confirmação | Escape/cancelar, foco contido e restaurado |
| Dataset navigation | `app.js` renderList/writeUrl | Este contrato | Página local de dez registros | Busca/clube/país/id/aba em URL, limite de página |
| Date input | `ui.js` field + history-model.js validDate | DATA-FORMAT.md e este contrato | Texto ISO AAAA-MM-DD, sem popup de calendário | Data real, anos 0001–9999, nascimento até a referência |

Não há biblioteca/framework frontend ou fluxo web anterior a reutilizar. Clube e
país, estádio e jogador compartilham os proprietários acima. O fluxo irmão para comparar visual e
comportamento é a ficha do outro tipo de cadastro. Tabelas de seleção em lote,
permissões e login não se aplicam a esta aplicação local.

## Rascunho, ações e estados

- Digitar altera somente o rascunho em memória. Navegar entre clubes, jogadores,
  seções e filtros mantém todas as edições; nenhuma troca interna descarta campos.
- “Salvar alterações” valida e envia a base inteira uma vez; a API incrementa a
  revisão. Controles de edição ficam bloqueados durante o envio. Só a confirmação
  da API permite mostrar “Alterações salvas”. O sucesso mantém a ficha selecionada.
- “Adicionar jogador/clube” abre o mesmo diálogo e cria IDs aleatórios uma vez.
  O novo cadastro fica selecionado na página de lista correspondente. Novo jogador
  começa com valores demonstrativos editáveis e aparência padrão explícita.
- Excluir exige diálogo que nomeia o cadastro e explica o efeito. A exclusão ainda
  é um rascunho reversível por “Descartar alterações” antes de salvar. Jogadores
  perdem vínculo e perfil junto ao cadastro; clubes com jogadores não são excluídos.
  A base precisa manter ao menos um clube e um jogador.
- IDs são somente leitura. Os quinze atributos, medidas, posições naturais,
  vínculo ao clube e aparência padrão têm campos próprios. Skin não suportada não
  é substituída automaticamente por aparência embutida.
- Exportar baixa exatamente o rascunho visível, incluindo alterações não salvas,
  sem publicar nada. O nome usa a revisão carregada, não uma revisão inventada.
- “Descartar alterações” exige confirmação e restaura o último snapshot carregado.
  Sair/recarregar com rascunho usa exclusivamente beforeunload do navegador; diálogos
  de produto nunca usam alert/confirm/prompt.
- Falha inicial mantém botão “Tentar novamente”. Falhas de gravação mantêm todos
  os dados; não existe retry automático. Timeout indica resultado não confirmado.
  ETag protege contra repetir a gravação sobre uma revisão que pode ter sido salva.
- Conflito mostra mensagem persistente, exportação disponível e carregamento da
  versão atual com confirmação de descarte. Nunca há sobrescrita forçada silenciosa.
- Nenhum rascunho é gravado em localStorage. Fechar a página sem salvar descarta-o.

## Navegação, formulários e acessibilidade

Listas carregam o catálogo inteiro, limitado pelo contrato de 1 MiB; navegação
visível é paginada de dez itens e a lista possui seu próprio limite de rolagem.
Filtros são locais, ignoram acentos/caixa sem alterar os nomes armazenados, respeitam
composição IME e têm limpeza explícita. Página é ajustada após filtro/exclusão.
O formulário usa rolagem natural da página, inclusive nas seções longas.

Busca, clube, página, seção e item escolhido ficam nos parâmetros da URL por
replaceState: cliques no editor não acrescentam dezenas de entradas ao histórico.
A URL preserva a visão quando a página é aberta novamente, mas não o rascunho.

Labels de campo são reais e erros apontam aria-describedby/aria-invalid. Posições
usam checkboxes nativos. Selects são nativos, com popup e interação do sistema
operacional aceitos. Atributos usam entrada numérica precisa e meter complementar.
Formulários desativam balões de validação nativa e usam os erros do aplicativo.
Dialog modal nativo oferece foco contido, fundo inerte, Escape e cancelamento;
exclusão/descartar iniciam no botão Cancelar. Botões de ação permanecem com tamanho
estável durante salvamento. Todas as ações têm foco e hover explícitos.

A prévia de aparência é SVG autorado e **ilustrativa**, rotulada como tal. Cores e
estilos são aproximados. O uniforme desenhado é neutro; o resultado real é conferido
no jogo. Faixa/acessório de meia é separado da meia principal controlada pelo kit.
Importar modelos e texturas continua fora deste recorte; os campeonatos e edições têm formulários próprios.

## Verificação

Cobrir CRUD de ambos os tipos, vínculo/free agent, posições múltiplas, todos os
atributos, aparência, ID preservado, salvamento e refresh do jogo; erro de campo,
falha de rede, conflito em duas sessões, descarte/cancelamento, zero resultados,
paginação, teclado/dialog/select e viewport estreito. O auditor estático não
substitui essas verificações. Evidências da execução ficam em Logs, fora das fontes.

## Referência histórica e cadastros v4

- Bases v4 exibem a faixa “Retrato da base”, sempre com data, título e alcance
  lidos do documento. “Relacionados para partidas” não é apresentado como elenco
  completo. A faixa dá acesso à referência, às limitações e às fontes da pesquisa.
- Países, estádios e referência aparecem na navegação apenas em v4. Abrir uma base
  v1–3 mantém os controles anteriores; o editor não cria uma data histórica, fontes
  ou localizações presumidas para promovê-la automaticamente. A migração exige
  autoria e validação explícitas do documento v4 fora deste fluxo.
- Países usam código de duas letras como identidade permanente. Estádios e fontes
  recebem IDs novos uma vez; renomear não muda vínculos. Excluir país com referências
  ou estádio vinculado a um clube é bloqueado com explicação. É permitido ficar sem
  estádios; a base precisa conservar um país e uma fonte.
- Na ficha do jogador, `name` permanece como nome cadastrado, `fullName` guarda
  o nome completo e `nickname` guarda o apelido. Listas e título usam apelido quando
  informado, com fallback para nome. A busca considera os três, ignorando acentos
  e caixa apenas na comparação. O apelido vazio remove a propriedade opcional.
- Nascimento, nacionalidade, pé preferido, capacidade e parâmetros opcionais
  começam vazios quando desconhecidos. Limpar um campo opcional omite a propriedade;
  não cria `null`, zero ou um valor factual fictício. Zero explicitamente digitado
  em orçamento ou torcida continua sendo um valor conhecido. Não há correção
  silenciosa de caixa, datas ou conteúdo pesquisado.
- Orçamentos usam unidades inteiras da moeda ISO de três letras e exigem moeda
  quando presentes. Reputação, torcida, orçamento e patrocínio são identificados
  como parâmetros editáveis de simulação. A carreira usa transferBudget como caixa inicial e monthlyWageBudget como custo mensal, preservando zeros explícitos; campos ausentes usam defaults declarados no formulário.
  Capacidade e vínculo de estádio não trocam o cenário 3D compilado.
- Clubes e estádios compartilham o filtro por país. A referência é um formulário
  único, sem lista lateral nem exclusão. Fontes têm título e URL HTTP(S), sem
  extração automática; o editor não atesta o conteúdo desses endereços.
- Textareas crescem com o conteúdo e usam a rolagem da página, com os mesmos
  tokens, labels e mensagens dos demais campos. Nas telas estreitas, a navegação
  quebra linhas e as fichas mantêm acesso a todas as ações.
- As novas telas participam do mesmo rascunho, descarte, exportação e ETag global.
  Editar uma fonte ou estádio não publica parcialmente nem muda a revisão local.
  Erros de campos conduzem à tela e ao controle correspondente.

Testes automatizados de histórico cobrem datas e limites, omissão versus `null`,
referências de exclusão, rascunho independente, IDs/proveniência na exportação,
apelidos, HTML escapado e compatibilidade v1–3. O teste real no navegador deve
cobrir os novos diálogos, país como filtro, formulário longo, confirmação de
exclusão e gravação/descartar/conflito do rascunho integral.

## Campeonatos, edições e calendário v5

- Campeonatos e Edições reutilizam lista, busca, paginação, formulários e diálogo
  dos demais cadastros. Campeonato identifica a competição; edição guarda os
  participantes, regras e calendário. Criar uma edição seleciona seu campeonato,
  gera um ID estável e abre a ficha para completar antes de salvar.
- Uma liga pode ser criada em bases v1–2 com promoção explícita ao contrato v3,
  sem inventar referência histórica. O formato Paulista 2026 requer base com
  países/estádios e promove a revisão de formato a v5 ao confirmar sua escolha.
- A escolha dos participantes usa checkboxes nativos. Nomes são literais e IDs
  preservados. Excluir campeonato com edições ou estádio usado em um confronto
  é bloqueado com orientação para corrigir a referência. Clubes participantes
  continuam protegidos contra exclusão.
- Pontos corridos oferece um ou dois turnos e pontos inteiros para vitória,
  empate e derrota. Os desempates suportados são informados em texto. O formato
  Paulista 2026 mantém seus parâmetros esportivos fixos; tipos arbitrários de
  regulamento não são oferecidos nem aceitos silenciosamente.
- Datas são texto ISO, como nas fichas históricas. O gerador por intervalo exige
  data inicial e quantidade de dias; explica que substituirá as datas de rodada.
  Não altera silenciosamente os dias dos jogos ou das eliminatórias. Alterar
  formato exige confirmação porque remove os confrontos incompatíveis.
- Calendário autorado tem um filtro nativo por rodada, persistido em `round` na
  URL, e exibe somente os jogos daquela rodada. Cada confronto edita clubes,
  rodada, data e eventual estádio alternativo sem mudar o principal do clube.
  Datas de eliminatórias seguem as posições das quatro quartas, duas semifinais
  e duas finais; jogos da mesma fase podem compartilhar data.
- A validação pura de `competition-rules.js` é compartilhada entre browser e
  adaptador Node e não depende da UI ou do core C#. O schema valida estrutura;
  o importador C# repete invariantes antes de ativar a base no jogo.
- Erros mantêm o rascunho, selecionam a edição/rodada correspondente e focam o
  campo específico. Oito jogos por clube, quatro mandos, adversários distintos,
  datas dentro de suas janelas e ordem entre fases são exigidos para publicar.
- A publicação continua única por base, com ETag e backup. Criar/excluir/editar
  competições não modifica resultados ou carreiras já salvos no jogo.
