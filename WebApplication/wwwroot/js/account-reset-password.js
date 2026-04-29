(function () {
    const form = document.getElementById("resetForm");
    if (!form) {
        return;
    }

    const resetUrl = window.AppConfig?.resetUrl ?? "";
    const userId = window.AppConfig?.resetUserId ?? "";
    const passwordResetToken = window.AppConfig?.resetToken ?? "";
    const loginPageUrl = window.AppConfig?.loginPageUrl ?? "/Account/Login";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    if (!resetUrl || !userId || !passwordResetToken) {
        return;
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const newPassword = form.newPassword.value;
        const confirmPassword = form.confirmPassword.value;

        if (newPassword !== confirmPassword) {
            toastManager?.show("Пароли не совпадают.", "error");
            return;
        }

        try {
            const response = await fetch(resetUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ userId, passwordResetToken, newPassword })
            });

            const result = await response.json();
            if (response.ok && result.success) {
                toastManager?.show(result.message || "Пароль успешно изменен", "success");
                window.location.href = loginPageUrl;
                return;
            }

            const errors = result.errors?.join("\n") || "";
            toastManager?.show((result.message || "Ошибка сброса пароля") + (errors ? "\n" + errors : ""), "error");
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при попытке сброса пароля", "error");
        }
    });
})();
