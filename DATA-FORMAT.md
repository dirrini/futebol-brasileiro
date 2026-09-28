# Contratos de base e mídia

Estado: contrato JSON v1 de clubes e jogadores implementado em 28/09/2026.
Pacotes ZIP, campeonatos, mídia carregável e skins completas nas seções seguintes
continuam sendo extensões planejadas. Consulte [README-DATABASE.md](README-DATABASE.md),
[ROADMAP.md](ROADMAP.md) e [ARCHITECTURE.md](ARCHITECTURE.md).

## Contrato executável atual: JSON v1

Schema: [database-v1.schema.json](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v1.schema.json).
Exemplo: [four-clubs.database.json](Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json).

O arquivo é JSON UTF-8 simples, não ZIP. Seu objeto raiz contém exatamente:

| Campo obrigatório | Conteúdo |
| --- | --- |
| schemaVersion | Inteiro 1 |
| databaseId | ID permanente da base |
| databaseRevision | Inteiro de 1 a 2147483647 |
| clubs | Um ou mais objetos com id e name |
| players | Um ou mais jogadores com id, name, naturalPositions, heightCm, weightKg e attributes |
| memberships | Zero ou mais vínculos clubId/playerId; um clube inicial por jogador |
| visualProfiles | Zero ou mais perfis com playerId e skin; no máximo um por jogador |

`skin` contém `skinId`, `revision` inteira positiva e `compatibilityProfile`.
O importador valida a forma da referência e a existência de PlayerId. Disponibilidade
do modelo, rig, compatibilidade gráfica e download ainda não são verificados:
esta etapa não carrega skins. `builtin-player`, usado no exemplo, é uma referência
descritiva reservada para a futura ponte visual, não um registro já resolvido.

Cada jogador declara suas posições dentre GK, RB, LB, CB, DM, CM, RM, LM, AM, LW,
RW e ST, sem repetição. Altura é um inteiro entre 150 e 210 cm; peso entre 45 e
100 kg. Os quinze atributos obrigatórios são strength, acceleration, topSpeed,
dribbleSpeed, jump, tackling, ballKeeping, passing, longBall, agility, shooting,
shootPower, positioning, reaction e ballControl; todos inteiros entre 0 e 100.

Nomes têm até 100 pontos de código Unicode e não podem conter apenas espaços.
Não são normalizados silenciosamente. IDs têm 1 a 64 caracteres ASCII dentre
letras, números, ponto, sublinhado e hífen, começando por letra ou número. São
sensíveis a maiúsculas. Jogador sem vínculo representa um jogador sem clube;
elencos vazios ou maiores que onze são válidos no catálogo. Verificar a capacidade
de escalar uma partida será responsabilidade da futura aplicação de partidas.

Objetos não aceitam propriedades desconhecidas. Null, campos ausentes e conversões
implícitas de strings para números são rejeitados. Regras, competições, caminhos
de arquivo e recursos binários não podem ser acrescentados ao v1 sem uma evolução
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
não devem ser regenerados ao editar nomes. Nomes, medidas e atributos foram
copiados dos quatro assets originais. As posições iniciais foram inferidas das
formações existentes numa migração pontual; desde então são dados independentes.

## Extensões planejadas

As seções abaixo orientam as próximas versões. Elas não descrevem campos extras
aceitos pelo importador v1 nem funcionalidades já disponíveis.

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
Listas de resultados e progresso pertencem ao futuro formato de save.

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

Os valores são ilustrativos; nenhum desses IDs/perfis está registrado no jogo
atual. O perfil visual não altera velocidade, força, IA, colisão ou regras.
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
