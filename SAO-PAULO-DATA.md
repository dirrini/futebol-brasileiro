# Amostra do São Paulo FC

Consulta das fontes: **28/09/2026**. Esta é uma base de demonstração para testar
o cadastro externo e o amistoso, preparada para este projeto. Não é uma publicação
oficial do clube.

O arquivo [four-clubs.database.json](Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json)
contém São Paulo FC, Milano, London e Catalagna: quatro clubes e 72 jogadores.
O São Paulo substitui Royal e seus onze jogadores, com os **39 atletas** listados
na [página oficial do elenco profissional](https://www.saopaulofc.net/esporte/futebol-masculino-profissional/).
Os outros três clubes, seus 33 jogadores, vínculos e perfis visuais foram mantidos.

A inclusão do clube na revisão 2 preservou `databaseId`. A revisão 3 promove a base
a `schemaVersion: 2` e grava as sete escolhas de aparência dos 72 jogadores,
copiadas exatamente dos bindings já usados, sem mudar elenco ou atributos.
São Paulo FC recebeu o novo `ClubId` `club-8515fca92adc4d77a428fb272bb7d160`; cada atleta recebeu um novo
`PlayerId` opaco, gerado uma única vez e gravado no JSON. O novo clube não herda a
identidade esportiva de Royal. Futuras edições dos mesmos registros devem
preservar esses IDs, mesmo quando nomes, atributos ou vínculos mudarem.

## Dados consultados e dados de demonstração

- **Nomes e presença no elenco:** relação oficial consultada na data acima,
  incluindo jovens que integram o elenco profissional. Não é uma lista de
  relacionados para uma partida específica.
- **Alturas:** fichas oficiais individuais abaixo, convertidas de metros para
  centímetros. Para Sabino, a ficha estava indisponível; foi usada a altura
  publicada na notícia oficial de contratação de 2024. A presença dele no elenco
  atual está confirmada na relação profissional e no jogo de 19/09/2026.
- **Posições naturais:** adaptação das funções publicadas ao vocabulário do jogo.
  As alternativas CM/DM/AM e LW/RW/LM/RM são escolhas táticas do protótipo, não uma
  classificação oficial ou exaustiva das posições de cada atleta.
- **Pesos:** valores fictícios para teste, porque as fichas atuais não publicam
  esse dado. Nesta revisão, foram calculados como `round(23 * (heightCm / 100)^2)`
  em kg, apenas para manter proporções diferentes no modelo genérico. Não são
  medições dos jogadores nem recomendações de composição corporal.
- **Quinze atributos:** valores fictícios por molde de posição principal. Atletas
  do mesmo molde usam os mesmos valores; não representam scouting, estatísticas
  oficiais, desempenho atual ou comparação entre jogadores reais.
- **Disponibilidade:** todos os 39 estão disponíveis no teste. Lesões, suspensões,
  condição física, contratos e inscrição em competições não são simulados pelo
  contratos JSON v1/v2.
- **Escalação:** o amistoso seleciona onze por compatibilidade de posição e
  desempate por ID. Não escolhe os melhores atributos nem reproduz o time titular
  do São Paulo. Os demais atletas permanecem no catálogo, sem serem truncados.
- **Aparência:** todos referenciam `builtin-player`, revisão 1, perfil
  `football-player-v1`. São personagens genéricos do jogo, sem rostos ou modelos
  3D dos atletas. Tom de pele, cabelo/barba e cores de chuteiras/faixa da meia
  conservam os presets demonstrativos anteriores, agora editáveis no JSON v2 e
  no editor local. Não são características físicas verificadas dos jogadores reais.
  A configuração de uniforme e escudo pertence ao adaptador visual
  Unity, separado deste JSON; esta amostra não implementa skins da comunidade.

## Elenco e fontes individuais

As posições abaixo são os códigos efetivamente cadastrados. A primeira define o
molde dos atributos demonstrativos. Os links são fontes para identidade, função
geral e altura; não para os pesos ou atributos inventados para o teste.

| Atleta | Posições do protótipo | Altura (cm) | Fonte oficial |
| --- | --- | ---: | --- |
| Rafael Toloi | CB | 185 | [Ficha](https://www.saopaulofc.net/atleta/rafael-toloi/) |
| Arboleda | CB | 187 | [Ficha](https://www.saopaulofc.net/atleta/arboleda/) |
| Lucas | AM, RW, RM | 174 | [Ficha](https://www.saopaulofc.net/atleta/lucas/) |
| Marcos Antonio | CM, DM | 166 | [Ficha](https://www.saopaulofc.net/atleta/marcos-antonio/) |
| Calleri | ST | 181 | [Ficha](https://www.saopaulofc.net/atleta/calleri/) |
| Luciano | ST, AM | 181 | [Ficha](https://www.saopaulofc.net/atleta/luciano/) |
| Ferreira | LW, LM | 175 | [Ficha](https://www.saopaulofc.net/atleta/ferreira/) |
| Iago | LB | 181 | [Ficha](https://www.saopaulofc.net/atleta/iago/) |
| Enzo | LB | 177 | [Ficha](https://www.saopaulofc.net/atleta/enzo/) |
| Bobadilla | CM, DM | 181 | [Ficha](https://www.saopaulofc.net/atleta/bobadilla/) |
| André Silva | ST | 181 | [Ficha](https://www.saopaulofc.net/atleta/andre-silva/) |
| Wendell | LB | 176 | [Ficha](https://www.saopaulofc.net/atleta/wendell/) |
| Lucas Ramon | RB | 180 | [Ficha](https://www.saopaulofc.net/atleta/lucas-ramon/) |
| Buta | RB | 172 | [Ficha](https://www.saopaulofc.net/atleta/buta/) |
| Cédric | RB | 172 | [Ficha](https://www.saopaulofc.net/atleta/cedric/) |
| Domingos Duarte | CB | 192 | [Ficha](https://www.saopaulofc.net/atleta/domingos-duarte/) |
| Rafael | GK | 192 | [Ficha](https://www.saopaulofc.net/atleta/rafael/) |
| Victor Sá | LW, RW, LM | 175 | [Ficha](https://www.saopaulofc.net/atleta/victor-sa/) |
| Newton Jr. | DM, CM | 188 | [Ficha](https://www.saopaulofc.net/atleta/newton-jr/) |
| Pablo Maia | DM, CM | 178 | [Ficha](https://www.saopaulofc.net/atleta/pablo-maia/) |
| Coronel | GK | 192 | [Ficha](https://www.saopaulofc.net/atleta/coronel/) |
| Pedro Lima | RB | 178 | [Ficha](https://www.saopaulofc.net/atleta/pedro-lima/) |
| Luan | DM | 175 | [Ficha](https://www.saopaulofc.net/atleta/luan/) |
| Tete | RW, LW | 175 | [Ficha](https://www.saopaulofc.net/atleta/tete/) |
| Sabino | CB | 185 | [Contratação, 2024](https://www.saopaulofc.net/sao-paulo-define-a-chegada-do-zagueiro-sabino/) |
| Artur | RW, RM | 165 | [Ficha](https://www.saopaulofc.net/atleta/artur/) |
| João Pedro Pantiga | GK | 191 | [Ficha](https://www.saopaulofc.net/atleta/joao-pedro-pantiga/) |
| Gustavo Santana | LW, ST | 183 | [Ficha](https://www.saopaulofc.net/atleta/gustavo-santana/) |
| Lucca | LW, RW | 174 | [Ficha](https://www.saopaulofc.net/atleta/lucca/) |
| Pedro Ferreira | AM, CM | 176 | [Ficha](https://www.saopaulofc.net/atleta/pedro-ferreira/) |
| Djhordney | DM, CM | 181 | [Ficha](https://www.saopaulofc.net/atleta/djhordney/) |
| Ryan Francisco | ST | 175 | [Ficha](https://www.saopaulofc.net/atleta/ryan-francisco/) |
| Young | GK | 202 | [Ficha](https://www.saopaulofc.net/atleta/young/) |
| Felipe Preis | GK | 192 | [Ficha](https://www.saopaulofc.net/atleta/felipe-preis/) |
| Isac | CB | 186 | [Ficha](https://www.saopaulofc.net/atleta/isac/) |
| Osorio | CB | 191 | [Ficha](https://www.saopaulofc.net/atleta/osorio/) |
| Nicolas | LB | 176 | [Ficha](https://www.saopaulofc.net/atleta/nicolas/) |
| Cauly | AM, CM | 175 | [Ficha](https://www.saopaulofc.net/atleta/cauly/) |
| Danielzinho | CM, AM | 167 | [Ficha](https://www.saopaulofc.net/atleta/danielzinho/) |

Os cinco goleiros, seis zagueiros, quatro laterais direitos, quatro laterais
esquerdos, dez meias/volantes e dez atacantes totalizam os 39 atletas. Lucas está
contado entre os meias, e Luciano entre os atacantes, sem excluir suas posições
alternativas.

## Atualidade da amostra

O [anúncio de Pedro Lima](https://www.saopaulofc.net/sao-paulo-acerta-emprestimo-do-lateral-direito-pedro-lima/)
e a [apresentação de Iago](https://www.saopaulofc.net/novo-lateral-do-tricolor-iago-e-apresentado-no-superct/)
confirmam reforços do segundo semestre de 2026. A segunda fonte também menciona
Victor Sá, Buta, Newton Jr. e Domingos Duarte.

A [ficha da partida contra o Internacional em 19/09/2026](https://www.saopaulofc.net/no-morumbis-tricolor-derrota-o-internacional-pelo-campeonato-brasileiro/)
confirma a utilização recente de atletas da relação, incluindo Pedro Lima e
Sabino. A base representa a lista do site consultada em 28/09/2026; não é
sincronizada automaticamente com transferências ou alterações futuras.

Para alterar ou experimentar nomes, medidas, posições, atributos e aparência,
use [o editor local](http://localhost:8080/editor/) ou edite o JSON preservando seus
IDs. O editor incrementa a revisão ao salvar; uma edição manual deve fazê-lo
explicitamente. Consulte [README-DATABASE.md](README-DATABASE.md) para o
fluxo de publicação local e [DATA-FORMAT.md](DATA-FORMAT.md) para os limites do
contrato. As fontes ficam neste documento porque o JSON v1/v2 rejeita propriedades
adicionais de procedência e comentários.

## Escudo e uniformes do protótipo

O template visual local usa escudo triangular com a sigla SPFC e as cores
branca, vermelha e preta. As camisas são emulações geométricas simplificadas:

- Primeiro uniforme: branco, faixas horizontais vermelha e preta e escudo
  central, tomando como referência o [lançamento oficial de 2026](https://www.saopaulofc.net/new-balance-e-sao-paulo-futebol-clube-iniciam-contagem-para-o-centenario-com-nova-camisa-de-2026/).
- Segundo uniforme: listras verticais vermelhas, brancas e pretas, com escudo
  no peito esquerdo, conforme o [lançamento oficial do uniforme away](https://www.saopaulofc.net/tricolor-desde-sempre-new-balance-e-sao-paulo-apresentam-nova-camisa-away-para-2026/).

Não reproduzem patrocinadores, estrelas, detalhes de fabricação nem a coleção
completa. O kit do goleiro é um recurso de contraste do protótipo. As aparências
genéricas são associadas em sequência aos 39 IDs e não representam características
físicas individuais. Os recursos ficam em `Assets/FootballSimulator/Arts/Teams/SaoPaulo`.
Alterações nesse material compilado exigem build; o JSON de nomes, atributos e presets
pode mudar apenas com refresh após a instalação desta versão.
