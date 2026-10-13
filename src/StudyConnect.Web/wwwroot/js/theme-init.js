// Runs before the page is painted so the saved theme is applied without a flash.
(function () {
    try {
        var theme = localStorage.getItem("sc-theme");
        if (!theme) {
            theme = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
        }
        document.documentElement.setAttribute("data-theme", theme);
    } catch (e) {
        document.documentElement.setAttribute("data-theme", "light");
    }
})();
