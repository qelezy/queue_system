(function () {
    const form = document.getElementById("updateUserForm");
    if (!form) {
        return;
    }

    const updateUserBaseUrl = window.AppConfig?.updateUserBaseUrl ?? "";
    const successRedirect = window.AppConfig?.adminUpdateSuccessRedirect ?? "/Admin/Index";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    if (!updateUserBaseUrl) {
        return;
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();

        const userId = form.userId.value.trim();
        const payload = {
            username: form.username.value.trim(),
            email: form.email.value.trim(),
            role: form.role.value.trim(),
            password: form.password.value
        };

        try {
            const response = await fetch(`${updateUserBaseUrl}/${encodeURIComponent(userId)}`, {
                method: "PATCH",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            const result = await response.json();
            if (response.ok && result.success) {
                toastManager?.show("Пользователь обновлен.", "success");
                form.reset();
                window.location.href = successRedirect;
                return;
            }

            const errors = result.errors?.join("\n") || "";
            toastManager?.show((result.message || "Ошибка обновления") + (errors ? "\n" + errors : ""), "error");
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при обновлении пользователя.", "error");
        }
    });
})();
