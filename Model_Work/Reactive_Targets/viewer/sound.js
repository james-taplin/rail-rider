// Load the original review sounds locally. Playback starts only after a viewer interaction.
const paths={fall:[1,2,3,4].map(i=>`../sounds/rat_fall_0${i}.wav`),rise:[1,2,3,4].map(i=>`../sounds/rat_rise_0${i}.wav`),reset:['../sounds/reset_hit.wav'],contact:['../sounds/reset_pole_contact.wav']};
let context, output, enabled=true, report=()=>{}, current={fall:-1,rise:-1}, failed=false;
const raw=new Map();
const decoded=new Map();
function createContext(){if(!context){const C=window.AudioContext||window.webkitAudioContext;if(!C)throw Error('Web Audio is not supported');context=new C();const compressor=context.createDynamicsCompressor();compressor.threshold.value=-3;compressor.knee.value=0;compressor.ratio.value=12;compressor.attack.value=.003;compressor.release.value=.15;compressor.connect(context.destination);output=compressor;}return context;}
function ready(){createContext();if(context.state==='suspended')context.resume().catch(showError);return context;}
function showError(e){if(!failed){failed=true;report(`Sound unavailable: ${e.message||e}`)}}
function load(path){if(!raw.has(path)){const pending=fetch(path).then(r=>{if(!r.ok)throw Error(`Audio file unavailable (${r.status})`);return r.arrayBuffer()});pending.catch(showError);raw.set(path,pending)}return raw.get(path)}
function buffer(path){if(!decoded.has(path))decoded.set(path,load(path).then(bytes=>context.decodeAudioData(bytes.slice(0))));return decoded.get(path)}
export function configureSound(callback){report=callback;try{createContext();for(const path of Object.values(paths).flat())buffer(path).catch(showError)}catch(e){showError(e)}}
export function soundEnabled(){return enabled}
export function setSoundEnabled(value){enabled=value;if(enabled){try{ready();report('Sound on · click a target to hear it')}catch(e){showError(e)}}else report('Sound muted');}
export function playSound(type,index=0,delay=0){if(!enabled)return;try{ready();const choices=paths[type];let selected;if(type==='reset'||type==='contact')selected=0;else{selected=index%choices.length;if(selected===current[type])selected=(selected+1)%choices.length;current[type]=selected;}const path=choices[selected];buffer(path).then(audio=>{if(!enabled)return;const source=context.createBufferSource();source.buffer=audio;source.connect(output);source.start(context.currentTime+delay);report(`Sound on · ${type==='fall'?'rat hit':type==='rise'?'rat return':type==='contact'?'paddle hits pole':'reset hit'}`)}).catch(showError);}catch(e){showError(e)}}
