async function request(method, path, data) {
  let body, headers = {}
  if (data instanceof FormData) {
    body = data
  } else if (data) {
    headers['Content-Type'] = 'application/x-www-form-urlencoded'
    body = new URLSearchParams(data)
  }
  const res = await fetch(path, { method, headers, body })
  return res.json()
}

export const get  = (path)       => request('GET',    path)
export const post = (path, data) => request('POST',   path, data)
export const patch = (path, data) => request('PATCH',  path, data)
export const del  = (path)       => request('DELETE', path)

export async function putOrder(names) {
  const body = new URLSearchParams()
  names.forEach(n => body.append('order[]', n))
  const res = await fetch('/modules/order', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  })
  return res.json()
}

export const postButton = (name, id) => post(`/modules/${name}/button`, { id })

export function mapValue(input, inStart, inEnd, outStart, outEnd) {
  return Math.round((input - inStart) * outEnd / inEnd + outStart)
}
