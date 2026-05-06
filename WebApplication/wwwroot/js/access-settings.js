(function () {
    var rootId = "access-matrix-root";

    function getRoot() {
        return document.getElementById(rootId);
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

    window.AccessSettingsUI = {
        save: function () {
            var root = getRoot();
            if (!root) {
                return;
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
            var toastManager = window.AppToasts && window.AppToasts.getManager("global-toast-stack");
            var msg = "Изменения сохранены (прототип, " + entries.length + " ячеек).";
            toastManager && toastManager.show(msg, "success");
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
