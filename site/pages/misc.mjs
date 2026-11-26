import { escapeHtml } from '../lib/layout.mjs'
import { orb } from '../lib/orb.mjs'

export function notFound({ root }) {
  return `
<section class="not-found">
  ${orb({ color: 'sky', shape: 'drop', eyes: 'sleepy', mouth: 'open', accessory: 'none', blush: false, size: 170, floating: true })}
  <span class="eyebrow">404</span>
  <h1 class="display-2">This orb wandered off</h1>
  <p class="lead">The page you're looking for isn't here. It may have moved, or the link has a typo.</p>
  <div class="actions center">
    <a class="button primary large" href="${root}">Back to home</a>
    <a class="button ghost large" href="${root}docs/">Read the docs</a>
  </div>
</section>
`
}

/** The license page: a plain-words summary, then the LICENSE file exactly as it is in the repository. */
export function licenseBody(licenseText) {
  return `<p class="updated">From the <code>LICENSE</code> file in the repository</p>
<h1>License</h1>
<div class="callout"><p>Orbs is released under the <strong>MIT License</strong>, one of the most widely used open-source licenses. In short: do almost anything you like with the code, keep the notice below in your copies, and know that it comes with no warranty.</p></div>
<h2 id="the-license">The license</h2>
<pre class="license-text">${escapeHtml(licenseText.trim())}</pre>
<h2 id="who-the-orbs-contributors-are">Who "Orbs contributors" are</h2>
<p>Everyone who has contributed code to Orbs keeps the copyright to their contribution and shares it under this license. The full list is in the Git history.</p>
<h2 id="orbs-you-make-are-yours">Orbs you make are yours</h2>
<p>The faces, names and settings you create in Orbs belong to you. Use your orbs' pictures however you like.</p>
<h2 id="what-the-license-doesnt-cover">What the license doesn't cover</h2>
<ul>
<li><strong>Third-party code.</strong> Orbs uses open-source libraries under their own licenses.</li>
<li><strong>Model providers and websites you connect.</strong> Their own terms apply to your use of them.</li>
</ul>
<h2 id="using-orbs-in-your-product">Using Orbs in your product</h2>
<p>Go for it. Include the license text above in your distribution, for example in an "Open-source licenses" screen. If your product is a changed version of Orbs, please give it its own name and logo.</p>`
}
