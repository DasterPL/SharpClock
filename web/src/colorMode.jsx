import { createContext, useContext, useState, useEffect } from 'react'

const STORAGE_KEY = 'sharpclock-theme'
const ColorModeContext = createContext(null)

function getSystemTheme() {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function ColorModeProvider({ children }) {
  const [colorMode, setColorMode] = useState(
    () => localStorage.getItem(STORAGE_KEY) ?? getSystemTheme()
  )

  useEffect(() => {
    document.documentElement.style.colorScheme = colorMode
    document.documentElement.dataset.theme = colorMode
  }, [colorMode])

  useEffect(() => {
    if (localStorage.getItem(STORAGE_KEY)) return
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const handler = e => setColorMode(e.matches ? 'dark' : 'light')
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])

  function toggleColorMode() {
    setColorMode(prev => {
      const next = prev === 'light' ? 'dark' : 'light'
      localStorage.setItem(STORAGE_KEY, next)
      return next
    })
  }

  return (
    <ColorModeContext.Provider value={{ colorMode, toggleColorMode }}>
      {children}
    </ColorModeContext.Provider>
  )
}

export function useColorMode() {
  return useContext(ColorModeContext)
}
