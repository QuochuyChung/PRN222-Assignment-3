// Client State Machine & Điều phối phòng phỏng vấn (Dev D - UI-03..UI-07)
(() => {
    const appEl = document.getElementById('interviewApp');
    if (!appEl) return;

    const sessionId = appEl.dataset.sessionId;
    const maxFollowUps = parseInt(appEl.dataset.maxFollowUps || '2', 10);
    const configuredSeconds = parseInt(appEl.dataset.answerSeconds || '60', 10);

    // DOM Elements
    const lblQuestionText = document.getElementById('lblQuestionText');
    const badgeQuestionType = document.getElementById('badgeQuestionType');
    const badgeFollowUpCount = document.getElementById('badgeFollowUpCount');
    const lblFollowUpNumber = document.getElementById('lblFollowUpNumber');
    const lblProgressQuestion = document.getElementById('lblProgressQuestion');
    const lblAiState = document.getElementById('lblAiState');
    const lblRemainingSeconds = document.getElementById('lblRemainingSeconds');
    const countdownProgress = document.getElementById('countdownProgress');
    const btnVoiceToggle = document.getElementById('btnVoiceToggle');
    const iconMic = document.getElementById('iconMic');
    const lblMicStatus = document.getElementById('lblMicStatus');
    const lblMicHint = document.getElementById('lblMicHint');
    const btnReplayAudio = document.getElementById('btnReplayAudio');
    const btnToggleTextInput = document.getElementById('btnToggleTextInput');
    const fallbackInputContainer = document.getElementById('fallbackInputContainer');
    const txtManualAnswer = document.getElementById('txtManualAnswer');
    const btnSubmitManualAnswer = document.getElementById('btnSubmitManualAnswer');
    const btnSkipQuestion = document.getElementById('btnSkipQuestion');
    const btnFinishEarly = document.getElementById('btnFinishEarly');
    const transcriptContainer = document.getElementById('transcriptContainer');
    const transcriptPlaceholder = document.getElementById('transcriptPlaceholder');
    const liveTranscriptBox = document.getElementById('liveTranscriptBox');
    const lblLiveTranscriptText = document.getElementById('lblLiveTranscriptText');
    const transcriptCountBadge = document.getElementById('transcriptCountBadge');
    const quickDemoButtons = document.querySelectorAll('.btn-quick-demo');

    // State Variables
    let currentState = null;
    let timerInterval = null;
    let remainingSeconds = configuredSeconds;
    let totalSecondsForQuestion = configuredSeconds;
    let turnCounter = 0;
    const CIRCLE_CIRCUMFERENCE = 2 * Math.PI * 54; // 339.292

    // Token CSRF
    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    // Khởi tạo vòng tròn đếm ngược
    if (countdownProgress) {
        countdownProgress.style.strokeDasharray = `${CIRCLE_CIRCUMFERENCE}`;
        countdownProgress.style.strokeDashoffset = '0';
    }

    // Khởi động
    initRoom();

    async function initRoom() {
        bindEvents();
        await loadOrStartState();
    }

    function bindEvents() {
        if (btnVoiceToggle) {
            btnVoiceToggle.addEventListener('click', () => {
                if (window.AiStt.isListening()) {
                    finishListeningAndSubmit();
                } else {
                    startListening();
                }
            });
        }

        if (btnReplayAudio) {
            btnReplayAudio.addEventListener('click', () => {
                if (currentState?.currentQuestionText) {
                    window.AiStt.stop();
                    readQuestionWithTts(currentState.currentQuestionText);
                }
            });
        }

        if (btnToggleTextInput) {
            btnToggleTextInput.addEventListener('click', () => {
                fallbackInputContainer.classList.toggle('d-none');
                if (!fallbackInputContainer.classList.contains('d-none')) {
                    txtManualAnswer.focus();
                }
            });
        }

        if (btnSubmitManualAnswer) {
            btnSubmitManualAnswer.addEventListener('click', () => {
                const text = txtManualAnswer.value.trim();
                if (!text) {
                    alert('Vui lòng nhập câu trả lời trước khi gửi.');
                    return;
                }
                submitAnswer(text);
                txtManualAnswer.value = '';
                fallbackInputContainer.classList.add('d-none');
            });
        }

        if (btnSkipQuestion) {
            btnSkipQuestion.addEventListener('click', async () => {
                if (confirm('Bạn có chắc chắn muốn bỏ qua câu hỏi này?')) {
                    await skipQuestion();
                }
            });
        }

        if (btnFinishEarly) {
            btnFinishEarly.addEventListener('click', async () => {
                if (confirm('Bạn có muốn dừng sớm và hoàn thành phiên thi ngay bây giờ?')) {
                    await finishSession();
                }
            });
        }

        // Demo kịch bản nhanh
        quickDemoButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                const demoType = btn.dataset.demo;
                let sampleText = '';
                if (demoType === 'good') {
                    sampleText = 'Theo em, kiến trúc 3 lớp giúp phân tách rõ ràng trách nhiệm giữa Presentation, Business Logic và Data Access, giúp hệ thống dễ mở rộng, bảo trì và viết Unit Test độc lập.';
                } else if (demoType === 'vague') {
                    sampleText = 'Kiến trúc này dùng để phân chia các tầng code cho gọn.';
                } else {
                    sampleText = 'Em chưa nắm rõ câu hỏi này, xin phép qua câu tiếp theo.';
                }
                submitAnswer(sampleText);
            });
        });
    }

    async function loadOrStartState() {
        setAiState('Đang tải trạng thái...', 'text-secondary');
        try {
            const resp = await fetch(`?handler=State&sessionId=${sessionId}`);
            if (resp.ok) {
                const state = await resp.json();
                applyState(state);
            } else {
                // Thử start nếu chưa có state
                const startResp = await fetch(`?handler=Start&sessionId=${sessionId}`, {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': csrfToken }
                });
                if (startResp.ok) {
                    const state = await startResp.json();
                    applyState(state);
                }
            }
        } catch (err) {
            console.error('Lỗi tải trạng thái:', err);
            setAiState('Lỗi kết nối server', 'text-danger');
        }
    }

    function applyState(state) {
        if (!state) return;
        currentState = state;

        // Kiểm tra phiên thi đã xong chưa
        if (state.state === 5 || state.state === 'Finished') { // Finished
            handleSessionFinished();
            return;
        }

        // Cập nhật tiến độ
        const currentQNum = Math.min(state.questionIndex + 1, state.questionCount);
        lblProgressQuestion.textContent = `Câu ${currentQNum}/${state.questionCount}`;

        // Cập nhật câu hỏi
        lblQuestionText.textContent = state.currentQuestionText || 'Đang chờ câu hỏi...';

        // Huy hiệu loại câu hỏi
        if (state.isFollowUp) {
            badgeQuestionType.className = 'badge rounded-pill bg-warning text-dark px-3 py-2';
            badgeQuestionType.innerHTML = '<i class="bi bi-arrow-repeat me-1"></i> AI Hỏi xoáy';
            badgeFollowUpCount.classList.remove('d-none');
            lblFollowUpNumber.textContent = `${state.followUpCount}/${state.maxFollowUps}`;
        } else {
            badgeQuestionType.className = 'badge rounded-pill bg-primary px-3 py-2';
            badgeQuestionType.innerHTML = '<i class="bi bi-patch-question me-1"></i> Câu hỏi chính';
            badgeFollowUpCount.classList.add('d-none');
        }

        // Thêm tin nhắn câu hỏi vào transcript
        appendAiQuestionToTranscript(state.currentQuestionText, state.isFollowUp);

        // Khởi động đếm ngược
        totalSecondsForQuestion = state.remainingSeconds > 0 ? state.remainingSeconds : configuredSeconds;
        startCountdown(state.remainingSeconds > 0 ? state.remainingSeconds : configuredSeconds);

        // Đọc câu hỏi bằng giọng nói
        readQuestionWithTts(state.currentQuestionText);
    }

    function readQuestionWithTts(text) {
        setAiState('AI đang đọc câu hỏi...', 'text-primary');
        setMicStatus('Chờ AI đọc xong...', 'text-muted');

        window.AiTts.speak(text, () => {
            setAiState('AI đang nói...', 'text-primary');
        }, () => {
            // Khi đọc xong -> Tự động bật Mic để thí sinh trả lời
            setAiState('Đến lượt bạn trả lời!', 'text-success');
            startListening();
        });
    }

    function startListening() {
        window.AiTts.stop();

        const success = window.AiStt.start({
            onStart: () => {
                setMicStatus('Micro đang lắng nghe...', 'text-success');
                btnVoiceToggle.classList.add('recording');
                liveTranscriptBox.classList.remove('d-none');
                lblLiveTranscriptText.textContent = 'Hãy nói câu trả lời của bạn...';
            },
            onInterim: (text) => {
                lblLiveTranscriptText.textContent = text || '...';
            },
            onSilence: (finalText) => {
                // Im lặng 3.5s -> tự động nộp bài
                if (finalText && finalText.trim().length > 0) {
                    finishListeningAndSubmit();
                }
            },
            onError: (err) => {
                console.warn('STT Error:', err);
                setMicStatus('Micro tạm dừng', 'text-warning');
                btnVoiceToggle.classList.remove('recording');
            },
            onEnd: () => {
                btnVoiceToggle.classList.remove('recording');
            }
        });

        if (!success) {
            setMicStatus('Không thể bật STT, dùng ô nhập chữ', 'text-danger');
            fallbackInputContainer.classList.remove('d-none');
        }
    }

    function finishListeningAndSubmit() {
        window.AiStt.stop();
        btnVoiceToggle.classList.remove('recording');
        const transcript = window.AiStt.getLastTranscript() || lblLiveTranscriptText.textContent;
        liveTranscriptBox.classList.add('d-none');

        if (transcript && transcript !== 'Hãy nói câu trả lời của bạn...' && transcript !== '...') {
            submitAnswer(transcript);
        } else {
            setMicStatus('Chưa ghi nhận câu trả lời', 'text-warning');
        }
    }

    // Server trả lỗi dạng { error } (mã 400/404/409/502) hoặc thân rỗng (500, hoặc 400 khi token chống giả mạo hết hạn).
    // Đọc đúng trường và không bao giờ để việc đọc JSON tự nó văng lỗi khó hiểu.
    async function readServerError(resp) {
        let detail = '';
        try {
            const body = await resp.json();
            detail = body.error || body.message || '';
        } catch (e) { /* thân phản hồi không phải JSON */ }

        if (detail) return detail;
        if (resp.status === 400) return 'Trang đã hết hạn (máy chủ vừa khởi động lại). Hãy tải lại trang (F5) rồi trả lời lại.';
        if (resp.status >= 500) return 'Máy chủ gặp lỗi (HTTP ' + resp.status + '). Hãy thử lại sau ít phút.';
        return 'Lỗi gửi câu trả lời (HTTP ' + resp.status + ')';
    }

    async function submitAnswer(transcript) {
        stopCountdown();
        window.AiTts.stop();
        window.AiStt.stop();
        btnVoiceToggle.classList.remove('recording');
        liveTranscriptBox.classList.add('d-none');

        // Hiển thị câu trả lời vào Transcript
        appendUserAnswerToTranscript(transcript);

        setAiState('AI đang đánh giá câu trả lời...', 'text-warning');
        setMicStatus('Đang xử lý...', 'text-muted');

        const turnId = generateUuid();
        try {
            const resp = await fetch(`?handler=SubmitAnswer&sessionId=${sessionId}&turnId=${turnId}&transcript=${encodeURIComponent(transcript)}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify({
                    sessionId: sessionId,
                    turnId: turnId,
                    transcript: transcript
                })
            });

            if (!resp.ok) {
                throw new Error(await readServerError(resp));
            }

            const result = await resp.json();

            // Nếu có nhận xét / lý do hỏi xoáy
            if (result.decision?.reason) {
                appendAiFeedbackToTranscript(result.decision.reason, result.decision.needFollowUp);
            }

            // Chuyển sang state mới
            applyState(result.state);
        } catch (err) {
            console.error('Lỗi submitAnswer:', err);
            alert('Lỗi: ' + err.message);
            setAiState('Gặp lỗi khi nộp bài', 'text-danger');
        }
    }

    async function skipQuestion() {
        stopCountdown();
        window.AiTts.stop();
        window.AiStt.stop();

        try {
            const resp = await fetch(`?handler=Skip&sessionId=${sessionId}`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': csrfToken }
            });
            if (resp.ok) {
                const newState = await resp.json();
                applyState(newState);
            }
        } catch (err) {
            console.error('Lỗi skip:', err);
        }
    }

    async function finishSession() {
        stopCountdown();
        window.AiTts.stop();
        window.AiStt.stop();

        try {
            await fetch(`?handler=Finish&sessionId=${sessionId}`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': csrfToken }
            });
            handleSessionFinished();
        } catch (err) {
            console.error('Lỗi finish:', err);
        }
    }

    function handleSessionFinished() {
        stopCountdown();
        window.AiTts.stop();
        window.AiStt.stop();

        lblQuestionText.textContent = 'Buổi phỏng vấn đã kết thúc!';
        setAiState('Đã hoàn thành', 'text-success');
        setMicStatus('Đã kết thúc', 'text-muted');

        const modalEl = document.getElementById('sessionCompletedModal');
        if (modalEl) {
            const modal = new bootstrap.Modal(modalEl);
            modal.show();
        }
    }

    // Timer & Countdown
    function startCountdown(seconds) {
        stopCountdown();
        remainingSeconds = seconds;
        updateTimerDisplay();

        timerInterval = setInterval(() => {
            remainingSeconds--;
            updateTimerDisplay();

            if (remainingSeconds <= 0) {
                stopCountdown();
                handleTimeout();
            }
        }, 1000);
    }

    function stopCountdown() {
        if (timerInterval) {
            clearInterval(timerInterval);
            timerInterval = null;
        }
    }

    function updateTimerDisplay() {
        lblRemainingSeconds.textContent = Math.max(0, remainingSeconds);

        const fraction = totalSecondsForQuestion > 0 ? (remainingSeconds / totalSecondsForQuestion) : 0;
        const offset = CIRCLE_CIRCUMFERENCE * (1 - fraction);
        if (countdownProgress) {
            countdownProgress.style.strokeDashoffset = `${offset}`;

            if (remainingSeconds <= 10) {
                countdownProgress.style.stroke = '#ef4444'; // Đỏ
                lblRemainingSeconds.className = 'display-6 fw-bold text-danger';
            } else if (remainingSeconds <= 25) {
                countdownProgress.style.stroke = '#f59e0b'; // Vàng
                lblRemainingSeconds.className = 'display-6 fw-bold text-warning';
            } else {
                countdownProgress.style.stroke = '#10b981'; // Xanh
                lblRemainingSeconds.className = 'display-6 fw-bold text-dark';
            }
        }
    }

    function handleTimeout() {
        setAiState('Hết giờ trả lời!', 'text-danger');
        const transcript = window.AiStt.getLastTranscript();
        if (transcript && transcript.trim().length > 0) {
            submitAnswer(transcript);
        } else {
            submitAnswer('Hết giờ trả lời, chưa kịp hoàn thành.');
        }
    }

    // UI Helpers
    function setAiState(text, colorClass) {
        lblAiState.textContent = text;
        lblAiState.className = colorClass;
    }

    function setMicStatus(text, colorClass) {
        lblMicStatus.textContent = text;
        lblMicStatus.className = `fw-bold ${colorClass}`;
    }

    function appendAiQuestionToTranscript(text, isFollowUp) {
        if (transcriptPlaceholder) transcriptPlaceholder.classList.add('d-none');
        turnCounter++;
        transcriptCountBadge.textContent = `${turnCounter} lượt`;

        const div = document.createElement('div');
        div.className = 'chat-bubble ai-bubble mb-3';
        div.innerHTML = `
            <div class="small fw-semibold text-primary mb-1">
                <i class="bi bi-robot me-1"></i> Giám khảo AI ${isFollowUp ? '<span class="badge bg-warning text-dark ms-1">Hỏi xoáy</span>' : ''}
            </div>
            <div class="text-dark">${escapeHtml(text)}</div>
        `;
        transcriptContainer.appendChild(div);
        transcriptContainer.scrollTop = transcriptContainer.scrollHeight;
    }

    function appendUserAnswerToTranscript(text) {
        const div = document.createElement('div');
        div.className = 'chat-bubble user-bubble mb-3';
        div.innerHTML = `
            <div class="small fw-semibold text-secondary mb-1">
                <i class="bi bi-person-fill me-1"></i> Bạn
            </div>
            <div class="text-dark">${escapeHtml(text)}</div>
        `;
        transcriptContainer.appendChild(div);
        transcriptContainer.scrollTop = transcriptContainer.scrollHeight;
    }

    function appendAiFeedbackToTranscript(reason, needFollowUp) {
        const div = document.createElement('div');
        div.className = `chat-bubble feedback-bubble mb-3 border-start border-4 ${needFollowUp ? 'border-warning bg-warning bg-opacity-10' : 'border-success bg-success bg-opacity-10'}`;
        div.innerHTML = `
            <div class="small fw-semibold ${needFollowUp ? 'text-warning' : 'text-success'} mb-1">
                <i class="bi ${needFollowUp ? 'bi-exclamation-circle' : 'bi-check-circle'} me-1"></i> Nhận xét của AI
            </div>
            <div class="small text-muted">${escapeHtml(reason)}</div>
        `;
        transcriptContainer.appendChild(div);
        transcriptContainer.scrollTop = transcriptContainer.scrollHeight;
    }

    function generateUuid() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function escapeHtml(str) {
        if (!str) return '';
        const p = document.createElement('p');
        p.textContent = str;
        return p.innerHTML;
    }
})();
