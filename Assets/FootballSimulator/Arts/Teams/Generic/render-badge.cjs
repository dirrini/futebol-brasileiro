// Rasterize the authored vector source for the legacy two-color logo shader.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
sharp(fs.readFileSync(path.join(__dirname, 'Badge.svg'))).png()
  .toFile(path.join(__dirname, 'Badge.png')).catch(error => { console.error(error); process.exitCode = 1; });
