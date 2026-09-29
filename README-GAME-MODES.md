# Menu e modos de jogo

Abra [localhost:8080](http://localhost:8080) após `./scripts/webgl.ps1`.
O hub UGUI/TextMeshPro oferece Quick match, Championship, Career e Options.
Os comandos de passe, corrida, marcação, goleiro e finalização estão em
[Controles da partida 3D](README-MATCH-CONTROLS.md).

## Quick match

Escolha país e clube de cada lado, uniforme e configurações e entre na partida
3D. A base atual tem 16 clubes do Paulista e 363 relacionados na rodada de abertura.
Apelidos têm prioridade nos menus. Amistosos não alteram os saves de competição.

## Championship

Escolha **Campeonato Paulista · Paulista 2026** e seu clube. A primeira fase tem
64 jogos em oito rodadas. Oito clubes avançam às quartas; os dois últimos caem.
Quartas e semifinais são únicas, com mando e chaveamento pela campanha; a final
é disputada em duas partidas. Veja [calendário, fontes e regras](CALENDAR-2026.md).

Você pode jogar o confronto no motor 3D ou simulá-lo. Os outros jogos da rodada
são simulados. Após a eliminação, **Simular rodada** permite acompanhar a decisão.
A tabela exibe campanha acumulada; o campeão e os rebaixados são indicados ao fim.
Cartões, sorteio e pênaltis são simulados e não representam eventos jogáveis em 3D.

O apito final registra o placar antes de descarregar o motor. Abandono deixa o jogo
pendente; cada tentativa tem outra identidade. Resultados repetidos não somam
pontos novamente. Uma partida interrompida por refresh volta ao início.

Campeonatos anteriores mantêm sua revisão e seus clubes, inclusive a antiga liga
demonstrativa. Ligas de turno único/ida e volta continuam suportadas e podem ser
cadastradas no editor, com participantes, pontos e datas próprias.

## Career

Crie nome/avatar de treinador, país, clube e mês/ano inicial. Para esta base, use
**janeiro de 2026**. A carreira começa em **01/01/2026** e exige uma edição do clube
que comece no mês escolhido. Datas sem calendário compatível recebem diagnóstico.
A escolha não inventa elencos de outros períodos; os relacionados de 10/11 de
janeiro são a aproximação cadastral usada nesta temporada.

O centro do treinador oferece:

- **Avançar 1 dia** ou **Até o próximo jogo**, parando no dia de um jogo seu pendente.
- Jogar em 3D ou simular quando chega a data da partida.
- Treino equilibrado, recuperação ou intenso, com condição/preparo e rendimento
  temporário no 3D. O catálogo original permanece intacto.
- Elenco e ficha com os quinze atributos; formação 4-4-2, 4-3-3 ou 4-2-3-1 e
  mentalidade defensiva, equilibrada ou ofensiva, com escalação automática.
- Busca de jogadores, propostas com reserva de caixa, cancelamento e resposta
  no próximo avanço diário. Contratações alteram somente o elenco dessa carreira.
- Caixa, folha mensal, receita mensal e bilheteria, com extrato de movimentações.
- Notícias de dois veículos fictícios, produzidas pelos acontecimentos da carreira.
- Calendário/classificação e criação de nova carreira com confirmação de substituição.

O avanço termina no fim da edição. Contratos, empréstimos, vendas, agendamento de
amistosos, temporadas seguintes e várias competições simultâneas ficam para as
próximas etapas. Detalhes e limites em [carreira embrionária](CAREER-PROTOTYPE.md).

Saves antigos de perfil continuam legíveis; não são convertidos silenciosamente
em temporadas. Para jogar a carreira diária, crie uma nova no mês disponível.
Os avatares continuam retratos gráficos 2D editáveis no Unity.

## Options

Idioma inglês/português, câmera padrão e dificuldade são salvos imediatamente.
O primeiro idioma é inglês. Nomes próprios vêm da base e não são traduzidos.

## Persistência e autoria

A competição e a carreira fixam o JSON completo da revisão ao nascer. Editar no
[editor da base](http://localhost:8080/editor/) e dar refresh oferece o conteúdo
para novas sessões, sem mudar uma temporada em andamento.

Campeonatos declarativos usam formato 3 (legados continuam no 2); carreiras diárias usam formato 4, acrescentando posições e funções individuais à tática
e propostas aos dias, resultados, treino, finanças e notícias. Carreiras diárias
v2/v3 e saves anteriores de formato 1 continuam aceitos. O armazenamento usa
dois slots por modo e compressão GZip; só troca o slot ativo depois de gravar.
Há limites de **8 MiB descomprimidos** e **112 KiB por slot armazenado**. A quota
real do navegador ainda pode rejeitar uma escrita: o jogo avisa e preserva o
último save confirmado. Não há recuperação automática de um slot corrompido.
Limpar dados do site apaga o progresso; localhost e 127.0.0.1 são origens distintas.

Domain/Application contêm as regras puras. Infraestrutura converte saves e
adapta a partida; Presentation apenas exibe consultas e envia comandos. Layout,
fontes, cores, tema, retratos e textos ficam nos prefabs/assets autorados. O comando
**Create game hub assets** acrescenta os controles novos preservando os existentes.
Escudos, uniformes e modelos pertencem ao build; o save não arquiva suas versões.

## Regulamentos e inspeção da equipe

Novas competições usam as fases declarativas configuradas no editor, incluindo grupos por posições da fase anterior e playoffs dos derrotados. A fase que concede o título é explícita; vagas e prêmios são processados separadamente. Saves existentes conservam seu regulamento original.

Na carreira, **Tática** mostra a formação em campo e a prévia dos onze escolhidos pelo planejador. Mudar a formação mostra um rascunho; o botão de aplicar confirma. **Elenco** e **Mercado** usam uma ficha com identidade, posições naturais e quinze atributos agrupados, na escala real 0–100. O layout foi inspirado nas referências fornecidas, sem inventar retratos, potencial, histórico ou estatísticas ausentes.
