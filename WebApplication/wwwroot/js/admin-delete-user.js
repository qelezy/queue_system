(function () {
    const form = document.getElementById("deleteUserForm");
    if (!form) {
        return;
    }

    const deleteUserBaseUrl = window.AppConfig?.deleteUserBaseUrl ?? "";
    const successRedirect = window.AppConfig?.adminDeleteSuccessRedirect ?? "/Admin/Index";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    if (!deleteUserBaseUrl) {
        return;
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const userId = form.userId.value.trim();
        if (!userId) {
            return;
        }

        if (!window.confirm("Удалить пользователя? Это действие необратимо.")) {
            return;
        }

        try {
            const response = await fetch(`${deleteUserBaseUrl}/${encodeURIComponent(userId)}`, {
                method: "DELETE"
            });

            const result = await response.json();
            if (response.ok && result.success) {
                toastManager?.show(result.message || "Пользователь удален.", "success");
                form.reset();
                window.location.href = successRedirect;
                return;
            }

            const errors = result.errors?.join("\n") || "";
            toastManager?.show((result.message || "Ошибка удаления") + (errors ? "\n" + errors : ""), "error");
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при удалении пользователя.", "error");
        }
    });
})();
