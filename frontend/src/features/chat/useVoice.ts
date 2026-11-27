import { useCallback, useEffect, useRef, useState } from 'react'
import { voiceApi } from '@/api/endpoints'

export type VoiceState = 'idle' | 'listening' | 'transcribing' | 'speaking'

export function recordingSupported(): boolean {
  return typeof MediaRecorder !== 'undefined' && !!navigator.mediaDevices?.getUserMedia
}

/**
 * Talking to an orb: record from the microphone and turn it into text, and play replies aloud.
 * The state also drives the orb's face (listening, speaking).
 */
export function useVoice(onTranscript: (text: string) => void) {
  const [state, setState] = useState<VoiceState>('idle')
  const [error, setError] = useState<string | null>(null)
  const recorder = useRef<MediaRecorder | null>(null)
  const audio = useRef<HTMLAudioElement | null>(null)

  useEffect(
    () => () => {
      recorder.current?.stream.getTracks().forEach((t) => t.stop())
      audio.current?.pause()
    },
    [],
  )

  const listen = useCallback(async () => {
    setError(null)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      const chunks: Blob[] = []
      const rec = new MediaRecorder(stream)
      rec.ondataavailable = (e) => e.data.size > 0 && chunks.push(e.data)
      rec.onstop = async () => {
        stream.getTracks().forEach((t) => t.stop())
        setState('transcribing')
        try {
          const blob = new Blob(chunks, { type: rec.mimeType || 'audio/webm' })
          const { text } = await voiceApi.transcribe(blob)
          if (text) onTranscript(text)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Could not understand the recording.')
        } finally {
          setState('idle')
        }
      }
      recorder.current = rec
      rec.start()
      setState('listening')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'The microphone is not available.')
      setState('idle')
    }
  }, [onTranscript])

  const stopListening = useCallback(() => recorder.current?.stop(), [])

  const speak = useCallback(async (text: string, voice?: string, orbId?: string) => {
    setError(null)
    try {
      audio.current?.pause()
      const blob = await voiceApi.speak(text, voice, orbId)
      const player = new Audio(URL.createObjectURL(blob))
      audio.current = player
      setState('speaking')
      player.onended = () => {
        URL.revokeObjectURL(player.src)
        setState('idle')
      }
      await player.play()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not play the reply.')
      setState('idle')
    }
  }, [])

  const stopSpeaking = useCallback(() => {
    audio.current?.pause()
    setState('idle')
  }, [])

  return { state, error, listen, stopListening, speak, stopSpeaking }
}
