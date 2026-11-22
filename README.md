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
Com `Firebase:ProjectId` preenchido, o login passa a exigir ID token do Firebase (config pública no `fm_front`).

Segredos (`Gemini:ApiKey`, `WhatsApp:Token`, etc.): use `Financeiro.Api/appsettings.Development.local.json` (já no `.gitignore`), não o `appsettings.json`.

### WhatsApp (dev)

Enquanto o webhook Meta não estiver no ar, simule inbound:

```bash
curl -X POST http://localhost:5274/api/whatsapp/dev/mensagem \
  -H "Content-Type: application/json" \
  -d "{\"numero\":\"11987654321\",\"texto\":\"gastei 45 no almoço\",\"identificador\":\"msg-1\"}"
```

#### Webhook real com ngrok

1. API rodando em `http://localhost:5274`.
2. `ngrok http 5274` → copie a URL `https://….ngrok-free.app`.
3. No Meta Developer → WhatsApp → Configuration → Webhook:
   - **Callback URL:** `https://….ngrok-free.app/api/whatsapp/webhook`
   - **Verify token:** o mesmo de `WhatsApp:VerifyToken` (em Development: `fm-dev-verify` no `appsettings.Development.json`)
4. Assine o campo `messages`.
5. Depois preencha no local: `WhatsApp:Token` (access token), `PhoneNumberId` e `AppSecret` — esses vêm do painel Meta, não do callback de verificação.

Endpoints:

- `GET /api/whatsapp/webhook` — challenge de verificação
- `POST /api/whatsapp/webhook` — mensagens de texto inbound (resposta outbound ainda precisa do Token)
