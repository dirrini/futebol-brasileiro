# Controles da partida 3D

Os nomes abaixo seguem o controle PlayStation. O jogo usa as posições genéricas
do gamepad: X = botão inferior, círculo = direito, quadrado = esquerdo e
triângulo = superior. No Xbox, correspondem a A, B, X e Y.

| Ação | Controle | Teclado |
| --- | --- | --- |
| Movimentar | Analógico esquerdo | Setas |
| Correr | Segurar R1 enquanto se move | Segurar Shift |
| Passe curto com a bola | Segurar e soltar X | Segurar e soltar S |
| Passe em profundidade | Segurar e soltar triângulo | Segurar e soltar W |
| Passe alto / cruzamento | Segurar e soltar círculo | Segurar e soltar A |
| Chute | Segurar e soltar quadrado | Segurar e soltar D |
| Perseguir o portador e tentar bote | Segurar X sem a bola | Segurar S |
| Carrinho | Círculo sem a bola | A |
| Trocar jogador manualmente | L1 | Q |
| Pausar | Options / Start | Esc |

A barra acima do jogador enche em 500 ms, de verde a vermelho. A soltura executa
o passe ou chute. A potência modula a velocidade dos passes rasteiros e o alcance
do passe alto; atributos e contexto continuam participando do resultado. Uma
tentativa de roubar a bola não garante sucesso: distância e disputa de atributos
também importam. Pausa, perda de foco, perda de posse ou troca de jogador cancelam
uma carga pendente.

O controle passa ao destinatário quando a bola é efetivamente enviada. Uma
interceptação ou posse posterior pode mudar o destinatário. L1 continua disponível
para escolher manualmente. O goleiro também pode ser controlado ao receber passe,
defender com a bola dominada ou cobrar tiro de meta: X faz a saída curta e quadrado
envia a bola longa.

Para finalizar um cruzamento, pressione e solte quadrado quando a bola estiver
chegando ao jogador controlado. A solicitação fica disponível por uma janela
curta, e o motor escolhe cabeceio, peixinho, voleio ou bicicleta conforme altura,
alcance e orientação em relação ao gol. A bicicleta exige estar de costas para o
gol; o peixinho atende uma bola mais baixa à frente do corpo. O contato acontece
no evento da animação e é novamente validado nesse instante: uma bola distante,
interceptada ou fora da faixa do gesto não recebe um chute artificial.

Os clips `AerialHeader`, `AerialLowHeader`, `AerialVolley`, `AerialBicycle` e
`DivingHeader` ficam em `Arts/FootballPlayer/Animations/Actions`, com estados em
`PlayerLocomotion.controller`. São animações iniciais reaproveitadas e adaptadas
do acervo do projeto, ajustáveis no Unity. Os parâmetros em
`Resources/Singletons/EngineOptions_BallHitAnimations.asset` controlam a janela
de entrada, as faixas de contato e a recuperação. Ainda precisam de refinamento
visual e balanceamento com partidas completas e diferentes alturas de jogador.

O modo de carreira e as partidas rápidas compartilham o motor 3D. A simulação
rápida de placares não reproduz estes controles nem cada decisão física da partida.
