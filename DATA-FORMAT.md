# Contratos de base e mídia

Estado: contratos JSON v1, v2 e v3 em 29/09/2026. V2 acrescenta presets de
aparência; v3 acrescenta campeonatos e edições com regulamento e datas de rodadas.
Pacotes ZIP, mídia carregável e skins completas nas seções seguintes
continuam sendo extensões planejadas. Consulte [README-DATABASE.md](README-DATABASE.md),
[ROADMAP.md](ROADMAP.md) e [ARCHITECTURE.md](ARCHITECTURE.md).

## Contratos executáveis atuais: JSON v1, v2 e v3

Schema: [database-v1.schema.json](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v1.schema.json).
Extensão de aparência: [database-v2.schema.json](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v2.schema.json).
Extensão de competições: [database-v3.schema.json](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v3.schema.json).
Exemplo: [four-clubs.database.json](Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json).

O arquivo é JSON UTF-8 simples, não ZIP. Seu objeto raiz contém exatamente:

| Campo obrigatório | Conteúdo |
| --- | --- |
| schemaVersion | Inteiro 1, 2 ou 3 |
| databaseId | ID permanente da base |
| databaseRevision | Inteiro de 1 a 2147483647 |
| clubs | Um ou mais objetos com id e name |
| players | Um ou mais jogadores com id, name, naturalPositions, heightCm, weightKg e attributes |
| memberships | Zero ou mais vínculos clubId/playerId; um clube inicial por jogador |
| visualProfiles | Zero ou mais perfis com playerId e skin; appearance opcional em v2/v3; no máximo um por jogador |
| competitions (apenas v3) | Até 128 campeonatos com id e name |
| competitionEditions (apenas v3) | Até 128 edições com participantes, datas e regras suportadas |

`skin` contém `skinId`, `revision` inteira positiva e `compatibilityProfile`.
O importador valida a forma da referência e a existência de PlayerId. A ponte do
amistoso aceita `builtin-player`, revisão 1, perfil `football-player-v1`. Em v2/v3,
uma `appearance` completa define os sete presets; se omitida, usa a aparência
local associada ao PlayerId ou o default declarado com diagnóstico. Perfil
ausente também mantém o comportamento legado. V1 continua válido e rejeita o
novo campo; a migração é explícita por `schemaVersion: 2`.
Outras skins/revisões/perfis bloqueiam um clube quando estão entre os onze
escalados. O catálogo pode registrá-los, mas ainda não há download ou processamento
de modelos externos; não existe substituição silenciosa de uma skin solicitada.

### Aparência portátil v2

`appearance` é opcional, mas, quando presente, precisa conter exatamente os sete
campos abaixo, sem nulls nem valores desconhecidos. Os IDs são sensíveis a
maiúsculas. A aparência só é aceita com `builtin-player@1` e
`football-player-v1`; não modifica nem substitui uma skin personalizada.

| Campo | IDs disponíveis |
| --- | --- |
| skinTone | tone-1, tone-2, tone-3, tone-4, tone-5, tone-6 (do mais claro ao mais escuro na paleta atual) |
| hairStyle | none, short, styled, styled-alt, mohawk, locs, short-parted |
| hairColor | brown, dark-brown, black, light-yellow, yellow, gray, white, green, dark-green, blue, dark-blue, light-red, red, light-orange, orange |
| beardStyle | none, mustache, goatee, full |
| beardColor | Os mesmos IDs de hairColor |
| bootsColor | black, red, orange, purple, cyan, gray, white |
| sockAccessoryColor | none, black, gray, white |

Exemplo de campo em um perfil v2:

```json
"appearance": {
  "skinTone": "tone-3",
  "hairStyle": "short",
  "hairColor": "black",
  "beardStyle": "goatee",
  "beardColor": "dark-brown",
  "bootsColor": "cyan",
  "sockAccessoryColor": "white"
}
```

`none` remove cabelo/barba/faixa conforme o campo. `full` resolve para a barba
longa existente; `sockAccessoryColor` colore somente a faixa/acessório, enquanto
o meião completo pertence ao uniforme. Os nomes identificam a paleta legada:
`bootsColor: red`, por exemplo, corresponde atualmente a um rosa avermelhado.
As cores e meshes podem ser ajustadas nos recursos Unity; isso exige novo build.

O adaptador `BuiltinAppearanceMapper` converte os IDs para os enums do legado.
DTOs armazenam strings portáteis; Domain/Application não dependem desses enums
ou carregam materiais. Uma aparência completa dispensa binding de PlayerEntry.
Ela não altera altura, peso, atributos, colisores ou regras de gameplay.

### Dados esportivos e validação

Cada jogador declara suas posições dentre GK, RB, LB, CB, DM, CM, RM, LM, AM, LW,
RW e ST, sem repetição. Altura é um inteiro entre 150 e 210 cm; peso entre 45 e
100 kg. Os quinze atributos obrigatórios são strength, acceleration, topSpeed,
dribbleSpeed, jump, tackling, ballKeeping, passing, longBall, agility, shooting,
shootPower, positioning, reaction e ballControl; todos inteiros entre 0 e 100.

Nomes têm até 100 pontos de código Unicode e não podem conter apenas espaços.
Não são normalizados silenciosamente. IDs têm 1 a 64 caracteres ASCII dentre
letras, números, ponto, sublinhado e hífen, começando por letra ou número. São
sensíveis a maiúsculas. Jogador sem vínculo representa um jogador sem clube;
elencos vazios ou maiores que onze são válidos no catálogo. LineupPlanner verifica
se o clube pode fornecer um goleiro natural e dez jogadores de linha ao amistoso.

Objetos não aceitam propriedades desconhecidas. Null, campos obrigatórios ausentes e conversões
implícitas de strings para números são rejeitados. Competições e regras exigem v3.
Novos tipos de regra, caminhos de arquivo e recursos binários exigem outra evolução
explícita de versão e importador; não são campos silenciosamente ignorados.

O runtime impõe até 1 MiB UTF-8 e profundidade 32. JSON deve usar aspas duplas,
sem comentários, vírgulas finais ou chaves repetidas. Campos inteiros exigem token
inteiro: `1.0` e `1e0` são rejeitados, mesmo que validadores JSON Schema os tratem
matematicamente como inteiros. Essas verificações de sintaxe/tamanho, a unicidade
de IDs e as referências cruzadas complementam o schema Draft 7 fornecido.

O importador devolve sucesso com um snapshot completo, ou erros com `Code`,
`Path` e `Message`, sem catálogo parcial. Uma importação não altera a sessão.
O bootstrap só chama `CatalogSession.Activate` depois do sucesso; falhas conservam
o catálogo e os perfis visuais anteriores.

Os IDs aleatórios do exemplo foram gerados uma vez e ficam gravados no arquivo;
não devem ser regenerados ao editar nomes. Milano, London e Catalagna preservam
os dados copiados dos assets originais e posições inferidas das formações na
migração inicial. A revisão 2 substitui Royal por São Paulo FC e seus 39 atletas,
com novas identidades. Nomes e alturas têm fontes oficiais; pesos, atributos e
adaptações táticas são demonstrativos, conforme [SAO-PAULO-DATA.md](SAO-PAULO-DATA.md).
Escudo e dois uniformes são recursos Unity associados ao ClubId, sem acrescentar
campos de mídia ao JSON. A revisão 3 usa v2 e registra as sete escolhas visuais
copiadas dos bindings genéricos dos 72 jogadores; não são feições pesquisadas dos
atletas. A edição local compatível do JSON é lida por refresh
no Compose; novos recursos compilados ainda requerem build.

O editor local salva JSON validado com controle de concorrência por ETag/If-Match,
incremento de `databaseRevision` no servidor e substituição atômica do arquivo.
Exportar um rascunho não muda a revisão publicada. A aplicação pode ler v1 e
promove v1 para v2 ao definir uma aparência; uma base v3 permanece v3 e conserva
suas competições. Os schemas de autoria são
separados; `StreamingAssets/FootballWorld/database.schema.json` e
`/editor/api/schema` publicam as três versões suportadas.

### Campeonatos e edições v3

O campeonato tem identidade própria (`id`, `name`); cada edição declara `id`,
`competitionId`, `name`, `participantClubIds`, `roundDates` e `rules`.
Os participantes são 2 a 64 ClubIds distintos e existentes. As datas são civis,
sem fuso horário, no formato exato `AAAA-MM-DD`, com anos 0001 a 9999 e dias reais.
Devem ser estritamente crescentes; início e fim são derivados da primeira e da
última rodada. V1/v2 continuam aceitos, com listas de competições vazias.

O regulamento implementado é:

```json
{
  "type": "round-robin",
  "version": 1,
  "legs": 1,
  "points": { "win": 3, "draw": 1, "loss": 0 },
  "tieBreakers": ["wins", "goal-difference", "goals-for"]
}
```

`legs` aceita 1 ou 2. Pontos são inteiros entre 0 e 100, com vitória maior que
empate e empate maior ou igual à derrota. A ordem de desempate acima é fixa
nesta versão; um empate esportivo completo conserva posição compartilhada.
Para N participantes, são necessárias `(N par ? N−1 : N) × legs` datas. Clubes
ímpares têm folgas. IDs de confrontos são gerados uma vez ao criar a sessão;
confrontos, resultados e classificação pertencem ao progresso, não ao JSON autoral.

A revisão 6 da amostra adiciona a Liga de demonstração, em 3, 10 e 17 de outubro
de 2026: quatro clubes, três rodadas e seis jogos, turno único, 3/1/0 pontos.
Esse calendário é demonstrativo e não representa um campeonato oficial.
O editor web conserva e valida estes dados ao editar jogadores/clubes; a autoria
de competições ainda é feita no JSON. Excluir um clube participante é bloqueado
até remover sua participação explicitamente.

O mês/ano da carreira é estado da carreira, separado das datas da base. Escolher
outro período não fabrica elencos históricos: uma futura base desse período
deverá fornecer os clubes, jogadores, regras e edições correspondentes.

## Extensões planejadas

As seções abaixo orientam as próximas versões. Elas não descrevem campos extras
aceitos pelos importadores atuais nem funcionalidades já disponíveis.

## Versões e identidades

- `schemaVersion`: versão do formato de intercâmbio.
- `databaseId`: identidade estável de uma base; não muda ao renomeá-la.
- `databaseRevision`: revisão imutável do conteúdo dessa base.
- `assetId`/`skinId` e revisão: identidades estáveis de mídia e skins.
- `compatibilityProfile`: versão do contrato visual exigido por uma skin.

IDs são strings opacas, geradas uma vez. Exemplos legíveis neste documento não
impõem IDs derivados de nomes. Não usar índices, caminhos absolutos, Unity GUIDs
ou nomes de pessoas como chaves persistentes. Referências fixam revisão; não usar
um alias mutável como `latest` para reproduzir uma temporada salva.

O manifesto registra arquivos, tipos, tamanhos e SHA-256 do conteúdo. O hash
identifica/verifica bytes; não comprova autoria ou confiança de um pacote. A
especificação executável definirá como calcular o digest do conjunto sem incluir
o próprio digest de forma recursiva.

## Base esportiva e imagens

Formato proposto: `.futdb`, um ZIP com manifesto, dados JSON e mídia opcional.

```text
base.futdb
  manifest.json
  database.json
  media/
    logos/club-001.png
    portraits/player-001.jpg
  skin-packs/                   # opcional: conteúdo já preparado e versionado
```

`database.json` conterá coleções de clubes, jogadores, vínculos iniciais de elenco,
competições, edições/participantes e regulamentos. Aparência e associação de mídia
ficam em perfis visuais associados aos IDs, separados dos atributos esportivos.
Listas de resultados e progresso pertencem ao save versionado separado da base;
veja [README-GAME-MODES.md](README-GAME-MODES.md).

Regulamentos declaram um tipo suportado e seus parâmetros. O primeiro tipo é liga
de turno único. JSON não contém código, expressões executáveis nem nomes de
classes a instanciar. Tipos ou versões incompatíveis são rejeitados com uma
mensagem que identifique a competição e o campo.

PNG/JPG são os primeiros formatos propostos para imagens. Referências usam
`assetId`; o manifesto resolve caminhos relativos ao pacote. O carregador Unity
cria as texturas e adapta a exibição. Um escudo colorido precisa de uma rota de
material adequada: o legado usa máscaras de duas cores. Uniformes seguem o layout
e os slots declarados pelo perfil de compatibilidade.

JSON Schema valida a estrutura. Verificações semânticas adicionais validam IDs
únicos, referências existentes, limites de atributos e capacidade de formar uma
escalação válida. O cadastro pode representar jogadores sem clube; um vínculo
existente nunca pode apontar para um jogador ou clube inexistente.

## Fonte de skin da comunidade

Formato proposto: `.fbskin-source`, ZIP portátil para o editor e o worker.

```text
player-skin.fbskin-source
  manifest.json
  model/player.fbx
  textures/face.png
  textures/body.png
  textures/hair.png
  preview/thumbnail.png         # opcional; não substitui a prévia processada
```

O manifesto descreve a identidade e revisão da skin, autor, nome de exibição,
perfil de compatibilidade, arquivo do modelo, mapeamento de ossos/material slots
e arquivos de textura. Nome de exibição, como "Messi", não é a chave da skin.
São permitidos modelos completos dentro do perfil, além de variações de textura
do modelo de referência; a primeira versão do editor não pode restringir o usuário
apenas a trocar cores ou escolher presets.

A primeira rota de fonte usa FBX e imagens PNG/JPG seguindo o template de autoria.
O template e o perfil precisam ser construídos e testados antes de abrir essa
rota a usuários. GLB é uma extensão futura sujeita a avaliação de importador; não
aceitar e descartar silenciosamente recursos que ainda não conseguimos converter.

O perfil terá IDs estáveis para ossos/slots exigidos e regras para escala, pose,
skinning e materiais. A especificação de limites de arquivo, textura, malha e
memória será fixada após a prova técnica WebGL, antes da entrega do upload. A
importação valida conteúdo real, referências e limites, além de extensões.
Extração de ZIP fica confinada à área temporária do trabalho, com limites de
quantidade/tamanho descompactado e sem caminhos externos. Apenas formatos de
conteúdo permitidos são aceitos; não importar scripts ou plugins do pacote.

## Conteúdo de skin preparado para o jogo

Formato proposto: `.fbskin`, com manifesto de runtime e conteúdo Unity preparado.
Não equivale ao pacote fonte e não requer FBX no navegador.

O manifesto registra SkinId/revisão, digest da fonte, versão do processador,
compatibilityProfile, alvo `WebGL`, versão Unity, compatibilidade do player,
catálogo/endereço do prefab visual, bundles e dependências com seus hashes.
Um mesmo pacote fonte pode gerar artefatos diferentes para WebGL e desktop.

O worker constrói prefabs/materiais com componentes fornecidos pelo projeto e
prepara o Avatar compatível. Os controles, animações de gameplay e eventos de
contato continuam sob responsabilidade do jogo. A skin não introduz código ou
substitui colliders. A aparência é ajustada às dimensões de gameplay do jogador.

Fluxo de processamento exposto pelo editor:

```text
Recebido -> Validando -> Compilando -> Pronto para revisão -> Disponível
                    \-> Requer correção
                                  \-> Falha de processamento
```

Cada trabalho tem um ID e identifica fonte/revisão. Cancelar ou reenviar uma fonte
não permite que a conclusão de um trabalho antigo substitua a revisão nova.
"Pronto para revisão" exige prévia do resultado processado com o rig, materiais e
animações do jogo. A revisão confirma a aparência, não substitui validações
automáticas. Falhas mantêm a última revisão válida e apontam o que corrigir.

Exemplo parcial de associação em um perfil visual:

```json
{
  "playerId": "player-001",
  "portraitAssetId": "portrait-001",
  "skin": {
    "skinId": "skin-001",
    "revision": 1,
    "compatibilityProfile": "football-player-v1",
    "fallbackSkinId": "builtin-player-v1"
  }
}
```

Os valores e campos desse exemplo são ilustrativos da extensão futura; não formam
um objeto válido dos contratos v1/v2/v3 atuais. O perfil visual não altera velocidade,
força, IA, colisão ou regras.
Referências abreviadas como `portraitAssetId` e `fallbackSkinId` são resolvidas
pelo manifesto imutável da base para revisões e digests exatos. Isso também vale
para o catálogo de recursos embutidos; restaurar um save não consulta um alias
mutável para decidir qual retrato ou fallback carregar.

## Importar, exportar e distribuir

- O editor permite enviar a fonte, acompanhar o worker e selecionar uma skin
  disponível. No primeiro ambiente, enviar significa importar para um worker
  local; o mesmo fluxo poderá ganhar um serviço hospedado.
- Exportação portátil inclui os pacotes gráficos preparados necessários para a
  plataforma declarada. Uma exportação dependente de catálogo externo lista as
  dependências exatas e informa que elas não estão incluídas.
- O runtime verifica identidade, revisão, compatibilidade e integridade antes de
  ativar conteúdo. Recurso obrigatório inválido bloqueia a importação; recurso
  visual com fallback declarado pode usar o padrão com aviso e manter o vínculo.
- Preparar tudo em uma área temporária e ativar a revisão somente após validação.
  A base ativa e a skin anterior permanecem intactas em falhas.
- Importar no editor ou exportar para um arquivo não publica automaticamente o
  conteúdo em uma galeria. Hospedagem, contas e distribuição pública serão uma
  entrega separada; o pacote deve continuar transferível como arquivo.
- No WebGL, seleção de arquivos, cache e persistência são adaptadores do navegador.
  Não depender de caminhos absolutos da máquina do autor. Não afirmar que o pacote
  sobrevive a recargas antes de implementar e verificar persistência de mídia.

Referências: [JSON Schema](https://json-schema.org/overview/what-is-jsonschema),
[imagens em runtime](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ImageConversion.LoadImage.html),
[Avatar humanoide](https://docs.unity3d.com/2022.3/Documentation/Manual/ConfiguringtheAvatar.html)
e [distribuição Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@1.22/manual/remote-content-intro.html).
