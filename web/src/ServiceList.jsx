import { useState, useEffect, useCallback } from 'react'
import { HStack, Text, Box, Flex } from '@chakra-ui/react'
import { Switch } from '@chakra-ui/react'
import { get, patch } from './api.js'

function timeAgo(iso) {
  if (!iso) return 'never'
  const diff = Math.floor((Date.now() - new Date(iso)) / 1000)
  if (diff < 60)   return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  return `${Math.floor(diff / 3600)}h ago`
}

function ServiceRow({ svc, onToggle }) {
  return (
    <Flex align="center" gap={3} py={1}>
      <Box w={2} h={2} borderRadius="full" flexShrink={0}
        bg={!svc.IsRunning ? 'gray.400' : svc.LastError ? 'red.400' : 'green.400'} />
      <Text fontWeight="medium" flex={1}>{svc.Name}</Text>
      <Text fontSize="xs" color="gray.500" flexShrink={0}>
        {timeAgo(svc.LastRun)}
      </Text>
      {svc.LastError && (
        <Text fontSize="xs" color="red.400" maxW="200px" lineClamp={1} title={svc.LastError}>
          {svc.LastError}
        </Text>
      )}
      <Switch.Root
        checked={svc.IsRunning}
        onCheckedChange={e => onToggle(svc.Name, e.checked)}
        colorPalette="green"
        flexShrink={0}
      >
        <Switch.HiddenInput />
        <Switch.Control><Switch.Thumb /></Switch.Control>
      </Switch.Root>
    </Flex>
  )
}

export default function ServiceList() {
  const [services, setServices] = useState([])

  const refresh = useCallback(async () => {
    try {
      const res = await get('/services')
      setServices(res.Response ?? [])
    } catch {}
  }, [])

  useEffect(() => {
    refresh()
    const id = setInterval(refresh, 10_000)
    return () => clearInterval(id)
  }, [refresh])

  async function toggle(name, power) {
    try {
      const res = await patch(`/services/${name}`, { Power: String(power) })
      if (res.Response)
        setServices(prev => prev.map(s => s.Name === name ? res.Response : s))
    } catch {}
  }

  if (services.length === 0) return null

  return (
    <Box>
      {services.map(s => <ServiceRow key={s.Name} svc={s} onToggle={toggle} />)}
    </Box>
  )
}
