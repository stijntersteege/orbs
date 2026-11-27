import { fromBase64Url, toBase64Url } from './webauthn'

describe('webauthn helpers', () => {
  it('round-trips bytes through base64url without padding', () => {
    const bytes = new Uint8Array([0, 250, 251, 252, 253, 254, 255])
    const encoded = toBase64Url(bytes)
    expect(encoded).not.toMatch(/[+/=]/)
    expect(new Uint8Array(fromBase64Url(encoded))).toEqual(bytes)
  })
})
