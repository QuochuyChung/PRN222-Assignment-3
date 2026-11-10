// Module TTS (Text-to-Speech) giọng đọc tiếng Việt (Dev D - UI-04)
window.AiTts = (() => {
    let synth = window.speechSynthesis;
    let selectedVoice = null;
    let isSpeaking = false;

    function initVoices() {
        if (!synth) return;
        const voices = synth.getVoices();
        // Tìm giọng tiếng Việt
        selectedVoice = voices.find(v => v.lang === 'vi-VN' || v.lang === 'vi_VN' || v.name.includes('Vietnamese')) 
            || voices.find(v => v.lang.startsWith('vi'))
            || null;
    }

    if (synth) {
        initVoices();
        if (synth.onvoiceschanged !== undefined) {
            synth.onvoiceschanged = initVoices;
        }
    }

    function speak(text, onStartCallback, onEndCallback) {
        if (!synth) {
            console.warn('Trình duyệt không hỗ trợ Web Speech Synthesis.');
            if (onEndCallback) onEndCallback();
            return;
        }

        // Hủy lượt đọc cũ nếu có
        stop();

        const cleanText = (text || '').trim();
        if (!cleanText) {
            if (onEndCallback) onEndCallback();
            return;
        }

        const utterance = new SpeechSynthesisUtterance(cleanText);
        utterance.lang = 'vi-VN';
        utterance.rate = 1.0;
        utterance.pitch = 1.0;

        if (selectedVoice) {
            utterance.voice = selectedVoice;
        }

        utterance.onstart = () => {
            isSpeaking = true;
            if (onStartCallback) onStartCallback();
        };

        utterance.onend = () => {
            isSpeaking = false;
            if (onEndCallback) onEndCallback();
        };

        utterance.onerror = (e) => {
            console.warn('Lỗi phát âm TTS:', e);
            isSpeaking = false;
            if (onEndCallback) onEndCallback();
        };

        synth.speak(utterance);
    }

    function stop() {
        if (synth && (synth.speaking || synth.pending)) {
            synth.cancel();
        }
        isSpeaking = false;
    }

    return {
        speak,
        stop,
        isSpeaking: () => isSpeaking
    };
})();
