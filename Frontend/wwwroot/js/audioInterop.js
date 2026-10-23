// [EDUCATIONAL NOTE - Lab 1 S1 & Lab 8 S2]:
// JavaScript Interop for High-Performance HTML5 Audio Streaming:
// 1. HTTP 206 Byte-Range Buffering: The browser's HTML5 <audio> engine handles range requests
//    natively. The 'audio.buffered' property exposes TimeRanges indicating which byte chunks
//    have arrived over HTTP 206 Partial Content.
// 2. Telemetry (Time-to-First-Frame): We measure latency from 'loadstart' until 'canplay' / 'playing',
//    exposing real-time streaming latency (ms) directly to the student UI.
// 3. Next-Track Prefetching: At 75% playback progress, we inject an HTTP 206 prefetch to warm
//    the browser's local cache before the user or auto-play transitions to the next song.

window.audioInterop = {
    audioElement: null,
    dotNetHelper: null,
    loadStartTime: 0,
    hasReportedLatency: false,

    setupAudio: function (audioElement, dotNetHelper) {
        if (!audioElement) return;

        this.audioElement = audioElement;
        this.dotNetHelper = dotNetHelper;
        this.loadStartTime = performance.now();
        this.hasReportedLatency = false;

        audioElement.addEventListener('loadstart', () => {
            window.audioInterop.loadStartTime = performance.now();
            window.audioInterop.hasReportedLatency = false;
        });

        audioElement.addEventListener('canplay', () => {
            if (!window.audioInterop.hasReportedLatency && window.audioInterop.loadStartTime > 0) {
                const latency = Math.round(performance.now() - window.audioInterop.loadStartTime);
                window.audioInterop.hasReportedLatency = true;
                if (window.audioInterop.dotNetHelper) {
                    window.audioInterop.dotNetHelper.invokeMethodAsync('OnPlaybackLatencyReady', latency);
                }
            }
        });

        audioElement.addEventListener('timeupdate', () => {
            if (!window.audioInterop.dotNetHelper) return;
            const current = audioElement.currentTime || 0;
            const duration = audioElement.duration || 0;
            const bufferPercent = window.audioInterop.calculateBufferedPercent(audioElement);

            window.audioInterop.dotNetHelper.invokeMethodAsync('OnTimeUpdate', current, duration, bufferPercent);
        });

        audioElement.addEventListener('progress', () => {
            if (!window.audioInterop.dotNetHelper) return;
            const current = audioElement.currentTime || 0;
            const duration = audioElement.duration || 0;
            const bufferPercent = window.audioInterop.calculateBufferedPercent(audioElement);

            window.audioInterop.dotNetHelper.invokeMethodAsync('OnBufferProgress', bufferPercent);
        });

        audioElement.addEventListener('ended', () => {
            if (window.audioInterop.dotNetHelper) {
                window.audioInterop.dotNetHelper.invokeMethodAsync('OnTrackEnded');
            }
        });

        audioElement.addEventListener('waiting', () => {
            if (window.audioInterop.dotNetHelper) {
                window.audioInterop.dotNetHelper.invokeMethodAsync('OnBufferingStateChanged', true);
            }
        });

        audioElement.addEventListener('playing', () => {
            if (window.audioInterop.dotNetHelper) {
                window.audioInterop.dotNetHelper.invokeMethodAsync('OnBufferingStateChanged', false);
            }
        });

        audioElement.addEventListener('error', (e) => {
            console.warn('[audioInterop] HTML5 Audio Error:', audioElement.error);
            if (window.audioInterop.dotNetHelper) {
                window.audioInterop.dotNetHelper.invokeMethodAsync('OnAudioError', audioElement.error ? audioElement.error.message : 'Unknown error');
            }
        });
    },

    play: function (audioElement) {
        const el = audioElement || this.audioElement;
        if (el) {
            const promise = el.play();
            if (promise !== undefined) {
                promise.catch(err => {
                    console.log('[audioInterop] Autoplay prevented or aborted:', err.message);
                });
            }
        }
    },

    pause: function (audioElement) {
        const el = audioElement || this.audioElement;
        if (el) {
            el.pause();
        }
    },

    seek: function (audioElement, seconds) {
        const el = audioElement || this.audioElement;
        if (el && Number.isFinite(seconds)) {
            el.currentTime = seconds;
        }
    },

    setVolume: function (audioElement, volume) {
        const el = audioElement || this.audioElement;
        if (el && Number.isFinite(volume)) {
            el.volume = Math.max(0, Math.min(1, volume));
        }
    },

    calculateBufferedPercent: function (audio) {
        if (!audio || !audio.duration || audio.duration <= 0) return 0;
        const buffered = audio.buffered;
        if (!buffered || buffered.length === 0) return 0;

        // Find the buffer range covering the current time position
        const current = audio.currentTime;
        for (let i = 0; i < buffered.length; i++) {
            if (buffered.start(i) <= current && current <= buffered.end(i)) {
                return Math.min(100, Math.round((buffered.end(i) / audio.duration) * 100));
            }
        }

        // Otherwise return the highest buffered end relative to duration
        const lastEnd = buffered.end(buffered.length - 1);
        return Math.min(100, Math.round((lastEnd / audio.duration) * 100));
    },

    // [EDUCATIONAL NOTE - Lab 1 S1]:
    // Next-Track Prefetching:
    // Fetches initial audio byte chunk (first 256KB) using HTTP Range request
    // to prime the browser's HTTP cache. When user clicks Next or song finishes,
    // playback starts with near-zero latency.
    prefetchTrack: function (streamUrl) {
        if (!streamUrl) return;

        try {
            // Check if link prefetch already exists
            let existingLink = document.querySelector(`link[rel="prefetch"][href="${streamUrl}"]`);
            if (!existingLink) {
                const link = document.createElement('link');
                link.rel = 'prefetch';
                link.as = 'fetch';
                link.href = streamUrl;
                document.head.appendChild(link);
            }

            // Also send a byte-range prefetch probe
            fetch(streamUrl, {
                headers: { 'Range': 'bytes=0-262143' } // First 256KB
            }).then(resp => {
                console.log(`[audioInterop] Prefetched next track byte range (HTTP ${resp.status}): ${streamUrl}`);
            }).catch(err => {
                console.warn('[audioInterop] Prefetch probe ignored:', err.message);
            });
        } catch (e) {
            console.warn('[audioInterop] Prefetch exception:', e);
        }
    }
};
