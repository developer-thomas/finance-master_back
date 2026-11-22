# fm_back

API do Finance Master (ASP.NET Core + EF Core + PostgreSQL).

## Branches (git flow)

- `main` — estável
- `develop` — integração
- `feature/*` — trabalho em andamento

## Local

```bash
docker compose up -d
dotnet ef database update --project Financeiro.Infraestrutura --startup-project Financeiro.Api
dotnet run --project Financeiro.Api
```

API em `http://localhost:5274`.

Sem `Firebase:ProjectId`, o ambiente Development aceita token `Bearer local.<base64>` gerado pelo `fm_front` em modo local.

### WhatsApp (dev)

Com o número já vinculado no app, simule uma mensagem inbound:

```bash
curl -X POST http://localhost:5274/api/whatsapp/dev/mensagem \
  -H "Content-Type: application/json" \
  -d "{\"numero\":\"11987654321\",\"texto\":\"gastei 45 no almoço\",\"identificador\":\"msg-1\"}"
```

Texto completo grava na conta padrão. Texto incompleto só responde o que faltou (sem pendência nesta etapa).
