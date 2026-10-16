# Libris API

Backend do Libris: acervo pessoal de livros, registros de leitura e números. Design em `docs/superpowers/specs/2026-10-16-libris-design.md`.

## Rodar local

```bash
docker compose -f docker-compose.dev.yml up -d
dotnet run --project src/Libris.Api --launch-profile http
curl http://localhost:5090/v1/health
```

## Testes

Precisam do Docker rodando (Testcontainers sobe um Postgres por classe de teste).

```bash
dotnet test
```

## Configuração

| Chave | Padrão | Observação |
|---|---|---|
| `ConnectionStrings:Postgres` | — | obrigatória |
| `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` | — / `libris` / `libris-app` | |
| `Auth:AllowSignup` | `false` | ligar só pra criar a conta |
| `Auth:LoginPermitPerMinute` | `10` | tentativas de login por IP por minuto |
| `Cors:AllowedOrigins` | origens do Tauri | |
| `Covers:Path` | `/data/covers` | volume Docker em produção |
| `Isbn:GoogleBooksApiKey` | vazio | opcional; sem ela a cota anônima do Google costuma estar esgotada |

## Deploy

`docs/deploy.md` (mesma VPS e Postgres do BillFolder).
