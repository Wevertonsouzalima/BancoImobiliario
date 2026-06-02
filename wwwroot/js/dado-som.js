// wwwroot/js/dado-som.js
// Gera o som do dado sinteticamente via Web Audio API.
// Nao precisa de arquivos .mp3 hospedados.

let ctx = null;

function getCtx() {
    if (!ctx) {
        ctx = new (window.AudioContext || window.webkitAudioContext)();
    }
    // Navegadores suspendem o contexto ate uma interacao do usuario
    if (ctx.state === 'suspended') {
        ctx.resume();
    }
    return ctx;
}

// Gera um "click" seco, como um dado solido batendo
function batida(audioCtx, tempo, volume) {
    const dur = 0.06;
    const buffer = audioCtx.createBuffer(1, audioCtx.sampleRate * dur, audioCtx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < data.length; i++) {
        // Ruido com decaimento rapido = som de algo solido batendo
        data[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / data.length, 3);
    }
    const src = audioCtx.createBufferSource();
    src.buffer = buffer;

    const gain = audioCtx.createGain();
    gain.gain.value = volume;

    const filtro = audioCtx.createBiquadFilter();
    filtro.type = 'lowpass';
    filtro.frequency.value = 2200;

    src.connect(filtro);
    filtro.connect(gain);
    gain.connect(audioCtx.destination);
    src.start(tempo);
}

// tipo: "chacoalhar" (dados girando na mao) ou "jogar" (dados caindo na mesa)
export function tocar(tipo) {
    try {
        const audioCtx = getCtx();
        const agora = audioCtx.currentTime;

        if (tipo === 'chacoalhar') {
            // Varias batidas leves e rapidas = dados girando na mao
            for (let i = 0; i < 9; i++) {
                batida(audioCtx, agora + i * 0.09 + Math.random() * 0.03, 0.18);
            }
        } else if (tipo === 'jogar') {
            // 2-3 batidas fortes = dados caindo e quicando na mesa
            batida(audioCtx, agora, 0.55);
            batida(audioCtx, agora + 0.12, 0.4);
            batida(audioCtx, agora + 0.22, 0.25);
        }
    } catch (e) {
        // Falha de audio nao deve quebrar o jogo
        console.warn('Som do dado indisponivel:', e);
    }
}
