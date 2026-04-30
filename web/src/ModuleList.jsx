import { useState, useRef } from 'react'
import {
  Accordion, Box, Flex, HStack, Button, Stack, Text,
} from '@chakra-ui/react'
import { Switch } from '@chakra-ui/react'
import { get, post, patch, putOrder, postButton } from './api.js'
import { lang } from './i18n.js'
import ModuleField from './ModuleField.jsx'

function DragHandle({ onDragStart, onTouchStart }) {
  return (
    <Box
      as="span"
      draggable
      onDragStart={onDragStart}
      onTouchStart={onTouchStart}
      onClick={e => e.stopPropagation()}
      cursor="grab"
      display="flex"
      alignItems="center"
      px={1}
      color="gray.400"
      flexShrink={0}
      style={{ touchAction: 'none' }}
    >
      <i className="material-icons" style={{ fontSize: '20px', userSelect: 'none' }}>drag_indicator</i>
    </Box>
  )
}

function TimerControls({ module, setLoading, onRefresh, onPauseChange }) {
  const extra = module.Extra
  if (!extra) return null
  const running = extra.running
  const buzzed  = extra.buzzed

  async function pressButton(id) {
    const wasRunning = extra.running
    setLoading(true)
    try {
      await postButton(module.Name, id)
      await onRefresh(module.Name)
      if (id === 'User1' && !wasRunning)
        onPauseChange?.(true)
    } finally { setLoading(false) }
  }

  return (
    <HStack gap={2} justify="center" pt={1}>
      <Button
        size="sm"
        colorPalette={running ? 'yellow' : buzzed ? 'red' : 'green'}
        onClick={() => pressButton('User1')}
        flex={1}
      >
        <i className="material-icons">{running ? 'pause' : 'play_arrow'}</i>
        {running ? 'Pauza' : buzzed ? 'Nowy' : 'Start'}
      </Button>
      <Button size="sm" colorPalette="gray" variant="outline" onClick={() => pressButton('User2')}>
        <i className="material-icons">replay</i>
      </Button>
    </HStack>
  )
}

function ModuleItem({ module, index, onStatusChange, onRefreshValues, setLoading, onDragStart, onTouchStart, onDragOver, onDrop, onPauseChange }) {
  const running = module.Status === 'started'

  async function savePower(checked) {
    setLoading(true)
    try {
      const res = await patch(`/modules/${module.Name}`, { Power: String(checked) })
      if (res.Response) onStatusChange(module.Name, res.Response.Status)
    } finally { setLoading(false) }
  }

  async function saveField(fieldName, value) {
    setLoading(true)
    try {
      const res = await patch(`/modules/${module.Name}`, { [fieldName]: value })
      if (res.Response) onStatusChange(module.Name, res.Response.Status)
      await onRefreshValues(module.Name)
    } finally { setLoading(false) }
  }

  async function reload() {
    setLoading(true)
    try { await post('/modules/reload', { name: module.Name }) }
    finally { setLoading(false) }
  }

  async function switchTo() {
    setLoading(true)
    try {
      const res = await post('/modules/switch', { name: module.Name, pause: 'true' })
      if (res.Response?.Error) alert('Module is disabled!')
      else onPauseChange?.(res.Response?.Pause ?? true)
    } finally { setLoading(false) }
  }

  return (
    <Accordion.Item
      value={module.Name}
      data-idx={index}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      <Accordion.ItemTrigger _hover={{ bg: 'gray.100' }} _dark={{ _hover: { bg: 'whiteAlpha.100' } }} transition="background 0.12s">
        <DragHandle
          onDragStart={onDragStart}
          onTouchStart={onTouchStart}
        />
        <Flex align="center" gap={2} flex={1}>
          <i className="material-icons" style={{ fontSize: '20px' }}>{module.Icon}</i>
          <Text fontWeight="medium">{module.Name}</Text>
        </Flex>
        <Box
          w="10px" h="10px" borderRadius="full"
          bg={running ? 'green.400' : 'red.400'}
          mr={2} flexShrink={0}
        />
        <Accordion.ItemIndicator />
      </Accordion.ItemTrigger>
      <Accordion.ItemContent>
        <Stack p={3} gap={3}>
          <Box
            px={2} py={1} mx={-2} borderRadius="md"
            _hover={{ bg: 'gray.100' }} _dark={{ _hover: { bg: 'whiteAlpha.100' } }}
            transition="background 0.12s"
          >
            <Switch.Root
              defaultChecked={running}
              onCheckedChange={e => savePower(e.checked)}
              width="full" display="flex" alignItems="center" justifyContent="space-between"
            >
              <Switch.HiddenInput />
              <Switch.Label>{running ? lang.on : lang.off}</Switch.Label>
              <Switch.Control><Switch.Thumb /></Switch.Control>
            </Switch.Root>
          </Box>

          {module.Values.map(field => (
            <Box
              key={field.Name}
              px={2} py={1} mx={-2} borderRadius="md"
              _hover={{ bg: 'gray.100' }} _dark={{ _hover: { bg: 'whiteAlpha.100' } }}
              transition="background 0.12s"
            >
              <ModuleField
                moduleName={module.Name}
                field={field}
                onChange={saveField}
              />
            </Box>
          ))}

          <TimerControls module={module} setLoading={setLoading} onRefresh={onRefreshValues} onPauseChange={onPauseChange} />

          <HStack justify="flex-end" gap={2}>
            <Button size="sm" colorPalette="yellow" onClick={reload}>
              <i className="material-icons">refresh</i>
            </Button>
            <Button size="sm" colorPalette="green" onClick={switchTo}>
              <i className="material-icons">visibility</i>
            </Button>
          </HStack>
        </Stack>
      </Accordion.ItemContent>
    </Accordion.Item>
  )
}

export default function ModuleList({ modules, setModules, setLoading, onPauseChange }) {
  const [openItems, setOpenItems] = useState([])
  const dragFrom = useRef(null)

  function updateStatus(name, isRunning) {
    setModules(prev =>
      prev.map(m => m.Name === name ? { ...m, Status: isRunning ? 'started' : 'stopped' } : m)
    )
  }

  async function refreshModuleValues(name) {
    try {
      const res = await get('/modules')
      const updated = (res.Response ?? []).find(m => m.Name === name)
      if (updated)
        setModules(prev => prev.map(m => m.Name === name ? { ...m, Values: updated.Values, Extra: updated.Extra } : m))
    } catch {}
  }

  async function drop(fromIndex, toIndex) {
    if (fromIndex === null || fromIndex === toIndex) return
    const next = [...modules]
    const [moved] = next.splice(fromIndex, 1)
    next.splice(toIndex, 0, moved)
    setModules(next)
    setLoading(true)
    try { await putOrder(next.map(m => m.Name)) }
    finally { setLoading(false) }
  }

  function getIndexFromEl(el) {
    const item = el?.closest('[data-idx]')
    return item ? parseInt(item.dataset.idx) : null
  }

  function handleDragStart(i) {
    dragFrom.current = i
  }

  function handleDragOver(e) {
    e.preventDefault()
  }

  function handleDrop(toIndex) {
    drop(dragFrom.current, toIndex)
    dragFrom.current = null
  }

  function handleTouchStart(i, e) {
    dragFrom.current = i

    const onMove = (ev) => {
      ev.preventDefault()
    }

    const onEnd = (ev) => {
      const t = ev.changedTouches[0]
      const el = document.elementFromPoint(t.clientX, t.clientY)
      const toIndex = getIndexFromEl(el)
      if (toIndex !== null) drop(dragFrom.current, toIndex)
      dragFrom.current = null
      document.removeEventListener('touchmove', onMove)
      document.removeEventListener('touchend', onEnd)
    }

    document.addEventListener('touchmove', onMove, { passive: false })
    document.addEventListener('touchend', onEnd, { once: true })
  }

  return (
    <Accordion.Root
      multiple
      value={openItems}
      onValueChange={({ value }) => setOpenItems(value)}
    >
      {modules.map((m, i) => (
        <ModuleItem
          key={m.Name}
          module={m}
          index={i}
          onStatusChange={updateStatus}
          onRefreshValues={refreshModuleValues}
          setLoading={setLoading}
          onDragStart={() => handleDragStart(i)}
          onTouchStart={(e) => handleTouchStart(i, e)}
          onDragOver={handleDragOver}
          onDrop={() => handleDrop(i)}
          onPauseChange={onPauseChange}
        />
      ))}
    </Accordion.Root>
  )
}
