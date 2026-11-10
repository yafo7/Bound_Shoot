import { writeFileSync } from 'node:fs';

// Original 16-bar loop. Pitched parts and percussion are synthesized without samples.
const rate = 44100;
const menu = process.argv.includes('--menu');
const beat = 60 / (menu ? 108 : 144);
const frames = Math.round(rate * beat * 64);
const mix = new Float32Array(frames);
const frequency = note => 440 * 2 ** ((note - 69) / 12);
const tau = Math.PI * 2;
let seed = 321;
const noise = () => {
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  return seed / 2147483648 - 1;
};

function layer(at, seconds, gain, voice) {
  const offset = Math.round(at * beat * rate);
  const length = Math.round(seconds * rate);
  for (let i = 0; i < length; i++) {
    const t = i / rate;
    const envelope = Math.min(1, t / 0.007, (length - i) / (rate * 0.025));
    mix[(offset + i) % frames] += gain * envelope * voice(t);
  }
}

const chords = menu ? [[48,60,64,67],[45,57,60,64],[41,53,57,60],[43,55,59,62]]
  : [[45, 57, 60, 64], [41, 53, 57, 60], [43, 55, 59, 62], [40, 52, 56, 59]];
const melody = menu ? [[76,79,84,79,76,72,74,76],[76,72,69,72,76,79,76,72],
  [77,76,72,69,72,77,81,77],[74,71,67,71,74,79,77,74]]
  : [[69,72,76,74,72,69,67,69], [69,72,77,76,72,69,65,67],
  [67,71,74,79,77,74,71,74], [68,71,76,74,71,68,64,68]];
for (let bar = 0; bar < 16; bar++) {
  const chord = chords[bar % 4];
  for (let step = 0; step < 8; step++) {
    const at = bar * 4 + step / 2;
    const bass = frequency(chord[0] + (step === 3 || step === 7 ? 12 : 0));
    layer(at, 0.19, 0.14, t => Math.sin(tau * bass * t) + 0.25 * Math.sin(tau * bass * t * 2));
    const arp = frequency(chord[1 + step % 3] + 12);
    layer(at + 0.25, 0.10, 0.045, t => Math.sin(tau * arp * t) * Math.exp(-16 * t));
    const lead = frequency(melody[bar % 4][step] + (bar >= 8 && step === 2 ? 12 : 0));
    if (!menu || step % 2 === 0)
      layer(at, menu ? 0.36 : 0.16, 0.072, t => (Math.sin(tau * lead * t) + 0.27 * Math.sin(tau * lead * t * 3)) * Math.exp(-5 * t));
    layer(at, 0.047, 0.047, t => noise() * Math.exp(-62 * t));
    if (step === 0 || step === 4 || (bar % 2 === 1 && step === 7))
      layer(at, 0.19, menu ? 0.13 : 0.31, t => Math.sin(tau * (48 * t + 52 * (1 - Math.exp(-24 * t)) / 24)) * Math.exp(-19 * t));
    if (step === 2 || step === 6)
      layer(at, 0.12, menu ? 0.045 : 0.17, t => (0.8 * noise() + 0.2 * Math.sin(tau * 170 * t)) * Math.exp(-25 * t));
  }
}

const wav = Buffer.alloc(44 + 2 * frames);
wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4);
wav.write('WAVEfmt ', 8); wav.writeUInt32LE(16, 16);
wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 2, 28);
wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
wav.write('data', 36); wav.writeUInt32LE(frames * 2, 40);
for (let i = 0; i < frames; i++) wav.writeInt16LE(Math.round(Math.tanh(mix[i] * 1.35) * 32767), 44 + i * 2);
const track = menu ? 'ForestAssembly' : 'RopeRush';
writeFileSync(new URL(`../unity-client/Assets/BoundShoot/Music/Resources/${track}.wav`, import.meta.url), wav);
