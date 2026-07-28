const { Jimp } = require('jimp');
const path = require('path');
const fs = require('fs');

const outDir = path.join(__dirname, '..', 'public', 'icons');
if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

const SIZES = [48, 72, 96, 128, 144, 152, 192, 384, 512];
const BG = 0xff4f46e5;

async function generate() {
  for (const size of SIZES) {
    const image = new Jimp({ width: size, height: size, color: BG });
    await image.write(path.join(outDir, `icon-${size}x${size}.png`));
    console.log(`Generated ${size}x${size}`);
  }

  const iOS_SIZES = [
    { name: 'apple-touch-icon-152x152.png', size: 152 },
    { name: 'apple-touch-icon-167x167.png', size: 167 },
    { name: 'apple-touch-icon-180x180.png', size: 180 },
  ];
  for (const { name, size } of iOS_SIZES) {
    const image = new Jimp({ width: size, height: size, color: BG });
    await image.write(path.join(outDir, name));
    console.log(`Generated ${name}`);
  }
  console.log('All icons generated!');
}

generate().catch(console.error);
