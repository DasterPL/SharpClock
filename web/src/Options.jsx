import { useState, useRef } from 'react'
import {
  Accordion, Box, Button, Card, Flex, HStack,
  Heading, List, Slider, Stack, Text,
} from '@chakra-ui/react'
import { Switch } from '@chakra-ui/react'
import { post, patch, del, mapValue } from './api.js'
import WifiPanel from './WifiPanel.jsx'

function Section({ icon, title, children, disabled = false }) {
  return (
    <Accordion.Item value={title} disabled={disabled}>
      <Accordion.ItemTrigger _hover={{ bg: 'gray.100' }} _dark={{ _hover: { bg: 'whiteAlpha.100' } }} transition="background 0.12s">
        <Flex align="center" gap={2} flex={1}>
          <i className="material-icons" style={{ fontSize: '20px' }}>{icon}</i>
          <Text fontWeight="medium">{title}</Text>
        </Flex>
        <Accordion.ItemIndicator />
      </Accordion.ItemTrigger>
      <Accordion.ItemContent>
        <Box p={3}>{children}</Box>
      </Accordion.ItemContent>
    </Accordion.Item>
  )
}

function BrightnessSlider({ initValue, setLoading }) {
  const [display, setDisplay] = useState(initValue)

  async function save(val) {
    const internal = mapValue(val, 0, 100, 2, 32)
    setLoading(true)
    try { await patch('/properties', { Brightness: internal }) }
    finally { setLoading(false) }
  }

  return (
    <Box>
      <HStack mb={2}>
        <i className="material-icons">brightness_medium</i>
        <Text fontSize="sm">{display}%</Text>
      </HStack>
      <Slider.Root
        value={[display]}
        min={0}
        max={100}
        onValueChange={({ value }) => setDisplay(value[0])}
        onValueChangeEnd={({ value }) => save(value[0])}
      >
        <Slider.Control>
          <Slider.Track>
            <Slider.Range />
          </Slider.Track>
          <Slider.Thumb index={0} />
        </Slider.Control>
      </Slider.Root>
    </Box>
  )
}

function Properties({ properties, setLoading, onPauseChange }) {
  const paused = properties?.Pause ?? false
  const [animated, setAnimated] = useState(properties.AnimatedSwitching)
  const [random, setRandom] = useState(properties.RandomMode)
  const displayBrightness = mapValue(properties.Brightness, 2, 32, 0, 100)

  async function togglePause() {
    setLoading(true)
    try {
      const res = await post('/modules/pause')
      onPauseChange?.(res.Response?.Pause ?? false)
    } finally { setLoading(false) }
  }

  async function toggleAnimated(e) {
    setLoading(true)
    try {
      await patch('/properties', { AnimatedSwitching: String(e.checked) })
      setAnimated(e.checked)
    } finally { setLoading(false) }
  }

  async function toggleRandom(e) {
    setLoading(true)
    try {
      await patch('/properties', { RandomMode: String(e.checked) })
      setRandom(e.checked)
    } finally { setLoading(false) }
  }

  return (
    <Stack gap={3}>
      <BrightnessSlider initValue={displayBrightness} setLoading={setLoading} />

      <Flex align="center" justify="space-between" px={1}>
        <Text fontSize="sm">Module auto switch</Text>
        <Switch.Root checked={!paused} onCheckedChange={togglePause} colorPalette="green">
          <Switch.HiddenInput />
          <Switch.Control><Switch.Thumb /></Switch.Control>
        </Switch.Root>
      </Flex>

      <Flex align="center" justify="space-between" px={1}>
        <Text fontSize="sm">Animated switching</Text>
        <Switch.Root checked={animated} onCheckedChange={toggleAnimated} colorPalette="teal">
          <Switch.HiddenInput />
          <Switch.Control><Switch.Thumb /></Switch.Control>
        </Switch.Root>
      </Flex>

      <Flex align="center" justify="space-between" px={1}>
        <Text fontSize="sm">Random mode</Text>
        <Switch.Root checked={random} onCheckedChange={toggleRandom} colorPalette="purple">
          <Switch.HiddenInput />
          <Switch.Control><Switch.Thumb /></Switch.Control>
        </Switch.Root>
      </Flex>

      <HStack gap={2}>
        <Button flex={1} colorPalette="blue" onClick={() => post('/modules/prev')}>
          <i className="material-icons">navigate_before</i>
          Prev
        </Button>
        <Button flex={1} colorPalette="blue" onClick={() => post('/modules/next')}>
          Next
          <i className="material-icons">navigate_next</i>
        </Button>
      </HStack>

      <HStack gap={2}>
        <Button flex={1} colorPalette="gray" onClick={() => post('/system/shutdown', { hard: 'false' })}>
          Stop <i className="material-icons">stop</i>
        </Button>
        <Button
          flex={1}
          colorPalette="red"
          onClick={() => { if (confirm('Shutdown the device?')) post('/system/shutdown', { hard: 'true' }) }}
        >
          Shutdown <i className="material-icons">power_settings_new</i>
        </Button>
        <Button flex={1} colorPalette="orange" onClick={() => post('/system/shutdown', { hard: 'restart' })}>
          Restart <i className="material-icons">refresh</i>
        </Button>
      </HStack>
    </Stack>
  )
}

function AppInstaller({ dlls, setDlls, setLoading }) {
  const fileRef = useRef()
  const [fileName, setFileName] = useState('')

  async function install() {
    const files = fileRef.current?.files
    if (!files?.length) return
    setLoading(true)
    const form = new FormData()
    form.append('file', files[0], files[0].name)
    try {
      await fetch('/plugins', { method: 'POST', body: form })
      setDlls(prev => prev.includes(files[0].name) ? prev : [...prev, files[0].name])
      setFileName('')
      fileRef.current.value = ''
    } finally {
      setLoading(false)
    }
  }

  async function remove(dll) {
    if (!confirm(`Remove ${dll}?`)) return
    setLoading(true)
    try {
      await del(`/plugins/${dll}`)
      setDlls(prev => prev.filter(d => d !== dll))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Stack gap={3}>
      <input
        type="file"
        accept=".dll"
        ref={fileRef}
        style={{ display: 'none' }}
        onChange={e => setFileName(e.target.files?.[0]?.name ?? '')}
      />
      <HStack>
        <Button flex={1} variant="outline" onClick={() => fileRef.current.click()}>
          <i className="material-icons">folder_open</i>
          {fileName || 'Wybierz plik .dll'}
        </Button>
        <Button colorPalette="teal" onClick={install} disabled={!fileName}>
          <i className="material-icons">save_alt</i>
          Install
        </Button>
      </HStack>
      <List.Root>
        {dlls.map(dll => (
          <List.Item
            key={dll}
            px={2} py={1} borderRadius="md"
            _hover={{ bg: 'gray.100' }} _dark={{ _hover: { bg: 'whiteAlpha.100' } }}
            transition="background 0.12s"
          >
            <Flex align="center" justify="space-between">
              <Text fontSize="sm">{dll}</Text>
              <Button size="xs" colorPalette="red" variant="ghost" onClick={() => remove(dll)}>
                <i className="material-icons" style={{ fontSize: '16px' }}>delete</i>
              </Button>
            </Flex>
          </List.Item>
        ))}
      </List.Root>
    </Stack>
  )
}

export default function OptionsCard({ properties, dlls, setDlls, setLoading, onPauseChange }) {
  return (
    <Card.Root>
      <Card.Header>
        <Heading size="md">Options</Heading>
      </Card.Header>
      <Card.Body>
        <Accordion.Root collapsible>
          <Section icon="add_box" title="App Installer">
            <AppInstaller dlls={dlls} setDlls={setDlls} setLoading={setLoading} />
          </Section>
          <Section icon="settings_applications" title="Properties">
            {properties && <Properties properties={properties} setLoading={setLoading} onPauseChange={onPauseChange} />}
          </Section>
          <Section icon="wifi" title="WiFi">
            <WifiPanel setLoading={setLoading} />
          </Section>
          <Section icon="nights_stay" title="Night Mode" disabled />
        </Accordion.Root>
      </Card.Body>
    </Card.Root>
  )
}
