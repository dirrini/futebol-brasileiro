# Contrato de interação — Futebol Brasileiro

Este documento cobre o hub e as telas de jogo. O editor externo tem seu contrato
em [database-editor/client/UX-CONTRACT.md](database-editor/client/UX-CONTRACT.md).
Direção visual em [DESIGN.md](DESIGN.md). Regras e propriedade dos dados seguem
[ARCHITECTURE.md](ARCHITECTURE.md) e [DATA-FORMAT.md](DATA-FORMAT.md).

## Proprietários canônicos

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
| --- | --- | --- | --- | --- |
| Form | GameHubView + TMP_InputField | GameHubSession e HubCareerProfile | Criar e atualizar treinador | Nome, ano, clube, erro e retenção da ficha |
| Select/Listbox | TMP_Dropdown, template salvo em GameHub.prefab | Prefab + tema | Edição, clube, idioma, câmera, dificuldade e mês | Popup, teclado, scroll e texto PT/EN |
| Scrollbar | ScrollRect/Scrollbar autorados no prefab | GameHub.prefab + GameHubTheme | Classificação, confrontos, opções longas | Mouse, teclado e scrollbar visível |
| Toast | Status/SaveWarning em GameHubView | GameHubSession | Carregando, erro, confirmação e aviso persistente | Status sem deslocar ações, retry e fechamento de aviso |
| CRUD | GameHubSession | ARCHITECTURE.md e save local versionado | Criar/substituir carreira e campeonato | Gravação, restauração, revisão fixada e confirmação |
| Dialog | Confirmation em GameHub.prefab + GameHubView | Este contrato | Substituir campeonato ou perfil | Cancelar inicial, Escape, foco e bloqueio do fundo |
| Navigation | GameHubSession.Navigate | Este contrato | Hub e retorno ao modo de origem | Amistoso, campeonato, perfil e opções |
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

## Perfil de carreira

Esta etapa cria um treinador com nome, um de três retratos 2D, mês/ano inicial e
clube. Anos de 1 a 9999 e meses de 1 a 12 são aceitos; o ano usa campo inteiro, sem
uma lista artificial limitada a décadas. O primeiro valor vem do início da edição
disponível. A data não transforma o elenco atual em uma base histórica.

A ficha declara que ainda não simula calendário. Criar/salvar mostra o perfil
confirmado e mantém o formulário. Alterar um perfil existente exige confirmação
antes de substituí-lo. Erros preservam os campos; nome vazio ou ano inválido recebe
foco para correção. A ficha em memória é mantida ao alternar telas do hub; somente
o perfil salvo sobrevive ao descarregamento da UI ou refresh do navegador.

Retratos são composição UGUI autorada, com IDs independentes do nome. Suas paletas
são editáveis no tema. Não representam skins 3D, não mudam jogadores ou física e
não fazem upload de modelos.

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

Verificar início, PT/EN, popup das opções, retorno do amistoso, criação e restauração
do perfil, ano 1/9999/inválido, cancelamento de substituição, criação/retomada de
campeonato, próximo jogo e classificação após resultado, falha de base/retry,
save inválido, nomes longos e lista com scroll. Conferir 960 × 600, tela cheia,
mouse e navegação por teclado; documentar os testes efetivamente executados.
