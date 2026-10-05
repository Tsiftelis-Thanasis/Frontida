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

/* ─── Scroll-based Animations ────────────────────────────────────────────── */
document.addEventListener("DOMContentLoaded", function () {
    // Check if Intersection Observer is supported
    if (!("IntersectionObserver" in window)) {
        console.log("Intersection Observer not supported");
        return;
    }

    // Create observer for scroll animations
    var observerOptions = {
        threshold: 0.1,
        rootMargin: "0px 0px -50px 0px"
    };

    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add("animate-fade-in-up");
                entry.target.style.opacity = "1";
                // Optional: stop observing after animation
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);

    // Observe cards and similar elements
    var elementsToAnimate = document.querySelectorAll(
        ".caregiver-card:not(.animate-fade-in-up), " +
        ".post-card:not(.animate-fade-in-up), " +
        ".t4-feature-card:not(.animate-fade-in-up), " +
        ".service-card:not(.animate-fade-in-up), " +
        ".cta-card:not(.animate-fade-in-up)"
    );

    elementsToAnimate.forEach(function (el, index) {
        // Set initial opacity to 0 for scroll animation
        if (!el.classList.contains("animate-fade-in-up")) {
            el.style.opacity = "0";
            el.style.animationDelay = (index * 0.05) + "s";
            observer.observe(el);
        }
    });
});

/* ─── Icon Animation on Hover ────────────────────────────────────────────── */
document.addEventListener("DOMContentLoaded", function () {
    // Add rotate animation to icons on hover
    var icons = document.querySelectorAll(
        ".svc-icon-lg, " +
        ".trust-icon, " +
        ".fa-star, " +
        ".fa-circle-check"
    );

    icons.forEach(function (icon) {
        icon.addEventListener("mouseenter", function () {
            this.style.transition = "transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1)";
            this.style.transform = "scale(1.1) rotate(8deg)";
        });

        icon.addEventListener("mouseleave", function () {
            this.style.transform = "scale(1) rotate(0deg)";
        });
    });
});

/* ─── Staggered Grid Animations ──────────────────────────────────────────── */
document.addEventListener("DOMContentLoaded", function () {
    // Add staggered animation delays to card grids
    var cardGrids = document.querySelectorAll(".row g-3, .row g-4");

    cardGrids.forEach(function (grid) {
        var cards = grid.querySelectorAll(".col-sm-6, .col-lg-4, .col-xl-4, .col-lg-3, .col-md-6");
        cards.forEach(function (card, index) {
            var delay = index * 50; // 50ms stagger
            card.style.animationDelay = (delay / 1000) + "s";
        });
    });
});

/* ─── Button Click Animation ─────────────────────────────────────────────── */
document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".btn").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            // Create ripple effect
            var ripple = document.createElement("span");
            var rect = this.getBoundingClientRect();
            var size = Math.max(rect.width, rect.height);
            var x = e.clientX - rect.left - size / 2;
            var y = e.clientY - rect.top - size / 2;

            ripple.style.width = ripple.style.height = size + "px";
            ripple.style.left = x + "px";
            ripple.style.top = y + "px";
            ripple.classList.add("ripple");

            // Only add ripple effect if button supports it (has overflow hidden)
            if (window.getComputedStyle(this).overflow === "hidden") {
                this.appendChild(ripple);
                setTimeout(function () {
                    ripple.remove();
                }, 600);
            }
        });
    });
});

/* ─── Smooth Page Load Animation ─────────────────────────────────────────── */
document.addEventListener("DOMContentLoaded", function () {
    // Animate hero section elements
    var heroElements = document.querySelectorAll(
        ".t4-hero h1, .t4-hero p, .t4-hero .t4-btn-white, .t4-hero .t4-btn-outline"
    );

    heroElements.forEach(function (el, index) {
        el.style.opacity = "0";
        el.style.animation = "fadeInUp 0.6s ease 0.1s" + (index * 0.1) + "s both";
    });
});
