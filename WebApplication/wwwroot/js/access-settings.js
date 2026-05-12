(function () {
    var rootId = "access-matrix-root";

    function getRoot() {
        return document.getElementById(rootId);
    }

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : "";
    }

    function syncColToggles() {
        var root = getRoot();
        if (!root) {
            return;
        }
        root.querySelectorAll(".access-matrix__group").forEach(function (group) {
            if (!(group instanceof HTMLElement)) {
                return;
            }
            group.querySelectorAll(".access-matrix__col-toggle").forEach(function (toggle) {
                if (!(toggle instanceof HTMLInputElement)) {
                    return;
                }
                var role = toggle.getAttribute("data-role");
                if (!role) {
                    return;
                }
                var checks = group.querySelectorAll(
                    'tr[data-item-key] input.access-matrix__checkbox[data-role="' + role + '"]'
                );
                var list = Array.prototype.slice.call(checks);
                if (list.length === 0) {
                    toggle.checked = false;
                    toggle.indeterminate = false;
                    return;
                }
                var checkedCount = list.filter(function (i) {
                    return i.checked;
                }).length;
                toggle.checked = checkedCount === list.length;
                toggle.indeterminate = checkedCount > 0 && checkedCount < list.length;
            });
        });
    }

    function countCheckedPerRole(root) {
        var map = {};
        root.querySelectorAll("input.access-matrix__checkbox[data-role][data-item]").forEach(function (input) {
            if (!(input instanceof HTMLInputElement)) {
                return;
            }
            var role = input.getAttribute("data-role");
            if (!role) {
                return;
            }
            if (!map[role]) {
                map[role] = 0;
            }
            if (input.checked) {
                map[role] += 1;
            }
        });
        return map;
    }

    window.AccessSettingsUI = {
        save: async function () {
            var root = getRoot();
            if (!root) {
                return;
            }
            var toastManager = window.AppToasts && window.AppToasts.getManager("global-toast-stack");

            var perRole = countCheckedPerRole(root);
            var roles = Object.keys(perRole);
            for (var i = 0; i < roles.length; i++) {
                if (perRole[roles[i]] === 0) {
                    var msg =
                        "У каждой роли должно остаться хотя бы одно разрешение. Включите доступ хотя бы для одной строки.";
                    toastManager && toastManager.show(msg, "warning");
                    return;
                }
            }

            var entries = [];
            root.querySelectorAll("input.access-matrix__checkbox[data-role][data-item]").forEach(function (input) {
                if (!(input instanceof HTMLInputElement)) {
                    return;
                }
                entries.push({
                    role: input.getAttribute("data-role"),
                    item: input.getAttribute("data-item"),
                    granted: input.checked,
                });
            });

            var url = root.getAttribute("data-save-matrix-url");
            if (!url) {
                toastManager && toastManager.show("Не задан URL сохранения.", "warning");
                return;
            }

            try {
                var response = await fetch(url, {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        RequestVerificationToken: getAntiForgeryToken(),
                    },
                    body: JSON.stringify({ entries: entries }),
                });
                var data = null;
                try {
                    data = await response.json();
                } catch (e) {
                    data = null;
                }
                if (!response.ok) {
                    var errMsg =
                        (data && data.message) ||
                        "Не удалось сохранить настройки доступа.";
                    toastManager && toastManager.show(errMsg, "warning");
                    return;
                }
                toastManager && toastManager.show("Изменения сохранены.", "success");
            } catch (e) {
                toastManager && toastManager.show("Сеть недоступна или запрос прерван.", "warning");
            }
        },
    };

    document.addEventListener("DOMContentLoaded", function () {
        var root = getRoot();
        if (!root) {
            return;
        }

        root.addEventListener("change", function (e) {
            var target = e.target;
            if (!(target instanceof HTMLInputElement)) {
                return;
            }
            if (target.classList.contains("access-matrix__col-toggle")) {
                var role = target.getAttribute("data-role");
                if (!role) {
                    return;
                }
                var table = target.closest("table.access-matrix__table");
                if (!table) {
                    return;
                }
                var checked = target.checked;
                table.querySelectorAll(
                    'tr[data-item-key] input.access-matrix__checkbox[data-role="' + role + '"]'
                ).forEach(function (input) {
                    input.checked = checked;
                });
                syncColToggles();
                return;
            }
            if (target.classList.contains("access-matrix__checkbox")) {
                syncColToggles();
            }
        });

        syncColToggles();
    });
})();
