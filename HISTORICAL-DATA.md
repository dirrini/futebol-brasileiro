# Base histórica — abertura do Paulistão 2026

A revisão 9 introduziu o cadastro histórico v4. A revisão 10, schema v5, mantém
esses elencos e adiciona [calendário e regulamento](CALENDAR-2026.md). O recorte substitui os clubes fictícios por **16 clubes e 363
jogadores relacionados nos oito jogos de 10 e 11 de janeiro de 2026**. É uma
fotografia verificável da primeira rodada, não a relação completa de contratos ou
inscrições do mês. Um atleta ausente dessa rodada pode pertencer ao clube e ainda
assim não estar neste recorte. Pesquisa realizada em 29/09/2026.

O arquivo continua em
[four-clubs.database.json](Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json)
para preservar os caminhos de importação e do Compose; o nome do arquivo é legado.
`databaseId`, o ID do São Paulo e os IDs dos 16 atletas presentes nas duas amostras
foram preservados. Os demais registros receberam IDs próprios. Saves existentes
continuam usando o catálogo fixado quando foram criados.

## Fontes e cobertura

| Súmula FPF | Clubes | Relacionados |
| --- | --- | --- |
| [Jogo 1](https://conteudo.fpf.org.br/sumulas/2026/3973/1.pdf) | São Bernardo / Capivariano | 23 / 23 |
| [Jogo 2](https://conteudo.fpf.org.br/sumulas/2026/3973/2.pdf) | Santos / Novorizontino | 23 / 22 |
| [Jogo 3](https://conteudo.fpf.org.br/sumulas/2026/3973/3.pdf) | Guarani / Primavera | 23 / 23 |
| [Jogo 4](https://conteudo.fpf.org.br/sumulas/2026/3973/4.pdf) | Portuguesa / Palmeiras | 23 / 20 |
| [Jogo 5](https://conteudo.fpf.org.br/sumulas/2026/3973/5.pdf) | Corinthians / Ponte Preta | 23 / 23 |
| [Jogo 6](https://conteudo.fpf.org.br/sumulas/2026/3973/6.pdf) | Velo Clube / Botafogo | 23 / 22 |
| [Jogo 7](https://conteudo.fpf.org.br/sumulas/2026/3973/7.pdf) | Noroeste / Red Bull Bragantino | 23 / 23 |
| [Jogo 8](https://conteudo.fpf.org.br/sumulas/2026/3973/8.pdf) | Mirassol / São Paulo | 23 / 23 |

Presença, grafia da súmula e clube histórico vêm desses documentos. Fichas públicas
da [FPF](https://futebolpaulista.com.br/Atletas/#Serie-A1) complementam 190 datas de
nascimento, nomes completos e nacionalidades. São consultas atuais de identidade,
não prova de vínculo em janeiro nem histórico de mudanças de nacionalidade.
Outras fichas técnicas de clubes, FPF e reportagens contemporâneas complementam
os apelidos e algumas funções. Todas as fontes utilizadas estão em
`snapshot.sources`, editáveis em **Referência da base**.

`nickname` é o apelido exibido no editor, nos menus e na partida. Se omitido,
`name` é usado; `fullName` fica separado. Nomes esportivos do São Paulo já presentes
na amostra anterior são preservados. Campos biográficos sem confirmação ficam
ausentes, não recebem valores inventados. Nomes truncados nas súmulas não viram
nomes completos supostamente confirmados: a ficha registra a pendência.

## Parâmetros do protótipo

Posições finas, altura/peso, aparência e atributos ainda não formam um levantamento
de scouting. Funções publicadas são adaptadas ao vocabulário do jogo; onde não há
posição específica confirmada, a ficha avisa que a distribuição é provisória para
a simulação. Goleiros confirmados permanecem separados dos jogadores de linha.
O planejador monta onze por posição e ID; não reproduz a escalação histórica.

Novos atletas usam altura e peso fictícios por molde de posição e os mesmos moldes
de quinze atributos da amostra anterior. Os 16 registros preservados do São Paulo
mantêm as alturas e parâmetros documentados em [SAO-PAULO-DATA.md](SAO-PAULO-DATA.md).
Presets de aparência são genéricos, editáveis e não representam traços verificados
de pessoas reais. Os clubes novos usam escudo e kits neutros compilados; o São Paulo
mantém os recursos visuais já existentes. Não há upload de skins nesta entrega.

Orçamentos, reputação, tamanho de torcida e patrocínio são campos disponíveis no
editor, mas ficam sem preenchimento nesta base enquanto não houver uma decisão
explícita de conteúdo de simulação. Pé preferido desconhecido também é omitido.
Não foram importados placares futuros, cartões, lesões, contratos ou estatísticas
do restante de 2026 para a fotografia de janeiro.

## Estádios

Os 16 clubes apontam para seu estádio principal, com país e cidade. Palmeiras
aponta para **Allianz Parque** e Bragantino para **Nabi Abi Chedid**, mesmo que
tenham utilizado outros estádios em janeiro. Esse vínculo não declara que o local
estava disponível para cada partida: o Nabi estava em reconstrução.

Capacidades preenchidas são números publicados por fontes identificadas, incluindo
o [guia de dezembro de 2025](https://record.r7.com/esporte-record/fotos/paulistao-2026-conheca-os-estadios-das-equipes-que-disputam-o-campeonato-estadual-27122025/)
e documentação oficial do Palmeiras. Não são laudos de lotação. Capacidades de
Primavera, Botafogo e Bragantino foram omitidas por divergência, ambiguidade ou obra.

O estádio 3D da partida continua genérico. O calendário v5 já permite declarar um
estádio específico por confronto, separado do principal do clube. Períodos de
reforma/eventos, seus conflitos e a escolha automática de mandos alternativos
ficam para depois. Receitas de eventos também não são simuladas; a gestão atual
tem receitas mensais e bilheteria simplificadas. Veja [ROADMAP.md](ROADMAP.md).

## Uso e limites

Edite em [localhost:8080/editor/](http://localhost:8080/editor/), salve e atualize a
página do jogo. Alterações de dados compatíveis não precisam de novo build; novos
modelos, assets compilados ou mudanças de contrato exigem o fluxo WebGL.

Quick match usa os 16 clubes, agrupados em Brasil. A carreira diária começa em
01/01/2026 quando janeiro é escolhido e usa este recorte dos relacionados como
aproximação inicial, sem resolver vínculos históricos por dia. O Paulista é
jogável em Championship e Career; calendário publicado, complementos das finais
e divergências de remarcação estão em [CALENDAR-2026.md](CALENDAR-2026.md).
Contratações por proposta já alteram os vínculos somente no save da carreira;
não representam transferências históricas pesquisadas. Histórico completo,
contratos e outras temporadas continuam planejados.
