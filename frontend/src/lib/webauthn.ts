// Passkeys in the browser. The server sends WebAuthn options as JSON with base64url-encoded bytes;
// the browser API wants ArrayBuffers, and its answer must go back as JSON again.

type Json = Record<string, unknown>

export function passkeysSupported(): boolean {
  return typeof globalThis.PublicKeyCredential === 'function' && !!globalThis.navigator?.credentials
}

export function fromBase64Url(value: string): ArrayBuffer {
  const base64 = value
    .replaceAll('-', '+')
    .replaceAll('_', '/')
    .padEnd(Math.ceil(value.length / 4) * 4, '=')
  const binary = atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i)
  return bytes.buffer
}

export function toBase64Url(buffer: ArrayBuffer | ArrayBufferView): string {
  const bytes =
    buffer instanceof ArrayBuffer
      ? new Uint8Array(buffer)
      : new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength)
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '')
}

const decodeList = (list: unknown) =>
  Array.isArray(list) ? list.map((c: Json) => ({ ...c, id: fromBase64Url(c.id as string) })) : undefined

/** Creates a passkey from the server's creation options. Returns the answer as JSON text. */
export async function createPasskey(optionsJson: string): Promise<string> {
  const options = JSON.parse(optionsJson) as Json
  const user = options.user as Json
  const publicKey = {
    ...options,
    challenge: fromBase64Url(options.challenge as string),
    user: { ...user, id: fromBase64Url(user.id as string) },
    excludeCredentials: decodeList(options.excludeCredentials),
  } as unknown as PublicKeyCredentialCreationOptions
  const credential = (await navigator.credentials.create({ publicKey })) as PublicKeyCredential | null
  if (!credential) throw new Error('No passkey was created.')
  const response = credential.response as AuthenticatorAttestationResponse
  return JSON.stringify({
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    extensions: credential.getClientExtensionResults(),
    clientExtensionResults: credential.getClientExtensionResults(),
    response: {
      attestationObject: toBase64Url(response.attestationObject),
      clientDataJSON: toBase64Url(response.clientDataJSON),
      transports: response.getTransports?.() ?? [],
    },
  })
}

/** Signs in with a passkey from the server's request options. Returns the answer as JSON text. */
export async function getPasskey(optionsJson: string): Promise<string> {
  const options = JSON.parse(optionsJson) as Json
  const publicKey = {
    ...options,
    challenge: fromBase64Url(options.challenge as string),
    allowCredentials: decodeList(options.allowCredentials),
  } as unknown as PublicKeyCredentialRequestOptions
  const credential = (await navigator.credentials.get({ publicKey })) as PublicKeyCredential | null
  if (!credential) throw new Error('No passkey was chosen.')
  const response = credential.response as AuthenticatorAssertionResponse
  return JSON.stringify({
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    extensions: credential.getClientExtensionResults(),
    clientExtensionResults: credential.getClientExtensionResults(),
    response: {
      authenticatorData: toBase64Url(response.authenticatorData),
      clientDataJSON: toBase64Url(response.clientDataJSON),
      signature: toBase64Url(response.signature),
      userHandle: response.userHandle ? toBase64Url(response.userHandle) : null,
    },
  })
}
