import { useState } from 'react'
import { Accordion, Box, Stack, Text } from '@chakra-ui/react'
import { patch } from './api.js'
import ModuleField from './ModuleField.jsx'

function GlobalSettingsItem({ gs, setLoading }) {
  async function saveField(fieldName, value) {
    setLoading(true)
    try {
      await patch(`/globalSettings/${gs.Name}`, { [fieldName]: value })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Accordion.Item value={gs.Name}>
      <Accordion.ItemTrigger
        _hover={{ bg: 'gray.100' }} _dark={{ _hover: { bg: 'whiteAlpha.100' } }}
        transition="background 0.12s"
      >
        <Text fontWeight="medium" flex={1}>{gs.Name}</Text>
        <Accordion.ItemIndicator />
      </Accordion.ItemTrigger>
      <Accordion.ItemContent>
        <Stack p={3} gap={3}>
          {gs.Values.map(field => (
            <Box
              key={field.Name}
              px={2} py={1} mx={-2} borderRadius="md"
              _hover={{ bg: 'gray.100' }} _dark={{ _hover: { bg: 'whiteAlpha.100' } }}
              transition="background 0.12s"
            >
              <ModuleField moduleName={gs.Name} field={field} onChange={saveField} />
            </Box>
          ))}
        </Stack>
      </Accordion.ItemContent>
    </Accordion.Item>
  )
}

export default function GlobalSettings({ globalSettings, setLoading }) {
  const [openItems, setOpenItems] = useState([])

  if (!globalSettings || globalSettings.length === 0) return null

  return (
    <Accordion.Root
      multiple
      value={openItems}
      onValueChange={({ value }) => setOpenItems(value)}
    >
      {globalSettings.map(gs => (
        <GlobalSettingsItem key={gs.Name} gs={gs} setLoading={setLoading} />
      ))}
    </Accordion.Root>
  )
}
