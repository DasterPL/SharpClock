import { useState, useEffect, useRef, useCallback } from 'react'
import {
  Box, Button, Card, Flex, HStack, Heading, Text,
} from '@chakra-ui/react'
import { get, del } from './api.js'

const LOG_COLORS = {
  '[Error]': 'red.400',
  '[Draw Error]': 'red.400',
  '[Unhandled]': 'red.400',
  '[Skip]': 'yellow.400',
  '[HTTP Server]': 'purple.400',
  'System Ready': 'green.400',
}

function colorForLine(line) {
  for (const [key, color] of Object.entries(LOG_COLORS))
    if (line.includes(key)) return color
  return null
}

export default function LogViewer({ setLoading }) {
  const [lines, setLines] = useState(null)
  const [autoRefresh, setAutoRefresh] = useState(false)
  const bottomRef = useRef(null)

  const refresh = useCallback(async (showLoader = true) => {
    if (showLoader) setLoading(true)
    try {
      const res = await get('/log')
      setLines(res.Response ?? [])
    } finally {
      if (showLoader) setLoading(false)
    }
  }, [setLoading])

  useEffect(() => { refresh() }, [refresh])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [lines])

  useEffect(() => {
    if (!autoRefresh) return
    const id = setInterval(() => refresh(false), 3000)
    return () => clearInterval(id)
  }, [autoRefresh, refresh])

  async function clear() {
    setLoading(true)
    try {
      await del('/log')
      setLines([])
    } finally {
      setLoading(false)
    }
  }

  return (
    <Card.Root>
      <Card.Header pb={2}>
        <Flex align="center" justify="space-between" gap={4}>
          <Heading size="md">Log</Heading>
          <HStack gap={2}>
            <Button
              size="sm"
              colorPalette={autoRefresh ? 'teal' : 'gray'}
              onClick={() => setAutoRefresh(a => !a)}
            >
              <i className="material-icons">{autoRefresh ? 'pause' : 'play_arrow'}</i>
            </Button>
            <Button size="sm" colorPalette="yellow" onClick={() => refresh()}>
              <i className="material-icons">refresh</i>
            </Button>
            <Button size="sm" colorPalette="red" variant="outline" onClick={clear}>
              <i className="material-icons">delete_sweep</i>
            </Button>
          </HStack>
        </Flex>
      </Card.Header>
      <Card.Body pt={0}>
        <Box
          fontFamily="mono" fontSize="xs"
          bg="gray.900" _light={{ bg: 'gray.50', borderWidth: '1px', borderColor: 'gray.200' }}
          borderRadius="md" p={3}
          h="320px" overflowY="auto"
        >
          {lines === null ? (
            <Text color="gray.400">Ładowanie...</Text>
          ) : lines.length === 0 ? (
            <Text color="gray.500">Brak wpisów w logu.</Text>
          ) : (
            lines.map((line, i) => {
              const color = colorForLine(line)
              return (
                <Box key={i} color={color ?? 'gray.300'} _light={{ color: color ?? 'gray.700' }} lineHeight="short">
                  {line}
                </Box>
              )
            })
          )}
          <div ref={bottomRef} />
        </Box>
      </Card.Body>
    </Card.Root>
  )
}
