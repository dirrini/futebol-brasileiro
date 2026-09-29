# Menu e modos de jogo

Abra [localhost:8080](http://localhost:8080) após publicar o projeto com
`./scripts/webgl.ps1`. O menu inicial usa UGUI/TextMeshPro e oferece os fluxos abaixo.

## Quick match

Abre a seleção existente de duas equipes, seguida de uniformes e configurações
da partida. País e equipe são escolhidos separadamente em cada lado; a base atual
tem os 16 clubes brasileiros do Paulistão 2026. Apelidos dos jogadores aparecem
quando preenchidos, com fallback para o nome cadastrado. Usa o catálogo externo
carregado ao abrir o jogo. O resultado de um
amistoso não altera o campeonato salvo.

## Championship

Escolha uma edição disponível na base e o clube que deseja controlar, filtrado
por país. A base histórica atual deixa as coleções de competições vazias: o
regulamento e o calendário oficial do Paulistão ainda são a próxima etapa. Não há
novo campeonato disponível nessa base. Um campeonato salvo continua usando sua
própria revisão fixada, inclusive os clubes antigos.

A liga
demonstrativa reúne São Paulo FC, Milano, London e Catalagna em três rodadas,
nos dias 3, 10 e 17 de outubro de 2026. São seis confrontos: três são jogados com
seu clube e os outros são simulados ao concluir a rodada. Essas datas e essa
competição são exemplos de funcionamento, não um calendário oficial.

Vitória vale três pontos, empate um e derrota zero. Os desempates são vitórias,
saldo de gols e gols marcados. Clubes com todos esses números iguais compartilham
a posição; o identificador estabiliza a ordem visual sem criar um campeão único.

O apito final registra o resultado antes de descarregar o motor 3D. Abandonar
uma partida ou falhar no carregamento deixa o confronto pendente. Uma nova
tentativa recebe outra identidade; repetir um resultado já aceito não soma
pontos novamente. A tabela distingue jogos pendentes, jogados e simulados.

O progresso é salvo neste navegador, com o JSON da revisão utilizada no início.
Atualizar a página retoma os resultados salvos; não retoma o minuto de uma partida
interrompida. Criar outro campeonato substitui o anterior após confirmação.
Limpar os dados do site ou usar outro navegador não transfere esse progresso.
Uma falha de salvamento é informada e mantém o último save válido.

No JSON v3, `competitions` cadastra a competição e `competitionEditions` define
participantes, datas por rodada e regras. São aceitos pontos corridos com turno
único ou ida/volta; outros formatos recebem erro de importação. O editor externo
preserva e valida essas coleções ao salvar clubes/jogadores, mas seus formulários
de campeonatos ainda não foram implementados. Veja [DATA-FORMAT.md](DATA-FORMAT.md).

## Career

Crie o treinador com nome, um dos três avatares gráficos disponíveis, mês/ano
inicial, país e equipe. Sem edição cadastrada, o primeiro mês/ano vem da data de
observação da base (janeiro de 2026). O perfil fica salvo neste navegador e pode ser consultado no
mesmo menu. Criar um novo perfil exige confirmar a substituição do anterior.

Este recorte cria o perfil. Ainda não avança o tempo, monta calendário real,
resolve elencos históricos, negocia contratos ou executa partidas da carreira.
Escolher um ano passado ou futuro não transforma a base atual em uma base daquele
período. A próxima etapa precisa acrescentar vigências e cobertura temporal aos
dados, além da coordenação do calendário. O ano aceita 1–9999; a disponibilidade
real de conteúdo para uma data será verificada quando esse recurso existir.

Os avatares são retratos gráficos editáveis no Unity, separados das skins 3D de
jogadores. Não incluem upload de modelos ou criação de um treinador 3D nesta etapa.

## Options

Idioma inglês/português, câmera padrão e dificuldade padrão ficam salvos
localmente. A escolha inicial de idioma é inglês. Câmeras especiais de escanteio
e impedimento não fazem parte da lista de câmeras padrão.

Nomes próprios de clubes e competições vêm da base e não são traduzidos. A opção
de idioma se aplica aos textos de navegação e configuração cobertos pelo catálogo
de localização do jogo; os dados esportivos permanecem os mesmos.

## Autoria e responsabilidades

- `Domain` contém as definições imutáveis de competição, regras e datas.
- `Application` gera confrontos, valida tentativas/resultados, calcula a tabela
  e simula os jogos dos outros clubes sem depender de Unity ou JSON.
- `Infrastructure/GameModes` conecta esses serviços ao motor, às preferências e
  aos saves locais versionados; a base é revalidada ao restaurar seu snapshot.
- `Presentation` exibe consultas e envia comandos. Layout, tema, tipografia,
  cores, avatares e textos são assets editáveis, não árvores criadas pelo runtime.
- `Editor/GameHubAuthoring` prepara os assets iniciais de autoria. O comando
  preserva recursos já existentes para não sobrescrever ajustes no Editor.

O save da competição é independente do JSON publicado pelo editor. Editar uma
equipe afeta novas partidas rápidas e novos campeonatos após refresh. Uma
competição já iniciada conserva sua revisão; não há migração automática.

O armazenamento mantém dois slots e só troca o slot ativo após gravar o novo
conteúdo. Tamanho excessivo, conteúdo inválido ou indisponibilidade de gravação
geram diagnóstico. Os dados anteriores não são apagados silenciosamente.

O limite atual é de 384 KiB para o save completo, incluindo o JSON da base e o
progresso. Um campeonato que já exceda esse tamanho não pode ser criado; se o
progresso ultrapassar o limite depois, o jogo continua em memória e informa a
falha, mas o refresh recupera somente o último estado salvo. Se o slot ativo
estiver corrompido ou incompatível, ele é preservado e a restauração é bloqueada;
não há recuperação automática pelo outro slot.

Novos saves compactam os espaços de formatação do JSON antes de gravar; não removem
campos ou referências e preservam o formato de save existente. O limite continua
valendo para o envelope completo, e não apenas para o arquivo do catálogo.

O JSON salvo conserva os presets e referências de skins declarados na base.
Escudos, uniformes e demais recursos fornecidos pelos bindings locais pertencem
ao build instalado; o save não arquiva essas versões dos assets.
