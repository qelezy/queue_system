(async function () {
    const status = document.getElementById("status");
    if (!status) {
        return;
    }

    const baseUrl = window.AppConfig?.confirmEmailUrl ?? "";
    const userId = window.AppConfig?.confirmEmailUserId ?? "";
    const token = window.AppConfig?.confirmEmailToken ?? "";
    const loginPageUrl = window.AppConfig?.loginPageUrl ?? "/Account/Login";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    if (!baseUrl || !userId || !token) {
        toastManager?.show("Недостаточно данных для подтверждения email.", "error");
        status.innerText = "Недостаточно данных.";
        return;
    }

    try {
        const requestUrl = `${baseUrl}?userId=${encodeURIComponent(userId)}&token=${encodeURIComponent(token)}`;
        const response = await fetch(requestUrl);
        const result = await response.json();

        if (response.ok && result.success) {
            toastManager?.show(result.message || "Email успешно подтвержден", "success");
            status.innerText = "Email подтвержден.";
            window.setTimeout(() => {
                window.location.href = loginPageUrl;
            }, 1200);
            return;
        }

        const errors = result.errors ? result.errors.join(", ") : "";
        toastManager?.show((result.message || "Ошибка подтверждения") + (errors ? `: ${errors}` : ""), "error");
        status.innerText = "Ошибка подтверждения.";
    } catch {
        toastManager?.show("Произошла ошибка при подключении к серверу.", "error");
        status.innerText = "Ошибка подключения.";
    }
})();
