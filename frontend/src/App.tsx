import { useEffect, useMemo, useState } from 'react'

// --- API types (mirror the backend contracts) ---
type PuzzleDateInfo = { date: string; isToday: boolean }
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

function loadProgress(p: PuzzleMeta): Progress {
  const raw = localStorage.getItem(`ostquiz:${p.date}`)
  if (raw) {
    const saved = JSON.parse(raw) as Partial<Progress>
    return { date: p.date, step: 0, finished: false, won: false, hints: [], guesses: [], token: p.token, answer: null, ...saved }
  }
  return { date: p.date, step: 0, finished: false, won: false, hints: [], guesses: [], token: p.token, answer: null }
}
function saveProgress(p: Progress) {
  localStorage.setItem(`ostquiz:${p.date}`, JSON.stringify(p))
}

export function App() {
  const { theme, toggle } = useTheme()
  const [dates, setDates] = useState<PuzzleDateInfo[]>([])
  const [selectedDate, setSelectedDate] = useState('')
  const [puzzle, setPuzzle] = useState<PuzzleMeta | null>(null)
  const [progress, setProgress] = useState<Progress | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  const [options, setOptions] = useState<GameOption[]>([])

  // Load the archive once; default to the newest (today).
  useEffect(() => {
    fetch('/api/puzzles')
      .then((r) => r.json())
      .then((d: PuzzleDateInfo[]) => {
        setDates(d)
        if (d.length) setSelectedDate(d[0].date)
        else setError('No puzzles have been published yet.')
      })
      .catch(() => setError('Failed to load puzzles.'))
  }, [])

  // Load the selected day's puzzle + progress.
  useEffect(() => {
    if (!selectedDate) return
    setError(null)
    setPuzzle(null)
    setProgress(null)
    setQuery('')
    setOptions([])
    fetch(`/api/puzzles/${selectedDate}`)
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error())))
      .then((p: PuzzleMeta) => {
        setPuzzle(p)
        setProgress(loadProgress(p))
      })
      .catch(() => setError('Failed to load that day.'))
  }, [selectedDate])

  // Autocomplete.
  useEffect(() => {
    if (!query.trim()) return setOptions([])
    const ctrl = new AbortController()
    fetch(`/api/games?search=${encodeURIComponent(query)}&limit=8`, { signal: ctrl.signal })
      .then((r) => r.json())
      .then(setOptions)
      .catch(() => {})
    return () => ctrl.abort()
  }, [query])

  const audioUrl = useMemo(() => {
    if (!progress || !selectedDate) return ''
    return `/api/puzzles/${selectedDate}/audio?step=${progress.step}&token=${encodeURIComponent(progress.token)}`
  }, [progress, selectedDate])

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
    <div className="app">
      <header className="header">
        <h1 className="title">🎵 Game OST Quiz</h1>
        <div className="header-actions">
          {dates.length > 0 && (
            <select className="select" value={selectedDate} onChange={(e) => setSelectedDate(e.target.value)}>
              {dates.map((d) => (
                <option key={d.date} value={d.date}>
                  {d.isToday ? `Today · ${d.date}` : d.date}
                </option>
              ))}
            </select>
          )}
          <button className="icon-btn" onClick={toggle} title="Toggle light/dark" aria-label="Toggle theme">
            {theme === 'dark' ? '☀️' : '🌙'}
          </button>
        </div>
      </header>
      <p className="subtitle">Which game is this soundtrack from?</p>

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
    </div>
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
  const allowed = puzzle.durationSteps[progress.step]
  const allowedLabel = allowed === null || allowed === undefined ? 'full audio' : `${allowed}s`

  return (
    <>
      <audio key={audioUrl} controls src={audioUrl} />
      <p className="meta-row">
        Guess {Math.min(progress.step + 1, puzzle.totalGuesses)} / {puzzle.totalGuesses} · audio: {allowedLabel}
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
          <input
            className="input"
            value={query}
            onChange={(e) => onQuery(e.target.value)}
            placeholder="Guess the game…"
          />
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
