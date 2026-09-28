# São Paulo FC — visuais para o protótipo

Emulação geométrica simplificada dos uniformes de 2026: principal branco com
faixas vermelha e preta e escudo central; reserva com listras verticais,
ombros pretos, gola branca e escudo no peito esquerdo. Calção e meias brancos
no principal, pretos no reserva. Não reproduz patrocinadores, estrelas,
marcas do fabricante, selos comemorativos ou o tecido. O goleiro conserva
o modelo genérico existente, em verde para contrastar com os jogadores.
As faces e os corpos continuam sendo os modelos genéricos do jogo.

Referências visuais oficiais consultadas em 28/09/2026:

- [Uniforme principal de 2026](https://www.saopaulofc.net/new-balance-e-sao-paulo-futebol-clube-iniciam-contagem-para-o-centenario-com-nova-camisa-de-2026/).
- [Uniforme reserva de 2026](https://www.saopaulofc.net/tricolor-desde-sempre-new-balance-e-sao-paulo-apresentam-nova-camisa-away-para-2026/).
- [Escudo, cores e símbolos](https://www.saopaulofc.net/o-clube/simbolos/).

## Autoria

Os cinco SVGs são as fontes vetoriais editáveis, em **cores reais**. Toda a
geometria, incluindo as letras do escudo, está em caminhos/polígonos; não há
dependência de fontes instaladas nem cópia ou edição de imagens existentes.

- `Badge.svg`: escudo do menu e dos placares, com fundo transparente.
- `HomeAtlas.svg` / `AwayAtlas.svg`: atlas UV 512 × 512 para os modelos 3D.
- `HomePreview.svg` / `AwayPreview.svg`: frente e costas do uniforme no seletor.

As ilhas UV seguem o modelo legado: camisa frontal à esquerda, costas à
direita, calções ao centro e meia no canto inferior direito. O escudo também
é desenhado no atlas, pois o material legado não recebe o `LogoEntry` da equipe
na camisa. Preserve as ilhas e a orientação ao editar.

Com Node.js e o pacote `sharp` disponíveis, execute nesta pasta:

```powershell
node .\render-masks.cjs
```

O script rasteriza os SVGs e atualiza seus cinco PNGs correspondentes. Não
modifica arquivos `.meta`. Os PNGs são **máscaras para o shader**, portanto
seus tons vermelhos/azuis no Inspector não representam a aparência final:

| Cor na máscara PNG | Propriedade no material | Cor final usada aqui |
|---|---|---|
| Vermelho puro | `Color1` / `TeamLogoColor1` | Branco |
| Azul puro | `Color2` / `TeamLogoColor2` | Vermelho |
| Preto | Sem substituição | Preto |

Os SVGs usam as classes `white`, `red` e `black`. O rasterizador converte as
duas primeiras classes para as máscaras exigidas pelo shader. Os PNGs têm
compressão de textura desativada para preservar as bordas e as cores da máscara.

## Unity e catálogo

Abra `SaoPauloHomeKit.asset`, `SaoPauloAwayKit.asset` e `SaoPauloLogo.asset`
diretamente no Inspector para ajustar cores, texturas e cor da numeração.
Os SVGs podem ser abertos num editor vetorial; depois exporte novamente os PNGs.

`SaoPauloVisualTemplate.asset` reúne os três recursos. É somente um template
visual, sem elenco esportivo. `IsValid` fica desmarcado porque o Inspector
legado de times exige onze jogadores. **Não use o botão Initialize** desse
Inspector para editar esta base; edite os três recursos visuais diretamente.
Nomes, atributos e vínculos dos jogadores são fornecidos pelo JSON, e a formação
do amistoso é definida na associação por `ClubId` em `LegacyMatchBindings`.

Estes recursos visuais fazem parte do build Unity. Atualizá-los requer um novo
build; editar os dados do catálogo JSON servido externamente pode ser testado
por refresh, conforme o fluxo descrito no README da base na raiz do projeto.
