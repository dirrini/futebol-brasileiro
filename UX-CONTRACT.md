# Contrato de interação — Futebol Brasileiro

Este documento cobre o hub e as telas de jogo. O editor externo tem seu contrato
em [database-editor/client/UX-CONTRACT.md](database-editor/client/UX-CONTRACT.md).
Direção visual em [DESIGN.md](DESIGN.md). Regras e propriedade dos dados seguem
[ARCHITECTURE.md](ARCHITECTURE.md) e [DATA-FORMAT.md](DATA-FORMAT.md).

## Proprietários canônicos

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
| --- | --- | --- | --- | --- |
| Form | GameHubView + TMP_InputField | GameHubSession e HubCareerProfile | Criar e substituir carreira | Nome, ano, clube, erro e retenção da ficha |
| Select/Listbox | TMP_Dropdown, template salvo em GameHub.prefab | Prefab + tema | País, edição, clube, idioma, câmera, dificuldade e mês | Popup, teclado, scroll e texto PT/EN |
| Scrollbar | ScrollRect/Scrollbar autorados no prefab | GameHub.prefab + GameHubTheme | Classificação, confrontos, opções longas | Mouse, teclado e scrollbar visível |
| Toast | Status/SaveWarning em GameHubView | GameHubSession | Carregando, erro, confirmação e aviso persistente | Status sem deslocar ações, retry e fechamento de aviso |
| CRUD | GameHubSession | ARCHITECTURE.md e save local versionado | Criar/substituir carreira e campeonato | Gravação, restauração, revisão fixada e confirmação |
| Dialog | Confirmation em GameHub.prefab + GameHubView | Este contrato | Substituir campeonato ou perfil | Cancelar inicial, Escape, foco e bloqueio do fundo |
| Navigation | GameHubSession.Navigate | Este contrato | Hub e retorno ao modo de origem | Amistoso, campeonato, carreira e opções |
| Locale | GameText/GameTextCatalog/LocalizedText | GameText.asset | Inglês inicial e português selecionável | Novas telas e retornos afetados traduzidos |

## Navegação e preferências

O início oferece **Quick match**, **Championship**, **Career** e **Options** em
inglês por padrão. A escolha de português atualiza as telas novas e os controles
afetados do fluxo de partida. Nomes autorados de clubes, treinadores e campeonatos
permanecem como dados, sem tradução automática ou interpretação de rich text.

Amistoso abre a seleção existente; seu botão de retorno leva ao hub. A navegação
durante preparação, abandono e conclusão deve conservar o modo que originou a
partida. O hub não substitui o catálogo por dados legados em uma falha de leitura.
Carregamento e erros ocupam o rodapé reservado, com Tentar novamente quando cabe.

Quick match usa o seletor de setas já existente para país e clube, independentemente
para mandante e visitante. Career e Championship usam TMP_Dropdown, com país antes
do clube; no campeonato, somente os participantes da edição aparecem. País com
apenas uma opção não oferece troca fictícia. Bases antigas agrupam clubes sem
país em “Sem país informado / Country unspecified”. A seleção continua por ClubId.
Jogadores exibem `nickname` quando preenchido, com fallback para `name`; `fullName`
permanece um dado cadastral. Textos do catálogo são literais, sem rich text.

Idioma, dificuldade e câmera são preferências locais, salvas imediatamente; o
texto explica esse comportamento. A câmera escolhida é aplicada ao iniciar a
partida. As opções são apenas câmeras de jogo usuais, sem as câmeras especiais de
impedimento/escanteio. Dificuldade compartilha a fonte de verdade com a preparação.

## Campeonato

Edição e clube são selecionados por ID estável. O cadastro informa o período da
edição. Iniciar cria uma sessão com a revisão da base fixada. Continuar retorna à
sessão salva, sem reiniciar resultados por atualizar o catálogo.

A tela mostra classificação, confrontos e próximo jogo real. Tabelas apresentam
dados da sessão; clube controlado recebe cor e peso tipográfico, sem depender só
da cor. Listas possuem rolagem própria, com barras visíveis. A ação de jogar fica
indisponível durante uma execução ou quando não há próximo confronto.

Iniciar outro campeonato quando existe save exige confirmação que explica a perda
dos resultados. Cancelar é a seleção inicial e preserva a sessão. A mesma proteção
se aplica a saves existentes que não puderam ser restaurados, sem descartá-los
silenciosamente. Simulação, correlação de resultado e persistência pertencem à
aplicação, não à tela.

## Carreira diária

O formulário conserva nome/avatar, país, clube e mês/ano. Iniciar exige uma edição
do clube que comece no mês escolhido; para a base atual, janeiro de 2026. O primeiro
dia desse mês inicia o relógio da carreira. Datas sem edição recebem erro e mantêm
os campos. O recorte cadastral é fixado, sem prometer resolver elencos históricos.

Criar abre o centro do treinador. Retornar a Career continua a carreira diária;
**Nova carreira** abre o formulário e só substitui o save após confirmação explícita
sobre perda de calendário, resultados e finanças. Perfis antigos são preservados
como perfis, sem migração que invente uma temporada. O formulário também oferece
**Continuar carreira** quando existe uma diária restaurada.

Avançar um dia e ir ao próximo jogo atualizam treino e gestão, simulam os outros
confrontos por data e param nos jogos do clube. Jogar/simular ficam habilitados
somente no dia da partida. Após o jogo, a tela volta ao centro do treinador. Uma
execução interrompida mantém seu ID consumido e restaura o confronto como pendente.
O relógio termina no fim da edição; não cria anos, competições ou jogos fictícios.

Treino só produz efeito ao avançar o dia; trocar repetidamente não gera bônus.
Condição, preparo e rendimento 3D são visíveis. Caixa e extrato distinguem receitas
e despesas; números usam a cultura ativa e moeda da base. Notícias vêm de veículos
fictícios identificados. Detalhes em [CAREER-PROTOTYPE.md](CAREER-PROTOTYPE.md).

Calendário/classificação é o dashboard compartilhado do campeonato, com retorno
à carreira que o abriu. O título **Campanha acumulada** vale para Paulista; campeão
e rebaixados aparecem separadamente. Um próximo confronto ainda não definido não
é apresentado como eliminação definitiva. Na competição independente, simulação
da próxima rodada permite acompanhar as fases após sair da disputa.

A gravação inclui revisão completa da base, calendário e gestão. Falha mantém
aviso persistente e o último save confirmado; o progresso em memória não deve ser
confundido com conteúdo já salvo. Os novos slots usam compressão com limites; os
saves antigos continuam legíveis. A integridade esportiva/financeira é validada
ao restaurar, incluindo receitas já processadas e histórico de treino.

## Controles, layout e autoria

UI usa Canvas UGUI/TMP existente, resolução de referência 1600 × 900 e escala
Expand. A área funciona na exportação de 960 × 600 e em tela cheia. Campos usam
labels explícitos, foco visível, input numérico preciso e TMP_Dropdown nativo do
projeto, com template editável. Nenhum popup HTML é necessário dentro do jogo.

O Canvas do hub usa o componente canônico `UICanvas` para atribuir a câmera de UI
ao seu `GraphicRaycaster`. O vínculo acompanha o prefab e é reparado pelo comando
de criação sem reconstruir o layout. A primeira atualização seleciona Career no
início; a página anterior começa sem valor, inclusive quando o destino é Home.
O Inspector do hub guarda a ordem 30, aplicada após a instanciação sob o Canvas de
UI: acima da seleção de times (0) e abaixo dos painéis de carregamento (50).
Isso mantém o retorno do amistoso clicável sem cobrir o restante da seleção; nessa
subtela o fundo do hub fica desativado. Popups usam a ordenação nativa do TMP.

O diálogo bloqueia cliques no fundo e mantém navegação entre Cancelar/Substituir.
Escape cancela e restaura foco ao controle anterior. Transições curtas de botão
não mudam a posição das ações. Não há animação contínua ou efeito dependente de
movimento para entender a seleção.

GameHub.prefab guarda a árvore completa, posicionamento, fontes, cores, templates
de linha e estados. GameHubTheme.asset fornece defaults visuais e paletas de
retratos; GameText.asset guarda todas as traduções. GameHubView coordena eventos e
preenche componentes existentes. Linhas dinâmicas são instâncias de templates
autorados; não se cria o formulário inteiro em runtime.

O comando Editor de criação preserva prefabs e traduções existentes. O comando
explícito de reconstrução avisa que substituirá ajustes de layout; não é executado
automaticamente ao abrir o projeto ou compilar. Ajustes cotidianos devem ser feitos
no prefab/Inspector, preservando GUIDs e referências.

Aplicar o tema é uma ação Editor separada da reconstrução: **Apply game hub theme**
atualiza apenas cores/fontes/estados dos componentes com GameHubThemeBinding,
preservando posições, dimensões, textos e eventos. Traduções existentes não são
substituídas pelo seed ao repetir a criação dos assets.

## Verificação esperada

### Gestão do elenco e propostas

Elenco, Tática e Mercado pertencem à carreira e retornam ao centro do treinador.
Consultas usam o catálogo efetivo da carreira, que inclui transferências aceitas;
o JSON autoral não é alterado por uma contratação. Busca por nome, apelido e nome
completo ignora caixa e acentos. Posição filtra as posições naturais, sem confundi-las
com vagas na formação. Mercado permite Todos os clubes, um clube ou Sem clube.
Filtros e paginação ficam na apresentação; uma nova carreira reinicia esse estado.

Elenco e mercado usam páginas de dez jogadores, contagem e rolagem. A seleção é um
ID estável, mantém a ficha durante atualizações e é limpa/substituída se deixar de
corresponder ao filtro. Fichas apresentam dados ausentes como “—” ou Não informado;
todos os quinze atributos usam a escala 0–100. Uma busca vazia mostra instrução
para revisar o nome ou limpar filtros e não perde o rascunho da busca.

Proposta exige seleção elegível e inteiro de 0 a 2.147.483.647, com saldo disponível.
Jogadores sem clube custam zero; jogadores com clube exigem taxa positiva. A
confirmação informa custo total, reserva até a decisão e limite de escopo (taxa de
transferência, sem negociar novos salários/contratos). Cancelar ou Escape restaura
foco. Uma falha mantém diálogo e entrada, com a mensagem retornada pela aplicação;
sucesso fecha o diálogo e atualiza o estado. A aplicação permanece a autoridade
para orçamento, elegibilidade e aceite. A resposta é processada no próximo dia.

Minhas propostas tem paginação de cinco registros e exibe nome, valor, estado,
envio, decisão e motivo de recusa. Pendentes podem ser canceladas sem confirmação
adicional; isso libera o valor reservado. Aceitas passam a constar no elenco efetivo
e no XI automático quando escolhidas pelo adaptador. Transferências aceitas e
recusadas também geram notícias fictícias; a taxa aceita aparece no extrato.

Tática tem rascunho local preservado ao sair e voltar, com indicação de alterações
pendentes. Salvar é explícito e aplica formação, postura, posições e funções às próximas partidas.
O campo e o XI exibidos mostram a prévia do rascunho tático. A partida só usa
as escolhas após Salvar. Arrastar ajusta a vaga do XI automático, com limites por
posição; não troca titulares. Selecionar um marcador ou linha do XI mostra as
funções compatíveis com aquela posição. Restaurar uma posição ou todas mantém
o estado como rascunho; trocar formação reinicia posições/funções e trocar postura
as preserva. No fim do calendário, consultas continuam,
mas editar tática, enviar e cancelar propostas ficam desabilitados com motivo claro.

Todos os textos dessas telas têm chaves PT/EN. A largura reservada ao rodapé não é
ocupada por filtros, atributos ou botões. Nenhuma regra de transferência ou desenho
de formulário é construída em runtime: comandos passam pelo Hub, regras ficam no
core e componentes/linhas têm templates autorados no prefab.

### Checagens da gestão

Conferir todas as posições/quinze atributos, nomes extensos, pesquisa sem resultado,
paginação, filtro de clube/sem clube, proposta inválida/sem saldo/abaixo da estimativa,
confirmação cancelada e enviada, cancelamento pendente, resposta após avanço do dia,
elenco após contratação, tática salva e refresh do navegador. Exercitar PT/EN e o
retorno ao centro do treinador. Registrar o que foi efetivamente verificado no build.

Verificar início, PT/EN, popup das opções, retorno do amistoso, criação e restauração
do perfil, ano 1/9999/inválido, cancelamento de substituição, criação/retomada de
campeonato, próximo jogo e classificação após resultado, falha de base/retry,
save inválido, nomes longos e lista com scroll. Conferir 960 × 600, tela cheia,
mouse e navegação por teclado; documentar os testes efetivamente executados.
