// Module STT (Speech-to-Text) nhận diện giọng nói tiếng Việt (Dev D - UI-05)
window.AiStt = (() => {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    let recognition = null;
    let isListening = false;
    let silenceTimer = null;
    let lastTranscript = '';

    const SILENCE_TIMEOUT_MS = 3500; // 3.5 giây im lặng sẽ kích hoạt onSilence

    function isSupported() {
        return !!SpeechRecognition;
    }

    function start(callbacks) {
        if (!isSupported()) {
            if (callbacks?.onError) {
                callbacks.onError('Trình duyệt không hỗ trợ Web Speech API.');
            }
            return false;
        }

        stop();

        try {
            recognition = new SpeechRecognition();
            recognition.lang = 'vi-VN';
            recognition.continuous = true;
            recognition.interimResults = true;
            lastTranscript = '';

            recognition.onstart = () => {
                isListening = true;
                if (callbacks?.onStart) callbacks.onStart();
            };

            recognition.onresult = (event) => {
                resetSilenceTimer(callbacks?.onSilence);

                let interim = '';
                let final = '';

                for (let i = event.resultIndex; i < event.results.length; i++) {
                    const trans = event.results[i][0].transcript;
                    if (event.results[i].isFinal) {
                        final += trans + ' ';
                    } else {
                        interim += trans;
                    }
                }

                if (final) {
                    lastTranscript = (lastTranscript + ' ' + final).trim();
                }

                const currentCombined = (lastTranscript + ' ' + interim).trim();

                if (callbacks?.onInterim) {
                    callbacks.onInterim(currentCombined);
                }
            };

            recognition.onerror = (event) => {
                console.warn('Lỗi SpeechRecognition:', event.error);
                if (event.error !== 'no-speech' && callbacks?.onError) {
                    callbacks.onError(event.error);
                }
            };

            recognition.onend = () => {
                isListening = false;
                clearTimeout(silenceTimer);
                if (callbacks?.onEnd) callbacks.onEnd(lastTranscript);
            };

            recognition.start();
            return true;
        } catch (e) {
            console.error('Không thể khởi tạo SpeechRecognition:', e);
            if (callbacks?.onError) callbacks.onError(e.message);
            return false;
        }
    }

    function resetSilenceTimer(onSilenceCallback) {
        clearTimeout(silenceTimer);
        silenceTimer = setTimeout(() => {
            if (isListening && onSilenceCallback) {
                onSilenceCallback(lastTranscript);
            }
        }, SILENCE_TIMEOUT_MS);
    }

    function stop() {
        clearTimeout(silenceTimer);
        if (recognition && isListening) {
            try {
                recognition.stop();
            } catch (e) { }
        }
        isListening = false;
    }

    return {
        isSupported,
        start,
        stop,
        isListening: () => isListening,
        getLastTranscript: () => lastTranscript
    };
})();
