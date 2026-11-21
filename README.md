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
