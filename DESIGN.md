---
version: alpha
name: Futebol Brasileiro
description: Indicadores de gameplay e estados dos menus do simulador de futebol em Unity.
colors:
  shot-low: "#31D66B"
  shot-medium: "#FFD447"
  shot-full: "#F44336"
  shot-track: "#121B22F2"
  shot-border: "#FFFFFFBF"
omitted:
  - section: typography
    reason: Os textos dos menus usam os assets TextMeshPro existentes; o indicador de chute não usa texto.
  - section: rounded
    reason: A barra usa retângulos de UGUI sem cantos arredondados.
  - section: spacing
    reason: A geometria usa unidades do Canvas Unity, descritas abaixo.
components:
  shot-power-bar:
    backgroundColor: "#121B22F2"
---

# Futebol Brasileiro

## Países na seleção de equipes

Quick match conserva o controle de setas/Snap da seleção legada, com uma linha
de país acima de cada clube e estado independente para mandante e visitante.
As setas ficam desabilitadas quando só há uma opção. Career e Championship usam
o TMP_Dropdown canônico do hub, com país antes do clube; Championship restringe
ambos aos participantes da edição. Bases sem país exibem uma categoria explícita
“Sem país informado / Country unspecified”. Layouts e textos continuam autorados
nos prefabs MainMenuPanel e GameHub e no catálogo GameText.

Clubes sem recursos próprios usam um escudo geométrico neutro e kits genéricos
contrastantes, editáveis nos assets de Teams/Generic. Esses recursos não pretendem
reproduzir os uniformes reais. O São Paulo preserva seus bindings existentes.

## Extensão histórica do editor web — v4

Os cadastros de países e estádios seguem a mesma ficha branca, lista paginada,
barra lateral petróleo, tipografia e tokens canônicos de `database-editor/client/styles.css`.
Não há um segundo tema para os novos recursos. Uma faixa compacta “Retrato da
base”, com filete verde, mantém data e alcance da pesquisa visíveis entre o estado
de salvamento e a área de trabalho. Título, data e alcance vêm do documento; não
há texto fixo que apresente elencos parciais como completos.

“Referência da base” usa uma ficha ampla, limitada a 960 px, com contexto e fontes
em sequência. Clubes separam identificação, elenco e parâmetros de simulação;
jogadores separam nome cadastrado, apelido e nome completo. O apelido aparece nos
títulos/listas quando informado. Dados desconhecidos permanecem visualmente
vazios e possuem ajuda explícita, sem sinalização de erro enquanto opcionais.

Datas são texto ISO com ajuda `AAAA-MM-DD`; não há calendário que atribua o dia
atual ao abrir o campo. Textareas crescem automaticamente. A navegação de cinco
destinos quebra linhas em telas estreitas; não usa menus ocultos nem botões sem
ação para campeonatos e skins futuras. O contrato de interação permanece em
`database-editor/client/UX-CONTRACT.md`; componentes de formulário, diálogo,
feedback e rascunho continuam únicos e compartilhados.

## Overview

HUD funcional para acompanhar a partida sem desviar o olhar do jogador.
Esta referência documenta o indicador de potência e os estados da seleção de times.
Preserva a composição dos menus, os nomes de jogadores e a seta de controle
existentes. A carga de chute e passes leva 500 ms e passa de verde para vermelho.

## Colors

Verde indica carga baixa, amarelo indica metade e vermelho indica carga máxima.
O comprimento do preenchimento também comunica a potência, independentemente
da percepção de cores. Fundo escuro e borda clara separam o indicador do gramado.

## Layout

Barra horizontal de 76 × 10 unidades no Canvas existente de 1920 × 1080,
com mínimo visual de 60,8 × 8 pixels. Ancoragem a 2,8 unidades acima do jogador.
Mantém orientação horizontal, independentemente da rotação da seta de controle.
Fica oculta quando o ponto está atrás da câmera ou fora dos limites da tela.

## Components

`Resources/FootballWorld/MatchControlSettings.asset` guarda tamanho, ancoragem,
escala mínima e Gradient de cores editáveis no Inspector. `ShotPowerBar.cs`
consome esses parâmetros e cria o indicador UGUI sob o Canvas de `UI/InputPointer`.
Este documento espelha os padrões; não há geração de CSS ou tema web.

`TeamInputListener` controla a carga: apertar inicia, soltar executa a ação, 500 ms reais
atingem o máximo. A barra enche linearmente e não pisca. Pausa, perda de foco,
perda de posse, troca de jogador e saída da partida cancelam o indicador.
O indicador não recebe cliques. A mesma barra serve chute, passe curto, passe em
profundidade e passe alto, com apenas uma carga ativa por jogador. Sem posse,
X pressiona o portador e círculo pede carrinho; esses comandos não mostram carga.

## Do's and Don'ts

- Manter a barra próxima ao jogador e mostrar somente durante a carga.
- Preservar o movimento durante a carga e a força máxima anterior do chute.
- Não usar apenas a cor para representar a potência.
- Não adicionar texto ou efeitos que encubram a ação da partida.

## Seleção de times e preparação da partida

A seleção mantém os dois cartões, escudos, estrelas, setas e botão de entrada
no vestiário existentes. O catálogo FootballWorld fornece os clubes e seus IDs;
o menu apenas apresenta a seleção mantida por `FriendlyMatchSession`.
Nenhum erro de carregamento substitui os clubes por uma base legada.

Os controles continuam em inglês, como os menus atuais. O estado aparece em
uma área permanente do rodapé de `MainMenuPanel.prefab`, sem mover os cartões
ou o botão principal. O texto usa a fonte TMP já empregada no menu, quebra de
linha, tamanho automático entre 22 e 26 unidades e reticências para conter
mensagens longas dentro de 930 × 48 unidades do Canvas. Mensagens da base e
nomes de clubes não interpretam rich text. Detalhes técnicos ficam nos logs.
Status e Retry ficam centrados em Y = −470, com altura de 48 unidades, para
caber no Canvas de 960 × 600 exibido no navegador sem corte inferior.
Nomes extensos usam tamanho automático de 28 a 57,8 e reticências dentro do
cartão existente, sem invadir as setas ou os demais elementos.

- Durante o carregamento, o status explica o progresso e a seleção e o botão
  de entrada no vestiário ficam inativos.
- Falhas mostram uma mensagem útil e `RETRY` no mesmo rodapé; uma tentativa
  em curso desativa essa ação. Uma base vazia ou sem dois clubes aptos não
  permite iniciar a partida.
- Clubes incapazes de montar o time continuam identificáveis pelo nome e
  pelo escudo disponível; overall aparece como `—` e as estrelas ficam vazias.
  Selecionar o mesmo clube dos dois lados também impede iniciar a partida.
- Os botões indisponíveis não recebem ponteiro nem navegação por controle;
  a opacidade reduzida é ajustável no Inspector e o status explica o motivo.
- `BACK TO TEAMS`, abaixo de `START MATCH` na preparação, permite voltar à
  seleção e manter os IDs escolhidos. Troca de uniformes e início da partida
  conservam os controles existentes.

Os elementos de status, Retry e retorno são autorados nos prefabs
`Arts/UI/Panels/MainMenuPanel.prefab`, `Arts/UI/Panels/TeamSelection.prefab`
e `Arts/UI/MatchThemes/MatchStartPanel/UpcomingMatchPanel.prefab`, sob
`Assets/FootballSimulator`. Posições, dimensões, fonte e estilo continuam
editáveis no Unity Editor; os dois novos botões reutilizam `TextButton`.
Os scripts coordenam dados, assinaturas de eventos e interação. A verificação
visual deve cobrir loading, erro/retry, clubes inválidos, seleção repetida,
retorno do vestiário e retorno de uma partida no build WebGL.

## Amostra São Paulo FC

A revisão 2 do catálogo substitui Royal por São Paulo FC. Os menus preservam
seus componentes e mostram o nome importado, o escudo tricolor e os dois kits
associados ao ClubId. O escudo deve continuar reconhecível nas dimensões dos
cartões e placares existentes; o nome não deve invadir as setas.

Os uniformes são uma emulação simplificada dos modelos de 2026: primeiro branco
com faixas horizontais vermelha e preta e escudo central; segundo com listras
verticais vermelhas, brancas e pretas e escudo no lado esquerdo do peito. Sem
patrocinadores ou reprodução de detalhes de fabricação. O goleiro usa um kit de
contraste do protótipo. Referências e procedência estão em SAO-PAULO-DATA.md.

Texturas, escudo e templates ficam em `Arts/Teams/SaoPaulo`, sob
`Assets/FootballSimulator`. A arte vetorial de origem é editável; cores e
referências são ajustáveis nos ScriptableObjects do Unity. Os shaders legados
usam vermelho como máscara de Color1 (branco) e azul para Color2 (vermelho), além
de preto e alpha. Não usar a máscara sem essa conversão como imagem de referência
visual. O escudo no peito faz parte dos atlas e dos previews dos kits.

Jogadores usam aparências genéricas existentes. A amostra demonstra dados e
identidade do clube; não representa rostos ou skins personalizadas dos atletas.
Verificar os dois kits na preparação e o primeiro em partida no navegador.

## Editor web da base

O editor é uma aplicação de autoria em pt-BR, disponível em `/editor/`, separada
da interface de gameplay. A apresentação usa azul petróleo (`#183d47`), papel
frio (`#edf2f1`), texto escuro (`#173b46`) e verde (`#23735a`) para a ação principal.
Tokens e estados compartilhados pertencem a `database-editor/client/styles.css`.
Tipografia local: Segoe UI para leitura, Bahnschrift para títulos e Consolas para
identificadores e revisão; não há fontes ou recursos carregados de terceiros.

A navegação lateral oferece Jogadores e Clubes. Lista com busca, filtro e paginação
acompanha a ficha selecionada. A ficha tem seções Ficha, Atributos e Aparência;
as duas entidades compartilham campos, diálogos, estado de rascunho e salvamento.
O formulário conserva rolagem natural, e a lista limita sua própria rolagem. Em
telas estreitas, os painéis se empilham sem esconder ações essenciais.

O rascunho pertence à base inteira: navegar entre fichas preserva as alterações;
“Salvar alterações” publica uma revisão validada. Erros e conflitos permanecem
visíveis e não descartam o rascunho. Exclusão e descarte usam diálogo com nome e
consequência, foco inicial em Cancelar e fechamento por Escape. O contrato completo
de interação fica em `database-editor/client/UX-CONTRACT.md`.

A prévia de aparência é uma ilustração SVG, claramente identificada como tal.
Ela aproxima cores e estilos do modelo padrão; o uniforme é neutro. “Faixa da meia”
edita apenas o acessório disponível no motor. A cor do meião pertence ao kit.
Prévia 3D real e upload de modelos são etapas futuras, sem controles simulados.
Verificar criação/edição/remoção, validação, conflito, descarte, teclado, largura
reduzida e aplicação dos dados no jogo; auditoria estática não substitui esse teste.

## Hub inicial e modos de jogo

O novo hub usa a identidade Futebol Brasileiro, com quatro destinos reais:
Quick match, Championship, Career e Options. O idioma inicial é inglês; a opção
Português traduz o hub e os controles afetados da partida. A seleção antiga de
times permanece como subtela de amistoso. A referência visual é a ficha de clube
com um quadro tático discreto, evitando efeitos de estádio pesados para um menu.

Career recebe o cartão maior, com retrato 2D autorado e chamada para criar o
treinador. Os outros três destinos compartilham cartões compactos, título,
descrição e seta. Nenhum cartão apresenta um fluxo futuro como se estivesse pronto.
O perfil informa que ainda não há simulação de calendário; sua data não muda a
época do elenco carregado.

Paleta inicial: petróleo `#183D47` no fundo, papel frio `#EDF2F1` nos painéis,
texto `#173B46`, secundário `#5B767B`, ação verde `#23735A`, destaque `#BFE0C9`,
linha `#C2D6D0`, branco e perigo `#AA3341`. O fundo traz linhas suaves de campo
autoradas em Images/RectTransforms. O destaque é a composição dos cartões e do
retrato; o restante mantém contraste, hierarquia e espaço para leitura.

Fontes reutilizam LiberationSans SDF existente: bold em títulos e ações, regular
nas descrições e dados. A referência é Canvas de 1600 × 900, títulos 46–56 unidades,
ações 26–34, corpo 24–28 e texto auxiliar 19–22. Auto-size tem limite inferior
explícito e reticências para nomes extensos. Status ocupa uma faixa reservada no
rodapé; erro ou carregamento não desloca os controles de iniciar/salvar.

`Resources/FootballWorld/GameHub.prefab` é a fonte da composição final. O Inspector
permite ajustar cada cor, posição, dimensão, texto e transição. O tema
`GameHubTheme.asset` guarda defaults de autoria e paletas dos três retratos, além
de fonte e sprites reutilizáveis; os componentes de retrato e linhas consultam esse
recurso. `GameText.asset` centraliza chaves e textos PT/EN. O builder Editor cria a
primeira versão a partir desses recursos, preserva autoria existente nas chamadas
normais e só refaz o layout mediante ação explícita de reconstrução.

O mapeamento do tema é explícito: `GameHubTheme.asset` → papéis em
`GameHubThemeBinding` → cor/fonte dos componentes autorados. Alterar diretamente
um Image/TMP no prefab afeta seu visual salvo. Após alterar o tema, usar
**Tools → Futebol Brasileiro → Apply game hub theme** para reaplicar os papéis
sem modificar geometria, conteúdo de texto ou eventos. Esse comando substitui
cores/fontes dos componentes vinculados; remover um vínculo permite uma exceção
visual deliberada. Paletas de retratos e realce das linhas são lidos do tema pelos
respectivos componentes. A estrutura da tela nunca é refeita automaticamente.

As subtelas compartilham cabeçalho, retorno, campos e botões. Classificação e
confrontos têm suas próprias barras de rolagem; nomes de clube permanecem texto
simples. O clube controlado é marcado por fundo e peso de fonte. Confirmações de
substituição usam o mesmo painel, com Cancelar como foco inicial e botão de perigo.
O contrato de comportamento do jogo está em `UX-CONTRACT.md`.

## Calendário, editor de competições e centro do treinador

O centro do treinador prolonga a ficha do clube existente: data e treinador no
topo, treino/caixa/próximo jogo à esquerda, imprensa e extrato à direita. A data
civil é o elemento principal da navegação diária. Ações ficam acima do rodapé
reservado a erros e avisos de salvamento. Notícias e extrato têm rolagem própria,
com barras visíveis; nenhuma notícia fictícia é apresentada como fonte histórica.

`GameHubTheme.asset` permanece a fonte de cor/fonte, aplicada pelos mesmos helpers
UGUI autorais e `GameHubThemeBinding`. `GameHubCareerOffice` apenas preenche campos
e emite comandos. A árvore completa fica em `GameHub.prefab`; a migração adiciona
esta página e seus controles sem reconstruir páginas não relacionadas. Textos PT/EN
são chaves de `GameText.asset`, incluindo os parâmetros explícitos de simulação.

No campeonato, a tabela do Paulista é rotulada **Campanha acumulada** para não
confundir a regra dos mandos com a classificação geral final. Campeão e rebaixados
são apresentados separadamente. Jogos mostram data, fase, origem e pênaltis.
Quando faltam resultados da fase, a carreira informa que o próximo confronto
aguarda definição, sem declarar eliminação antecipadamente.

O editor reutiliza a identidade, campos, confirmação e rascunho existentes nas
abas **Campeonatos** e **Edições**. Uma edição organiza participantes, regra suportada
e calendário; Paulista exibe jogos por rodada e oito datas de slots eliminatórios.
Estas tabelas pertencem ao editor web, sem introduzir CSS ou HTML dentro do Unity.

## Elenco, padrão tático e mercado da carreira

O centro do treinador oferece três destinos junto ao cabeçalho: **Elenco**,
**Tática** e **Mercado**. O cabeçalho com data/clube ocupa a coluna esquerda;
esses atalhos ocupam a direita sem disputar espaço com notícias, caixa ou rodapé.
O retorno das três telas leva ao centro do treinador.

Elenco e busca de mercado compartilham o mesmo padrão de lista e ficha. À esquerda,
nome/apelido, posição natural e filtros; à direita, apelido em destaque, clube,
nome completo, biografia e os quinze atributos em três colunas. O realce da seleção
combina fundo e marcador textual. A lista mostra o elenco completo, tem páginas
de dez jogadores, rolagem própria e contagem; um filtro sem resultados preserva os
campos e oferece Limpar. O mercado acrescenta filtro de clube e jogadores sem clube.

Mercado separa **Buscar jogadores** e **Minhas propostas** em duas abas na própria
tela. Valores são números inteiros, com moeda no resumo; estimativa e saldo ficam
próximos da ação. A confirmação apresenta jogador, clube, custo total, reserva de
caixa e resposta no próximo dia. O diálogo é o mesmo usado nas outras telas,
ampliado para esse resumo; enviar usa verde, substituir continua usando perigo.
Propostas têm estado textual, datas e motivo; pendentes têm cancelamento direto.

Tática separa o formulário explícito da visualização do XI automático salvo.
Formação e postura são rascunhos até **Salvar padrão tático**. O XI vem do adaptador
existente, sem repetir regras de escalação na UI; a tela explica a ausência de
escalação manual nesta etapa. As três páginas usam os mesmos papéis do tema e
templates UGUI/TMP editáveis em `GameHub.prefab`. O partial Editor adiciona apenas
os controles ausentes e preserva as outras páginas e ajustes posteriores.

## Inspeção de jogadores e quadro tático

A ficha e a prévia tática usam painéis de inspeção escuros dentro do hub petróleo,
seguindo as referências de gestão de futebol fornecidas pelo usuário. O destaque
é o campo com posições, acompanhado dos mesmos nomes legíveis na lista do XI.
Não são exibidas estrelas, avaliações de potencial, retratos ou estatísticas que
a base e a carreira ainda não fornecem.

O tema existente permanece canônico: `GameHubTheme.asset` → papéis de
`GameHubThemeBinding` → Images/TMP do prefab. Os papéis adicionais são
InspectionPanel (`#102733`), InspectionRow (`#1D3640`), InspectionText (`#F0F6F4`),
InspectionMuted (`#AEC5C5`), PitchSurface (`#255B47`) e PitchMarking (`#70A88B`).
`AttributeColor`, também editável no tema, colore números conhecidos na escala
0–100; o número permanece explícito e ausências usam “—”. Tipografia continua
LiberationSans SDF, com títulos bold, rótulos regulares e valores destacados.

Elenco e Mercado compartilham a ficha larga à direita, com identificação e
biografia no cabeçalho, minimapa de posições naturais e três grupos de cinco
atributos: Técnica, Físico e Jogo. A lista de jogadores permanece à esquerda,
com busca, filtros, contagem, paginação e scrollbar próprios. Mercado conserva
valor da oferta, confirmação e ação abaixo da ficha. O rodapé global continua
reservado a avisos. As posições naturais não são confundidas com vagas do XI.

Tática usa três colunas: controles e padrão salvo, campo da formação e lista dos
onze jogadores. O campo e a lista acompanham o rascunho local; somente Salvar
padrão tático altera a carreira. Os três layouts 4-4-2, 4-3-3 e 4-2-3-1 são árvores
UGUI separadas com posições e dimensões ajustáveis no Inspector. O arrastamento
ajusta as posições do XI automático; a escolha manual de titulares continua fora
do escopo. `CareerTacticalBoard` apresenta o plano tático em rascunho sem alterar
os jogadores nem seus atributos.

`GameHubAuthoring.CareerInspection` aplica a migração uma vez, indicada por
`CareerInspectionLayoutV1`. Reutiliza controles e referências existentes,
reposiciona as scrollbars sem remover o reparo `VerticalScrollbarLayoutV2` e
preserva ajustes visuais posteriores. `CareerNaturalPositionMap` e a ficha são
componentes de apresentação; não alteram posições, atributos ou escalação.
A evolução intencional substitui o antigo XI exclusivamente salvo por uma prévia
do rascunho, sempre identificada na tela. A verificação deve cobrir formação sem
salvar, salvar/retomar, PT/EN, nomes extensos, ficha vazia e oferta no Mercado.

## Ajustes de posição e função no quadro tático

Selecionar um marcador ou uma linha do XI destaca a vaga nos dois lugares. O
painel esquerdo reúne formação, postura, função compatível e uma descrição curta
do efeito. O arraste mantém o ponto de captura e respeita os limites da posição;
o goleiro permanece em sua zona própria. Com o marcador focado, as setas fazem
ajustes pequenos e Esc devolve o foco aos controles. O passo do teclado, as áreas
clicáveis, os contornos e todos os controles são editáveis no Inspector.

Restaurar vaga devolve posição e função padrão da vaga selecionada. Restaurar
tudo faz o mesmo para as onze vagas da formação atual. Trocar formação também
restaura seu padrão; trocar somente a postura preserva os ajustes. Essas ações
alteram o rascunho, e somente Salvar padrão tático grava a configuração da
carreira. As mensagens informam esse comportamento e a seleção automática do XI.

A migração incremental `CareerTacticsInteractionV3` reaproveita o campo e a lista
do prefab, adiciona interação UGUI aos marcadores existentes e reorganiza somente
a coluna de controles. Coordenadas salvas são relativas ao campo, nunca em pixels
ou posições da tela. Funções usam as opções e limites do núcleo, sem atribuir
estrelas, atributos extras ou promessas de sistemas de jogo ausentes. A verificação
inclui arraste, limites, teclado, compatibilidade por posição, restauração,
troca de formação/postura, rascunho ao navegar e persistência após salvar/retomar.
