# Libris — Design da v1

**Data**: 2026-10-16
**Repos**: `Libris` (API .NET) + `LibrisApp` (Angular + Tauri, macOS primeiro)
**Nome**: de *Ex Libris*, o selo que marca "este livro pertence a…". O app é o catálogo do que é *meu*.

## Contexto

Hoje as leituras ficam numa planilha do Excel que só registra o que já foi terminado (mês, título, autor). Não existe catálogo dos livros que eu tenho, não dá pra saber onde parei em cada livro que comecei (leio vários ao mesmo tempo) e não dá pra ver quanto li num mês ou num ano. Os livros que eu quero comprar acabam virando screenshot perdido no Photos.

O Libris resolve isso com quatro perguntas:

1. **O que eu tenho?** Acervo completo: lidos, lendo, não iniciados e abandonados.
2. **Onde eu parei?** Progresso de cada leitura em andamento.
3. **Quanto eu li?** Livros terminados e páginas lidas, por mês e por ano.
4. **Que livros eu tenho desse gênero?** Filtro por gênero dentro do acervo. Serve de referência pra eu procurar mais livros parecidos *fora* do app.

Mais a **Wishlist**: livros que eu ainda não tenho e quero comprar.

## Objetivos, não-objetivos, anti-objetivos

**Objetivos (sucesso)**
- Cadastrar um livro pelo ISBN em poucos segundos: o app preenche tudo e eu só confiro.
- Registrar "parei na 140" em poucos cliques: Acervo → filtro "Lendo" → livro → Ficha → digitar → Anotar.
- Responder "quanto li em março?" (livros e páginas) sem conta de cabeça.
- Trocar de aparelho sem perder nada: os dados e as capas ficam no servidor.

**Não-objetivos da v1**
- Recomendação ou busca de livros que eu não tenho. A busca por gênero é só dentro do acervo.
- Integração com lojas, Goodreads etc.
- Importar a planilha atual. Começo do zero e cadastro os livros antigos na mão.
- Modo offline / cache local. O app é **online-only**.
- Scan de código de barras (evolução futura).
- App de celular (KMP/CMP), que vem depois e vai reaproveitar a mesma API.
- Releitura (ler o mesmo livro mais de uma vez com histórico separado). Na v1, cada livro tem uma leitura só.
- Multiusuário. É uma usuária só, com o cadastro de conta desligado em produção.

**Anti-objetivos (falha mesmo funcionando)**
- Parecer um app genérico de formulários. A identidade visual (mesa de biblioteca, fichas de papel, fita lateral) é parte do produto.
- Exigir digitação de tudo no cadastro, quando o ISBN existe.
- Estatística que não bate com o que eu registrei (por exemplo, páginas contadas em dobro).

## Identidade visual

Referências guardadas em `docs/design/`:
- `mock-original.webp`: mock validado (Acervo, Ficha, Registrar Livro).
- `telas-v5.html`: rascunho aprovado das telas que faltavam (Acervo com filtros, Ficha com registro de leitura, Números), no **modo escuro** com o fundo final. Para abrir no navegador, o `libris-ledger-bg.svg` precisa estar na mesma pasta. (`telas-v2.html` a `telas-v4.html` são versões anteriores, só como histórico.)
- `libris-ledger-bg.svg`: **fundo final** da mesa, uma textura de livro-razão verde-oliva escura com grade. `damask-bg.svg` e `damask-bg-forest.svg` são tentativas anteriores, só como histórico.

**Tokens (paleta "A Gathering of Spooks")**

| Token | Cor | Uso |
|---|---|---|
| `ghoulish-ale` | `#E7BC89` | títulos e textos soltos sobre a mesa |
| `vampire-venom` | `#6B0302` | títulos, ações principais, carimbo ativo |
| `pumpkin-poison` | `#C55B31` | detalhes: tags, barra de progresso, gráfico |
| `eye-of-the-wolf` | `#DA8825` | detalhes: tags |
| `haunted-forest` | `#675B31` | ícones da fita, tags |
| `forest-deep` | `#3E3820` | cor de base da mesa (por trás do `libris-ledger-bg.svg`) |

Papel das fichas e da fita: `paper` `#F7E7CC`, o tom mais claro da paleta. As fichas têm sombra rígida deslocada (sem blur).

**Tipografia**: **Aleo** para títulos, nomes de livros e carimbos; **Yantramanav** (Regular e Light) para texto corrido, menus e dados.

**Decisões já tomadas**
- **Sem capitular** ou letra iluminada: os títulos são texto normal em Aleo ("Acervo de leituras", "Números de 2026"), sem caixa-alta forçada.
- **Mesa escura**: o fundo é o livro-razão verde-oliva (`libris-ledger-bg.svg`) e as fichas de papel continuam claras por cima. Textos que ficam direto sobre a mesa (títulos de página, subtítulos, filtros de gênero/busca) usam `ghoulish-ale`; textos dentro das fichas continuam em `vampire-venom`/tinta.
- Navegação por uma **fita de tecido** vertical à esquerda, na cor mais clara (`paper` `#F7E7CC`) pra se destacar sobre a mesa escura, com ponta em V, com ícone e rótulo em `haunted-forest` e o item ativo em `vampire-venom`: **Acervo · Wishlist · Números · Registrar**.
- Situação do livro aparece como **carimbo** (borda grossa, leve rotação), tanto no filtro quanto na ficha.
- O fundo é o `libris-ledger-bg.svg` repetido (tile de 512×512) sobre `forest-deep`, com um **overlay escuro** por cima (`rgba(20,16,8,.45)`) pra melhorar a legibilidade do que fica sobre a mesa.

## Domínio

Todas as entidades têm `id` (UUID), `userId`, `createdAt` e `updatedAt`. Nada de soft delete, porque não existe sync.

### Book
| Campo | Notas |
|---|---|
| `title` | obrigatório |
| `authors` | lista de nomes, pelo menos 1 |
| `isbn` | opcional (livros de sebo muitas vezes não têm). Guardado normalizado como ISBN-13. Único por usuária quando presente. |
| `totalPages` | obrigatório para livros do acervo; opcional na wishlist |
| `publisher`, `publishedYear` | opcionais |
| `synopsis` | opcional, texto livre |
| `coverId` | opcional, aponta pra capa guardada no servidor |
| `genres` | lista de `Genre` (N:N) |
| `ownership` | `owned` \| `wishlist` |
| `status` | `not_started` \| `reading` \| `read` \| `abandoned`. Só faz sentido com `ownership = owned` (na wishlist é nulo). |
| `startedAt`, `finishedAt` | datas (sem hora). Ver regras. |
| `addedAt` | data em que entrou no app |

### Genre
`name` único por usuária (case-insensitive). A lista vai crescendo conforme eu cadastro: no formulário, um campo de tags com autocomplete das existentes, e criar um gênero novo é só digitar. Também dá pra renomear e apagar (apagar tira o gênero dos livros, sem apagar os livros).

### ReadingLog
| Campo | Notas |
|---|---|
| `bookId` | |
| `date` | data da leitura (sem hora), vem com hoje e é editável |
| `page` | página em que parei, entre 1 e `totalPages` |

### Regras

- **Progresso atual** = `page` do registro com a data mais recente (no empate, o criado por último). Sem registros, 0.
- **Páginas lidas num registro** = `page` − `page` do registro anterior do mesmo livro (ordenado por data e criação). O primeiro registro conta a partir de 0. Um delta negativo (corrigi pra trás) conta como 0. As estatísticas somam esses deltas pela `date` de cada registro.
- **Qualquer registro** num livro `not_started` → status vira `reading`. Se `startedAt` estiver vazio, recebe a `date` desse registro. *(Decisão minha: vale também pra um livro que voltou pra `not_started` e já tem registros.)*
- **Registro com `page = totalPages`** → status vira `read`, e se `finishedAt` estiver vazio, recebe a `date` desse registro.
- **Mudar o status na mão** (pelos carimbos da Ficha):
  - para `reading`: preenche `startedAt` com hoje, se vazio;
  - para `read`: preenche `finishedAt` com hoje, se vazio;
  - para `abandoned`: não mexe nas datas;
  - para `not_started`: limpa `startedAt` e `finishedAt` (os registros continuam lá).
- **Datas editáveis na mão** na Ficha, principalmente pros livros antigos que eu vou cadastrar como "lido em março de 2024" sem registros de página.
- **Criar um livro nunca preenche datas sozinho.** As regras de "preenche com hoje" valem só pra mudança de status *depois* do cadastro. O cadastro aceita `startedAt`/`finishedAt` opcionais, enviados pelo formulário (ver Registrar). *(Decisão minha: senão, todo livro antigo cadastrado como Lido cairia no ano atual.)*
- **ISBN repetido**: cadastrar um ISBN que já existe (no acervo ou na wishlist) é recusado com uma mensagem que aponta o livro existente. *(Decisão minha.)*
- **Apagar ou editar um registro** recalcula o progresso. O status e as datas **não** voltam sozinhos; se precisar, eu corrijo pelos carimbos.
- **Wishlist → "Comprei"**: `ownership` vira `owned`, `status` vira `not_started` e `addedAt` vira hoje. Se `totalPages` estiver vazio, o app pede antes de confirmar.
- **Datas** são datas de calendário, sem hora nem fuso. "Hoje" é a data local do Mac.

### Estatísticas (calculadas na API)

Para um ano escolhido:
- **Livros terminados no ano**: `finishedAt` dentro do ano (independe de ter registros).
- **Páginas lidas no ano**: soma dos deltas dos registros com `date` no ano.
- **Em leitura agora**: total de livros com `status = reading` (não depende do ano).
- **Por mês** (12 entradas): páginas lidas e livros terminados.
- **Detalhe de um mês**: livros terminados (título, autor, `finishedAt`) e livros que andaram (título, soma de páginas no mês), ordenados por páginas.

Livros antigos cadastrados com `finishedAt` e sem registros contam em "terminados" mas não em "páginas". Isso é esperado.

## Telas (LibrisApp)

Todas seguem a estrutura do mock: fita à esquerda e fichas de papel sobre o fundo adamascado.

1. **Login**: e-mail e senha numa ficha. Os tokens ficam guardados no app e o refresh é automático. Sem tela de cadastro.
2. **Acervo** (`ownership = owned`): título "Acervo de leituras" com o subtítulo "N livros no acervo · M em leitura".
   - Filtros por carimbo de situação (Todos · Não iniciado · Lendo · Lido · Abandonado), com escolha única. O padrão ao abrir é **Todos**.
   - Seletor de **gênero** (um por vez, ou todos) e busca por **título ou autor**.
   - Os filtros se combinam entre si. Lista ordenada por título.
   - **Card**: capa à esquerda, título (Aleo), autor, "pág. X de Y · Z%", barra de progresso e tags de gênero. **Sem** campo de registro no card.
   - Clicar no card abre a Ficha.
   - Sem livros, um estado vazio que leva a Registrar.
3. **Ficha do livro**: em duas folhas, como no `telas-v2.html`.
   - **Esquerda**: capa grande, título, autor, editora/ano/páginas, gêneros, sinopse, progresso, carimbos de situação (clicáveis, mudam o status), início/término com "editar datas". Ações: editar livro e apagar livro (com confirmação).
   - **Direita**: **Registrar leitura** (página + data com hoje por padrão, botão "Anotar") e o **Histórico** de registros (data, de → até, +páginas), cada um editável e apagável.
   - Pra livros `not_started` também aparece o registro: anotar a primeira página já inicia a leitura.
4. **Registrar / Editar livro**: o formulário "datilografado" do mock (linhas pautadas e área tracejada pra capa).
   - Campo **ISBN** com botão "Buscar". A busca preenche título, autores, páginas, editora, ano, sinopse, capa e **sugestões** de gênero (chips que eu aceito ou descarto).
   - Tudo continua editável. Sem ISBN, ou se a busca não achar nada, eu preencho na mão.
   - **Capa**: arrastar um arquivo pra área tracejada ou clicar pra escolher. Uma capa que veio do ISBN aparece já preenchida e pode ser trocada.
   - **Destino**: Acervo (padrão) ou Wishlist. No acervo, também a situação inicial (padrão Não iniciado).
   - **Datas opcionais** conforme a situação inicial: Lendo/Abandonado mostram "Início"; Lido mostra "Início" e "Término". Ficam em branco por padrão; nos livros antigos da planilha, que só têm o mês, basta escolher qualquer dia daquele mês (as estatísticas são mensais). *(Decisão minha.)*
   - Botão "Cadastrar obra" / "Salvar".
5. **Wishlist** (`ownership = wishlist`): mesmo layout de cards do Acervo, mas sem progresso nem situação. Busca por título/autor e filtro por gênero. Na Ficha da wishlist, o lugar do registro de leitura é ocupado pelo botão **"Comprei"**.
6. **Números**: título "Números de 2026" com navegação ‹ ano ›.
   - Três cards de totais: terminados no ano, páginas no ano, em leitura agora.
   - Gráfico de barras de páginas por mês (mês clicável).
   - Detalhe do mês: "Terminados em <mês>" e "Andaram em <mês> · N págs.".
   - Ao abrir, vem selecionado o mês atual (ou dezembro, em anos passados).

Toda gravação mostra uma confirmação discreta (toast), e erros de rede viram uma mensagem clara de "sem conexão com o servidor".

## Arquitetura

### API — `Libris` (.NET 10)

Copia o molde do **Verto** (`~/Repos/Verto`), sem dependência de código entre os projetos:
- Clean Architecture: `Libris.Api` / `Libris.Application` / `Libris.Domain` / `Libris.Infrastructure`, com Minimal APIs e EF Core + Npgsql.
- **Auth copiado do Verto** como está (JWT HS256, refresh-token families com rotação, Argon2id, limitador de concorrência em `/v1/auth`), mais um rate limit simples por IP (janela fixa) no `login`. *(Decisão minha: sem contador de falhas/lockout.)* `Auth__AllowSignup` fica ligado só pra criar a minha conta e depois desligado.
- Toda consulta é filtrada por `userId` do token.

**Endpoints (v1)**
- `/v1/auth/{signup,login,refresh,logout}`, `GET /v1/me`, `GET /v1/health`
- `GET /v1/books?ownership=&status=&genreId=&q=` · `POST /v1/books` · `GET/PUT/DELETE /v1/books/{id}`
- `POST /v1/books/{id}/acquire` ("Comprei")
- `PATCH /v1/books/{id}/status`: aplica as regras de status/datas
- `GET/POST /v1/books/{id}/logs` · `PUT/DELETE /v1/books/{id}/logs/{logId}`
- `GET/POST /v1/genres` · `PUT/DELETE /v1/genres/{id}`
- `GET /v1/isbn/{isbn}`: busca os metadados. Não grava nada, devolve sugestão + URL da capa externa.
- `POST /v1/covers` (multipart, ou `{ "url": "…" }` pra baixar a capa sugerida pelo ISBN) → `{ coverId }`. `GET /v1/covers/{coverId}` devolve a imagem.
- `GET /v1/stats/{year}` (totais + 12 meses) · `GET /v1/stats/{year}/{month}` (detalhe)

"Hoje" é sempre a data do cliente: as ações que preenchem datas (`status`, `acquire`, registro) recebem a data local no payload, porque a API roda em UTC. O `POST /v1/covers {url}` só baixa de hosts conhecidos (Open Library, Google Books). *(Decisões minhas.)*

Os payloads de livro devolvem o progresso já calculado (`currentPage`, `percent`) e os gêneros expandidos, pra que o cliente não precise calcular nada. Isso facilita o futuro app KMP.

**Busca por ISBN**: Open Library primeiro (`/isbn/{isbn}.json` + autores + capa em `covers.openlibrary.org`). Se faltar título ou páginas, completa com o Google Books (`volumes?q=isbn:`), sem chave na v1. Aceita ISBN-10 e ISBN-13, valida o dígito verificador e normaliza pra 13. Os gêneros vêm de `subjects` / `categories` como **sugestão**, deduplicados e limitados a uns 5.

**Capas**:
- Guardadas em disco num volume Docker montado na API (`/data/covers`), com o nome igual ao `coverId` (UUID).
- No upload, a API valida o tipo (JPEG/PNG/WebP), limita o tamanho (uns 5 MB) e reduz pra no máximo ~600 px de altura.
- `GET /v1/covers/{id}` é **público** (o UUID não é adivinhável), assim o `<img>` funciona sem header de auth e com cache longo.
- Trocar ou apagar a capa de um livro apaga o arquivo antigo.

### App — `LibrisApp` (Angular + Tauri 2)

- Angular na versão estável atual, com componentes standalone, signals e o novo control flow, sem NgModules. SCSS com os tokens como CSS custom properties.
- Tauri 2 só como casca nativa do Mac na v1 (janela, ícone, empacotamento). Nenhum comando Rust de negócio: tudo passa pela API via HTTP.
- Fontes Aleo e Yantramanav **empacotadas** no app (não via Google Fonts), pra janela abrir sempre igual.
- **Estrutura**:
  - `core/` (cliente HTTP, interceptor de auth, guard, armazenamento de tokens);
  - `features/` (acervo, ficha, registrar, wishlist, numeros, login);
  - `shared/ui/` (fita, card, carimbo, campo pautado, barra de progresso).
- Estado com services + signals por feature, sem lib de estado.
- URL da API por ambiente: `localhost` em dev e `https://libris-api.billfolder.app` no build de release.

### Infra (mesma VPS do BillFolder)

Segue o `docs/deploy.md` do Verto, trocando os nomes:
- Database e role `libris` no Postgres do BillFolder, isolados.
- Serviço `libris-api` no `/opt/billfolder/compose.yml` (rede `billfolder`, `mem_limit`, porta própria em `127.0.0.1`), **mais um volume nomeado pras capas**.
- DNS `libris-api.billfolder.app` (DNS only) + nginx + certbot. O `client_max_body_size` precisa cobrir o upload de capa.
- CI/CD: workflow copiado do Verto (build, test, push no GHCR `ghcr.io/jesstoselli/libris-api`, deploy por SSH).
- **Backup** diário: `pg_dump` do database `libris` **e** um tar do volume de capas, retenção de 14 dias, no mesmo crontab.
- O app Mac é buildado localmente (`tauri build`), sem pipeline de distribuição na v1.

## Testes

- **API**: testes de integração com Postgres real (Testcontainers, como no Verto). Cobrem:
  - auth;
  - isolamento por usuária;
  - CRUD de livros, gêneros e registros;
  - **regras de status/datas**;
  - **cálculo de deltas e estatísticas** (incluindo correção pra trás, empate de data, livro sem registros com `finishedAt`);
  - "Comprei";
  - upload/redimensionamento de capa.
  - A busca por ISBN é testada contra respostas gravadas da Open Library e do Google Books (sem rede no teste).
- **App**: testes unitários dos services e dos componentes com lógica (filtros, formulário com ISBN, registro de leitura).

## Ordem sugerida de construção

1. API: esqueleto + auth copiado + health + deploy na VPS (o banco já fica de pé).
2. API: livros, gêneros, registros e regras, com testes.
3. API: ISBN e capas.
4. API: estatísticas.
5. App: setup Angular + Tauri, tokens visuais, fundo, fita, login.
6. App: Acervo + Ficha + registro de leitura.
7. App: Registrar/Editar com ISBN e capa.
8. App: Wishlist e Números.

Os passos 5 a 8 podem começar contra a API local enquanto os passos 2 a 4 avançam.

## Fica a critério de quem constrói

- Ícones da fita, desde que sigam o traço do mock.
- Como renderizar o damasco com boa performance: o SVG tem ~600 KB de paths, então pode valer rasterizar num tile PNG/WebP.
- Detalhes de layout responsivo da janela (tamanho mínimo, quantas colunas de card).
- Formato exato de erro da API (seguir o do Verto).
- Biblioteca de gráfico, ou barras em CSS/SVG feitas à mão (prefira à mão, como no rascunho).
