// Flexible date entry: accepts common formats, normalizes to YYYY-MM-DD on blur/submit.
// Missing month/day default to the 1st (e.g. "1950" => 1950-01-01, "Jul 1950" => 1950-07-01).
(function () {
    'use strict';

    var MONTHS = {
        jan: 1, january: 1, feb: 2, february: 2, mar: 3, march: 3,
        apr: 4, april: 4, may: 5, jun: 6, june: 6, jul: 7, july: 7,
        aug: 8, august: 8, sep: 9, sept: 9, september: 9,
        oct: 10, october: 10, nov: 11, november: 11, dec: 12, december: 12
    };

    function pad(n) {
        return (n < 10 ? '0' : '') + n;
    }

    function daysInMonth(year, month) {
        return new Date(year, month, 0).getDate();
    }

    function makeIso(year, month, day) {
        if (year < 1 || year > 9999 || month < 1 || month > 12) return null;
        if (day < 1 || day > daysInMonth(year, month)) return null;
        return year + '-' + pad(month) + '-' + pad(day);
    }

    // Returns: undefined for empty input, null for unparseable, or a YYYY-MM-DD string.
    function parse(raw) {
        if (raw == null) return undefined;
        var s = String(raw).trim();
        if (s === '') return undefined;

        var cleaned = s.replace(/,/g, ' ').replace(/\s+/g, ' ').trim();

        // ISO-ish, with optional month/day: YYYY, YYYY-MM, YYYY-MM-DD (also / or . separators).
        var iso = cleaned.match(/^(\d{4})(?:[-/.](\d{1,2})(?:[-/.](\d{1,2}))?)?$/);
        if (iso) {
            return makeIso(+iso[1], iso[2] ? +iso[2] : 1, iso[3] ? +iso[3] : 1);
        }

        // Numeric month-first: M/YYYY or M/D/YYYY (also - or . separators).
        var us = cleaned.match(/^(\d{1,2})(?:[-/.](\d{1,2}))?[-/.](\d{4})$/);
        if (us) {
            return makeIso(+us[3], +us[1], us[2] ? +us[2] : 1);
        }

        // Month-name formats: "Jul 1950", "19 Jul 1950", "July 19 1950".
        var parts = cleaned.toLowerCase().split(' ');
        var monthIndex = -1;
        var month = 0;
        for (var i = 0; i < parts.length; i++) {
            var key = parts[i].replace(/\.$/, '');
            if (MONTHS[key]) {
                monthIndex = i;
                month = MONTHS[key];
                break;
            }
        }
        if (monthIndex === -1) return null;

        var nums = [];
        for (var j = 0; j < parts.length; j++) {
            if (j === monthIndex) continue;
            var token = parts[j].replace(/(st|nd|rd|th)$/, '');
            if (/^\d{1,4}$/.test(token)) nums.push(+token);
        }

        var year = null;
        for (var k = 0; k < nums.length; k++) {
            if (nums[k] > 31 || String(nums[k]).length === 4) year = nums[k];
        }
        if (year == null) return null;

        var day = 1;
        for (var m = 0; m < nums.length; m++) {
            if (nums[m] !== year && nums[m] >= 1 && nums[m] <= 31) {
                day = nums[m];
                break;
            }
        }
        return makeIso(year, month, day);
    }

    // Normalizes one input in place. Returns true when valid (or empty), false when unparseable.
    function normalizeInput(input) {
        if (!input) return true;
        var result = parse(input.value);
        if (result === undefined) {
            input.value = '';
            input.classList.remove('is-invalid');
            return true;
        }
        if (result === null) {
            input.classList.add('is-invalid');
            return false;
        }
        input.value = result;
        input.classList.remove('is-invalid');
        return true;
    }

    function normalizeForm(form) {
        var valid = true;
        var firstInvalid = null;
        form.querySelectorAll('.js-flex-date').forEach(function (input) {
            if (!normalizeInput(input)) {
                valid = false;
                if (!firstInvalid) firstInvalid = input;
            }
        });
        if (firstInvalid) firstInvalid.focus();
        return valid;
    }

    document.addEventListener('focusout', function (e) {
        var t = e.target;
        if (t && t.classList && t.classList.contains('js-flex-date')) {
            normalizeInput(t);
        }
    });

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form || !form.querySelectorAll) return;
        if (!normalizeForm(form)) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);

    window.FlexDate = { parse: parse, normalizeInput: normalizeInput, normalizeForm: normalizeForm };
})();
