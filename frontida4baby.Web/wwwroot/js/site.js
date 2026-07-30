// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// jQuery Validate's default "range" method reads a checkbox's static value="true"
// attribute (via .val()), not its checked state, then compares it as a string against
// data-val-range-min/max="True" (capitalized, from .NET's bool.ToString()). "true" is
// lexicographically greater than "True", so [Range(typeof(bool),"true","true")]
// checkboxes (e.g. "Accept Terms") always fail client-side, even when checked.
// Make the range validator checkbox-aware; leave numeric ranges untouched.
// Deferred to DOMContentLoaded: jquery.validate loads later in the page (in the
// per-view Scripts section, after this file), so jQuery.validator doesn't exist yet
// when this script itself is parsed.
// Password visibility toggle — any input wrapped in .password-input-wrapper
// with a sibling .password-toggle-btn gets a show/hide eye icon.
document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".password-toggle-btn").forEach(function (btn) {
        btn.addEventListener("click", function () {
            var input = btn.parentElement.querySelector("input");
            var icon = btn.querySelector("i");
            var willShow = input.type === "password";
            input.type = willShow ? "text" : "password";
            if (icon) {
                icon.classList.toggle("fa-eye", !willShow);
                icon.classList.toggle("fa-eye-slash", willShow);
            }
            btn.setAttribute("aria-label", willShow ? "Απόκρυψη κωδικού" : "Εμφάνιση κωδικού");
        });
    });
});

document.addEventListener("DOMContentLoaded", function () {
    if (window.jQuery && window.jQuery.validator) {
        var defaultRangeMethod = jQuery.validator.methods.range;
        jQuery.validator.methods.range = function (value, element, param) {
            if (element.type === "checkbox") {
                return element.checked;
            }
            return defaultRangeMethod.call(this, value, element, param);
        };
    }
});
