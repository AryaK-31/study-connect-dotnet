// Small JavaScript helpers called from Blazor through IJSRuntime (JS interop).
window.studyConnect = (function () {
    var storageKey = "sc-theme";

    function readTheme() {
        try {
            return localStorage.getItem(storageKey);
        } catch (e) {
            return null;
        }
    }

    function applyTheme() {
        var theme = readTheme();
        if (!theme) {
            theme = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
        }
        document.documentElement.setAttribute("data-theme", theme);
        return theme;
    }

    function toggleTheme() {
        var current = document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
        var next = current === "dark" ? "light" : "dark";
        document.documentElement.setAttribute("data-theme", next);
        try {
            localStorage.setItem(storageKey, next);
        } catch (e) {
            /* private mode - theme just won't persist */
        }
        return next;
    }

    function scrollToBottom(element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    }

    // Creates a file in the browser and triggers a download (used for the CSV roster export).
    function downloadFile(fileName, contentType, content) {
        var blob = new Blob([content], { type: contentType });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }

    // Live password-strength meter for inputs marked with data-password-meter="<meter element id>".
    var rules = {
        length: function (p) { return p.length >= 8; },
        upper: function (p) { return /[A-Z]/.test(p); },
        lower: function (p) { return /[a-z]/.test(p); },
        digit: function (p) { return /\d/.test(p); },
        special: function (p) { return /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?~`]/.test(p); }
    };
    var colours = ["var(--danger)", "var(--danger)", "var(--warm)", "var(--warning)", "var(--accent)", "var(--success)"];

    function updatePasswordMeter(input) {
        var meter = document.getElementById(input.getAttribute("data-password-meter"));
        if (!meter) {
            return;
        }
        var value = input.value || "";
        var passed = 0;
        Object.keys(rules).forEach(function (name) {
            var ok = rules[name](value);
            if (ok) {
                passed++;
            }
            var item = meter.querySelector('[data-rule="' + name + '"]');
            if (item) {
                item.classList.toggle("ok", ok);
            }
        });
        var fill = meter.querySelector(".meter-fill");
        if (fill) {
            fill.style.width = (passed / 5 * 100) + "%";
            fill.style.background = colours[passed];
        }
    }

    document.addEventListener("input", function (event) {
        var target = event.target;
        if (target && target.hasAttribute && target.hasAttribute("data-password-meter")) {
            updatePasswordMeter(target);
        }
    });

    // Re-apply the theme after Blazor's enhanced navigation swaps page content.
    if (window.Blazor && typeof window.Blazor.addEventListener === "function") {
        window.Blazor.addEventListener("enhancedload", applyTheme);
    }

    applyTheme();

    return {
        applyTheme: applyTheme,
        toggleTheme: toggleTheme,
        scrollToBottom: scrollToBottom,
        downloadFile: downloadFile
    };
})();
