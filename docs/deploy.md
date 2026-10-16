# Libris API — deploy na VPS (runbook)

Executar na ordem. A VPS e o Postgres são os mesmos do BillFolder, então cada passo tem uma verificação.

**Antes de colar qualquer bloco, olhe o prompt.** `>` (zsh) é o seu Mac; `deploy@billfolder-prod` é a VPS. Os passos dizem onde cada comando roda.

Valores fixos desta instalação:

| O quê | Valor |
|---|---|
| IP da VPS | `178.104.160.8` |
| Usuário SSH | `deploy` (tem `sudo` completo, com senha) |
| Chave SSH | `~/.ssh/billfolder_deploy_key` |
| Pasta do compose | `/opt/billfolder` (arquivo `compose.yml`, rede `billfolder`) |
| Host da API | `libris-api.billfolder.app` |
| Imagem | `ghcr.io/jesstoselli/libris-api:latest` |

Entrar na VPS (do Mac):

```bash
ssh -i ~/.ssh/billfolder_deploy_key deploy@178.104.160.8
```

## 1. Repo no GitHub e secrets (Mac)

Cadastre os secrets **antes** do primeiro push: o `gh repo create ... --push` já dispara o deploy, e sem secrets o job "Deploy to VPS" falha no `ssh-keyscan`. Se isso acontecer, é só rodar de novo o job depois (passo 6).

```bash
cd ~/Repos/Libris
gh repo create libris-api --private --source=. --remote=origin
gh secret set VPS_HOST --body "178.104.160.8"
gh secret set VPS_USER --body "deploy"
gh secret set VPS_SSH_KEY < ~/.ssh/billfolder_deploy_key
```

Os secrets do GitHub não podem ser lidos de volta (nem os do BillFolder): os valores vêm da tabela acima. Para conferir a chave antes: `ssh -i ~/.ssh/billfolder_deploy_key -o BatchMode=yes deploy@178.104.160.8 whoami` deve responder `deploy`.

## 2. DNS na Cloudflare

Painel → `billfolder.app` → **DNS → Records → Add record**:

| Campo | Valor |
|---|---|
| Type | `A` |
| Name | `libris-api` |
| IPv4 | `178.104.160.8` |
| Proxy status | **DNS only** (nuvem cinza, igual ao registro `api`) |

DNS only é o que deixa o `certbot` (passo 5) emitir o certificado direto na VPS.

Verificar (Mac): `dig +short libris-api.billfolder.app` → `178.104.160.8`.

## 3. Banco e role (VPS)

Rode **uma vez só**: repetir duplica as linhas do `.env` e gera uma senha que não bate com a do banco.

```bash
cd /opt/billfolder
docker compose ps postgres
LIBRIS_DB_PASSWORD=$(openssl rand -hex 24)
echo "LIBRIS_DB_PASSWORD=$LIBRIS_DB_PASSWORD" >> .env
echo "LIBRIS_JWT_KEY=$(openssl rand -hex 48)" >> .env
echo "LIBRIS_ALLOW_SIGNUP=true" >> .env
docker compose exec -T postgres psql -U billfolder -d postgres <<SQL
CREATE ROLE libris LOGIN PASSWORD '$LIBRIS_DB_PASSWORD';
CREATE DATABASE libris OWNER libris;
REVOKE CONNECT ON DATABASE billfolder FROM PUBLIC;
SQL
```

Verificar:
- `docker compose exec -T postgres psql -U libris -d billfolder -c 'select 1'` **falha** com `permission denied for database "billfolder"`.
- `curl -sS -o /dev/null -w '%{http_code}\n' https://api.billfolder.app/v1/health` → `200`.

Efeito colateral conhecido: `postgres` e `api` do BillFolder usam `env_file: .env`, então o Compose passa a ver a config deles como alterada. Nada acontece sozinho, mas o próximo `docker compose up` sem `--no-deps` recria o container do Postgres (segundos fora do ar; os dados ficam no volume). Para escolher o momento: `docker compose up -d postgres api`.

## 4. Serviço no compose (VPS)

```bash
cd /opt/billfolder
cp compose.yml compose.yml.bak-$(date +%F)
nano compose.yml
```

Cole **só** o bloco abaixo dentro de `services:`, logo depois do fim do serviço `api:` (a linha `retries: 3` do healthcheck dele), com uma linha em branco antes e outra depois. `libris-api:` fica com 2 espaços, alinhado com `api:` e `postgres:`. Não copie nenhuma linha que já existe no arquivo.

```yaml
  libris-api:
    image: ghcr.io/jesstoselli/libris-api:latest
    container_name: libris-api
    restart: unless-stopped
    # Caps Libris's memory so an Argon2 login burst can't push the host into OOM-killing BillFolder.
    mem_limit: 512m
    depends_on:
      postgres:
        condition: service_healthy
    ports:
      - "127.0.0.1:8082:8080"
    environment:
      ConnectionStrings__Postgres: "Host=postgres;Database=libris;Username=libris;Password=${LIBRIS_DB_PASSWORD}"
      Jwt__Key: "${LIBRIS_JWT_KEY}"
      Jwt__Issuer: "libris"
      Jwt__Audience: "libris-app"
      Jwt__RefreshTokenDays: "90"
      Auth__AllowSignup: "${LIBRIS_ALLOW_SIGNUP:-false}"
      Database__MigrateOnStartup: "true"
      Cors__AllowedOrigins__0: "tauri://localhost"
      Cors__AllowedOrigins__1: "http://tauri.localhost"
      # Opcional: sem chave, o Google Books usa uma cota anônima que costuma estar esgotada.
      Isbn__GoogleBooksApiKey: "${LIBRIS_GOOGLE_BOOKS_KEY:-}"
    volumes:
      - libris_covers:/data/covers
    networks:
      - billfolder
```

O Libris guarda as capas num volume próprio. No fim do `compose.yml`, procure a chave **`volumes:` de nível superior** (sem espaços antes):

```bash
grep -n '^volumes:' compose.yml
```

- Se aparecer uma linha: acrescente logo abaixo dela, com 2 espaços, `  libris_covers:`.
- Se não aparecer nada: acrescente no fim do arquivo as duas linhas `volumes:` e `  libris_covers:`.

Nunca crie um segundo `volumes:` de nível superior (o Compose recusa com `mapping key "volumes" already defined`).

Sem `networks: - billfolder` o Libris não enxerga o `postgres`.

Verificar:

```bash
docker compose config --quiet && echo OK
docker compose config --services
```

`OK` e a lista com `postgres`, `api`, `libris-api` (e `verto-api`, se o Verto já estiver lá). Erro `mapping key "..." already defined at line N` = linha colada em duplicidade: veja com `cat -n compose.yml | sed -n '30,80p'` e apague a cópia (`sed -i 'N,Md' compose.yml`). Se embolar: `cp compose.yml.bak-$(date +%F) compose.yml`.

Não rode `docker compose up` aqui: a imagem só existe no GHCR depois do passo 6.

## 5. nginx + TLS (VPS)

Precisa de `sudo` com a senha do `deploy`. Confira com `sudo -v && echo SUDO_OK`.

```bash
ls /etc/nginx/sites-enabled && certbot --version
```

Esperado: `api.billfolder.app` (e `default`) e `certbot 2.x`.

```bash
sudo tee /etc/nginx/sites-available/libris-api.billfolder.app > /dev/null <<'NGINX'
server {
    listen 80;
    server_name libris-api.billfolder.app;

    # A 5 MB cover upload plus multipart overhead; nginx's 1 MB default would 413 it.
    client_max_body_size 6m;

    location / {
        proxy_pass http://127.0.0.1:8082;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
NGINX
sudo ln -s /etc/nginx/sites-available/libris-api.billfolder.app /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d libris-api.billfolder.app
```

Esperado: `syntax is ok`, `test is successful` e `Successfully deployed certificate`. Até o passo 6, o endereço responde 502 (o container ainda não existe).

### Se a senha do `sudo` do `deploy` não funcionar

O root só aceita chave por SSH e o console web da Hetzner costuma errar caracteres (teclado US, sem colar). O caminho que funciona:

1. Hetzner Console → **Servers → billfolder-prod → Rescue → Reset root password** (só esse botão; "Enable rescue & power cycle", Power, Rebuild e Delete reiniciam ou apagam a VPS). Guarde a senha nova.
2. No Mac, entre como `deploy` por SSH e rode `su -`, colando a senha nova do root.
3. Como root: `passwd deploy` → defina e guarde a senha do `deploy`. `exit`.
4. Como `deploy`: `sudo -v && echo SUDO_OK`.

## 6. Deploy (Mac)

Cada push na `main` testa, publica a imagem e sobe o container (`docker compose up -d --no-deps libris-api`).

```bash
cd ~/Repos/Libris
git push -u origin main
gh run watch
```

Se o deploy falhou antes dos secrets existirem, rode só o job que falhou: `gh run rerun <run-id> --failed` (o id aparece em `gh run list`).

Esperado: os dois jobs verdes, `✓ Deploy complete`. Se o pull da imagem falhar com `denied`, a VPS não tem permissão no pacote novo: na VPS, `docker login ghcr.io` com um token `read:packages`.

Verificar: `curl -fsS https://libris-api.billfolder.app/v1/health` → `"status":"ok"`.

## 7. Conta da dona e cadastro fechado

Criar a conta (Mac). Pede email e senha sem deixar a senha no histórico; deve imprimir `200`:

```bash
read "LIBRIS_EMAIL?Email: "; read -s "LIBRIS_PASS?Senha (min. 8): "; echo; jq -n --arg e "$LIBRIS_EMAIL" --arg p "$LIBRIS_PASS" '{email:$e,password:$p,displayName:"Jess"}' | curl -sS -o /dev/null -w '%{http_code}\n' -X POST https://libris-api.billfolder.app/v1/auth/signup -H "Content-Type: application/json" --data @-; unset LIBRIS_PASS
```

Fechar o cadastro (Mac):

```bash
ssh -i ~/.ssh/billfolder_deploy_key deploy@178.104.160.8 'cd /opt/billfolder && sed -i "s/^LIBRIS_ALLOW_SIGNUP=true/LIBRIS_ALLOW_SIGNUP=false/" .env && docker compose up -d --no-deps libris-api'
```

Verificar: deve dar `403`.

```bash
curl -sS -o /dev/null -w '%{http_code}\n' -X POST https://libris-api.billfolder.app/v1/auth/signup -H "Content-Type: application/json" -d '{"email":"teste@example.com","password":"qualquer-coisa-123","displayName":"X"}'
```

## 8. Backup diário (VPS)

O BillFolder faz backup às 03:00 e o Verto às 03:15 pelo crontab do `deploy`. O do Libris roda às 03:30 e salva **duas coisas**: o banco `libris` e o volume das capas (sem ele, uma VPS nova teria as fichas sem capa). Guarda 14 dias e registra tudo em `/opt/billfolder/logs/backup-libris.log`.

Confira o nome real do volume (o Compose prefixa com o nome do projeto):

```bash
docker volume ls | grep libris
```

Esperado: `billfolder_libris_covers`. Se for outro, passe `COVERS_VOLUME=<nome>` na linha do cron abaixo.

O backup é o script `scripts/backup-libris.sh` deste repo. Ele usa `pipefail` e só renomeia o arquivo depois que tudo deu certo: se o `pg_dump` falhar, o cron registra o erro, não fica um `.sql.gz` vazio com cara de backup, e a rotação de 14 dias não roda (os backups bons não são apagados).

Copiar o script (Mac):

```bash
scp -i ~/.ssh/billfolder_deploy_key ~/Repos/Libris/scripts/backup-libris.sh deploy@178.104.160.8:/opt/billfolder/scripts/backup-libris.sh
```

Agendar (VPS):

```bash
chmod +x /opt/billfolder/scripts/backup-libris.sh
(crontab -l | grep -v 'libris'; echo '30 3 * * * /opt/billfolder/scripts/backup-libris.sh >> /opt/billfolder/logs/backup-libris.log 2>&1') | crontab -
crontab -l | tail -3
```

O `grep -v 'libris'` substitui uma linha anterior do Libris em vez de duplicar; as linhas do BillFolder e do Verto ficam intactas.

Testar agora (VPS):

```bash
/opt/billfolder/scripts/backup-libris.sh && ls -la /opt/billfolder/backups/libris-*
gunzip -c /opt/billfolder/backups/libris-$(date +%F).sql.gz | grep -c "CREATE TABLE"
tar tzf /opt/billfolder/backups/libris-covers-$(date +%F).tgz | head
```

Esperado: os dois arquivos do dia; **8** tabelas (`users`, `refresh_tokens`, `books`, `genres`, `book_genres`, `reading_logs`, `covers`, `__EFMigrationsHistory`); e a lista de `.webp` das capas (vazia até a primeira capa).

### Restaurar as capas

```bash
docker run --rm -v billfolder_libris_covers:/c -v /opt/billfolder/backups:/b alpine tar xzf /b/libris-covers-AAAA-MM-DD.tgz -C /c
```
