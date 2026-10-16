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
