import { useState, useEffect } from 'react'

const STORAGE_KEY = 'theme'

function ThemeToggle() {
  const [isDark, setIsDark] = useState(() => localStorage.getItem(STORAGE_KEY) === 'dark')

  useEffect(() => {
    document.documentElement.classList.toggle('dark-mode', isDark)
    document.body.classList.toggle('dark-mode', isDark)
    localStorage.setItem(STORAGE_KEY, isDark ? 'dark' : 'light')
  }, [isDark])

  return (
    <button type="button" onClick={() => setIsDark(prev => !prev)}>
      {isDark ? 'Tryb jasny' : 'Tryb ciemny'}
    </button>
  )
}

export default ThemeToggle
