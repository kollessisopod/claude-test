# 🎵 Game OST Quiz

A daily, Wordle-style browser game: listen to a clip and guess which game's soundtrack
it's from. You get **6 guesses**; each failed/skipped guess unlocks more audio
(`1s → 2s → 3s → 5s → 10s → full`) and reveals one more hint about the game, in order:
**genre → release date → metacritic score → publisher → developer**.

Play is anonymous — the daily puzzle is the same for everyone and your progress lives in
your browser's `localStorage` (no accounts, no tracking). A separate **Previous days** page lists
the archive (paginated, 20 at a time with a "Load more days" button); each day shows your result
as colored boxes (one per attempt, gray for unplayed). Toggle a **light/dark theme** any time.
When a round ends — won, failed, or skipped out — the game plays the **full track** and reveals
the answer with its **game cover** and **album cover** (or a "no image" placeholder if none).

Each guess is listed with a colored indicator:

- 🟩 **green** — correct
- 🟨 **yellow** — wrong, but the same franchise as the answer (e.g. guessing *DOOM Eternal* when
  the answer is *DOOM*)
- 🟥 **red** — wrong (or skipped)

Franchise grouping is the `Game.Franchise` field — two games match when they share the same
non-null value. It's set in the dev seed today; a later RAWG enrichment pass can populate it
automatically.

### Theming

All colors are CSS custom properties defined once in [frontend/src/theme.css](frontend/src/theme.css)
(`:root` for light, `[data-theme="dark"]` for dark). Re-skin the app by editing those variables —
components reference only the tokens, never raw colors. The toggle persists to `localStorage` and
defaults to the OS `prefers-color-scheme`.

## Architecture

Fully containerized, orchestrated by `docker-compose`:

| Service     | Tech                              | Port (host) | Purpose                                   |
|-------------|-----------------------------------|-------------|-------------------------------------------|
| `postgres`  | PostgreSQL 17                     | 5432        | Game catalog + puzzle metadata            |
| `minio`     | MinIO (S3-compatible)             | 9000 / 9001 | Audio clips + cover images (API/console)  |
| `backend`   | ASP.NET Core (.NET 9), EF Core    | 8080        | Game API + admin upload API               |
| `frontend`  | React + Vite + TS (nginx)         | 3000        | The player-facing game                    |
| `admin`     | React + Vite + TS (nginx)         | 3001        | Uploader: import metadata, create puzzles |

Game metadata (genre, release date, metacritic, publisher, developer, covers) comes from the
**RAWG API**. Audio is uploaded manually via the admin app — game OST audio isn't legally
fetchable from an API.

## Quick start

```bash
cp .env.example .env          # optionally set RAWG_API_KEY
docker compose up --build
```

Then open:
- **Player game:** http://localhost:3000
- **Admin uploader:** http://localhost:3001  (admin key defaults to `dev-admin-key`)
- **API + Swagger:** http://localhost:8080/swagger
- **MinIO console:** http://localhost:9001  (`minioadmin` / `minioadmin`)

On first start the backend applies EF migrations and seeds a small game catalog plus a
playable puzzle for today (with a synthesized audio clip and per-step trimmed clips), so
everything is testable immediately — even without a RAWG key.

### Filling out the game catalog (autocomplete pool)

The seed ships only **8 games**, so the guess autocomplete is sparse until you import the full
catalog from RAWG. To get the real list (Assassin's Creed, etc.):

1. Get a free key at https://rawg.io/apidocs and set `RAWG_API_KEY=...` in `.env`.
2. `docker compose up -d backend` to pick up the key.
3. Open the admin app (http://localhost:3001), enter the admin key, click **Import top 200 games**
   (or `POST /api/admin/games/import` with `X-Admin-Key`).

Publisher/developer for a game are fetched from RAWG lazily the first time that hint is needed.

## API overview

Puzzles are addressed by date (`yyyy-MM-dd`). The frontend lists the archive, defaults to today,
and lets you replay earlier days. `<date>` must be today or earlier.

Public:
- `GET  /health`
- `GET  /api/games?search=&limit=` — autocomplete pool (`id`, `name` only)
- `GET  /api/games/{id}/cover` — game cover image (404 if none)
- `GET  /api/puzzles?skip=0&take=20` — archive page, newest first (`{ items: [{ date, isToday }], total }`)
- `GET  /api/puzzles/{date}` — puzzle metadata + initial progression token (no answer/hints)
- `GET  /api/puzzles/{date}/audio?step=0..5&token=` — audio clip for a token-unlocked step
- `GET  /api/puzzles/{date}/hint?step=1..5&token=` — the hint unlocked at a step
- `GET  /api/puzzles/{date}/album-cover` — album cover image (404 if none)
- `POST /api/puzzles/{date}/guess` — `{ token, guessGameId }` → `{ correct, gameOver, revealedHint?, answer?, nextToken?, guessedGameName?, franchiseMatch, fullAudioToken? }` (`fullAudioToken` unlocks the whole track at game over)

Admin (require header `X-Admin-Key: <ADMIN_KEY>`):
- `POST /api/admin/games/import` — `{ count }` pull top-N games from RAWG
- `POST /api/admin/puzzles` — `multipart/form-data` with `date`, `gameId` (or `rawgId`), an `audio`
  file (mandatory), and optional `gameCover` / `albumCover` image files

### Smoke test

```bash
curl localhost:8080/health
curl localhost:8080/api/puzzles                       # archive
DATE=$(date -u +%Y-%m-%d)
TOKEN=$(curl -s "localhost:8080/api/puzzles/$DATE" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
curl "localhost:8080/api/games?search=cel"
curl "localhost:8080/api/puzzles/$DATE/audio?step=0&token=$TOKEN" --output clip.wav
curl -X POST "localhost:8080/api/puzzles/$DATE/guess" \
  -H 'content-type: application/json' -d "{\"token\":\"$TOKEN\",\"guessGameId\":1}"
```

## Local backend development (without containers)

Requires the .NET 9 SDK. Run Postgres + MinIO via compose, then:

```bash
cd backend/OstQuiz.Api
dotnet run    # uses appsettings.json (localhost endpoints)
```

EF migrations (the `dotnet-ef` tool is pinned in `.config/dotnet-tools.json`):

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Name> -o Data/Migrations --project backend/OstQuiz.Api
```

## Progression tokens (anti-skip-ahead)

Play is anonymous with no server-side session, so the current step travels in a compact
**HMAC-signed token** (`base64url(yyyy-MM-dd|step).base64url(sig)`) instead of being trusted
from the client:

- `GET /api/puzzles/today` issues an initial token for step 0.
- `audio`/`hint` require the token and reject any `step` beyond what it unlocks (`403`), or an
  invalid/forged/expired token (`401`).
- `guess` **derives the step from the token** (never from the request body); each wrong/skipped
  guess mints the next-step token, returned as `nextToken`.

This makes it impossible to fetch more audio or reveal later hints than legitimately earned.
Set `TOKENS_SECRET` in production.

## Known limitations (first pass)

- **Repeated guesses at the same step:** without server-side state we don't track which guesses
  were consumed, so a client could resubmit many guesses against the same token to discover the
  answer by brute force. Step-gated *content* (audio length, hint reveals) is fully enforced;
  closing this fully needs server-side per-puzzle guess tracking or rate limiting.
- Frontend/admin are intentionally lean first-pass UIs.

## Per-step audio clips

When a puzzle is created (admin upload or seed), the backend uses **ffmpeg** to trim the source
into sample-accurate clips for steps 0–4 (`1, 2, 3, 5, 10s`), stores them in MinIO, and records
`PuzzleClip` rows. `GET /api/puzzles/today/audio?step=N` serves the matching clip; the final
step serves the full audio. If ffmpeg is unavailable or a source file can't be decoded, clip
generation is skipped and the API falls back to the full audio for every step.
