window.kaizen = window.kaizen || {};
kaizen.spinner = (function () {
    const scriptEl = document.currentScript;
    const selectedSpinner = scriptEl?.dataset.spinner || "ring";
    const enableSound = scriptEl?.dataset.sound === "true";
    const showDelay = parseInt(scriptEl?.dataset.showDelay || "250", 10);

    let timeoutId = null;
    let showDelayId = null;
    let clockIntervalId = null;
    let showClock = false;
    let styleInjected = false;
    let audioCtx = null;
    let humNodes = null;

    function startHum() {
        stopHum();
        try {
            audioCtx = audioCtx || new AudioContext();
            if (audioCtx.state === "suspended") audioCtx.resume();

            const gain = audioCtx.createGain();
            gain.gain.setValueAtTime(0, audioCtx.currentTime);
            gain.gain.linearRampToValueAtTime(0.06, audioCtx.currentTime + 0.3);
            gain.connect(audioCtx.destination);

            const osc1 = audioCtx.createOscillator();
            osc1.type = "sine";
            osc1.frequency.value = 90;
            osc1.connect(gain);
            osc1.start();

            const osc2 = audioCtx.createOscillator();
            osc2.type = "sine";
            osc2.frequency.value = 180;
            const gain2 = audioCtx.createGain();
            gain2.gain.value = 0.3;
            osc2.connect(gain2);
            gain2.connect(gain);
            osc2.start();

            humNodes = { gain, osc1, osc2, gain2 };
        } catch (e) {
            // audio unavailable
        }
    }

    function stopHum() {
        if (!humNodes) return;
        try {
            humNodes.osc1.stop();
            humNodes.osc2.stop();
            humNodes.gain.disconnect();
            humNodes.gain2.disconnect();
        } catch (e) {
            // already stopped
        }
        humNodes = null;
    }

    // Each spinner builds its DOM imperatively (no innerHTML / no untrusted strings).
    const spinners = {
        ring: {
            css: `
                .form-spinner-anim {
                    width: 40px;
                    height: 40px;
                    border: 3px solid rgba(0, 0, 0, 0.1);
                    border-top-color: var(--accent, #333);
                    border-radius: 50%;
                    animation: fs-ring 0.8s linear infinite;
                }
                @keyframes fs-ring {
                    to { transform: rotate(360deg); }
                }
            `,
            build: () => {
                const d = document.createElement("div");
                d.className = "form-spinner-anim";
                return d;
            }
        },

        pulse: {
            css: `
                .form-spinner-anim {
                    display: flex;
                    gap: 8px;
                }
                .form-spinner-anim span {
                    width: 12px;
                    height: 12px;
                    background: var(--accent, #333);
                    border-radius: 50%;
                    animation: fs-pulse 1.2s ease-in-out infinite;
                }
                .form-spinner-anim span:nth-child(2) { animation-delay: 0.15s; }
                .form-spinner-anim span:nth-child(3) { animation-delay: 0.3s; }
                @keyframes fs-pulse {
                    0%, 80%, 100% { transform: scale(0.4); opacity: 0.4; }
                    40% { transform: scale(1); opacity: 1; }
                }
            `,
            build: () => {
                const d = document.createElement("div");
                d.className = "form-spinner-anim";
                for (let i = 0; i < 3; i++) d.appendChild(document.createElement("span"));
                return d;
            }
        },

        bolt: {
            css: `
                .form-spinner-anim {
                    width: 48px;
                    height: 48px;
                    animation: fs-bolt 1.5s ease-in-out infinite;
                }
                .form-spinner-anim svg {
                    width: 100%;
                    height: 100%;
                    filter: drop-shadow(0 0 6px var(--accent-kicker, #e2c16f));
                }
                @keyframes fs-bolt {
                    0%, 100% { opacity: 0.3; transform: scale(0.9); }
                    20% { opacity: 1; transform: scale(1.1); }
                    40% { opacity: 0.6; transform: scale(1); }
                    60% { opacity: 1; transform: scale(1.05); }
                    80% { opacity: 0.4; transform: scale(0.95); }
                }
            `,
            build: () => {
                const d = document.createElement("div");
                d.className = "form-spinner-anim";
                const ns = "http://www.w3.org/2000/svg";
                const svg = document.createElementNS(ns, "svg");
                svg.setAttribute("viewBox", "0 0 24 24");
                svg.setAttribute("fill", "var(--accent, #333)");
                const path = document.createElementNS(ns, "path");
                path.setAttribute("d", "M13 2L4.5 12.5h5.3L8.3 22l9.2-11.5h-5.8z");
                svg.appendChild(path);
                d.appendChild(svg);
                return d;
            }
        },

        bars: {
            css: `
                .form-spinner-anim {
                    display: flex;
                    gap: 4px;
                    align-items: center;
                    height: 40px;
                }
                .form-spinner-anim span {
                    width: 6px;
                    height: 16px;
                    background: var(--accent, #333);
                    border-radius: 3px;
                    animation: fs-bars 1s ease-in-out infinite;
                }
                .form-spinner-anim span:nth-child(2) { animation-delay: 0.1s; }
                .form-spinner-anim span:nth-child(3) { animation-delay: 0.2s; }
                .form-spinner-anim span:nth-child(4) { animation-delay: 0.3s; }
                .form-spinner-anim span:nth-child(5) { animation-delay: 0.4s; }
                @keyframes fs-bars {
                    0%, 100% { transform: scaleY(1); }
                    50% { transform: scaleY(2.2); }
                }
            `,
            build: () => {
                const d = document.createElement("div");
                d.className = "form-spinner-anim";
                for (let i = 0; i < 5; i++) d.appendChild(document.createElement("span"));
                return d;
            }
        }
    };

    const spinner = spinners[selectedSpinner] || spinners.ring;

    const baseCss = `
        .form-spinner-overlay {
            position: fixed;
            inset: 0;
            z-index: 9999;
            display: flex;
            align-items: center;
            justify-content: center;
            background: rgba(255, 255, 255, 0.6);
            backdrop-filter: blur(2px);
            opacity: 0;
            transition: opacity 0.15s ease;
            pointer-events: none;
        }
        .form-spinner-overlay--visible {
            opacity: 1;
            pointer-events: auto;
        }
        .form-spinner-content {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 1.5rem;
        }
        .form-spinner-timeout {
            display: none;
            text-align: center;
            font-size: 0.9rem;
            color: var(--text-secondary, #6b6b6b);
        }
        .form-spinner-timeout--visible {
            display: block;
        }
        .form-spinner-dismiss {
            margin-top: 0.5rem;
            padding: 0.5rem 1rem;
            border: 1px solid var(--border, #ccc);
            border-radius: 4px;
            background: white;
            cursor: pointer;
            font-size: 0.85rem;
        }
        .form-spinner-dismiss:hover {
            background: var(--surface-hover, #f5f5f5);
        }
        .form-spinner-clock {
            font-size: 1.1rem;
            font-variant-numeric: tabular-nums;
            color: var(--text-secondary, #6b6b6b);
        }
    `;

    function injectStyle() {
        if (styleInjected && document.getElementById("form-spinner-style")) return;
        const style = document.createElement("style");
        style.id = "form-spinner-style";
        style.textContent = baseCss + spinner.css;
        document.head.appendChild(style);
        styleInjected = true;
    }

    function getOrCreateOverlay() {
        let el = document.getElementById("form-spinner-overlay");
        if (el) return el;

        injectStyle();

        el = document.createElement("div");
        el.id = "form-spinner-overlay";
        el.className = "form-spinner-overlay";

        const content = document.createElement("div");
        content.className = "form-spinner-content";

        content.appendChild(spinner.build());

        const timeout = document.createElement("div");
        timeout.className = "form-spinner-timeout";

        const p = document.createElement("p");
        p.textContent = "Taking longer than expected…";
        timeout.appendChild(p);

        const dismiss = document.createElement("button");
        dismiss.type = "button";
        dismiss.className = "form-spinner-dismiss";
        dismiss.textContent = "Dismiss";
        dismiss.addEventListener("click", hide);
        timeout.appendChild(dismiss);

        content.appendChild(timeout);
        el.appendChild(content);
        document.body.appendChild(el);

        return el;
    }

    function show(form) {
        showClock = form?.hasAttribute("data-spinner-clock") || false;

        document.querySelectorAll('button[type="submit"], button:not([type])')
            .forEach(b => b.disabled = true);

        showDelayId = setTimeout(() => {
            showDelayId = null;
            const el = getOrCreateOverlay();

            if (showClock) {
                let seconds = 0;
                const clockEl = document.createElement("div");
                clockEl.className = "form-spinner-clock";
                clockEl.textContent = "0:00";
                el.querySelector(".form-spinner-content").appendChild(clockEl);
                clockIntervalId = setInterval(() => {
                    seconds++;
                    const m = Math.floor(seconds / 60);
                    const s = seconds % 60;
                    clockEl.textContent = `${m}:${s.toString().padStart(2, "0")}`;
                }, 1000);
            }

            // Force reflow so the transition runs.
            void el.offsetHeight;
            el.classList.add("form-spinner-overlay--visible");
            if (enableSound) try { startHum(); } catch (e) { /* audio unavailable */ }

            if (!showClock) {
                timeoutId = setTimeout(() => {
                    const msg = el.querySelector(".form-spinner-timeout");
                    if (msg) msg.classList.add("form-spinner-timeout--visible");
                }, 10000);
            }
        }, showDelay);
    }

    function hide() {
        if (showDelayId) {
            clearTimeout(showDelayId);
            showDelayId = null;
        }
        if (timeoutId) {
            clearTimeout(timeoutId);
            timeoutId = null;
        }
        if (clockIntervalId) {
            clearInterval(clockIntervalId);
            clockIntervalId = null;
        }

        const el = document.getElementById("form-spinner-overlay");
        if (el) el.remove();

        if (enableSound) try { stopHum(); } catch (e) { /* audio unavailable */ }

        document.querySelectorAll('button[type="submit"], button:not([type])')
            .forEach(b => b.disabled = false);
    }

    document.addEventListener("submit", (e) => {
        if (e.target.method !== "get") {
            show(e.target);
        }
    });

    Blazor.addEventListener("enhancedload", hide);

    return { show, hide };
})();
