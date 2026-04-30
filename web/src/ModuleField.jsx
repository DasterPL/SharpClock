import { useState, useRef, useEffect } from 'react'
import { Box, Button, HStack, Input, Popover, Text, Stack, Slider, Textarea } from '@chakra-ui/react'
import { Switch } from '@chakra-ui/react'
import { lang, getLabel } from './i18n.js'

function getVisLabel(field, opt) {
  try {
    const group = field.visibleOptions?.find(v => v.lang === lang.languageName)
      ?? field.visibleOptions?.find(v => v.lang === 'en')
    const match = group?.values.find(v => v.split('=')[0] === opt)
    if (match) return match.split('=')[1]
  } catch {}
  return opt
}

function DurationField({ label, id, defaultValue, emit }) {
  const totalSec = Math.round(parseInt(defaultValue, 10) / 1000)
  const minRef = useRef()
  const secRef = useRef()

  function save() {
    const m = Math.max(0, parseInt(minRef.current.value) || 0)
    const s = Math.max(0, Math.min(59, parseInt(secRef.current.value) || 0))
    emit(String((m * 60 + s) * 1000))
  }

  return (
    <Box>
      <Text fontSize="sm" mb={1}>{label}</Text>
      <HStack gap={1} align="center">
        <Input type="number" size="sm" min={0} ref={minRef}
          defaultValue={Math.floor(totalSec / 60)} onBlur={save} w="64px" />
        <Text fontSize="sm" color="gray.500">min</Text>
        <Input type="number" size="sm" min={0} max={59} ref={secRef}
          defaultValue={totalSec % 60} onBlur={save} w="64px" />
        <Text fontSize="sm" color="gray.500">s</Text>
      </HStack>
    </Box>
  )
}

function TimePickerField({ label, id, defaultValue, emit }) {
  const parts = (defaultValue || '00:00:00').split(':')
  const [hour, setHour] = useState(parseInt(parts[0], 10) || 0)
  const [minute, setMinute] = useState(parseInt(parts[1], 10) || 0)
  const [open, setOpen] = useState(false)
  const hourRef = useRef(null)
  const minRef = useRef(null)

  useEffect(() => {
    if (!open) return
    const t = setTimeout(() => {
      hourRef.current?.querySelector('[data-sel]')?.scrollIntoView({ block: 'center', behavior: 'instant' })
      minRef.current?.querySelector('[data-sel]')?.scrollIntoView({ block: 'center', behavior: 'instant' })
    }, 30)
    return () => clearTimeout(t)
  }, [open])

  function confirm() {
    emit(`${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`)
    setOpen(false)
  }

  const display = `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`
  const colStyle = { overflowY: 'auto', height: '160px', width: '52px', scrollbarWidth: 'none' }

  function Col({ items, selected, onSelect, ref }) {
    return (
      <div ref={ref} style={colStyle}>
        {items.map(i => (
          <Box key={i} {...(i === selected ? { 'data-sel': '' } : {})}
            h="32px" display="flex" alignItems="center" justifyContent="center"
            cursor="pointer" borderRadius="md" fontSize="sm"
            fontWeight={i === selected ? 'bold' : 'normal'}
            bg={i === selected ? 'teal.500' : 'transparent'}
            color={i === selected ? 'white' : 'inherit'}
            _hover={{ bg: i === selected ? 'teal.600' : 'gray.100' }}
            _dark={{ _hover: { bg: i === selected ? 'teal.600' : 'whiteAlpha.200' } }}
            onClick={() => onSelect(i)}
          >
            {String(i).padStart(2, '0')}
          </Box>
        ))}
      </div>
    )
  }

  return (
    <Box>
      <Text fontSize="sm" mb={1}>{label}</Text>
      <Popover.Root open={open} onOpenChange={({ open }) => setOpen(open)} positioning={{ placement: 'bottom-start' }}>
        <Popover.Trigger asChild>
          <Button variant="outline" size="sm">
            <i className="material-icons">schedule</i>
            {display}
          </Button>
        </Popover.Trigger>
        <Popover.Positioner>
          <Popover.Content w="auto" p={0}>
            <Popover.Body p={3}>
              <HStack gap={0} align="start">
                <Box>
                  <Text fontSize="xs" textAlign="center" color="gray.500" mb={1}>godz.</Text>
                  <Col items={Array.from({ length: 24 }, (_, i) => i)} selected={hour} onSelect={setHour} ref={hourRef} />
                </Box>
                <Box display="flex" alignItems="center" h="160px" px={1} mt="22px">
                  <Text fontWeight="bold" fontSize="lg">:</Text>
                </Box>
                <Box>
                  <Text fontSize="xs" textAlign="center" color="gray.500" mb={1}>min.</Text>
                  <Col items={Array.from({ length: 60 }, (_, i) => i)} selected={minute} onSelect={setMinute} ref={minRef} />
                </Box>
              </HStack>
              <Button colorPalette="teal" size="sm" w="full" mt={3} onClick={confirm}>OK</Button>
            </Popover.Body>
          </Popover.Content>
        </Popover.Positioner>
      </Popover.Root>
    </Box>
  )
}

function SliderField({ label, field, emit }) {
  const isFloat = field.type === 'Single'
  const step = field.step ?? (isFloat ? 0.1 : 1)
  const parse = v => isFloat ? parseFloat(v) : parseInt(v, 10)
  const [display, setDisplay] = useState(parse(field.Value))

  return (
    <Box>
      <HStack mb={2} justify="space-between">
        <Text fontSize="sm">{label}</Text>
        <Text fontSize="sm" color="gray.500" fontVariantNumeric="tabular-nums">
          {isFloat ? display.toFixed(1) : display}
        </Text>
      </HStack>
      <Slider.Root
        min={field.min} max={field.max} step={step}
        value={[display]}
        onValueChange={({ value }) => setDisplay(value[0])}
        onValueChangeEnd={({ value }) => emit(isFloat ? value[0].toFixed(1) : String(Math.round(value[0])))}
      >
        <Slider.Control>
          <Slider.Track><Slider.Range /></Slider.Track>
          <Slider.Thumb index={0} />
        </Slider.Control>
      </Slider.Root>
    </Box>
  )
}

function StepperField({ label, id, field, emit }) {
  const isFloat = field.type === 'Single'
  const step = field.step ?? (isFloat ? 0.1 : 1)
  const parse = v => isFloat ? parseFloat(v) : parseInt(v, 10)
  const fmt = v => isFloat ? v.toFixed(1) : String(v)
  const [value, setValue] = useState(parse(field.Value))

  function change(delta) {
    const next = isFloat ? Math.round((value + delta) * 100) / 100 : value + delta
    setValue(next)
    emit(fmt(next))
  }

  return (
    <Box>
      <Text fontSize="sm" mb={1}>{label}</Text>
      <HStack gap={2}>
        <Button size="sm" variant="outline" onClick={() => change(-step)}>
          <i className="material-icons">remove</i>
        </Button>
        <Input
          size="sm" type="number" value={value} textAlign="center" w="80px"
          onChange={e => { const v = parse(e.target.value); if (!isNaN(v)) setValue(v) }}
          onBlur={e => { const v = parse(e.target.value); if (!isNaN(v)) { setValue(v); emit(fmt(v)) } }}
        />
        <Button size="sm" variant="outline" onClick={() => change(step)}>
          <i className="material-icons">add</i>
        </Button>
      </HStack>
    </Box>
  )
}

function EnumToggleField({ label, field, emit }) {
  const options = field.options.split(',')
  const [value, setValue] = useState(field.Value)

  return (
    <Box>
      <Text fontSize="sm" mb={1}>{label}</Text>
      <HStack gap={3} align="center">
        <Text fontSize="sm" color={value === options[0] ? 'inherit' : 'gray.400'} transition="color 0.15s">
          {getVisLabel(field, options[0])}
        </Text>
        <Switch.Root
          checked={value === options[1]}
          onCheckedChange={({ checked }) => {
            const next = checked ? options[1] : options[0]
            setValue(next)
            emit(next)
          }}
        >
          <Switch.HiddenInput />
          <Switch.Control><Switch.Thumb /></Switch.Control>
        </Switch.Root>
        <Text fontSize="sm" color={value === options[1] ? 'inherit' : 'gray.400'} transition="color 0.15s">
          {getVisLabel(field, options[1])}
        </Text>
      </HStack>
    </Box>
  )
}

function EnumSelectField({ label, field, emit }) {
  const options = field.options.split(',')
  return (
    <Box>
      <Text fontSize="sm" mb={1}>{label}</Text>
      <select
        defaultValue={field.Value}
        onChange={e => emit(e.target.value)}
        style={{
          width: '100%', padding: '6px 10px', borderRadius: '6px',
          fontSize: '14px', border: '1px solid var(--chakra-colors-border)',
          background: 'transparent', color: 'inherit', cursor: 'pointer',
        }}
      >
        {options.map(opt => (
          <option key={opt} value={opt}>{getVisLabel(field, opt)}</option>
        ))}
      </select>
    </Box>
  )
}

export default function ModuleField({ moduleName, field, onChange }) {
  const label = getLabel(field.VisibleName, lang.languageName) ?? field.Name
  const id = `${moduleName}_${field.Name}`
  const emit = val => onChange(field.Name, val)

  switch (field.type) {
    case 'Int32':
    case 'Single':
    case 'float':
      if (field.Name === 'Timer')
        return <DurationField label={label} id={id} defaultValue={field.Value} emit={emit} />
      if (field.min != null && field.max != null) {
        if (field.max - field.min > 200)
          return <StepperField label={label} id={id} field={field} emit={emit} />
        return <SliderField label={label} field={field} emit={emit} />
      }
      return <StepperField label={label} id={id} field={field} emit={emit} />

    case 'Boolean':
      return (
        <Switch.Root
          id={id}
          defaultChecked={field.Value === 'True'}
          onCheckedChange={e => emit(String(e.checked))}
          width="full" display="flex" alignItems="center" justifyContent="space-between"
        >
          <Switch.HiddenInput />
          <Switch.Label>{label}</Switch.Label>
          <Switch.Control><Switch.Thumb /></Switch.Control>
        </Switch.Root>
      )

    case 'String':
      if (field.multiline)
        return (
          <Box>
            <Text fontSize="sm" mb={1}>{label}</Text>
            <Textarea
              id={id} size="sm" defaultValue={field.Value}
              onBlur={e => emit(e.target.value)}
              rows={3} resize="vertical"
            />
          </Box>
        )
      return (
        <Box>
          <Text fontSize="sm" mb={1}>{label}</Text>
          <Input id={id} size="sm" defaultValue={field.Value} onBlur={e => emit(e.target.value)} />
        </Box>
      )

    case 'Password':
      return (
        <Box>
          <Text fontSize="sm" mb={1}>{label}</Text>
          <Input
            type="password" id={id} size="sm" placeholder="••••••••"
            onBlur={e => { if (e.target.value) emit(e.target.value) }}
          />
        </Box>
      )

    case 'Enum': {
      const options = field.options.split(',')
      if (options.length === 2)
        return <EnumToggleField label={label} field={field} emit={emit} />
      return <EnumSelectField label={label} field={field} emit={emit} />
    }

    case 'Color':
      return (
        <Box>
          <Text fontSize="sm" mb={1}>{label}</Text>
          <input
            type="color" id={id} defaultValue={field.Value}
            onChange={e => emit(e.target.value)}
            style={{ width: '48px', height: '32px', cursor: 'pointer', border: 'none', borderRadius: '4px' }}
          />
        </Box>
      )

    case 'TimeSpan':
      return <TimePickerField label={label} id={id} defaultValue={field.Value} emit={emit} />

    default:
      return null
  }
}
