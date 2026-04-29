(function () {
    const form = document.getElementById("forgotForm");
    if (!form) {
        return;
    }

    const forgotUrl = window.AppConfig?.forgotUrl ?? "";
    const successRedirect = window.AppConfig?.forgotSuccessRedirect ?? "/Account/Login";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    if (!forgotUrl) {
        return;
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const email = form.email.value;

        try {
            const response = await fetch(forgotUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ email })
            });

            const result = await response.json();
            if (response.ok && result.success) {
                toastManager?.show(result.message || "Ссылка для сброса пароля отправлена на ваш email.", "success");
                window.location.href = successRedirect;
                return;
            }

            const errors = result.errors?.join("\n") || "";
            toastManager?.show((result.message || "Ошибка запроса") + (errors ? "\n" + errors : ""), "error");
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при попытке отправки запроса", "error");
        }
    });
})();
