// Rasterize the authored vector sources; no existing raster image is edited.
// Requires Node.js and sharp. Run from this directory: node render-masks.cjs
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');

const sources = ['Badge', 'HomeAtlas', 'AwayAtlas', 'HomePreview', 'AwayPreview'];
async function main() {
  for (const source of sources) {
    const svg = fs.readFileSync(path.join(__dirname, source + '.svg'), 'utf8');
    // Legacy shaders replace red with Color1 and blue with Color2; black stays black.
    const mask = svg.replaceAll('fill:#ffffff', 'fill:#ff0000')
      .replaceAll('fill:#e31b23', 'fill:#0000ff');
    await sharp(Buffer.from(mask)).png().toFile(path.join(__dirname, source + '.png'));
  }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
