(function () {
    function createManager(container) {
        const maxToasts = Math.max(1, Number.parseInt(container.dataset.maxToasts || "3", 10) || 3);
        const autoCloseMs = Math.max(500, Number.parseInt(container.dataset.autoCloseMs || "3000", 10) || 3000);

        function hideToastSmooth(toast) {
            if (!(toast instanceof HTMLElement) || !toast.isConnected || toast.classList.contains("is-hiding")) return;
            toast.classList.add("is-hiding");
            window.setTimeout(() => toast.remove(), 260);
        }

        function show(message, type) {
            if (!message) return;

            while (container.children.length >= maxToasts) {
                container.firstElementChild?.remove();
            }

            const normalizedType = type === "success" ? "success" : "error";
            const toast = document.createElement("div");
            toast.className = `app-toast ${normalizedType === "success" ? "app-toast--success" : "app-toast--error"}`;
            toast.setAttribute("role", normalizedType === "success" ? "status" : "alert");
            toast.innerHTML = `
                <span class="app-toast__text">${message}</span>
                <button type="button" class="app-toast__close" aria-label="Закрыть уведомление">×</button>
            `;

            toast.querySelector(".app-toast__close")?.addEventListener("click", () => hideToastSmooth(toast));
            container.appendChild(toast);

            window.setTimeout(() => hideToastSmooth(toast), autoCloseMs);
        }

        function flushInitialSuccess() {
            const initialSuccessMessage = (container.dataset.successMessage || "").trim();
            if (!initialSuccessMessage) return;
            show(initialSuccessMessage, "success");
            container.dataset.successMessage = "";
        }

        return { show, flushInitialSuccess };
    }

    function getManager(containerId) {
        const container = document.getElementById(containerId);
        if (!(container instanceof HTMLElement)) return null;

        const existing = container.__toastManager;
        if (existing) return existing;

        const manager = createManager(container);
        container.__toastManager = manager;
        return manager;
    }

    window.AppToasts = {
        getManager
    };
})();
