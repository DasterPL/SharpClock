import { useState, useEffect, useRef, useCallback } from 'react'
import { Box, Button, Card, Flex, HStack, Heading } from '@chakra-ui/react'
import { get } from './api.js'

const COLS = 32
const ROWS = 8
const CELL = 20
const GAP = 2

function drawPixels(canvas, src) {
  const img = new Image()
  img.onload = () => {
    const off = document.createElement('canvas')
    off.width = COLS
    off.height = ROWS
    off.getContext('2d').drawImage(img, 0, 0)
    const data = off.getContext('2d').getImageData(0, 0, COLS, ROWS).data

    canvas.width = COLS * CELL
    canvas.height = ROWS * CELL
    const ctx = canvas.getContext('2d')
    ctx.fillStyle = '#111'
    ctx.fillRect(0, 0, canvas.width, canvas.height)

    for (let x = 0; x < COLS; x++) {
      for (let y = 0; y < ROWS; y++) {
        const i = (y * COLS + x) * 4
        ctx.fillStyle = `rgb(${data[i]},${data[i + 1]},${data[i + 2]})`
        ctx.fillRect(x * CELL + GAP, y * CELL + GAP, CELL - GAP * 2, CELL - GAP * 2)
      }
    }
  }
  img.src = src
}

export default function ScreenPreview() {
  const [src, setSrc] = useState(null)
  const [live, setLive] = useState(true)
  const intervalRef = useRef(null)
  const canvasRef = useRef(null)

  const refresh = useCallback(async () => {
    try {
      const res = await get('/screen')
      if (res.Response?.Screen)
        setSrc('data:image/png;base64,' + res.Response.Screen)
    } catch { /* ignore */ }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  useEffect(() => {
    if (src && canvasRef.current)
      drawPixels(canvasRef.current, src)
  }, [src])

  useEffect(() => {
    if (live) {
      intervalRef.current = setInterval(refresh, 500)
    } else {
      clearInterval(intervalRef.current)
    }
    return () => clearInterval(intervalRef.current)
  }, [live, refresh])

  return (
    <Card.Root>
      <Card.Header pb={2}>
        <Flex align="center" justify="space-between" gap={4}>
          <Heading size="md">Screen Preview</Heading>
          <HStack gap={2}>
            <Button
              size="sm"
              colorPalette={live ? 'teal' : 'gray'}
              onClick={() => setLive(l => !l)}
            >
              <i className="material-icons">{live ? 'pause' : 'play_arrow'}</i>
            </Button>
            <Button size="sm" colorPalette="yellow" onClick={refresh}>
              <i className="material-icons">refresh</i>
            </Button>
          </HStack>
        </Flex>
      </Card.Header>
      <Card.Body pt={0}>
        <Box
          borderRadius="md" overflow="hidden"
          bg="#111"
          display="flex" justifyContent="center" alignItems="center"
          p={3}
        >
          {!src && (
            <Box color="gray.600" fontFamily="mono" fontSize="sm" p={4}>no signal</Box>
          )}
          <canvas
            ref={canvasRef}
            style={{
              display: src ? 'block' : 'none',
              width: '100%',
              maxWidth: `${COLS * CELL}px`,
              height: 'auto',
              imageRendering: 'pixelated',
              borderRadius: '4px',
            }}
          />
        </Box>
      </Card.Body>
    </Card.Root>
  )
}
