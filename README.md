# Zonar API: .NET 10 backend

| | |
|---|---|
| **Backend repo** | https://github.com/Tharaka-98/ZonarBackend |
| **Website repo** | https://github.com/Tharaka-98/AIEcoSystemZonar |
| **Live API** | https://zonar-api-c0bt.onrender.com/swagger |
| **Live website** | https://ai-eco-system-zonar.vercel.app |
| **Telegram bot** | [@ZonaraDemo_bot](https://t.me/ZonaraDemo_bot) |
| **Author** | Tharaka Senevirathne |

This is the backend for the Zonar website. It **scores community messages for quality (0–100)**, explains each score, and **gives reward points to useful contributors**. Spam, hype and duplicate messages earn nothing. It works with two clients:

1. **The website.** The Next.js site calls the REST API for its "Try the scorer" demo and leaderboard.
2. **A Telegram bot.** Add the bot to a group and it scores every message there and answers commands.

> **Privacy by design:** message text is **never stored**. The database keeps only a hash of each message (to detect duplicates), its length and its score. Telegram user IDs are converted with a salted HMAC hash, and contributors appear under pseudonyms such as `Contributor-7F3A2C`.

---

## Architecture

```
 Next.js site (Vercel)          Telegram group
        │  REST/JSON                  │  long polling
        ▼                             ▼
 ┌────────────────────────────────────────────────┐
 │              ASP.NET Core 10 Minimal API       │
 │  Endpoints ─► ContributionService              │
 │                 ├─ IdentityHasher (HMAC)       │
 │                 ├─ IMessageScorer              │
 │                 │    ├─ RuleBasedScorer        │
 │                 │    └─ LlmScorer (optional)   │
 │                 ├─ RewardPolicy                │
 │                 └─ IContributionStore          │
 │  TelegramBotService (BackgroundService)        │
 └───────────────────────┬────────────────────────┘
                         ▼
                 SQLite (EF Core 10)
```

| Concern | How it's handled |
|---|---|
| Framework | ASP.NET Core 10 (LTS) Minimal APIs |
| Database | Entity Framework Core 10 — SQLite locally, PostgreSQL in production (chosen automatically from the connection string) |
| Scoring | Explainable rule engine, plus an optional OpenAI-compatible LLM with automatic fallback |
| Anti-abuse | Spam detection, 24h duplicate detection, daily points cap, and per-IP rate limiting |
| Telegram | Plain `HttpClient` calls to the Bot API with long polling (no webhook or public URL needed) |
| API docs | Swagger UI at `/swagger` |
| Errors | RFC 7807 Problem Details and validation errors |
| Ops | `/health` endpoint, Dockerfile, settings driven by configuration |
| Tests | xUnit unit tests and integration tests using `WebApplicationFactory` |

---

## Run it locally

**Requirements:** the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (LTS, supported until Nov 2028). It runs on macOS (Apple Silicon or Intel), Windows and Linux.

```bash
git clone https://github.com/Tharaka-98/ZonarBackend.git
cd ZonarBackend
dotnet run --project src/Zonar.Api
```

Then open **http://localhost:5080/swagger**. On first run it creates `zonar.db` and seeds some demo contributors.

Run the tests:

```bash
dotnet test
```

---

## API

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/score` | Scores a message without saving anything |
| `POST` | `/api/contributions` | Scores a message and awards points to a nickname (website demo) |
| `GET` | `/api/leaderboard?days=7&top=10&communityId=` | Top contributors |
| `GET` | `/api/contributors/{id}` | A contributor's profile and recent scores |
| `GET` | `/api/communities` | Communities (website, demo, Telegram groups) |
| `GET` | `/api/stats` | Platform statistics |
| `GET` | `/health` | Health check |

**Example request**

```bash
curl -X POST http://localhost:5080/api/score \
  -H "Content-Type: application/json" \
  -d '{"message":"Volume dropped 30% while price held support, which means sellers may be exhausted. What does the sentiment data say?"}'
```

**Example response**

```json
{
  "score": 100,
  "label": "High",
  "scorerUsed": "rules",
  "reasons": [
    { "text": "Detailed message", "impact": 12 },
    { "text": "Asks a meaningful question", "impact": 8 },
    { "text": "Explains reasoning", "impact": 10 },
    { "text": "Relevant, on-topic insight", "impact": 10 }
  ]
}
```

---

## How scoring and rewards work

1. **Rule engine (always on).** It starts at 50, then each rule adds or removes points and records a reason. Positive rules cover detail, varied vocabulary, questions, reasoning words, on-topic terms and data. Negative rules cover very short messages, repetition, ALL CAPS, stretched characters, links, mass mentions and scam phrases. A message is labelled **Spam** if it contains a scam phrase or scores below 20.
2. **AI mode (optional).** Set `Scoring:Provider=OpenAI` and an API key. Obvious spam is still rejected by the rules, so it never reaches the paid model. Everything else gets an AI score, blended as `0.6 × AI + 0.4 × rules`. If the AI call fails, the API falls back to the rule score.
3. **Rewards:**
   - A score of 70 or more earns **10 points**.
   - A score of 40 or more earns **3 points**.
   - Spam and duplicates earn **0 points**.
   - Each contributor can earn at most **50 points per 24 hours**.

The points are simulated. They are not a real cryptocurrency.

---

## Configuration

Use `appsettings.json`, environment variables (`Section__Key`) or `dotnet user-secrets`.

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings:Zonar` | `Data Source=zonar.db` | SQLite file, or a PostgreSQL connection string (see [Database](#database)) |
| `Cors:AllowedOrigins` | localhost:3000, Vercel URL | Front-end origins allowed to call the API |
| `Scoring:Provider` | `Rules` | `Rules` or `OpenAI` |
| `Scoring:ApiKey` / `Model` / `BaseUrl` | empty / `gpt-4o-mini` / OpenAI | LLM settings (any OpenAI-compatible API works) |
| `Rewards:*` | see the rules above | Thresholds, points, daily cap, duplicate window |
| `Privacy:HashSalt` | `change-me-in-production` | **Set a long random secret in production** |
| `Telegram:Enabled` / `BotToken` | `false` / empty | Turns the Telegram bot on |
| `RateLimiting:ScoringPerMinute` | `20` | Scoring requests allowed per IP per minute |
| `Seed:DemoData` | `true` | Seeds demo contributors when the database is empty |

Keep secrets out of git. The simplest way is a `src/Zonar.Api/appsettings.Local.json` file. It's git-ignored and loaded automatically:

```json
{
  "Telegram": { "Enabled": true, "BotToken": "123456:ABC..." },
  "Privacy": { "HashSalt": "a-long-random-string" }
}
```

Or use `dotnet user-secrets`:

```bash
cd src/Zonar.Api
dotnet user-secrets init
dotnet user-secrets set "Telegram:BotToken" "123456:ABC..."
dotnet user-secrets set "Privacy:HashSalt" "a-long-random-string"
dotnet user-secrets set "Scoring:ApiKey" "sk-..."   # optional
```

---

## Telegram bot setup

1. In Telegram, message **@BotFather** and send `/newbot`, then copy the token.
2. Still in BotFather, send `/setprivacy`, choose your bot and select **Disable**. This lets the bot read ordinary group messages.
3. Set `Telegram:Enabled=true` and `Telegram:BotToken` (e.g. in `appsettings.Local.json`), then run the API. On startup the bot registers its own `/` command menu and description.
4. Add the bot to a group and start chatting.

| Command | What it does |
|---|---|
| `/score <text>` | Shows how a message would score, with reasons |
| `/me` | Shows your points and stats |
| `/top` | Shows this group's 7-day leaderboard |
| `/stats` | Shows platform-wide statistics |
| `/help` | Lists the commands |

---

## Deploy

Vercel can't host .NET, so deploy the API separately. All the options below have free tiers.

### Database

The provider is chosen from the connection string, so the same build runs on either:

| Connection string | Provider |
|---|---|
| `Data Source=zonar.db` | SQLite (default, for local development) |
| `postgresql://user:pass@host/db?sslmode=require` | PostgreSQL |
| `Host=...;Database=...;Username=...` | PostgreSQL |

URI-style strings from Neon, Supabase, Render and Heroku are converted to Npgsql's key/value
format automatically, and TLS is required unless the URI sets `sslmode` itself.

**Why this matters:** a container filesystem is temporary. On a free host the SQLite file is
deleted whenever the service restarts, redeploys or wakes from sleep. Pointing
`ConnectionStrings__Zonar` at a managed PostgreSQL database (for example a free
[Neon](https://neon.tech) project) keeps contributions and leaderboards permanently.

**Render (recommended, free)**

1. On [render.com](https://render.com), click **New +** → **Web Service** and connect this repository. Render detects the `Dockerfile` automatically. Choose the **Free** instance type.
2. Add these environment variables:

| Key | Value |
|---|---|
| `PORT` | `8080` |
| `Telegram__Enabled` | `true` |
| `Telegram__BotToken` | your BotFather token |
| `Privacy__HashSalt` | a long random string |
| `Cors__AllowedOrigins__0` | `https://ai-eco-system-zonar.vercel.app` |
| `Cors__AllowedOrigins__1` | `http://localhost:3000` |
| `ConnectionStrings__Zonar` | PostgreSQL connection string — without it, data is lost on every restart |
| `Seed__DemoData` | `false` once real contributions exist |

3. Deploy, then check `https://zonar-api-c0bt.onrender.com/health` and `/swagger`. (This project is deployed at `https://zonar-api-c0bt.onrender.com`.)
4. In Vercel, set `NEXT_PUBLIC_API_URL=https://zonar-api-c0bt.onrender.com` and redeploy the website.
5. Free services sleep after 15 minutes without traffic. A free [UptimeRobot](https://uptimerobot.com) monitor on `https://zonar-api-c0bt.onrender.com/health` every 5 minutes keeps it (and the bot) awake.

> Only one copy of the bot can receive Telegram messages at a time. While the hosted version is running, set `Telegram:Enabled` to `false` locally.

**Docker (works anywhere: Render, Railway, Fly.io, Azure Container Apps)**

```bash
docker build -t zonar-api .
docker run -p 8080:8080 -v zonar-data:/data \
  -e Privacy__HashSalt="..." -e Telegram__Enabled=true -e Telegram__BotToken="..." \
  zonar-api
```

**Azure App Service** (students get free credit through *Azure for Students*)

```bash
cd src/Zonar.Api
az webapp up --runtime "DOTNETCORE:10.0" --sku F1 --name zonar-api-<yourname>
```

Then set the front end's `NEXT_PUBLIC_API_URL` to the deployed URL. Also add the site's URL to `Cors:AllowedOrigins`.

---

## Project structure

```
ZonarBackend/
├── src/Zonar.Api/
│   ├── Program.cs              # DI, middleware, endpoints
│   ├── Endpoints/              # REST endpoints and validation
│   ├── Services/
│   │   ├── ContributionService.cs   # core use case
│   │   ├── Scoring/            # rule engine, LLM scorer
│   │   ├── Rewards/            # reward policy
│   │   └── Privacy/            # HMAC identity hashing
│   ├── Telegram/               # Bot API client, polling worker, command handler
│   ├── Data/                   # EF Core DbContext, store, seeding
│   ├── Domain/                 # entities
│   ├── Contracts/              # request/response DTOs
│   └── Options/                # strongly-typed settings
├── tests/Zonar.Api.Tests/      # xUnit unit and integration tests
└── Dockerfile
```
