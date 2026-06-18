(function () {
    const pending = document.getElementById("confirmPending");
    const successBlock = document.getElementById("confirmSuccess");
    const caption = document.getElementById("confirmCaption");
    const errorBlock = document.getElementById("confirmError");
    const links = document.getElementById("confirmLinks");

    if (!pending || !successBlock || !caption || !errorBlock || !links) {
        return;
    }

    const baseUrl = window.AppConfig?.confirmEmailUrl ?? "";
    const userId = window.AppConfig?.confirmEmailUserId ?? "";
    const email = window.AppConfig?.confirmEmailEmail ?? "";
    const token = window.AppConfig?.confirmEmailToken ?? "";
    const loginPageUrl = window.AppConfig?.loginPageUrl ?? "/Account/Login";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");

    function showError(message) {
        pending.hidden = true;
        successBlock.hidden = true;
        errorBlock.hidden = false;
        errorBlock.textContent = message;
        links.hidden = false;
    }

    function showSuccess(message) {
        pending.hidden = true;
        errorBlock.hidden = true;
        links.hidden = true;
        successBlock.hidden = false;
        caption.textContent = message;
    }

    if (!baseUrl || !userId || !token) {
        toastManager?.show("Недостаточно данных для подтверждения email.", "error");
        showError("Недостаточно данных для подтверждения. Перейдите по ссылке из письма или обратитесь к администратору.");
        return;
    }

    (async function () {
        try {
            const emailParam = email ? `&email=${encodeURIComponent(email)}` : "";
            const requestUrl = `${baseUrl}?userId=${encodeURIComponent(userId)}${emailParam}&token=${encodeURIComponent(token)}`;
            const response = await fetch(requestUrl);
            const result = await response.json();

            if (response.ok && result.success) {
                const message = result.message || "Email успешно подтверждён";
                showSuccess(message);
                window.setTimeout(function () {
                    window.location.href = loginPageUrl;
                }, 1200);
                return;
            }

            const errors = result.errors ? result.errors.join(", ") : "";
            const errorMessage = (result.message || "Ошибка подтверждения") + (errors ? `: ${errors}` : "");
            toastManager?.show(errorMessage, "error");
            showError(errorMessage);
        } catch {
            toastManager?.show("Произошла ошибка при подключении к серверу.", "error");
            showError("Не удалось связаться с сервером. Попробуйте позже.");
        }
    })();
})();
