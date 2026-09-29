# Carreira diária e gestão básica

O protótipo permite criar treinador, avatar, mês/ano inicial e clube, avançar os
dias, consultar calendário e classificação, jogar ou simular compromissos, ajustar
treinamento e tática, consultar o elenco, buscar jogadores, fazer propostas e
acompanhar caixa e notícias. Para o exemplo, selecione janeiro de
2026 e um dos 16 participantes do Paulistão. A carreira começa no dia 1º e termina
no último dia da edição cadastrada, 8 de março. Não há virada automática de temporada.

## Fluxo de teste

1. Abra `http://localhost:8080` e escolha **Career / Carreira**.
2. Crie o treinador, selecione janeiro de 2026 e uma equipe da base atual.
3. Avance um dia ou até o próximo compromisso. O avanço para no jogo pendente da
   equipe controlada; não o resolve silenciosamente.
4. No dia da partida, jogue em 3D ou escolha a simulação explícita. Outros jogos
   são resolvidos pelo simulador conforme sua data chega.
5. Consulte os resultados, notícias e movimentos do caixa. Alterne o treino e
   observe os indicadores no dia seguinte. Recarregue a página para continuar.
6. Abra o elenco para consultar a ficha e os quinze atributos. Na tática, escolha
   formação e mentalidade, arraste os marcadores para ajustes finos e atribua uma
   função a cada posição. Use **Salvar tática** para aplicar à próxima partida;
   a escolha dos onze jogadores permanece automática.
7. Busque um jogador de outro clube, confira a estimativa de simulação e envie uma
   proposta. O valor fica reservado até a resposta no próximo dia ou o cancelamento.

Uma carreira já existente permanece fixada ao JSON da base usado na criação,
incluindo os clubes, jogadores e regulamento. Editar a base pelo editor afeta novas
carreiras. Saves antigos contendo apenas perfil continuam legíveis; é preciso
criar uma nova carreira para obter agenda diária. A data escolhida ainda não
seleciona vínculos de jogadores ou regras por intervalos históricos: consulte
[HISTORICAL-DATA.md](HISTORICAL-DATA.md) sobre o recorte observado dos elencos.

## Treinamento e partida

Condição começa em 100 e preparo em 50, ambos limitados ao intervalo 0–100.
Trocar o plano não concede bônus imediato: ele é aplicado na passagem do dia.

| Plano | Condição por dia | Preparo por dia |
| --- | ---: | ---: |
| Equilibrado | +1 | +1 |
| Recuperação | +4 | −2 |
| Intensivo | −3 | +3 |

Um compromisso concluído consome 12 pontos de condição e 3 de preparo. A partida
3D usa um fator entre 90% e 110%, calculado por
`90 + condição / 10 + preparo / 10`, com divisões inteiras. O adaptador aplica esse
fator a aceleração, velocidade, passe, chute e reação dos jogadores temporários
da equipe controlada. As definições originais dos jogadores não são alteradas.

O simulador de placares não usa esse fator nesta etapa. Treino não representa
sessões individuais, evolução permanente de atributos, lesões ou suspensões.

As formações disponíveis na carreira são 4-4-2, 4-3-3 e 4-2-3-1, com mentalidade
defensiva, equilibrada ou ofensiva. A formação orienta a seleção automática dos
onze por posições naturais; não há escolha manual de titulares ou reservas.
As opções pertencem ao save e são aplicadas aos objetos temporários da partida,
sem editar a formação autoral de outros modos. O simulador rápido ainda não avalia
tática, preparação nem a qualidade do elenco após contratações; esses efeitos são
aplicados à montagem e ao desempenho da equipe na partida 3D.

## Caixa de simulação

Valores são inteiros na moeda cadastrada; na ausência, usa-se BRL. O orçamento de
transferências cadastral fornece o caixa inicial de simulação. O orçamento mensal
de salários fornece o débito mensal. Zero explícito é respeitado e não aciona
um valor padrão.

`CareerManagementRules`, versão 1, fixa os seguintes parâmetros de protótipo no
save. Eles não pretendem reproduzir finanças reais dos clubes:

| Parâmetro | Valor padrão |
| --- | ---: |
| Caixa inicial quando orçamento está ausente | 5.000.000 |
| Salários mensais quando valor está ausente | 250.000 |
| Receita mensal de simulação | 200.000 |
| Receita por partida em casa concluída | 100.000 |

Receita e salários são lançados no primeiro dia de cada mês subsequente ao mês
inicial. A bilheteria é um valor fixo de teste por mando; não calcula lotação,
preço de ingresso, divisão de receita ou uso de estádio. O caixa pode ficar
negativo, sem insolvência ou punição automática. Não há movimentação financeira
real nem operações fora do jogo.

## Propostas e vínculos do elenco

A busca permite consultar jogadores e enviar um valor inteiro como taxa de
transferência. A estimativa é de simulação: média inteira dos quinze atributos,
elevada ao quadrado e multiplicada por 100, com mínimo de 10.000 para jogadores
vinculados. Uma média de 50 resulta em 250.000; não é uma avaliação de mercado real.
Jogadores sem clube podem ser contratados por taxa zero, se existirem na base.
Não é possível oferecer zero por um jogador vinculado ou contratar alguém do
próprio clube.

Propostas pendentes reservam o caixa disponível. O próximo avanço diário processa
as respostas, depois dos lançamentos mensais e antes da bilheteria desse dia.
A proposta é recusada se ficar abaixo da estimativa, se o caixa já não cobrir o
valor ou se o vendedor perder condições de escalar onze, incluindo um goleiro.
As propostas são processadas pela ordem de envio; cada contratação aceita pode
alterar a disponibilidade para a seguinte. A resposta é determinística, sem
negociação contratual ou decisões aleatórias de empresários.

É possível cancelar uma proposta pendente. Aceitação desconta a taxa uma vez e
altera o vínculo no elenco da carreira; a revisão original da base permanece
intacta. O histórico guarda envio, decisão, motivo e ordem das ações. Há limite
de 16 propostas pendentes e 128 propostas no histórico desta carreira. Gestão
de propostas e tática fica indisponível durante partidas e no fim do calendário.
Novas contratações não alteram a folha mensal nesta etapa: salário, duração de
contrato, janelas, empréstimos e ofertas recebidas ficam para depois.

## Notícias e persistência

**Gazeta da Bola** e **Diário da Arquibancada** são veículos fictícios. Publicam
boas-vindas, relatórios semanais de preparação, resultados, decisões de propostas, fechamento mensal e
encerramento da competição a partir de eventos do save. Textos são localizados
pela apresentação em português/inglês; o core guarda chaves e dados, sem chamadas
a serviços de IA ou notícias reais.

Saves diários usam versão 4, separada do schema da base, com posições e funções
por vaga tática. A versão 3 mantém formação, mentalidade e propostas e recebe
posições padrão; a versão 2 continua legível com tática 4-4-2 equilibrada e sem
inventar propostas. A restauração valida
a competição, reprocessa treino e propostas e confere movimentos, notícias e
resultados contabilizados. Recarregar, receber um callback repetido ou salvar
novamente não concede outra receita, cobrança ou resultado.

O armazenamento comprime os envelopes e mantém dois slots por modo, preservando
o snapshot confirmado enquanto grava o substituto. Os limites são 8 MiB de
conteúdo expandido e 112 KiB por slot; quatro slots no teto equivalem a 896 KiB
sob contabilização conservadora UTF-16, com margem para preferências. Dados incompatíveis ou grandes demais
produzem erro e preservam o save anterior. O conteúdo continua local ao navegador.

## Próximas extensões

Negociação de contratos, vendas, empréstimos, amistosos agendados, notícias mais
variadas, escolha manual da escalação, inscrições e suspensões, indisponibilidade de estádios,
temporadas encadeadas e resolução histórica de vínculos permanecem futuras.
Também não há editor de modelos comunitários ou novos estádios 3D nesta entrega.
O regulamento e as aproximações esportivas atuais estão em
[CALENDAR-2026.md](CALENDAR-2026.md).

## Prêmios e formatos v6

A carreira executa participação, vitória/empate por jogo e intervalos de posição ao completar uma fase. Lançamentos são idempotentes e reconstituídos no carregamento; o Paulista de exemplo mantém prêmios zerados até que valores sejam autorados. Prêmios positivos precisam usar a moeda da carreira, sem conversão inventada. Campo neutro não concede receita de mandante. Vagas são registradas, mas não iniciam outra temporada automaticamente.

O campo tático e a ficha de atributos usam prefabs editáveis no Unity. A prévia acompanha a formação escolhida, e salvar aplica a tática; não há escalação manual de titulares nesta etapa. A ficha mostra os quinze atributos existentes na escala 0–100.

## Ajustes de posição e funções individuais

Os marcadores representam vagas da formação, associadas ao XI automático. Arrastar
um marcador ajusta sua posição de referência; não troca o jogador escalado. Cada
vaga tem limites de deslocamento para preservar a estrutura da formação, com
limites próprios para o goleiro. Trocar a formação restaura suas posições e
funções padrão; trocar apenas a mentalidade preserva os ajustes. Restaurar uma
posição ou toda a formação altera o rascunho, que só se aplica após salvar.

| Posição | Funções além do padrão |
| --- | --- |
| Zagueiro | Zagueiro que avança |
| Lateral | Lateral cruzador, lateral invertido |
| Volante / meio-campista central | Volante de marcação, articulador recuado |
| Meio-campista central / ofensivo | Armador criativo |
| Centroavante | Atacante pivô, atacante móvel |
| Meio-campista aberto / ponta | Ponta aberto, atacante interior |

As instruções modificam referências de posicionamento e preferências da IA na
partida 3D, conforme o papel: avanço, apoio, marcação, passe ou cruzamento.
Não são movimentos garantidos a cada lance. O simulador rápido de placares ainda
não avalia essas instruções. Os ajustes pertencem à carreira, não ao cadastro
permanente do jogador; outros clubes e partidas rápidas mantêm seus padrões.

## O que os atributos já fazem na partida

A tabela completa de efeitos e limitações está em [Atributos e inteligência da partida](ATTRIBUTE-EFFECTS.md).

O adaptador copia os quinze atributos da base para os jogadores temporários. O
motor aplica curvas de balanceamento antes de usá-los: valores maiores não
representam aumentos lineares em metros por segundo ou probabilidade de sucesso.
Velocidade e aceleração afetam o movimento; finalização afeta precisão e decisão
de chute; potência controla a força separadamente; passe e lançamento alimentam
a precisão conforme a distância. Reação reduz a espera para uma nova decisão e
participa das defesas do goleiro. Posicionamento reduz o desvio da posição ideal.

As funções táticas são preferências de uma IA baseada em regras. Ainda não existe
um atributo separado de inteligência tática, familiaridade com a função ou um
planejador que avalie conjuntamente cobertura, espaços e momento do desmarque.
Por isso, aumentar posicionamento não promete decisões mais sofisticadas. Nesta
integração foram corrigidas ligações herdadas de potência/finalização,
reação/posicionamento e pesos de passe curto/longo, além do atraso de reação que
era zerado por divisão inteira.
