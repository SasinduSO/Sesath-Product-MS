/**
 * WaterlilyInventory — Shared Application Scripts (site.js)
 * Global AJAX configuration, antiforgery token injection,
 * notification helpers (Toastr, SweetAlert2), and currency formatting.
 */

(function ($) {
    'use strict';

    // Configure Toastr default options
    if (typeof toastr !== 'undefined') {
        toastr.options = {
            closeButton: true,
            debug: false,
            newestOnTop: true,
            progressBar: true,
            positionClass: 'toast-top-right',
            preventDuplicates: false,
            onclick: null,
            showDuration: '300',
            hideDuration: '1000',
            timeOut: '4000',
            extendedTimeOut: '1000',
            showEasing: 'swing',
            hideEasing: 'linear',
            showMethod: 'fadeIn',
            hideMethod: 'fadeOut'
        };
    }

    // Global jQuery AJAX Setup: Attach Antiforgery Token to all mutations
    $.ajaxPrefilter(function (options, originalOptions, jqXHR) {
        if (!options.type || options.type.toUpperCase() === 'GET' || options.type.toUpperCase() === 'HEAD') {
            return;
        }

        // Retrieve antiforgery token from rendered hidden input
        var token = $('input[name="__RequestVerificationToken"]').val();
        if (token) {
            jqXHR.setRequestHeader('RequestVerificationToken', token);
        }
    });

    // Global namespace for Waterlily utility helpers
    window.Waterlily = window.Waterlily || {
        // Formats a numeric price into display currency string (default USD)
        formatCurrency: function (amount) {
            if (amount === null || amount === undefined || isNaN(amount)) {
                return '$0.00';
            }
            return new Intl.NumberFormat('en-US', {
                style: 'currency',
                currency: 'USD',
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }).format(amount);
        },

        // Displays a Toastr success message
        notifySuccess: function (message) {
            if (typeof toastr !== 'undefined') {
                toastr.success(message || 'Operation completed successfully.');
            } else {
                console.log('Success:', message);
            }
        },

        // Displays a Toastr or SweetAlert2 error notification
        notifyError: function (message) {
            if (typeof toastr !== 'undefined') {
                toastr.error(message || 'An error occurred. Please try again.');
            } else {
                alert(message);
            }
        },

        // SweetAlert2 confirmation dialog helper
        confirmAction: function (title, text, confirmButtonText) {
            if (typeof Swal !== 'undefined') {
                return Swal.fire({
                    title: title || 'Are you sure?',
                    text: text || 'You will not be able to revert this action.',
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonColor: '#123F8A',
                    cancelButtonColor: '#667383',
                    confirmButtonText: confirmButtonText || 'Yes, proceed',
                    cancelButtonText: 'Cancel',
                    reverseButtons: true
                });
            }
            return Promise.resolve({ isConfirmed: window.confirm(text || title) });
        },

        // Parses API error envelope and extracts field-level validation errors
        handleAjaxError: function (jqXHR) {
            var envelope = jqXHR.responseJSON;
            if (envelope && envelope.message) {
                var msg = envelope.message;
                if (envelope.validationErrors) {
                    var fieldErrors = [];
                    for (var field in envelope.validationErrors) {
                        if (envelope.validationErrors.hasOwnProperty(field)) {
                            fieldErrors.push(envelope.validationErrors[field].join(' '));
                        }
                    }
                    if (fieldErrors.length > 0) {
                        msg += '<br><small>' + fieldErrors.join('<br>') + '</small>';
                    }
                }
                Waterlily.notifyError(msg);
            } else if (jqXHR.status === 400) {
                Waterlily.notifyError('Request validation failed or antiforgery token invalid.');
            } else if (jqXHR.status === 404) {
                Waterlily.notifyError('The requested resource was not found.');
            } else if (jqXHR.status === 409) {
                Waterlily.notifyError('A concurrency conflict occurred. The record was modified by another operation.');
            } else {
                Waterlily.notifyError('A server error occurred. Please try again later.');
            }
        }
    };

})(jQuery);
