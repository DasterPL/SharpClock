import { useState, useEffect, useCallback } from 'react'
import { Box, Button, Flex, HStack, Input, Stack, Text, Badge } from '@chakra-ui/react'
import { get, post } from './api.js'

function SignalBars({ signal }) {
  const strength = signal >= -55 ? 3 : signal >= -70 ? 2 : 1
  return (
    <HStack gap="2px" alignItems="flex-end" h="12px" flexShrink={0}>
      {[1, 2, 3].map(b => (
        <Box key={b} w="4px" h={`${b * 4}px`} borderRadius="1px"
          bg={b <= strength ? 'blue.400' : 'gray.500'}
          opacity={b <= strength ? 1 : 0.4}
        />
      ))}
    </HStack>
  )
}

export default function WifiPanel({ setLoading }) {
  const [status, setStatus]     = useState(null)
  const [networks, setNetworks] = useState(null)
  const [scanning, setScanning] = useState(false)
  const [selected, setSelected] = useState(null)
  const [password, setPassword] = useState('')
  const [manualSsid, setManualSsid] = useState('')
  const [connecting, setConnecting] = useState(false)
  const [message, setMessage]   = useState(null)

  const fetchStatus = useCallback(async () => {
    try {
      const res = await get('/wifi')
      setStatus(res.Response)
    } catch {}
  }, [])

  useEffect(() => { fetchStatus() }, [fetchStatus])

  async function scan() {
    setScanning(true)
    setNetworks(null)
    setMessage(null)
    setSelected(null)
    await fetchStatus()
    try {
      const res = await get('/wifi/scan')
      const nets = res.Response
      if (!Array.isArray(nets) || nets.length === 0)
        setMessage('Nie znaleziono sieci.')
      else
        setNetworks(nets)
    } finally {
      setScanning(false)
    }
  }

  async function connect() {
    const ssid = selected?.ssid || manualSsid.trim()
    if (!ssid) { setMessage('Wybierz sieć lub wpisz SSID'); return }
    const isOpen = selected ? selected.open : true
    if (!isOpen && !password) { setMessage('Podaj hasło'); return }
    setConnecting(true)
    setMessage(null)
    try {
      await post('/wifi/connect', { ssid, password: isOpen ? '' : password })
      setMessage(`Łączenie z "${ssid}"… Panel będzie dostępny pod nowym adresem IP.`)
      setSelected(null)
      setPassword('')
      setManualSsid('')
      setTimeout(fetchStatus, 6000)
    } finally {
      setConnecting(false)
    }
  }

  const hotspot = status?.mode === 'hotspot'
  const activeSsid = selected?.ssid || manualSsid.trim()
  const needsPassword = selected && !selected.open ? false : selected ? false : false
  const showPassword = selected && !selected.open

  return (
    <Stack gap={3}>

      {/* Status */}
      <Flex
        align="center" gap={2} px={3} py={2} borderRadius="md"
        bg={hotspot ? 'orange.100' : 'green.100'}
        _dark={{ bg: hotspot ? 'orange.900' : 'green.900' }}
      >
        <i className="material-icons" style={{ fontSize: '18px' }}>
          {hotspot ? 'wifi_tethering' : 'wifi'}
        </i>
        <Box flex={1}>
          {status === null ? (
            <Text fontSize="sm">Ładowanie…</Text>
          ) : hotspot ? (
            <Text fontSize="sm">Hotspot <b>SharpClock</b> aktywny — brak WiFi</Text>
          ) : (
            <Text fontSize="sm">
              <b>{status.ssid || '(brak)'}</b>
              <Text as="span" opacity={0.6}> — {status.ip || 'brak IP'}</Text>
            </Text>
          )}
        </Box>
        <Badge colorPalette={hotspot ? 'orange' : 'green'} size="sm">
          {hotspot ? 'hotspot' : 'WiFi'}
        </Badge>
      </Flex>

      {/* Scan */}
      <Button colorPalette="blue" onClick={scan} loading={scanning}>
        <i className="material-icons">wifi_find</i>
        Szukaj sieci
      </Button>

      {/* Network list */}
      {networks !== null && (
        <Stack gap={1} maxH="200px" overflowY="auto" borderRadius="md"
          borderWidth="1px" borderColor="gray.200" _dark={{ borderColor: 'whiteAlpha.200' }}
          p={1}
        >
          {networks.map(n => {
            const isSelected = selected?.ssid === n.ssid
            return (
              <Flex
                key={n.ssid}
                align="center" justify="space-between"
                px={3} py={2} borderRadius="md" cursor="pointer"
                bg={isSelected ? 'blue.500' : 'transparent'}
                color={isSelected ? 'white' : 'inherit'}
                _hover={{ bg: isSelected ? 'blue.500' : 'gray.100', _dark: { bg: isSelected ? 'blue.600' : 'whiteAlpha.100' } }}
                onClick={() => { setSelected(n); setPassword(''); setManualSsid('') }}
                transition="background 0.1s"
              >
                <HStack gap={2}>
                  <i className="material-icons" style={{ fontSize: '14px', opacity: 0.7 }}>
                    {n.open ? 'lock_open' : 'lock'}
                  </i>
                  <Text fontSize="sm" fontWeight={isSelected ? 'semibold' : 'normal'}>{n.ssid}</Text>
                </HStack>
                <SignalBars signal={n.signal} />
              </Flex>
            )
          })}
        </Stack>
      )}

      {/* Manual SSID */}
      {!selected && (
        <Input
          size="sm"
          placeholder="Lub wpisz SSID ręcznie"
          value={manualSsid}
          onChange={e => setManualSsid(e.target.value)}
        />
      )}

      {/* Selected network info + deselect */}
      {selected && (
        <Flex align="center" justify="space-between">
          <Text fontSize="sm">Wybrano: <b>{selected.ssid}</b></Text>
          <Button size="xs" variant="ghost" colorPalette="gray"
            onClick={() => { setSelected(null); setPassword('') }}
          >
            <i className="material-icons" style={{ fontSize: '14px' }}>close</i>
            Zmień
          </Button>
        </Flex>
      )}

      {/* Password */}
      {showPassword && (
        <Input
          size="sm" type="password"
          placeholder="Hasło WiFi"
          value={password}
          onChange={e => setPassword(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && connect()}
          autoFocus
        />
      )}

      <Button colorPalette="teal" onClick={connect} loading={connecting}
        disabled={connecting || (!activeSsid)}
      >
        <i className="material-icons">wifi</i>
        Połącz
      </Button>

      {message && (
        <Text fontSize="xs" color="gray.500">{message}</Text>
      )}

    </Stack>
  )
}
