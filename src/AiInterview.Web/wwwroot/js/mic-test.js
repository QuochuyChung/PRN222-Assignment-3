// Script kiểm tra Microphone & Giọng nói (Dev D - UI-02)
(() => {
    let audioContext = null;
    let analyser = null;
    let microphoneStream = null;
    let animationId = null;
    let speechRecognition = null;

    const canvas = document.getElementById('micWaveCanvas');
    const canvasCtx = canvas ? canvas.getContext('2d') : null;
    const btnStart = document.getElementById('btnStartMicTest');
    const btnStop = document.getElementById('btnStopMicTest');
    const volumeBar = document.getElementById('micVolumeBar');
    const statusOverlay = document.getElementById('micStatusOverlay');
    const transcriptPreview = document.getElementById('micTranscriptPreview');
    const modalEl = document.getElementById('testMicModal');

    if (!btnStart || !canvas || !canvasCtx) return;

    btnStart.addEventListener('click', async () => {
        try {
            statusOverlay.classList.add('d-none');
            microphoneStream = await navigator.mediaDevices.getUserMedia({ audio: true });

            audioContext = new (window.AudioContext || window.webkitAudioContext)();
            analyser = audioContext.createAnalyser();
            analyser.fftSize = 256;

            const source = audioContext.createMediaStreamSource(microphoneStream);
            source.connect(analyser);

            btnStart.disabled = true;
            btnStop.disabled = false;

            drawWaveform();
            startSpeechPreview();
        } catch (err) {
            console.error('Không thể truy cập microphone:', err);
            statusOverlay.classList.remove('d-none');
            statusOverlay.innerHTML = `
                <i class="bi bi-exclamation-triangle text-warning fs-3 d-block mb-1"></i>
                <span class="text-danger fw-semibold">Không thể truy cập Microphone!</span>
                <div class="small text-white-50 mt-1">Vui lòng kiểm tra quyền truy cập micro trong trình duyệt.</div>
            `;
        }
    });

    btnStop.addEventListener('click', () => {
        stopMicTest();
    });

    if (modalEl) {
        modalEl.addEventListener('hidden.bs.modal', () => {
            stopMicTest();
        });
    }

    function stopMicTest() {
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }

        if (microphoneStream) {
            microphoneStream.getTracks().forEach(t => t.stop());
            microphoneStream = null;
        }

        if (audioContext && audioContext.state !== 'closed') {
            audioContext.close();
            audioContext = null;
        }

        if (speechRecognition) {
            try { speechRecognition.stop(); } catch (e) { }
            speechRecognition = null;
        }

        btnStart.disabled = false;
        btnStop.disabled = true;

        if (statusOverlay) {
            statusOverlay.classList.remove('d-none');
            statusOverlay.innerHTML = `
                <i class="bi bi-mic-mute fs-3 d-block mb-1"></i>
                <span>Chưa bật kiểm tra mic</span>
            `;
        }

        if (volumeBar) {
            volumeBar.style.width = '0%';
        }

        if (canvasCtx) {
            canvasCtx.clearRect(0, 0, canvas.width, canvas.height);
        }
    }

    function drawWaveform() {
        if (!analyser) return;

        const bufferLength = analyser.frequencyBinCount;
        const dataArray = new Uint8Array(bufferLength);
        analyser.getByteFrequencyData(dataArray);

        // Tính volume tổng
        let sum = 0;
        for (let i = 0; i < bufferLength; i++) {
            sum += dataArray[i];
        }
        const average = sum / bufferLength;
        const volumePercent = Math.min(100, Math.round((average / 128) * 100));

        if (volumeBar) {
            volumeBar.style.width = `${volumePercent}%`;
            if (volumePercent > 60) {
                volumeBar.className = 'progress-bar progress-bar-striped progress-bar-animated bg-danger';
            } else if (volumePercent > 20) {
                volumeBar.className = 'progress-bar progress-bar-striped progress-bar-animated bg-success';
            } else {
                volumeBar.className = 'progress-bar progress-bar-striped progress-bar-animated bg-info';
            }
        }

        // Vẽ biểu đồ sóng âm
        canvasCtx.fillStyle = '#1e293b';
        canvasCtx.fillRect(0, 0, canvas.width, canvas.height);

        const barWidth = (canvas.width / bufferLength) * 2.5;
        let x = 0;

        for (let i = 0; i < bufferLength; i++) {
            const barHeight = (dataArray[i] / 255) * canvas.height;

            // Gradient màu từ xanh sang tím
            const gradient = canvasCtx.createLinearGradient(0, canvas.height, 0, canvas.height - barHeight);
            gradient.addColorStop(0, '#0ea5e9');
            gradient.addColorStop(1, '#8b5cf6');

            canvasCtx.fillStyle = gradient;
            canvasCtx.fillRect(x, canvas.height - barHeight, barWidth, barHeight);

            x += barWidth + 1;
        }

        animationId = requestAnimationFrame(drawWaveform);
    }

    function startSpeechPreview() {
        const SpeechRec = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRec) {
            if (transcriptPreview) {
                transcriptPreview.innerHTML = '<span class="text-warning">Trình duyệt không hỗ trợ Web Speech API trực tiếp, bạn vẫn có thể dùng mic và ô nhập chữ dự phòng.</span>';
            }
            return;
        }

        speechRecognition = new SpeechRec();
        speechRecognition.lang = 'vi-VN';
        speechRecognition.continuous = true;
        speechRecognition.interimResults = true;

        speechRecognition.onresult = (event) => {
            let interimTranscript = '';
            for (let i = event.resultIndex; i < event.results.length; i++) {
                interimTranscript += event.results[i][0].transcript;
            }
            if (transcriptPreview && interimTranscript) {
                transcriptPreview.innerHTML = `<strong class="text-success">${interimTranscript}</strong>`;
            }
        };

        speechRecognition.onerror = (e) => {
            console.warn('Speech recognition test warning:', e.error);
        };

        try {
            speechRecognition.start();
        } catch (e) { }
    }
})();
