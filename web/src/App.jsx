import { useState, useEffect } from 'react'
import { Stack, Card, Heading } from '@chakra-ui/react'
import { useColorMode } from './colorMode.jsx'
import { get } from './api.js'
import ModuleList from './ModuleList.jsx'
import GlobalSettings from './GlobalSettings.jsx'
import OptionsCard from './Options.jsx'
import LogViewer from './LogViewer.jsx'
import ScreenPreview from './ScreenPreview.jsx'

function Loader({ visible }) {
  return (
    <div className={`loader-bar${visible ? ' loader-bar--active' : ''}`}>
      <div className="loader-bar__fill" />
    </div>
  )
}

function Navbar() {
  const { colorMode, toggleColorMode } = useColorMode()
  return (
    <nav className="navbar">
      <div className="navbar-inner">
        <div className="navbar-brand">
          <img src="/logo.png" alt="" />
          SharpClock Manager
          <span className="navbar-version">v1.0.0</span>
        </div>
        <button className="navbar-btn" onClick={toggleColorMode} title="Toggle theme">
          <i className="material-icons">
            {colorMode === 'light' ? 'dark_mode' : 'light_mode'}
          </i>
        </button>
      </div>
    </nav>
  )
}

export default function App() {
  const [loading, setLoading] = useState(true)
  const [modules, setModules] = useState([])
  const [properties, setProperties] = useState(null)
  const [dlls, setDlls] = useState([])
  const [globalSettings, setGlobalSettings] = useState([])

  useEffect(() => {
    async function load() {
      try {
        const [modsRes, propsRes, dllsRes, gsRes] = await Promise.all([
          get('/modules'),
          get('/properties'),
          get('/plugins'),
          get('/globalSettings'),
        ])
        setModules(modsRes.Response ?? [])
        setProperties(propsRes.Response ?? {})
        setDlls(dllsRes.Response ?? [])
        setGlobalSettings(gsRes.Response ?? [])
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [])

  return (
    <div className="page">
      <Loader visible={loading} />
      <Navbar />
      <Stack gap={4} p={4} maxW="860px" mx="auto">
        <ScreenPreview />

        <Card.Root>
          <Card.Header>
            <Heading size="md">Module Settings</Heading>
          </Card.Header>
          <Card.Body>
            <ModuleList modules={modules} setModules={setModules} setLoading={setLoading}
              onPauseChange={p => setProperties(prev => ({ ...prev, Pause: p }))} />
          </Card.Body>
        </Card.Root>

        {globalSettings.length > 0 && (
          <Card.Root>
            <Card.Header>
              <Heading size="md">Global Settings</Heading>
            </Card.Header>
            <Card.Body>
              <GlobalSettings globalSettings={globalSettings} setLoading={setLoading} />
            </Card.Body>
          </Card.Root>
        )}

        <OptionsCard properties={properties} dlls={dlls} setDlls={setDlls} setLoading={setLoading} />

        <LogViewer setLoading={setLoading} />
      </Stack>
    </div>
  )
}
