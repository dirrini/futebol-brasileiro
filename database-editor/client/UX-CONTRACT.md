# Editor de base — contrato de interação

Esta aplicação local tem público de autoria/teste do Futebol Brasileiro, idioma
pt-BR e uso principal em desktop. É um editor de rascunho da base inteira. Consulte
`../../ARCHITECTURE.md`, `../../DATA-FORMAT.md` e `../../DESIGN.md` para as
fronteiras de domínio, versão portátil e referência visual. A API do editor valida
o documento integral e exige ETag; o jogo importa a base por uma fronteira própria.

## Proprietários canônicos

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
| --- | --- | --- | --- | --- |
| Form | `ui.js` field/select + model.js validateDocument | DATA-FORMAT.md e este contrato | Ficha do jogador e clube | Erro textual associado e foco no primeiro campo inválido |
| Select/Listbox | `ui.js` select | Este contrato | Nativo; geometria do popup do SO aceita, labels pt-BR | Teclado e popup |
| Scrollbar | `styles.css` baseline global | DESIGN.md | Gutters na lista | Track/thumb/hover/active e forced-colors |
| Toast | `app.js` renderFeedback/renderStatus + ui.js announce | Este contrato | Success/warning/error/info persistentes | Região live e mesma linguagem nos dois cadastros |
| CRUD | `app.js` createRecord/removeRecord/save | API ETag e este contrato | Rascunho da base; publicação explícita única | CRUD, conflito, sucesso mantém seleção |
| Dialog | `ui.js` showDialog + dialog HTML nativo modal | Este contrato | Criação e confirmação | Escape/cancelar, foco contido e restaurado |
| Dataset navigation | `app.js` renderList/writeUrl | Este contrato | Página local de dez registros | Busca/clubes/id/aba em URL, limite de página |

Não há biblioteca/framework frontend ou fluxo web anterior a reutilizar. Clube e
jogador compartilham os proprietários acima. O fluxo irmão para comparar visual e
comportamento é a ficha do outro tipo de cadastro. Tabelas de seleção em lote,
datas, permissões e login não se aplicam a esta aplicação local.

## Rascunho, ações e estados

- Digitar altera somente o rascunho em memória. Navegar entre clubes, jogadores,
  seções e filtros mantém todas as edições; nenhuma troca interna descarta campos.
- “Salvar alterações” valida e envia a base inteira uma vez; a API incrementa a
  revisão. Controles de edição ficam bloqueados durante o envio. Só a confirmação
  da API permite mostrar “Alterações salvas”. O sucesso mantém a ficha selecionada.
- “Adicionar jogador/clube” abre o mesmo diálogo e cria IDs aleatórios uma vez.
  O novo cadastro fica selecionado na página de lista correspondente. Novo jogador
  começa com valores demonstrativos editáveis e aparência padrão explícita.
- Excluir exige diálogo que nomeia o cadastro e explica o efeito. A exclusão ainda
  é um rascunho reversível por “Descartar alterações” antes de salvar. Jogadores
  perdem vínculo e perfil junto ao cadastro; clubes com jogadores não são excluídos.
  A base precisa manter ao menos um clube e um jogador.
- IDs são somente leitura. Os quinze atributos, medidas, posições naturais,
  vínculo ao clube e aparência padrão têm campos próprios. Skin não suportada não
  é substituída automaticamente por aparência embutida.
- Exportar baixa exatamente o rascunho visível, incluindo alterações não salvas,
  sem publicar nada. O nome usa a revisão carregada, não uma revisão inventada.
- “Descartar alterações” exige confirmação e restaura o último snapshot carregado.
  Sair/recarregar com rascunho usa exclusivamente beforeunload do navegador; diálogos
  de produto nunca usam alert/confirm/prompt.
- Falha inicial mantém botão “Tentar novamente”. Falhas de gravação mantêm todos
  os dados; não existe retry automático. Timeout indica resultado não confirmado.
  ETag protege contra repetir a gravação sobre uma revisão que pode ter sido salva.
- Conflito mostra mensagem persistente, exportação disponível e carregamento da
  versão atual com confirmação de descarte. Nunca há sobrescrita forçada silenciosa.
- Nenhum rascunho é gravado em localStorage. Fechar a página sem salvar descarta-o.

## Navegação, formulários e acessibilidade

Listas carregam o catálogo inteiro, limitado pelo contrato de 1 MiB; navegação
visível é paginada de dez itens e a lista possui seu próprio limite de rolagem.
Filtros são locais, ignoram acentos/caixa sem alterar os nomes armazenados, respeitam
composição IME e têm limpeza explícita. Página é ajustada após filtro/exclusão.
O formulário usa rolagem natural da página, inclusive nas seções longas.

Busca, clube, página, seção e item escolhido ficam nos parâmetros da URL por
replaceState: cliques no editor não acrescentam dezenas de entradas ao histórico.
A URL preserva a visão quando a página é aberta novamente, mas não o rascunho.

Labels de campo são reais e erros apontam aria-describedby/aria-invalid. Posições
usam checkboxes nativos. Selects são nativos, com popup e interação do sistema
operacional aceitos. Atributos usam entrada numérica precisa e meter complementar.
Formulários desativam balões de validação nativa e usam os erros do aplicativo.
Dialog modal nativo oferece foco contido, fundo inerte, Escape e cancelamento;
exclusão/descartar iniciam no botão Cancelar. Botões de ação permanecem com tamanho
estável durante salvamento. Todas as ações têm foco e hover explícitos.

A prévia de aparência é SVG autorado e **ilustrativa**, rotulada como tal. Cores e
estilos são aproximados. O uniforme desenhado é neutro; o resultado real é conferido
no jogo. Faixa/acessório de meia é separado da meia principal controlada pelo kit.
Importar modelos, texturas e campeonatos ainda não tem controles falsos nesta UI.

## Verificação

Cobrir CRUD de ambos os tipos, vínculo/free agent, posições múltiplas, todos os
atributos, aparência, ID preservado, salvamento e refresh do jogo; erro de campo,
falha de rede, conflito em duas sessões, descarte/cancelamento, zero resultados,
paginação, teclado/dialog/select e viewport estreito. O auditor estático não
substitui essas verificações. Evidências da execução ficam em Logs, fora das fontes.
