# Base externa: fundação de dados

Esta etapa carrega um catálogo de clubes/jogadores independente do motor Unity.
A base de exemplo contém Royal, Milano, London e Catalagna, com 44 jogadores e
44 vínculos. O jogo importa o catálogo ao iniciar; o amistoso e a seleção de times
existentes continuam usando o cadastro legado até a próxima etapa de integração.

## Arquivos de autoria

- [Base de exemplo](Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json).
- [JSON Schema v1](Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v1.schema.json).
- [Contrato e limites](DATA-FORMAT.md).
- [Arquitetura](ARCHITECTURE.md) e [próximas entregas](ROADMAP.md).

Os dados de exemplo são uma cópia pontual dos assets atuais. Alterar o JSON não
modifica TeamEntry nem PlayerEntry. Manter IDs ao editar ou reorganizar registros.
As posições naturais já são independentes de uma formação e podem ser editadas.
Elenco não é escalação: não há limite de onze jogadores no catálogo.

## Carregamento no Editor e WebGL

No Play Mode do Unity, FootballDatabaseBootstrap lê o arquivo de exemplo por uma
URI de arquivo. No player, lê `StreamingAssets/FootballWorld/database.json` por
UnityWebRequest. O processador de build valida a base e registra JSON/schema como
StreamingAssets adicionais sem copiar fontes para `Assets/StreamingAssets`.

Gere e publique usando o fluxo existente:

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
[FootballWorld] Loaded database <id> revision 1: 4 clubs, 44 players, 44 memberships.
```

O JSON e seu schema podem ser inspecionados no servidor local:

- [database.json](http://localhost:8080/StreamingAssets/FootballWorld/database.json)
- [database.schema.json](http://localhost:8080/StreamingAssets/FootballWorld/database.schema.json)

São arquivos externos ao WebAssembly, não dados embutidos em classes C#. Uma
futura ferramenta poderá fornecer outra revisão compatível sem recompilar C#.
O fluxo atual do projeto continua exigindo build novo após alterações, conforme
AGENTS.md. Não editar a cópia em Builds/UnityWebGLProject; ela é gerada pelo script.

O bootstrap persiste entre cenas. Expõe `Current.Session.ActiveCatalog`, `State`,
`Errors`, `VisualProfiles`, `SourceUri` e `ActiveSourceUri`. Para integrações Unity,
`Load(uri)` e `Reload()` são chamados na main thread. Apenas o último carregamento
solicitado pode ativar dados. Uma falha indica `Failed` e mantém o catálogo anterior;
os consumidores devem distinguir o estado da última tentativa do catálogo ativo.

Não há tela de upload, persistência da seleção de base, resolução de skins ou
adaptação dos clubes importados para uma partida nesta entrega. A evidência do
carregamento está no log e nas consultas da sessão; o menu atual permanece igual.

## Validação e testes

O importador usa Newtonsoft.Json somente na infraestrutura e mapeamento explícito
para DTOs/modelos, compatível com IL2CPP. Domain/Application não referenciam Unity,
Newtonsoft ou DTOs. A biblioteca DataContracts também é independente do Unity.

No Test Runner do Unity, executar os testes de FootballWorld em EditMode e PlayMode.
EditMode cobre contrato, referências, invariantes e snapshots. PlayMode verifica
leitura externa e o ciclo de vida do bootstrap. Testes ficam fora do player final.

Validação estrutural opcional, usando PowerShell 7 com `Test-Json` disponível:

```powershell
Test-Json `
  -LiteralPath 'Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json' `
  -SchemaFile 'Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v1.schema.json'
```

Isso não substitui o importador: IDs duplicados, referências cruzadas e as
restrições de sintaxe/tamanho descritas no contrato também precisam passar.
Antes de empacotar o player, FootballDatabaseBuildProcessor executa o importador
e interrompe o build se a base for inválida. Erros informam código, caminho do
campo e mensagem; não corrigem valores ou descartam registros silenciosamente.

Para testes automatizados nesta máquina, usar a cópia isolada sincronizada pelo
último build, sem fechar o Editor do projeto original. Não executar dois Unity
na mesma cópia simultaneamente. Resultados e logs devem ficar em `Logs/WebGL`.
