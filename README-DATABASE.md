# Base externa e modos de jogo

O jogo carrega um catálogo de clubes/jogadores independente do motor Unity.
A base v5 contém os 16 clubes e 363 jogadores relacionados nas súmulas de abertura
do Paulista de 10 e 11 de janeiro de 2026. É um recorte dos relacionados nesses
jogos, não dos elencos completos. Veja [fontes e limitações](HISTORICAL-DATA.md).
A seleção e o amistoso usam esse catálogo para os dados esportivos e os presets
de aparência. O [editor local](http://localhost:8080/editor/) permite editar a base
no navegador. Escudos, uniformes, formações, modelos e as próprias paletas/meshes
dos presets continuam sendo recursos Unity compilados.
O jogo aceita contratos v1–v5 e saves anteriores. A revisão 10 inclui calendário
e regulamento do Paulista 2026, editáveis em **Campeonatos** e **Edições**. Consulte
[os modos de jogo](README-GAME-MODES.md) para progresso salvo e carreira.

## Editar no navegador

Com os serviços do Compose iniciados, abra [localhost:8080/editor/](http://localhost:8080/editor/).
Escolha um jogador e use as abas **Ficha**, **Atributos** e **Aparência**. É possível
criar, editar e excluir clubes/jogadores, transferir o vínculo do jogador ou deixá-lo
sem clube, selecionar posições naturais e editar altura, peso e quinze atributos.
A partir de v4 também há países, estádios, localização dos clubes, campos de gestão,
biografia dos jogadores e a declaração do recorte com suas fontes. O apelido é
opcional e tem preferência na apresentação; `name` e o nome completo continuam
preservados. Campo opcional desconhecido deve ficar vazio na edição e ausente
do JSON. Zero em um campo numérico é um valor explícito, não uma lacuna.

Capacidade e localização do estádio são cadastro; não trocam o estádio 3D.
O vínculo do clube aponta para seu estádio oficial principal, mesmo quando uma
partida é disputada em outro local. Em v5, cada confronto autoral pode declarar
outro estádio no campo opcional `stadiumId`, editável na aba Edições.
Disponibilidade por período, reformas, eventos e escolha automática de um estádio
alternativo ainda não são implementados.
Na carreira diária, orçamento de transferências inicializa o caixa de simulação
e orçamento salarial define a despesa mensal. Reputação, torcida e texto de
patrocínio continuam descritivos. Consulte [a gestão](CAREER-PROTOTYPE.md). Nascimento, nacionalidade e pé
preferido não adicionam histórico, envelhecimento ou novas regras à partida.

Um clube com jogadores precisa ter esses vínculos removidos antes da exclusão.
Clubes participantes de uma edição também precisam ser removidos dessa edição
na aba Edições antes da exclusão; o editor preserva e valida esse vínculo.
Um país ou estádio referenciado também precisa ter seus vínculos tratados antes
da exclusão. Abrir versões v1–v3 preserva seu escopo: a ferramenta não inventa
proveniência ou localização para convertê-las automaticamente em uma base v4.

As alterações formam um rascunho único nesta aba; não há salvamento automático.
**Salvar alterações** valida e publica a base inteira, incrementando a revisão
automaticamente. Depois, atualize [a página do jogo](http://localhost:8080) para
iniciar uma partida com os novos dados. **Descartar alterações** restaura o último
conteúdo carregado; **Exportar JSON** baixa o conteúdo atual, incluindo mudanças
ainda não salvas, sem publicar no jogo. Recarregar/fechar a aba pode perder o
rascunho; exporte-o se precisar guardá-lo fora dessa sessão.

Se outra aba ou um editor de arquivos alterar a fonte, o salvamento detecta o
conflito e mantém o rascunho aberto. Exporte-o antes de recarregar a base e reaplicar
as alterações necessárias. Uma falha de validação também preserva o rascunho.
Cada salvamento mantém uma cópia da revisão anterior em
`Assets/FootballSimulator/Data/FootballWorld/Examples/.editor-backups/previous.database.json`.
Esse backup local é substituído a cada gravação e não entra no Git; não é um
histórico de revisões nem um save de temporada.

Esta é uma ferramenta intermediária de cadastro, competições e presets. As abas
Campeonatos e Edições editam os calendários e perfis de regras suportados. A prévia
de aparência é ilustrativa, não o personagem 3D real. Upload de imagens,
importação de modelos e processamento de skins da comunidade ficam para etapas futuras.

## Arquivos de autoria

- [Base de exemplo](Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json).
- [JSON Schema v1](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v1.schema.json).
- [JSON Schema v2](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v2.schema.json), com aparência portátil.
- [JSON Schema v3](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v3.schema.json), com competições e edições.
- [JSON Schema v5](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v5.schema.json), com calendário autoral e Paulista.
- [JSON Schema v4](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v4.schema.json), com cadastro observado, países, estádios e biografias.
- [Bindings visuais](Assets/FootballSimulator/Resources/FootballWorld/LegacyMatchBindings.asset).
- [Contrato e limites](DATA-FORMAT.md).
- [Arquitetura](ARCHITECTURE.md) e [próximas entregas](ROADMAP.md).

O arquivo mantém o nome `four-clubs.database.json` por compatibilidade com o
carregamento e o Compose; esse nome não representa o número atual de clubes.
A base conserva seu `databaseId`, o ClubId do São Paulo e PlayerIds existentes
quando correspondem à mesma pessoa. Alterar o JSON não modifica TeamEntry nem
PlayerEntry. Manter IDs ao editar ou reorganizar registros.
As posições naturais já são independentes de uma formação e podem ser editadas.
Elenco não é escalação: não há limite de onze jogadores no catálogo.

`snapshot.date` é a data de referência da observação; `rosterScope` identifica
relacionados de jogos (`matchday-squads`) ou elencos completos (`full-squads`).
Mudar a data não carrega outro elenco nem escolhe uma temporada. Para esta amostra,
a data é 11/01/2026 e o escopo são os relacionados. Notas e fontes identificam o
conteúdo pesquisado e as aproximações. Dados desconhecidos permanecem omitidos.

## Características físicas editáveis

Cada jogador possui `heightCm` (150–210 cm) e `weightKg` (45–100 kg) no JSON.
No motor atual, altura ajusta a escala vertical do personagem; peso ajusta sua
largura e profundidade. Esses valores também participam do dimensionamento do
collider legado. Salvar uma revisão compatível e atualizar o navegador aplica
os valores na próxima partida, sem recompilar no fluxo local descrito abaixo.

No JSON v2–v5, `visualProfiles[].appearance` contém sete escolhas: tom de pele,
estilo e cor do cabelo, estilo e cor da barba, cor das chuteiras e cor da faixa da
meia. A meia principal continua no uniforme do clube. Esses campos usam IDs de
presets compilados; não são cores RGB livres ou modelos novos. Veja as opções
completas em [DATA-FORMAT.md](DATA-FORMAT.md).

A aparência completa no JSON tem prioridade sobre os bindings e dispensa um
`PlayerEntry` de referência. Quando omitida, v1–v5 preservam a aparência associada
por `PlayerId` em `LegacyMatchBindings`, ou seu default declarado. O editor oferece
**Definir aparência na base** nesses casos; uma skin externa vinculada não é
sobrescrita por presets. Os presets da amostra são genéricos; não equivalem a
rostos ou modelos personalizados dos atletas.
Altura/peso e atributos esportivos permanecem separados dessas escolhas visuais.

## Carregamento no Editor e WebGL

No Play Mode do Unity, FootballDatabaseBootstrap lê o arquivo de exemplo por uma
URI de arquivo. No player, lê `StreamingAssets/FootballWorld/database.json` por
UnityWebRequest. O processador de build valida a base e registra JSON/schema como
StreamingAssets adicionais sem copiar fontes para `Assets/StreamingAssets`.

Para instalar ou atualizar o código e os recursos visuais compilados, gere e
publique usando o fluxo existente:

```powershell
.\scripts\webgl.ps1
```

Ao adicionar ou reorganizar assemblies, se o player deixar de registrar o
carregamento apesar de os testes do Editor passarem, executar
`.\scripts\webgl.ps1 -CleanBuild`. Essa opção renova o cache de scripts e dados
do player; ver [troubleshooting do WebGL](README-WEBGL.md). Verificar o log abaixo
no navegador, pois compilar e disponibilizar o JSON não prova sua ativação.

Depois de abrir [o jogo](http://localhost:8080), o console registra:

```text
[FootballWorld] Loaded database <id> revision <revision>: 16 clubs, 363 players, 363 memberships.
```

O JSON e seu schema podem ser inspecionados no servidor local:

- [database.json](http://localhost:8080/StreamingAssets/FootballWorld/database.json)
- [database.schema.json](http://localhost:8080/StreamingAssets/FootballWorld/database.schema.json)
- [Schema do editor](http://localhost:8080/editor/api/schema)

Os endpoints de schema descrevem as cinco versões suportadas. Os schemas de
autoria v1–v5 continuam separados nos arquivos indicados acima.

O Compose monta o diretório de autoria somente para leitura no `soccer-web`, e o Nginx entrega
o JSON original nesse endereço com `Cache-Control: no-store`. Para testar nomes,
elenco, posições, medidas, atributos ou presets de aparência diretamente no arquivo:

1. Edite e salve `Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json`.
2. Preserve os IDs existentes e incremente `databaseRevision` a cada revisão.
3. Atualize `http://localhost:8080` no navegador. Não é necessário executar o
   script, reiniciar o container ou recompilar o Unity.

A revisão é lida durante o carregamento da página; editar o arquivo não altera uma
partida que já está em andamento. Refresh encerra essa partida. JSON inválido
mostra o diagnóstico de importação; corrija a fonte e use Retry ou atualize.
O diretório é montado para suportar editores que salvam substituindo o arquivo.
Não editar a cópia em Builds/UnityWebGLProject; ela é gerada pelo script.
Campeonatos e carreiras salvos retomam a revisão com que foram criados. Mudanças
na base são usadas em novos campeonatos, carreiras e amistosos; atualizar a página
não migra uma competição em andamento. Uma partida interrompida volta a ficar pendente.
Os saves guardam o JSON completo da revisão. Compressão GZip permite usar a base
atual; há limites de 2 MiB descomprimidos e 112 KiB por slot, além da quota do
navegador. Consulte [persistência](README-GAME-MODES.md#persistência-e-autoria).

Essa atualização direta é o fluxo local do Compose. A exportação ainda inclui
uma cópia validada para outros servidores; nesses destinos, publique o JSON
atualizado no mesmo endereço. O bind local não publica imagens nem outros arquivos
da pasta. Escudos, uniformes, bindings, modelos 3D e contratos novos ainda precisam
de build. Alterar `visualProfiles` não instala uma skin externa automaticamente.

O bootstrap persiste entre cenas. Expõe `Current.Session.ActiveCatalog`, `State`,
`Errors`, `VisualProfiles`, `SourceUri` e `ActiveSourceUri`. Para integrações Unity,
`Load(uri)` e `Reload()` são chamados na main thread. Apenas o último carregamento
solicitado pode ativar dados. Uma falha indica `Failed` e mantém o catálogo anterior;
os consumidores devem distinguir o estado da última tentativa do catálogo ativo.

## Seleção e partida

O menu mostra os clubes do catálogo. Escolha dois clubes distintos e pressione
Play. Na preparação, confira nomes, escalações e kits; Back to teams cancela e
preserva as escolhas. Ao sair da partida, a seleção volta com os mesmos ClubIds.
Enquanto a base carrega, os seletores e Play ficam indisponíveis. Uma falha de
leitura mostra diagnóstico e Retry, sem recorrer ao cadastro antigo.
O filtro de país usa os códigos cadastrados a partir de v4. Clubes de bases antigas sem
país permanecem acessíveis na opção de país não informado; o filtro não troca IDs.

LineupPlanner escolhe onze jogadores de forma determinística por posições e IDs:
um goleiro natural, preferindo um jogador exclusivo de GK, e dez jogadores de
linha. Maximiza as correspondências naturais da formação e preenche eventuais
vagas restantes com jogadores de linha, avisando sobre improvisações. Não escolhe
por overall nem promete a melhor escalação tática. Reservas permanecem na base;
cadastros com menos de onze ou sem goleiro/linha suficientes aparecem bloqueados.

No Inspector de LegacyMatchBindings, as listas Clubs e Players associam IDs a
templates visuais existentes. Clube sem binding usa os defaults declarados de
escudo, kits e formação, com aviso. Jogador sem aparência no JSON e sem binding usa
DefaultPlayerAppearance, também com aviso. Uma aparência portátil completa não
precisa desse fallback. Para preservar a identidade ao renomear clubes/jogadores,
edite o nome no JSON mantendo o ID; não altere os vínculos visuais.

Nome de apresentação (`nickname`, quando presente; caso contrário, `name`),
medidas e quinze atributos do JSON entram nos objetos esportivos
temporários. Os sete presets são aplicados separadamente pelo adaptador visual;
templates fornecem recursos visuais, nunca atributos. Cada partida
possui clones próprios e a revisão do catálogo usada na preparação; o mapeamento
0–21 -> ClubId/PlayerId fica em FriendlyMatchSession.ActiveMatch.Players. Os clones
são liberados depois da UI e do motor ao cancelar, sair ou recuperar uma falha.

Sem perfil visual, ou com builtin-player@1 / football-player-v1, o jogo usa a
aparência embutida. Uma skin diferente em um titular impede a partida desse clube
com mensagem visível; não é substituída silenciosamente. Skins dos reservas são
verificadas quando escalados. Não há upload nem processamento de modelos externos
nesta etapa. Campeonatos e carreiras diárias salvos são separados da seleção de
amistoso; o calendário da carreira não resolve outra revisão histórica da base.

O console registra também a origem da seleção e da partida:

```text
[FootballWorld] Friendly selection uses database <id> revision <revision>: 16 clubs.
[FootballWorld] 3D friendly started with 22 imported players from database revision <revision>.
```

## Validação e testes

O importador usa Newtonsoft.Json somente na infraestrutura e mapeamento explícito
para DTOs/modelos, compatível com IL2CPP. Domain/Application não referenciam Unity,
Newtonsoft ou DTOs. A biblioteca DataContracts também é independente do Unity.

No Test Runner do Unity, executar os testes de FootballWorld em EditMode e PlayMode.
EditMode cobre contrato, referências, invariantes, escalação, mapeamento dos quinze
atributos, bindings por IDs e vida independente dos clones. PlayMode verifica
leitura externa, preservação dos perfis v2 após erro e o ciclo de vida do bootstrap.
Testes do prefab verificam os presets existentes e a remoção de cabelo/barba ao
selecionar `none`. As fixtures do adaptador ficam em
Code/FootballWorld/Editor/Tests, na assembly Editor padrão, pois usam o legado.
Os testes determinísticos usam uma cópia fixa da antiga base Royal/44 atletas e
dos bindings em diretórios `Tests/Fixtures`, separados da base editável. A
integração também verifica a base autoral e seus bindings reais.
Os testes v4 cobrem omissão versus `null`, apelido/nome completo, datas civis,
países/estádios, referências, limites de texto/números e proveniência; o backend
Node aplica as mesmas regras ao salvar.
Testes ficam fora do player final. Validar também navegação e partida no navegador;
testes do adaptador não comprovam o comportamento da física ou IA.

Validação estrutural opcional, usando PowerShell 7 com `Test-Json` disponível:

```powershell
Test-Json `
  -LiteralPath 'Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json' `
  -SchemaFile 'Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v5.schema.json'
```

Isso não substitui o importador: IDs duplicados, referências cruzadas e as
restrições de sintaxe/tamanho descritas no contrato também precisam passar.
Antes de empacotar o player, FootballDatabaseBuildProcessor executa o importador
e interrompe o build se a base for inválida. Erros informam código, caminho do
campo e mensagem; não corrigem valores ou descartam registros silenciosamente.

Para testes automatizados nesta máquina, usar a cópia isolada sincronizada pelo
último build, sem fechar o Editor do projeto original. Não executar dois Unity
na mesma cópia simultaneamente. Resultados e logs devem ficar em `Logs/WebGL`.

O editor fica separado em `database-editor/client` (ES modules do navegador) e
`database-editor/server` (Node 22 e Ajv 8). Para testar o backend localmente, com
Node 22 instalado, execute a partir da raiz:

```powershell
Push-Location .\database-editor
npm ci
npm test
Pop-Location
```

Mudanças apenas nessa aplicação, mantendo o contrato do jogo, são publicadas com
`docker compose up --build -d --wait soccer-web`; confira editor e jogo no navegador.
Mudanças no contrato, adaptador ou recursos Unity exigem também WebGL novo.
