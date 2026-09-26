---
version: alpha
name: Futebol Brasileiro
description: Indicadores de gameplay do simulador de futebol em Unity.
colors:
  shot-low: "#31D66B"
  shot-medium: "#FFD447"
  shot-full: "#F44336"
  shot-track: "#121B22F2"
  shot-border: "#FFFFFFBF"
omitted:
  - section: typography
    reason: O indicador de chute não usa texto; os textos existentes continuam em TextMeshPro.
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
Esta referência documenta apenas o indicador de chute; preserva os menus,
nomes de jogadores e a seta de controle existentes. A solicitação do usuário
define carga de 500 ms e transição de verde para vermelho.

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
