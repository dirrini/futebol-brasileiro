# Calendário e regulamento do Paulistão 2026

A edição de exemplo usa os 16 clubes da base histórica e o calendário publicado
pela FPF em dezembro de 2025. Resultados, classificação e classificados são
produzidos pelo save do jogador. A base não contém os resultados reais de 2026.

## Calendário publicado

Os 64 confrontos da primeira fase preservam adversários, mandos e datas individuais
da [tabela da FPF de 22/12/2025, páginas 2–3](https://futebolpaulista.com.br/Repositorio/Noticia/30630/30630_221220251050_0.pdf).
Cada clube disputa oito partidas, quatro em casa e quatro fora. A primeira rodada
ocorre em 10–11 de janeiro e a última em 15 de fevereiro. O relógio da carreira
trabalha com dias civis; horários de transmissão não são simulados.

Esta é a agenda publicada antes da temporada, sujeita a alterações posteriores.
Por exemplo, Corinthians x Capivariano permanece em 1º de fevereiro, conforme
essa publicação. Não confundir com a tabela consolidada dos jogos efetivamente
realizados, nem com resolução histórica de contratos ou transferências.

As datas das eliminatórias complementam o calendário com publicações posteriores
da FPF. A ordem dos slots foi preservada, mas os participantes são definidos pela
campanha jogada:

| Slot | Dia de 2026 | Origem |
| --- | --- | --- |
| Quartas: 1º x 8º | 22/02 | [FPF, 16/02](https://futebolpaulista.com.br/Noticias/Detalhe.aspx?Noticia=31989) |
| Quartas: 2º x 7º | 21/02 | Mesma publicação |
| Quartas: 3º x 6º | 21/02 | Mesma publicação |
| Quartas: 4º x 5º | 22/02 | Mesma publicação |
| Semifinal: 1ª x 4ª campanha dos classificados | 28/02 | [FPF, 23/02](https://futebolpaulista.com.br/Noticias/Detalhe.aspx?Noticia=32026) |
| Semifinal: 2ª x 3ª campanha dos classificados | 01/03 | Mesma publicação |
| Final: ida | 04/03 | [FPF, 02/03](https://www.futebolpaulista.com.br/noticias/Detalhe.aspx?Noticia=32069) |
| Final: volta | 08/03 | Mesma publicação |

Os horários e locais desses jogos foram definidos depois das respectivas
classificações reais; não se afirma que estivessem todos conhecidos em janeiro.

## Regras executáveis

O perfil `paulista-2026`, versão 1, concede 3/1/0 pontos. Os oito primeiros avançam;
os dois últimos da primeira fase são marcados como rebaixados. Quartas e
semifinais têm jogo único. As semifinais reorganizam os quatro sobreviventes
pela campanha acumulada, incluindo as quartas. O melhor classificado manda as
quartas; a melhor campanha acumulada manda a semifinal e a volta da final.
Desempates: vitórias, saldo, gols pró, menos vermelhos, menos amarelos e sorteio.
Referência: [REC, artigos 9–16, páginas 6–9](https://futebolpaulista.com.br/Repositorio/Noticia/30546/30546_1.pdf).

A final usa o agregado de duas partidas, sem gol visitante ou prorrogação. Igualdade
no agregado leva aos pênaltis, conforme [esclarecimento da FPF de 01/03](https://futebolpaulista.com.br/Noticias/Detalhe.aspx?Noticia=32067)
e o [cenário oficial de classificação publicado em 07/03](https://futebolpaulista.com.br/Noticias/Detalhe.aspx?Noticia=32095).
O REC inicial deixava a quantidade de partidas da final para definição posterior.

A tabela exibida representa campanha acumulada. Campeão, vice e rebaixados têm
decisões próprias; não usar a ordem acumulada como classificação final oficial
dos eliminados nem como concessão de vagas nacionais futuras.

## Autoria no editor

Em `http://localhost:8080/editor/`, o cadastro de campeonatos separa a identidade
da competição de suas edições. Cada edição aponta participantes por ID, datas,
perfil de regras e, quando exigidos, confrontos e slots das eliminatórias.
O editor mantém rascunho, validação e salvamento explícito da base inteira.

O contrato v5 acrescenta `authoredFixtures` e `playoffDates` ao perfil paulista.
Cada confronto autoral tem ID, rodada, dia, mandante, visitante e referência de
estádio opcional. As oito entradas de `playoffDates` seguem a ordem da tabela
acima. Confrontos finais são criados pelo core quando os classificados são
conhecidos; não são resultados pré-carregados. Perfis desconhecidos são rejeitados.

Ligas de turno único ou ida/volta continuam disponíveis com `round-robin`, versão
1. Alterar o JSON compatível e atualizar a página oferece a edição nova a novos
saves; uma carreira ou campeonato existente preserva sua revisão de origem.

## Limites do protótipo

- Cartões e disputas de pênaltis são complementos determinísticos de simulação,
  identificados na interface. Não são eventos capturados da partida 3D.
- O último desempate usa sorteio reproduzível por temporada. Não é um sorteio
  público realizado pela FPF.
- Cartões entram na comparação de campanhas; suspensões individuais, inscrições,
  listas A/B, recursos disciplinares e vagas em torneios nacionais ficam para
  etapas posteriores. Marcar rebaixados não cria automaticamente a edição de 2027.
- O estádio principal de um clube continua sendo seu cadastro oficial. Uma cidade
  alternativa na publicação da FPF não substitui esse vínculo. Referências de
  estádio no calendário são dados; esta etapa não importa modelos 3D de estádios
  nem implementa reformas, aluguel para eventos ou indisponibilidade por período.
- O calendário permanece no estado da competição, separado do motor 3D. FixtureId
  e ExecutionId correlacionam a conclusão; repetir um apito final não duplica pontos.

## Migração para formatos editáveis

A base atual v6/revisão 11 representa esse calendário com `format-paulista-2026`, fases declarativas e calendários por fase, preservando os IDs dos 64 confrontos iniciais e as datas cadastradas das eliminatórias. A primeira fase tem confrontos autorais; quartas/semifinais usam a campanha acumulada configurada em `rankingStageIds`. A final concede o título, e a primeira fase declara o rebaixamento. O perfil v5 acima continua suportado para bases e saves antigos. Outros formatos são modelos de autoria, não regulamentos oficiais atribuídos a competições reais.
