# WebGL local com Docker Compose

O Unity **2022.3.62f2 instalado no Windows** compila o jogo e os Addressables.
O Docker Compose empacota o resultado em Nginx e o disponibiliza em
[http://localhost:8080](http://localhost:8080). O jogo roda no navegador.

Este é o fluxo escolhido para o projeto: utiliza a ativação existente do
Unity Hub e não precisa de Unity, licença ou senha dentro de um container.

## Requisitos

- Unity 2022.3.62f2 ativado, com **WebGL Build Support** instalado pelo Unity Hub.
- Docker Desktop iniciado no modo **Linux containers**, com Docker Compose.
- Porta local 8080 disponível e navegador de desktop com WebGL 2.
- Salvar as alterações no Unity antes do build; a cópia usa os arquivos em disco.

## Gerar e iniciar com um comando

No PowerShell, na raiz do projeto:

```powershell
.\scripts\webgl.ps1
```

Se a política do PowerShell bloquear scripts, execute apenas este processo assim:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\webgl.ps1
```

O script detecta o Editor pela versão do projeto, valida o módulo WebGL e o
Docker, compila em modo batch, confere o resultado e executa
`docker compose up --build -d --wait soccer-web`.
A primeira compilação pode demorar porque o Unity importa os recursos para WebGL
e compila C# em WebAssembly. Acompanhe os logs em `Logs/WebGL`.

O script sincroniza Assets, Packages e ProjectSettings para uma cópia isolada em
`Builds/UnityWebGLProject`, com cache próprio. O projeto original pode continuar
aberto no Editor. Não execute dois builds deste script ao mesmo tempo.
O resultado é feito primeiro em uma pasta de preparação dentro de `Builds`.
Somente um build bem-sucedido substitui `Builds/WebGL`; a versão anterior é
preservada como backup dentro de `Builds`. Uma falha de compilação não publica
arquivos incompletos nem substitui a imagem que já estava servindo o jogo.

## Comandos úteis

```powershell
# Gerar arquivos WebGL sem iniciar o Docker.
.\scripts\webgl.ps1 -BuildOnly

# Reconstruir scripts e dados do player quando o cache incremental estiver desatualizado.
.\scripts\webgl.ps1 -CleanBuild

# Publicar o build que já existe, sem recompilar o Unity.
.\scripts\webgl.ps1 -SkipBuild

# Informar um Editor instalado em outro local (usar a mesma versão do projeto).
.\scripts\webgl.ps1 -UnityPath 'D:\Unity\2022.3.62f2\Editor\Unity.exe'

# Conferir o serviço e seus logs.
docker compose ps
docker compose logs --tail=50 soccer-web

# Parar o serviço.
docker compose down
```

Depois de alterar código ou recursos compilados do jogo, execute novamente
`.\scripts\webgl.ps1` e recarregue o navegador. `docker compose up --build` sozinho
reconstrói apenas a imagem do servidor: ele usa os arquivos WebGL que já estiverem
em `Builds/WebGL`. A edição do JSON externo tem o fluxo sem build descrito abaixo.

## Editar a base e atualizar o navegador

Com `soccer-web` iniciado pelo Compose, edite e salve
`Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json`.
Mantenha os IDs existentes e incremente `databaseRevision` para uma nova revisão.
Recarregue [o jogo](http://localhost:8080): o catálogo será lido novamente.
Não é necessário executar o script, reconstruir a imagem ou reiniciar o container
quando a única alteração for nesse JSON compatível. A recarga reinicia o jogo;
não atualiza uma partida em andamento nem preserva seu progresso.

O Compose monta o diretório de autoria em `/opt/football-database`, somente para
leitura. Isso também acompanha editores que salvam substituindo o arquivo.
É um bind da pasta já existente no computador, não um volume persistente Docker.
`create_host_path: false` impede criar uma pasta vazia se a origem estiver errada.
O Nginx publica somente o JSON no endereço que o player já utiliza:

[StreamingAssets/FootballWorld/database.json](http://localhost:8080/StreamingAssets/FootballWorld/database.json).

Essa resposta usa `Cache-Control: no-store`, sem ETag ou respostas condicionais
por data, para que a atualização não recupere uma revisão antiga do navegador.
O schema e os outros arquivos StreamingAssets continuam vindo do build. A pasta
de autoria completa, incluindo arquivos `.meta`, não é exposta por essa rota.
O JSON empacotado pelo Unity continua existindo no export, mas o servidor local
usa a origem montada. Um arquivo ausente retorna erro; não há fallback silencioso
para uma cópia antiga. A imagem com essa configuração deve ser iniciada por este
Compose, que fornece a pasta da base.

Antes de atualizar o navegador, a validação estrutural opcional em PowerShell 7 é:

```powershell
Test-Json `
  -LiteralPath 'Assets/FootballSimulator/Data/FootballWorld/Examples/four-clubs.database.json' `
  -SchemaFile 'Assets/FootballSimulator/Data/FootballWorld/Schemas/database-v1.schema.json'
```

O importador do jogo também valida referências, posições, IDs e limites; consulte
[README-DATABASE.md](README-DATABASE.md). Se o menu mostrar erro na base, corrija
e salve o JSON e use Retry ou recarregue a página. Os erros detalhados ficam no
console do navegador. Uma base inválida não é aceita parcialmente.

Escudos, uniformes, modelos 3D e bindings ainda são recursos compilados Unity.
Alterá-los exige `.\scripts\webgl.ps1`; colocar uma imagem ou um campo novo no
JSON não cria suporte de importação. Novos tipos de dados ou versões de contrato
também exigem adaptar, testar e publicar o player compatível.

## Como o build é configurado

O método `FStudio.Build.WebGLBuild.Run` fica em
`Assets/FootballSimulator/Code/Editor/WebGLBuild.cs` e pode ser chamado com
`-batchmode -nographics -quit -buildTarget WebGL -executeMethod`.

- Usa as cenas habilitadas no projeto: `_StartingScene` e `DefaultScene`.
- Compila os Addressables para WebGL antes do player, incluindo o estádio.
- Usa Gzip, sem Decompression Fallback e sem threads nativas de WebGL.
- Restaura as opções de publicação temporariamente alteradas pelo script.
- Retorna falha se o Unity ou os Addressables não conseguirem compilar.

A plataforma ativa e o cache do projeto original são preservados. Somente a
cópia de build usa WebGL. Os caches são gerados pelo Unity; não é necessário
apagá-los entre builds.

### Recuperar dados de build desatualizados

Use `.\scripts\webgl.ps1 -CleanBuild` quando, após adicionar novas assemblies,
o build terminar sem erros mas o navegador continuar sem executar seus pontos
de inicialização. Um caso observado foi `ScriptingAssemblies.json` antigo no
cache do player: os novos scripts e hooks estavam compilados, mas suas assemblies
não constavam na lista empacotada no WebGL.

Essa opção passa `WEBGL_CLEAN_BUILD=1` ao Unity e usa
[`BuildOptions.CleanBuildCache`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/BuildOptions.CleanBuildCache.html)
para reconstruir scripts e dados do player na cópia isolada. O build normal
continua incremental. A reconstrução pode demorar mais e preserva o último
export válido se falhar. Para apenas compilar, combine `-CleanBuild -BuildOnly`;
`-CleanBuild -SkipBuild` é rejeitado antes de iniciar qualquer ação.

O script restaura `WEBGL_CLEAN_BUILD` e `WEBGL_BUILD_PATH` ao terminar. Em uma
invocação direta do método `FStudio.Build.WebGLBuild.Run`, definir
`WEBGL_CLEAN_BUILD=1` também solicita a reconstrução completa. A ausência desse
valor mantém o comportamento incremental.

O Nginx envia `Content-Encoding: gzip` e os tipos corretos para JavaScript e
WebAssembly, sem comprimir novamente arquivos `.gz`. Os arquivos são
revalidados para evitar carregar uma versão antiga depois de uma publicação.
Veja os requisitos de servidor na
[documentação Unity 2022.3](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-deploying.html).

## Compatibilidade e validação

Os imports exclusivos do Editor foram protegidos, os carregamentos de
Addressables usam callbacks de conclusão compatíveis com WebGL e o gramado
seleciona o material alternativo existente nessa plataforma.
As esperas do jogo usam o relógio e o ciclo de atualização do Unity no WebGL,
pois `System.Threading.Timer`, usado por `Task.Delay`, não dispara nessa plataforma.
No Editor e nas demais plataformas, o comportamento de `Task.Delay` é preservado.

Depois de gerar, conferir no navegador: menu, carregamento do estádio, início
da partida, movimentação, passes, carga de chute, desarme, áudio e retorno ao
menu. O teste no Editor não substitui o teste do player WebGL.

Validação inicial em 26/09/2026: build concluído com zero erros, container
saudável, menu e partida renderizados no navegador, sem erros no console.
Conferidos HTTP 200 para arquivos do player e configuração dos Addressables,
`application/wasm` com `Content-Encoding: gzip`, e HTTP 404 para arquivo ausente.
A verificação inicial não cobre todas as ações de gameplay e dispositivos.

Somente o build e a configuração Nginx entram na imagem. Fontes, `.git`, caches
e credenciais são excluídos pelo `.dockerignore`. No ambiente local, o Compose
acrescenta o bind somente para leitura da base, conforme descrito acima.
A porta está vinculada a `127.0.0.1`: o serviço fica acessível neste computador.
