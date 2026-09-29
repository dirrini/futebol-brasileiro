# Formatos, campeonatos e edições

O editor em `http://localhost:8080/editor/` separa três cadastros:

- **Formato**: participantes esperados, fases, pontuação, desempates, confrontos,
  mando, limite de substituições e resultados esportivos como acesso/rebaixamento.
- **Campeonato**: nome, formato padrão, abrangência, elegibilidade, reputação,
  nível de premiação, valores dos prêmios, destinos das vagas, logo e taça.
- **Edição**: formato escolhido, participantes, composição dos grupos e datas.

O formato pode ser reutilizado. Alterar o padrão de um campeonato não troca o
formato das edições já cadastradas. Alterar um formato valida todas as edições que
o utilizam antes de publicar a base. Saves conservam a revisão integral com que
foram criados, incluindo o regulamento. Para testar regras novas, salve o editor,
atualize o jogo e crie uma nova carreira ou campeonato.

## Fases e ramificações

Cada fase tem ID estável, nome e modelo de liga ou rodada eliminatória. É possível
adicionar, remover, ordenar e configurar fases sem criar um novo tipo em C#.
Uma fase posterior escolhe sua origem: classificados, vencedores ou derrotados
de uma fase anterior. Referências só apontam para fases anteriores na ordem de
autoria, impedindo ciclos. O calendário de uma fase começa após sua origem;
ramificações diferentes podem ocorrer em paralelo.

Ligas aceitam turno único ou ida e volta, pontos por vitória/empate/derrota,
critérios ordenados de desempate e confrontos entre todos, dentro dos grupos,
entre grupos ou um calendário parcial autorado. Os critérios são vitórias,
saldo, gols marcados, menos vermelhos, menos amarelos e sorteio reproduzível.
O sorteio encerra a ordem para que vagas e confrontos sempre tenham resolução.
Cartões continuam complementos de simulação, não eventos individuais da partida 3D.

Cada fase eliminatória é uma rodada: quartas, semifinal e final são fases
separadas, com jogo único ou ida e volta. Confrontos podem usar cabeças de chave,
sorteio ou adversários de grupos diferentes. O mando pode privilegiar o melhor
classificado, o primeiro da chave, ser sorteado ou ocorrer em campo neutro.
Em ida e volta, melhor campanha concede o mando da volta; primeiro da chave
concede o mando da ida. A opção de gols fora vale
apenas para confrontos de ida e volta; empate pode ser resolvido por pênaltis ou
pela melhor campanha, nas combinações aceitas pelo motor.

**A fase que decide o título é explícita.** Uma competição também pode não ter
taça. Assim, um playoff de acesso não substitui o campeão da chave principal.
A competição só termina quando todas as ramificações terminam.

## Exemplo: liga, dois grupos e final

1. Configure a liga inicial e classifique os oito primeiros no geral.
2. Crie uma fase de liga com dois grupos e origem nos classificados dessa liga.
3. Na edição, atribua as posições **1, 4, 5, 8** ao grupo A e **2, 3, 6, 7** ao B.
   São posições da fase anterior; os clubes reais só serão conhecidos ao jogá-la.
4. Escolha confrontos dentro de cada grupo. Quatro clubes por grupo precisam de
   três rodadas em turno único ou seis em ida e volta.
5. Configure um resultado de acesso/classificação para posições 1–2 de cada grupo
   e associe seu destino no campeonato, quando houver uma competição de destino.
6. Classifique um clube por grupo para uma final, escolha um ou dois jogos e marque
   essa final como a fase que decide o título.

## Exemplo: playoff dos derrotados

Mantenha a origem da semifinal principal nos vencedores das quartas. Adicione
outra fase eliminatória cuja origem sejam **os derrotados das quartas**. Os quatro
derrotados disputam duas vagas; configure um resultado de acesso/classificação para
os dois vencedores. A fase que decide o título permanece sendo a final principal.
Não é necessário criar uma final fictícia entre os vencedores do playoff.

## Elegibilidade, vagas e prêmios

Elegibilidade combina países, estados, lista permitida e exclusões. Estados exigem
um país para evitar códigos ambíguos. O clube tem seu próprio campo de estado;
não é inferido pela cidade ou estádio. Ser elegível não inscreve automaticamente
o clube: participantes continuam sendo definidos na edição.

Regras de resultados selecionam intervalos de classificação geral ou por grupo.
O núcleo produz registros de acesso, classificação e rebaixamento. O campeonato
pode associar cada resultado a outra competição cadastrada. Isso não cria ainda
uma temporada seguinte ou uma carreira com calendários simultâneos; os registros
são a base para essa futura coordenação.

Premiação tem moeda e valores por participação, vitória, empate e intervalos de
classificação em uma fase. A carreira registra esses valores uma única vez e
reconstrói o extrato ao carregar. Vitória/empate por jogo consideram o placar do
jogo, não o agregado da eliminatória. A premiação por classificação ocorre após
todos os jogos da fase. Não há câmbio automático: prêmios positivos precisam usar
a moeda da carreira. Campos reputação/nível de premiação são metadados editáveis;
não determinam valores financeiros automaticamente.

## Mídia, substituições e estádio

Logo, imagem e modelo 3D da taça são referências opcionais a HTTPS ou caminhos
relativos de um pacote. Não armazenam binários em JSON. Esta etapa não baixa nem
prepara modelos, não verifica compatibilidade de rig/material e não cria a
animação de premiação. O editor identifica esse uso futuro.

O limite de substituições faz parte do regulamento e chega ao contexto da partida,
mas o motor legado ainda não oferece banco de reservas e tela de trocas. Não é
apresentado como substituições já jogáveis.

Campo neutro exige uma referência de estádio e não concede bilheteria de mandante
à carreira. A identidade do estádio é dado do confronto: o cenário 3D continua
usando o estádio compilado existente até a integração de modelos de estádios.

## Calendário e limites

O gerador dentro de grupos monta ligas independentes por grupo. O gerador entre
grupos usa a tabela circular completa e elimina confrontos proibidos; por isso
pode conter folgas e usar mais rodadas do que o mínimo matemático. Um calendário
autorado na fase inicial permite datas/confrontos exatos, respeitando a política
de adversários escolhida. Fases posteriores recebem clubes a partir dos resultados.
Datas por confronto das eliminatórias permitem distribuir uma rodada em dias
diferentes sem fixar antecipadamente os classificados.

Os limites de participantes, fases, jogos e payload são explícitos e validados.
Formatos são combinações das regras esportivas suportadas, não scripts livres.
Regras desconhecidas, ciclos, referências quebradas, grupos incompletos e
classificações incompatíveis são rejeitados, preservando o rascunho e a última base
válida. Importação antiga v1–v5 e seus saves continuam suportados.

O exemplo Paulista 2026 foi convertido preservando IDs, participantes e os 64
confrontos oficiais já cadastrados. Os modelos adicionais são exemplos editáveis,
não afirmações sobre regulamentos oficiais de outros anos.
