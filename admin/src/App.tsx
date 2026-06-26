import { useState, type CSSProperties } from 'react'

type GameOption = { id: number; name: string }

export function App() {
  const [adminKey, setAdminKey] = useState(localStorage.getItem('adminKey') ?? '')
  const [log, setLog] = useState<string[]>([])
  const append = (m: string) => setLog((l) => [m, ...l].slice(0, 20))

  function rememberKey(k: string) {
    setAdminKey(k)
    localStorage.setItem('adminKey', k)
  }

  async function importGames() {
    const res = await fetch('/api/admin/games/import', {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'X-Admin-Key': adminKey },
      body: JSON.stringify({ count: 200 }),
    })
    append(res.ok ? `Imported: ${(await res.json()).imported} games` : `Import failed (${res.status})`)
  }

  // --- create puzzle ---
  const [date, setDate] = useState('')
  const [query, setQuery] = useState('')
  const [options, setOptions] = useState<GameOption[]>([])
  const [game, setGame] = useState<GameOption | null>(null)
  const [audio, setAudio] = useState<File | null>(null)
  const [gameCover, setGameCover] = useState<File | null>(null)
  const [albumCover, setAlbumCover] = useState<File | null>(null)

  async function search(q: string) {
    setQuery(q)
    setGame(null)
    if (!q.trim()) return setOptions([])
    setOptions(await fetch(`/api/games?search=${encodeURIComponent(q)}&limit=8`).then((r) => r.json()))
  }

  async function createPuzzle() {
    if (!game || !audio || !date) return append('Pick a date, a game, and an audio file first.')
    const fd = new FormData()
    fd.append('date', date)
    fd.append('gameId', String(game.id))
    fd.append('audio', audio)
    if (gameCover) fd.append('gameCover', gameCover)
    if (albumCover) fd.append('albumCover', albumCover)
    const res = await fetch('/api/admin/puzzles', {
      method: 'POST',
      headers: { 'X-Admin-Key': adminKey },
      body: fd,
    })
    append(res.ok ? `Created puzzle for ${date}: ${game.name}` : `Create failed (${res.status}): ${await res.text()}`)
  }

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 640, margin: '40px auto', padding: 16 }}>
      <h1>OST Quiz · Admin</h1>

      <section style={card}>
        <label>Admin key</label>
        <input value={adminKey} onChange={(e) => rememberKey(e.target.value)} style={input} placeholder="X-Admin-Key" />
      </section>

      <section style={card}>
        <h2>1. Import game catalog (RAWG)</h2>
        <button onClick={importGames}>Import top 200 games</button>
      </section>

      <section style={card}>
        <h2>2. Create a puzzle</h2>
        <label>Date</label>
        <input type="date" value={date} onChange={(e) => setDate(e.target.value)} style={input} />

        <label>Answer game</label>
        <input value={query} onChange={(e) => search(e.target.value)} style={input} placeholder="Search games…" />
        {game ? (
          <p>Selected: <strong>{game.name}</strong></p>
        ) : (
          options.map((o) => (
            <button key={o.id} onClick={() => { setGame(o); setOptions([]); setQuery(o.name) }} style={{ display: 'block', width: '100%', textAlign: 'left', padding: 6 }}>
              {o.name}
            </button>
          ))
        )}

        <label>Audio file (mandatory)</label>
        <input type="file" accept="audio/*" onChange={(e) => setAudio(e.target.files?.[0] ?? null)} style={input} />

        <label>Game cover (optional)</label>
        <input type="file" accept="image/*" onChange={(e) => setGameCover(e.target.files?.[0] ?? null)} style={input} />

        <label>Album cover (optional)</label>
        <input type="file" accept="image/*" onChange={(e) => setAlbumCover(e.target.files?.[0] ?? null)} style={input} />

        <button onClick={createPuzzle}>Create puzzle</button>
      </section>

      <section style={card}>
        <h2>Log</h2>
        <ul>{log.map((l, i) => <li key={i}>{l}</li>)}</ul>
      </section>
    </div>
  )
}

const card: CSSProperties = { border: '1px solid #ddd', borderRadius: 8, padding: 16, marginBottom: 16 }
const input: CSSProperties = { display: 'block', width: '100%', padding: 8, margin: '6px 0 12px', boxSizing: 'border-box' }
