(function () {
    if (!/\/ar(\/|$)/.test(location.pathname)) {
        return;
    }

    var marker = 'data-jeninnet-ar-nav';
    var arabicNav;

    function arabicTocUrl() {
        var index = location.pathname.indexOf('/ar/');
        var root = index >= 0 ? location.pathname.slice(0, index + 4) : '/ar/';
        return new URL(root + 'toc.html', location.origin).href;
    }

    function applyArabicNav() {
        var navbar = document.getElementById('navbar');
        if (!navbar) {
            return;
        }

        var existing = navbar.querySelectorAll(':scope > ul.navbar-nav');
        var hasArabic = false;
        existing.forEach(function (element) {
            if (element.getAttribute(marker) === 'true') {
                hasArabic = true;
            }
        });

        if (hasArabic) {
            existing.forEach(function (element) {
                if (element.getAttribute(marker) !== 'true') {
                    element.remove();
                }
            });
            return;
        }

        if (!arabicNav) {
            return;
        }

        existing.forEach(function (element) {
            element.remove();
        });

        var clone = arabicNav.cloneNode(true);
        clone.className = 'nav level1 navbar-nav';
        clone.setAttribute(marker, 'true');

        var base = new URL(arabicTocUrl());
        clone.querySelectorAll('a[href]').forEach(function (anchor) {
            var href = anchor.getAttribute('href');
            if (/^(?:[a-z]+:|\/|#)/i.test(href)) {
                return;
            }

            anchor.setAttribute('href', new URL(href, base).href);
        });

        navbar.appendChild(clone);
    }

    function start() {
        var navbar = document.getElementById('navbar');
        if (!navbar) {
            return;
        }

        fetch(arabicTocUrl(), { credentials: 'same-origin' })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Arabic TOC request failed.');
                }

                return response.text();
            })
            .then(function (html) {
                var document = new DOMParser().parseFromString(html, 'text/html');
                arabicNav = document.querySelector('#toc > ul');
                applyArabicNav();
            })
            .catch(function () {
                return;
            });

        var observer = new MutationObserver(applyArabicNav);
        observer.observe(navbar, { childList: true, subtree: true });
        window.setTimeout(function () {
            observer.disconnect();
            applyArabicNav();
        }, 5000);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
}());
