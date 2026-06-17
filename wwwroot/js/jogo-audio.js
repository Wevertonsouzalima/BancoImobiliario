// wwwroot/js/jogo-audio.js
// Helper simples de áudio para o jogo. Toca arquivos .mp3 em wwwroot/sons/.
// Se um arquivo não existir, falha silenciosamente (não quebra o jogo).
//
// Uso via Blazor JS interop:
//   await JS.InvokeVoidAsync("jogoAudio.tocar", "dado");
//   await JS.InvokeVoidAsync("jogoAudio.setMudo", true);

window.jogoAudio = (function () {
    let mudo = false;
    let volume = 0.7;
    const cache = {};
    let loopAtual = null;   // Audio em loop no momento (ex.: rolagem do dado)

    function obter(nome) {
        if (!cache[nome]) {
            const a = new Audio(`/sons/${nome}.mp3`);
            a.preload = "auto";
            cache[nome] = a;
        }
        return cache[nome];
    }

    return {
        tocar: function (nome) {
            if (mudo) return;
            try {
                const base = obter(nome);
                const som = base.cloneNode(true);
                som.volume = volume;
                const p = som.play();
                if (p && typeof p.catch === "function") {
                    p.catch(() => { });
                }
            } catch (e) { }
        },

        // Toca um som em loop (ex.: dado rolando). Para o loop anterior, se houver.
        tocarLoop: function (nome) {
            this.pararLoop();
            if (mudo) return;
            try {
                const som = obter(nome).cloneNode(true);
                som.volume = volume;
                som.loop = true;
                loopAtual = som;
                const p = som.play();
                if (p && typeof p.catch === "function") {
                    p.catch(() => { });
                }
            } catch (e) { }
        },

        pararLoop: function () {
            try {
                if (loopAtual) {
                    loopAtual.pause();
                    loopAtual.currentTime = 0;
                    loopAtual = null;
                }
            } catch (e) { }
        },

        setMudo: function (valor) {
            mudo = !!valor;
            if (mudo) this.pararLoop();
        },

        getMudo: function () {
            return mudo;
        },

        setVolume: function (v) {
            volume = Math.max(0, Math.min(1, v));
        }
    };
})();