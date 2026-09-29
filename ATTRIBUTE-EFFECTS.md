# Atributos e inteligência da partida

Os quinze atributos do cadastro são copiados para os jogadores temporários da
partida 3D. Não são apenas informações da ficha. Antes de usá-los, o motor aplica
as curvas de `Resources/Singletons/PlayerSkillCurves.asset` e os multiplicadores
de `EngineSettings.asset`. A escala 0–100 não corresponde diretamente a uma
porcentagem de sucesso ou a uma velocidade em metros por segundo.

| Atributo | Uso no motor 3D |
| --- | --- |
| Força | Massa física e disputa corporal. |
| Aceleração | Rapidez de aproximação da velocidade desejada. |
| Velocidade | Velocidade máxima de deslocamento. |
| Velocidade com bola | Redução da velocidade ao conduzir e avaliação de drible. |
| Impulsão | Alcance corporal e alcance das defesas do goleiro. |
| Desarme | Disputa de atributos nas tentativas de bote e carrinho. |
| Proteção de bola | Resistência à perda na disputa e avaliação de drible. |
| Passe | Precisão do passe curto, com transição gradual para lançamento conforme a distância. |
| Passe longo | Precisão dos lançamentos e cruzamentos. |
| Agilidade | Velocidade de mudança da orientação do jogador. |
| Finalização | Erro de direção do chute e avaliação da chance de finalizar pela IA. |
| Potência de chute | Multiplicador da força, separado da precisão. |
| Posicionamento | Desvio da posição tática desejada e posicionamento do goleiro. |
| Reação | Atraso de nova decisão após passes e probabilidade de reação do goleiro. |
| Controle de bola | Disputa de domínio conforme altura e impacto da bola. |

Pressionar R1 permite alcançar o ritmo máximo daquele jogador; não transforma
dois jogadores de velocidades diferentes em jogadores igualmente rápidos. A
barra de potência também não substitui os atributos: ela controla a intensidade
da ação escolhida pelo usuário.

Formação, mentalidade, posições ajustadas e funções individuais definem destinos
e preferências. Por exemplo, o lateral cruzador avança e procura cruzar mais;
o lateral invertido centraliza; o pivô oferece recepção e devolução. Essas funções
usam regras e probabilidades, respeitando o estado do lance e o controle manual.

Ainda não há um atributo separado de inteligência tática, familiaridade com a
função ou uma análise coletiva que escolha o momento ideal de cada desmarque.
Posicionamento alto melhora a proximidade ao destino calculado, não cria esse
tipo de raciocínio. Identidade, biografia, reputação e outros campos cadastrais
também não devem ser interpretados como uma IA já implementada para cada campo.

A simulação rápida de placares é um sistema distinto e mais simples: não executa
esses cálculos físicos nem todas as instruções táticas. As regras e limitações da
gestão estão em [Carreira diária](CAREER-PROTOTYPE.md).

Nesta integração foram corrigidos quatro problemas do motor herdado: potência
lia finalização, reação lia posicionamento, os pesos de passe curto/longo estavam
invertidos e o atraso de reação era zerado por divisão inteira. Os testes cobrem
independência desses atributos, aumento de velocidade e aceleração, transição da
precisão do passe pela distância e reação maior com menor atraso.
