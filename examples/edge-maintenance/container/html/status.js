// Polls the edge's machine-readable status (same origin during maintenance) and
// updates the page. Works identically behind the edge whether the product is in
// planned maintenance or being redeployed.
async function refresh() {
  try {
    const r = await fetch('/__status', { cache: 'no-store' });
    if (!r.ok) return;
    const s = await r.json();
    const $ = (id) => document.getElementById(id);

    if (s.state === 'deploying') {
      $('badge').textContent = 'Update';
      $('title').textContent = 'Aktualisierung läuft';
      $('body').textContent = 'Eine neue Version wird gerade ausgerollt. Der Dienst ist gleich wieder verfügbar.';
    } else {
      $('badge').textContent = 'Wartung';
      $('title').textContent = 'Geplante Wartung';
      $('body').textContent = 'Dieser Dienst ist vorübergehend wegen Wartungsarbeiten nicht erreichbar. Bitte versuchen Sie es in Kürze erneut.';
    }

    const bits = [];
    if (s.reason) bits.push(s.reason);
    if (s.until) bits.push('Voraussichtlich bis ' + new Date(s.until).toLocaleString());
    $('meta').textContent = bits.join(' · ');

    // Once the product is running again, the edge proxies through — reload to leave the page.
    if (s.state === 'running') location.reload();
  } catch (_) {
    // Edge may be reloading its config briefly; ignore and retry.
  }
}

refresh();
setInterval(refresh, 5000);
