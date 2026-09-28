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

## Overview

HUD funcional para acompanhar a partida sem desviar o olhar do jogador.
Esta referência documenta o indicador de chute e os estados da seleção de times.
Preserva a composição dos menus, os nomes de jogadores e a seta de controle
existentes. A carga de chute leva 500 ms e passa de verde para vermelho.

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

`Assets/FootballSimulator/Code/MatchEngine/UI/ShotPowerBar.cs` é a fonte dos
valores visuais e cria as imagens UGUI sob o Canvas de `UI/InputPointer`.
Este documento espelha esses valores; não há geração de CSS ou tema web.

`TeamInputListener` controla a carga: apertar inicia, soltar dispara, 500 ms reais
atingem o máximo. A barra enche linearmente e não pisca. Pausa, perda de foco,
perda de posse, troca de jogador e saída da partida cancelam o indicador.
O indicador não recebe cliques nem altera os controles existentes.

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
