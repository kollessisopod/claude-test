import { useEffect, useMemo, useState } from 'react'

const TOTAL_GUESSES = 6
const PAGE_SIZE = 20

// --- API types (mirror the backend contracts) ---
type PuzzleDateInfo = { date: string; isToday: boolean }
type ArchivePage = { items: PuzzleDateInfo[]; total: number }
type PuzzleMeta = {
  puzzleId: number
  date: string
  totalGuesses: number
  durationSteps: (number | null)[]
  hintOrder: string[]
  token: string
}
type GameOption = { id: number; name: string }
type Hint = { step: number; kind: string; value: string }
type Answer = {
  gameId: number
  name: string
  genres: string[]
  releaseDate: string | null
  metacriticScore: number | null
  publisher: string | null
  developer: string | null
  gameCoverUrl: string | null
  albumCoverUrl: string | null
}
type GuessResult = {
  correct: boolean
  gameOver: boolean
  revealedHint: Hint | null
  answer: Answer | null
  nextToken: string | null
  guessedGameName: string | null
  franchiseMatch: boolean
  fullAudioToken: string | null
}

// 'green' correct · 'yellow' wrong but same franchise · 'red' wrong/skip
type GuessEntry = { label: string; status: 'green' | 'yellow' | 'red' }

type Progress = {
  date: string
  step: number
  finished: boolean
  won: boolean
  hints: Hint[]
  guesses: GuessEntry[]
  token: string
  answer: Answer | null
}

// --- theme ---
type Theme = 'light' | 'dark'
function useTheme() {
  const [theme, setTheme] = useState<Theme>(() => {
    const saved = localStorage.getItem('theme')
    if (saved === 'light' || saved === 'dark') return saved
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  })
  useEffect(() => {
    document.documentElement.dataset.theme = theme
    localStorage.setItem('theme', theme)
  }, [theme])
  return { theme, toggle: () => setTheme((t) => (t === 'dark' ? 'light' : 'dark')) }
}

// --- hash routing (no dependency) ---
type Route = { name: 'home' } | { name: 'archive' } | { name: 'day'; date: string }
function parseHash(): Route {
  const h = window.location.hash.replace(/^#/, '')
  if (h.startsWith('/archive')) return { name: 'archive' }
  const m = h.match(/^\/day\/(\d{4}-\d{2}-\d{2})/)
  if (m) return { name: 'day', date: m[1] }
  return { name: 'home' }
}
function useRoute() {
  const [route, setRoute] = useState<Route>(parseHash())
  useEffect(() => {
    const on = () => setRoute(parseHash())
    window.addEventListener('hashchange', on)
    return () => window.removeEventListener('hashchange', on)
  }, [])
  return route
}
const navigate = (hash: string) => { window.location.hash = hash }

// --- progress persistence ---
function loadProgress(p: PuzzleMeta): Progress {
  const raw = localStorage.getItem(`ostquiz:${p.date}`)
  const base: Progress = { date: p.date, step: 0, finished: false, won: false, hints: [], guesses: [], token: p.token, answer: null }
  if (raw) return { ...base, ...(JSON.parse(raw) as Partial<Progress>) }
  return base
}
function saveProgress(p: Progress) {
  localStorage.setItem(`ostquiz:${p.date}`, JSON.stringify(p))
}
function readGuesses(date: string): GuessEntry[] {
  const raw = localStorage.getItem(`ostquiz:${date}`)
  if (!raw) return []
  try {
    return (JSON.parse(raw) as Partial<Progress>).guesses ?? []
  } catch {
    return []
  }
}

export function App() {
  const { theme, toggle } = useTheme()
  const route = useRoute()
  const [todayDate, setTodayDate] = useState('')

  // Learn the newest (today's) date once, so the home view knows what to play.
  useEffect(() => {
    fetch(`/api/puzzles?skip=0&take=1`)
      .then((r) => r.json())
      .then((p: ArchivePage) => {
        if (p.items.length) setTodayDate(p.items[0].date)
      })
      .catch(() => {})
  }, [])

  return (
    <div className="app">
      <Header theme={theme} onToggle={toggle} route={route} />
      {route.name === 'archive' ? (
        <ArchiveView />
      ) : (
        <PlayView date={route.name === 'day' ? route.date : todayDate} isToday={route.name === 'home'} />
      )}
    </div>
  )
}

function Header({ theme, onToggle, route }: { theme: Theme; onToggle: () => void; route: Route }) {
  return (
    <header className="header">
      <h1 className="title" onClick={() => navigate('/')} role="button">🎵 Game OST Quiz</h1>
      <div className="header-actions">
        {route.name === 'home' ? (
          <button className="icon-btn" onClick={() => navigate('/archive')}>📅 Previous days</button>
        ) : (
          <button className="icon-btn" onClick={() => navigate('/')}>← Today</button>
        )}
        <button className="icon-btn" onClick={onToggle} title="Toggle light/dark" aria-label="Toggle theme">
          {theme === 'dark' ? '☀️' : '🌙'}
        </button>
      </div>
    </header>
  )
}

function PlayView({ date, isToday }: { date: string; isToday: boolean }) {
  const [puzzle, setPuzzle] = useState<PuzzleMeta | null>(null)
  const [progress, setProgress] = useState<Progress | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  const [options, setOptions] = useState<GameOption[]>([])

  useEffect(() => {
    if (!date) return
    setError(null)
    setPuzzle(null)
    setProgress(null)
    setQuery('')
    setOptions([])
    fetch(`/api/puzzles/${date}`)
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error())))
      .then((p: PuzzleMeta) => {
        setPuzzle(p)
        setProgress(loadProgress(p))
      })
      .catch(() => setError('Failed to load that day.'))
  }, [date])

  useEffect(() => {
    if (!query.trim()) return setOptions([])
    const ctrl = new AbortController()
    fetch(`/api/games?search=${encodeURIComponent(query)}&limit=8`, { signal: ctrl.signal })
      .then((r) => r.json())
      .then(setOptions)
      .catch(() => {})
    return () => ctrl.abort()
  }, [query])

  // When finished, play the FULL track (step = last); otherwise the current step's clip.
  const audioUrl = useMemo(() => {
    if (!progress || !date) return ''
    const step = progress.finished ? TOTAL_GUESSES - 1 : progress.step
    return `/api/puzzles/${date}/audio?step=${step}&token=${encodeURIComponent(progress.token)}`
  }, [progress, date])

  async function submit(gameId: number) {
    if (!puzzle || !progress || progress.finished) return
    const res: GuessResult = await fetch(`/api/puzzles/${puzzle.date}/guess`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ token: progress.token, guessGameId: gameId }),
    }).then((r) => r.json())

    const entry: GuessEntry = res.correct
      ? { label: res.answer?.name ?? 'Correct', status: 'green' }
      : { label: res.guessedGameName ?? 'Skipped', status: res.franchiseMatch ? 'yellow' : 'red' }

    const next: Progress = { ...progress, guesses: [...progress.guesses, entry] }
    if (res.correct || res.gameOver) {
      next.finished = true
      next.won = res.correct
      next.answer = res.answer
      if (res.fullAudioToken) next.token = res.fullAudioToken // unlocks the full track
    } else {
      next.step += 1
      if (res.nextToken) next.token = res.nextToken
      if (res.revealedHint) next.hints = [...next.hints, res.revealedHint]
    }
    setProgress(next)
    saveProgress(next)
    setQuery('')
    setOptions([])
  }

  return (
    <>
      <p className="subtitle">
        {isToday ? 'Which game is this soundtrack from?' : `Puzzle for ${date}`}
      </p>
      <div className="card">
        {error && <p className="error">{error}</p>}
        {!error && (!puzzle || !progress) && <p className="center">Loading puzzle…</p>}
        {!error && puzzle && progress && (
          <Game
            puzzle={puzzle}
            progress={progress}
            audioUrl={audioUrl}
            query={query}
            options={options}
            onQuery={setQuery}
            onGuess={submit}
            onSkip={() => submit(-1)}
          />
        )}
      </div>
    </>
  )
}

function Game(props: {
  puzzle: PuzzleMeta
  progress: Progress
  audioUrl: string
  query: string
  options: GameOption[]
  onQuery: (q: string) => void
  onGuess: (id: number) => void
  onSkip: () => void
}) {
  const { puzzle, progress, audioUrl, query, options, onQuery, onGuess, onSkip } = props
  const allowed = progress.finished ? null : puzzle.durationSteps[progress.step]
  const allowedLabel = allowed === null || allowed === undefined ? 'full audio' : `${allowed}s`

  return (
    <>
      <audio key={audioUrl} controls src={audioUrl} />
      <p className="meta-row">
        {progress.finished
          ? 'Full track unlocked'
          : `Guess ${Math.min(progress.step + 1, puzzle.totalGuesses)} / ${puzzle.totalGuesses} · audio: ${allowedLabel}`}
      </p>

      {progress.guesses.length > 0 && (
        <ul className="guesses">
          {progress.guesses.map((g, i) => (
            <li key={i} className={`guess ${g.status}`}>
              <span className="dot" />
              <span className="guess-label">{g.label}</span>
              {g.status === 'yellow' && <span className="tag">same franchise</span>}
            </li>
          ))}
        </ul>
      )}

      {progress.hints.length > 0 && (
        <ul className="hints">
          {progress.hints.map((h) => (
            <li key={h.step} className="hint">
              <span className="kind">{h.kind}</span>: {h.value}
            </li>
          ))}
        </ul>
      )}

      {!progress.finished ? (
        <div className="autocomplete">
          <input className="input" value={query} onChange={(e) => onQuery(e.target.value)} placeholder="Guess the game…" />
          {options.length > 0 && (
            <ul className="options">
              {options.map((o) => (
                <li key={o.id}>
                  <button className="option" onClick={() => onGuess(o.id)}>{o.name}</button>
                </li>
              ))}
            </ul>
          )}
          <div className="actions">
            <button className="btn ghost" onClick={onSkip}>Skip ▸</button>
          </div>
        </div>
      ) : (
        <EndCard progress={progress} />
      )}
    </>
  )
}

function EndCard({ progress }: { progress: Progress }) {
  const a = progress.answer
  return (
    <div>
      <p className={`end-banner ${progress.won ? 'win' : 'lose'}`}>
        {progress.won ? '🎉 You got it!' : '😔 Better luck next time'}
      </p>
      {a && (
        <>
          <p className="answer-name">{a.name}</p>
          <p className="answer-facts">
            {[
              a.genres.join(', '),
              a.releaseDate,
              a.metacriticScore != null ? `Metacritic ${a.metacriticScore}` : null,
              a.developer,
              a.publisher,
            ]
              .filter(Boolean)
              .join(' · ')}
          </p>
          <div className="covers">
            <Cover label="Game cover" url={a.gameCoverUrl} />
            <Cover label="Album cover" url={a.albumCoverUrl} />
          </div>
        </>
      )}
    </div>
  )
}

function Cover({ label, url }: { label: string; url: string | null }) {
  const [broken, setBroken] = useState(false)
  return (
    <div className="cover">
      <span className="cover-label">{label}</span>
      <div className="cover-frame">
        {url && !broken ? (
          <img src={url} alt={label} onError={() => setBroken(true)} />
        ) : (
          <div className="no-image">
            <span className="glyph">🖼️</span>
            <span className="label">No image</span>
          </div>
        )}
      </div>
    </div>
  )
}

function ArchiveView() {
  const [items, setItems] = useState<PuzzleDateInfo[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(false)

  function load(skip: number) {
    setLoading(true)
    fetch(`/api/puzzles?skip=${skip}&take=${PAGE_SIZE}`)
      .then((r) => r.json())
      .then((p: ArchivePage) => {
        setItems((prev) => (skip === 0 ? p.items : [...prev, ...p.items]))
        setTotal(p.total)
      })
      .finally(() => setLoading(false))
  }

  useEffect(() => { load(0) }, [])
  const hasMore = items.length < total

  return (
    <>
      <p className="subtitle">Previous days · {total} total — pick one to play</p>
      <ul className="archive-list">
        {items.map((d) => (
          <li key={d.date}>
            <button className="archive-item" onClick={() => navigate(`/day/${d.date}`)}>
              <span className="archive-date">{d.isToday ? `Today · ${d.date}` : d.date}</span>
              <ResultBoxes date={d.date} />
            </button>
          </li>
        ))}
      </ul>
      {hasMore && (
        <button className="btn" onClick={() => load(items.length)} disabled={loading}>
          {loading ? 'Loading…' : 'Load more days'}
        </button>
      )}
    </>
  )
}

function ResultBoxes({ date }: { date: string }) {
  const guesses = readGuesses(date)
  return (
    <span className="boxes" aria-label="results">
      {Array.from({ length: TOTAL_GUESSES }).map((_, i) => {
        const g = guesses[i]
        return <span key={i} className={`box ${g ? g.status : 'empty'}`} />
      })}
    </span>
  )
}
