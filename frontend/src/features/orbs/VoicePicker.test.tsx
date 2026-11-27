import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { mockApi, renderPage } from '@/test/render'
import { VoicePicker } from './VoicePicker'

describe('VoicePicker', () => {
  it('explains how to get voices when nothing can speak', async () => {
    mockApi({ 'GET /api/voice': { canListen: false, canSpeak: false, voices: [] } })
    renderPage(<VoicePicker name="Pip" value="" onChange={() => {}} />)

    expect(await screen.findByText(/set a text-to-speech model/)).toBeInTheDocument()
  })

  it('suggests the provider voices', async () => {
    mockApi({ 'GET /api/voice': { canListen: true, canSpeak: true, voices: ['alloy', 'nova'] } })
    const onChange = vi.fn<(v: string) => void>()
    renderPage(<VoicePicker name="Pip" value="" onChange={onChange} />)

    const field = await screen.findByLabelText('Voice')
    expect(field).toHaveAttribute('placeholder', 'alloy')
    await userEvent.type(field, 'n')
    expect(onChange).toHaveBeenCalledWith('n')
    expect(document.querySelectorAll('datalist option')).toHaveLength(2)
  })
})
