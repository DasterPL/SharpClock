const langs = {
  pl: { languageName: 'pl', on: 'Wł', off: 'Wył', module: 'Moduł' },
  en: { languageName: 'en', on: 'On', off: 'Off', module: 'Module' },
}

const code = navigator.language
export const lang = (code === 'pl-PL' || code === 'pl' || code === 'PL')
  ? langs.pl
  : langs.en

export function getLabel(visibleNames, langName) {
  return visibleNames?.find(v => v.lang === langName)?.value
    ?? visibleNames?.find(v => v.lang === 'en')?.value
    ?? null
}
