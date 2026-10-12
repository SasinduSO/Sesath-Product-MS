/**
 * WaterlilyInventory — Reporting Dashboard Client Script (reports.js)
 * Implements AJAX data retrieval from /reports/data?threshold=..., Chart.js
 * bar chart visualization, threshold validation, tied-leaders handling,
 * accessible table alternatives, and out-of-order race condition prevention.
 */

$(function () {
    'use strict';

    // State management
    var currentThreshold = 10;
    var valuationChartInstance = null;
    var activeAjaxRequest = null;
    var requestSequence = 0;

    // Initialize reporting dashboard
    init();

    function init() {
        bindEvents();
        loadDashboard(currentThreshold);
    }

    /**
     * Binds UI event listeners
     */
    function bindEvents() {
        // Apply threshold button
        $('#btnApplyThreshold').on('click', function () {
            applyThreshold();
        });

        // Threshold input: Enter key press
        $('#thresholdInput').on('keypress', function (e) {
            if (e.which === 13) {
                e.preventDefault();
                applyThreshold();
            }
        });

        // Refresh all reports button
        $('#btnRefreshAllReports').on('click', function () {
            loadDashboard(currentThreshold);
        });
    }

    /**
     * Validates and applies the user-specified threshold
     */
    function applyThreshold() {
        var $input = $('#thresholdInput');
        var valStr = $input.val().trim();
        var val = parseInt(valStr, 10);

        var $alert = $('#thresholdAlert');
        $alert.addClass('d-none').text('');

        if (valStr === '' || isNaN(val) || val < 0) {
            $alert.text('Threshold must be a valid non-negative integer (0 or greater).').removeClass('d-none');
            $input.addClass('is-invalid').focus();
            return;
        }

        $input.removeClass('is-invalid');
        currentThreshold = val;
        loadDashboard(currentThreshold);
    }

    /**
     * Loads dashboard data via GET /reports/data?threshold=...
     * Uses sequence counter and request aborting to prevent race conditions.
     */
    function loadDashboard(threshold) {
        // Increment request sequence to ignore stale responses
        var thisRequestId = ++requestSequence;

        // Abort previous in-flight request if any
        if (activeAjaxRequest && typeof activeAjaxRequest.abort === 'function') {
            activeAjaxRequest.abort();
            activeAjaxRequest = null;
        }

        // Show loading state
        setLoadingState(true);
        $('#currentThresholdLabel').text(threshold);
        $('#lowStockThresholdExplanation').html(
            'Products with inventory strictly below <strong>' + threshold + '</strong> units (<code>Stock &lt; @Threshold</code>).'
        );

        activeAjaxRequest = $.ajax({
            url: '/reports/data?threshold=' + threshold,
            type: 'GET',
            dataType: 'json'
        }).done(function (res) {
            // Guard: discard if a newer request was dispatched
            if (thisRequestId !== requestSequence) {
                return;
            }

            if (res && res.isSuccess && res.data) {
                renderDashboard(res.data);
            } else {
                showDashboardError('Unable to generate report dashboard.');
            }
        }).fail(function (jqXHR, textStatus) {
            // Guard: ignore intentional cancellations from abort()
            if (textStatus === 'abort' || thisRequestId !== requestSequence) {
                return;
            }

            showDashboardError('Database query failed. Please verify SQL Server connection.');
            Waterlily.handleAjaxError(jqXHR);
        }).always(function () {
            if (thisRequestId === requestSequence) {
                setLoadingState(false);
                activeAjaxRequest = null;
            }
        });
    }

    /**
     * Renders all five report components from authoritative server DTOs
     */
    function renderDashboard(data) {
        // 1. Average Price per Category
        renderAveragePrices(data.averagePrices || []);

        // 2. Highest Stock Value Category (with ties handling)
        renderHighestValueCategories(data.highestValueCategories || []);

        // 3. Category Inventory Valuation (Chart + Accessible Table)
        renderInventoryValuations(data.inventoryValuations || []);

        // 4. Low-Stock Alert
        renderLowStockProducts(data.lowStockProducts || [], data.lowStockThreshold);

        // 5. Top 5 Most Expensive Products
        renderTopExpensiveProducts(data.topExpensiveProducts || []);
    }

    /**
     * Report 1: Average Price per Category
     */
    function renderAveragePrices(items) {
        var $tbody = $('#avgPriceTableBody');
        $tbody.empty();
        $('#avgPriceCountBadge').text(items.length + ' Categories');

        if (items.length === 0) {
            $tbody.html('<tr><td colspan="2" class="text-center py-4 text-muted">No category price data available.</td></tr>');
            return;
        }

        items.forEach(function (item) {
            var $tr = $('<tr>');
            $tr.append($('<td>').addClass('fw-semibold').text(item.category));
            $tr.append($('<td>').addClass('text-end fw-bold text-primary').text(Waterlily.formatCurrency(item.averagePrice)));
            $tbody.append($tr);
        });
    }

    /**
     * Report 2: Highest Stock Value Category
     * Displays all tied categories if multiple share the maximum valuation.
     */
    function renderHighestValueCategories(leaders) {
        var $container = $('#highestValueContainer');
        $container.empty();

        if (leaders.length === 0) {
            $container.html('<p class="text-muted small mb-0">No inventory leadership data available.</p>');
            $('#highestValueTieBadge').hide();
            return;
        }

        // Show tied badge if more than 1 category shares the maximum value
        if (leaders.length > 1) {
            $('#highestValueTieBadge').text('Tied Leaders (' + leaders.length + ')').show();
        } else {
            $('#highestValueTieBadge').hide();
        }

        leaders.forEach(function (leader) {
            var units = leader.totalStockUnits !== undefined ? leader.totalStockUnits : (leader.totalStock || 0);
            var $card = $('<div>').addClass('highest-val-card');
            var $title = $('<div>').addClass('highest-val-title').text(leader.category);
            var $amount = $('<div>').addClass('highest-val-amount').text(Waterlily.formatCurrency(leader.inventoryValue));
            var $units = $('<div>').addClass('text-muted small mt-1').text('Total Stock: ' + Number(units).toLocaleString() + ' units');

            $card.append($title, $amount, $units);
            $container.append($card);
        });
    }

    /**
     * Report 3: Inventory Valuation by Category
     * Renders both Chart.js bar chart and accessible table alternative.
     */
    function renderInventoryValuations(items) {
        var $tbody = $('#valuationTableBody');
        var $tfoot = $('#valuationTableFoot');
        $tbody.empty();

        if (items.length === 0) {
            $tbody.html('<tr><td colspan="3" class="text-center py-4 text-muted">No category valuation data available.</td></tr>');
            $tfoot.hide();
            $('#totalValuationBadge').text('$0.00 Total');
            destroyChart();
            return;
        }

        var grandUnits = 0;
        var grandValuation = 0;
        var chartLabels = [];
        var chartValues = [];

        items.forEach(function (item) {
            var units = item.totalStockUnits !== undefined ? item.totalStockUnits : (item.totalStock || 0);
            grandUnits += units;
            grandValuation += (item.inventoryValue || 0);
            chartLabels.push(item.category);
            chartValues.push(item.inventoryValue);

            var $tr = $('<tr>');
            $tr.append($('<td>').addClass('fw-semibold').text(item.category));
            $tr.append($('<td>').addClass('text-end').text(Number(units).toLocaleString()));
            $tr.append($('<td>').addClass('text-end fw-semibold text-primary').text(Waterlily.formatCurrency(item.inventoryValue)));
            $tbody.append($tr);
        });

        // Update Grand Totals in accessible table
        $('#grandTotalUnits').text(grandUnits.toLocaleString() + ' units');
        $('#grandTotalValue').text(Waterlily.formatCurrency(grandValuation));
        $('#totalValuationBadge').text(Waterlily.formatCurrency(grandValuation) + ' Total');
        $tfoot.show();

        // Render Chart.js
        renderChart(chartLabels, chartValues);
    }

    /**
     * Renders / Updates Chart.js Bar Chart
     */
    function renderChart(labels, values) {
        var canvas = document.getElementById('categoryValuationChart');
        if (!canvas) return;

        destroyChart();

        var ctx = canvas.getContext('2d');
        valuationChartInstance = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Inventory Value ($ USD)',
                    data: values,
                    backgroundColor: '#2DA9E1',
                    hoverBackgroundColor: '#123F8A',
                    borderColor: '#123F8A',
                    borderWidth: 1,
                    borderRadius: 6,
                    barThickness: 'flex',
                    maxBarThickness: 50
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false, // Avoid animation delay on rapid updates
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return 'Valuation: ' + Waterlily.formatCurrency(context.raw);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            font: {
                                size: 12,
                                weight: 'bold'
                            },
                            color: '#202834'
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: '#DFE5EC'
                        },
                        ticks: {
                            callback: function (value) {
                                return '$' + (value >= 1000 ? (value / 1000) + 'k' : value);
                            },
                            font: {
                                size: 11
                            },
                            color: '#667383'
                        }
                    }
                }
            }
        });
    }

    /**
     * Destroys existing Chart.js instance safely
     */
    function destroyChart() {
        if (valuationChartInstance) {
            valuationChartInstance.destroy();
            valuationChartInstance = null;
        }
    }

    /**
     * Report 4: Low-Stock Products
     */
    function renderLowStockProducts(items, threshold) {
        var $tbody = $('#lowStockTableBody');
        $tbody.empty();
        $('#lowStockCountBadge').text(items.length + ' Items');

        if (items.length === 0) {
            $tbody.html(
                '<tr><td colspan="4" class="text-center py-4 text-muted">' +
                '<span class="text-success fw-semibold">&check; No low-stock items detected below threshold ' + threshold + '.</span>' +
                '</td></tr>'
            );
            return;
        }

        items.forEach(function (p) {
            var $tr = $('<tr>');
            $tr.append($('<td>').addClass('fw-semibold').text(p.name));
            $tr.append($('<td>').text(p.category));

            var $stockTd = $('<td>').addClass('text-end');
            var $stockBadge = $('<span>').addClass('stock-badge');
            if (p.stock === 0) {
                $stockBadge.addClass('stock-out-of-stock').text('0 (Out of stock)');
            } else {
                $stockBadge.addClass('stock-low-stock').text(p.stock + ' units');
            }
            $stockTd.append($stockBadge);
            $tr.append($stockTd);

            $tr.append($('<td>').addClass('text-end fw-semibold').text(Waterlily.formatCurrency(p.price)));
            $tbody.append($tr);
        });
    }

    /**
     * Report 5: Top 5 Most Expensive Products
     */
    function renderTopExpensiveProducts(items) {
        var $container = $('#topExpensiveContainer');
        $container.empty();

        if (items.length === 0) {
            $container.html('<div class="text-center py-4 text-muted">No product pricing data available.</div>');
            return;
        }

        items.forEach(function (item, index) {
            var rank = index + 1;
            var $row = $('<div>').addClass('rank-row');

            var $left = $('<div>').addClass('d-flex align-items-center gap-3');
            var $badge = $('<div>').addClass('rank-number-badge').text(rank);
            var $details = $('<div>');
            var $name = $('<div>').addClass('fw-bold text-dark').text(item.name);
            var $meta = $('<div>').addClass('text-muted small').text(item.category + ' • Stock: ' + (item.stock || 0) + ' units');
            $details.append($name, $meta);
            $left.append($badge, $details);

            var $price = $('<div>').addClass('fs-6 fw-bold text-primary').text(Waterlily.formatCurrency(item.price));

            $row.append($left, $price);
            $container.append($row);
        });
    }

    /**
     * Sets visual loading indicators
     */
    function setLoadingState(isLoading) {
        if (isLoading) {
            $('#thresholdSpinner').removeClass('d-none');
            $('#btnApplyThreshold').prop('disabled', true);
            $('#chartLoadingOverlay').removeClass('d-none');
        } else {
            $('#thresholdSpinner').addClass('d-none');
            $('#btnApplyThreshold').prop('disabled', false);
            $('#chartLoadingOverlay').addClass('d-none');
        }
    }

    /**
     * Displays error states in all report panels
     */
    function showDashboardError(msg) {
        var errorHtml = '<tr><td colspan="5" class="text-center py-4 text-danger fw-semibold">' + msg + '</td></tr>';
        $('#avgPriceTableBody').html(errorHtml);
        $('#valuationTableBody').html(errorHtml);
        $('#lowStockTableBody').html(errorHtml);
        $('#valuationTableFoot').hide();
        $('#highestValueContainer').html('<p class="text-danger small">' + msg + '</p>');
        $('#topExpensiveContainer').html('<p class="text-danger small text-center py-4">' + msg + '</p>');
        destroyChart();
    }

});
