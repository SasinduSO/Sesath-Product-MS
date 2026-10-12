/**
 * WaterlilyInventory — Product Inventory Client Script (products.js)
 * Implements AJAX CRUD, client-side filtering, sorting, pagination,
 * SweetAlert2 deletion confirmation, and accessible Bootstrap modal dialogs.
 *
 * NOTE: For this small assessment, sorting, search, and pagination are executed
 * on the collection loaded via GET /products/data. This provides instant responsiveness
 * on small datasets while exercising backend cache invalidation on mutations.
 */

$(function () {
    'use strict';

    // State management
    var allProducts = [];
    var filteredProducts = [];
    var currentSearch = '';
    var currentCategory = '';
    var currentStockStatus = '';
    var sortColumn = 'id';
    var sortDirection = 'asc';
    var currentPage = 1;
    var pageSize = 10;
    var lastFocusedElement = null;

    // Bootstrap Modal instances
    var productModalEl = document.getElementById('productModal');
    var productModal = productModalEl ? new bootstrap.Modal(productModalEl) : null;
    var detailsModalEl = document.getElementById('productDetailsModal');
    var detailsModal = detailsModalEl ? new bootstrap.Modal(detailsModalEl) : null;
    var bulkModalEl = document.getElementById('bulkAddModal');
    var bulkModal = bulkModalEl ? new bootstrap.Modal(bulkModalEl) : null;

    // Bulk upload state
    var currentBulkFile = null;
    var bulkPreviewData = null;
    var currentBulkFilterTab = 'all';

    // Initialize page
    init();

    function init() {
        bindEvents();
        loadProducts();
    }

    /**
     * Binds UI event listeners
     */
    function bindEvents() {
        // Open Add Single Product Modal
        $('#btnAddProduct').on('click', function () {
            lastFocusedElement = this;
            openAddModal();
        });

        // Open Add Bulk Modal
        $('#btnAddBulk').on('click', function () {
            lastFocusedElement = this;
            openBulkModal();
        });

        // Search input with immediate / debounced filter
        var searchTimeout = null;
        $('#productSearchInput').on('input', function () {
            var val = $(this).val();
            $('#btnClearSearch').toggle(val.length > 0);
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(function () {
                currentSearch = val.trim().toLowerCase();
                currentPage = 1;
                applyFiltersAndRender();
            }, 200);
        });

        // Clear search
        $('#btnClearSearch').on('click', function () {
            $('#productSearchInput').val('');
            $(this).hide();
            currentSearch = '';
            currentPage = 1;
            applyFiltersAndRender();
            $('#productSearchInput').focus();
        });

        // Category filter change
        $('#categoryFilterSelect').on('change', function () {
            currentCategory = $(this).val();
            currentPage = 1;
            applyFiltersAndRender();
        });

        // Stock status filter change
        $('#stockStatusFilterSelect').on('change', function () {
            currentStockStatus = $(this).val();
            currentPage = 1;
            applyFiltersAndRender();
        });

        // Page size change
        $('#pageSizeSelect').on('change', function () {
            pageSize = parseInt($(this).val(), 10) || 10;
            currentPage = 1;
            applyFiltersAndRender();
        });

        // Reset filters button
        $('#btnResetFilters').on('click', function () {
            $('#productSearchInput').val('');
            $('#btnClearSearch').hide();
            $('#categoryFilterSelect').val('');
            $('#stockStatusFilterSelect').val('');
            $('#pageSizeSelect').val('10');
            currentSearch = '';
            currentCategory = '';
            currentStockStatus = '';
            pageSize = 10;
            currentPage = 1;
            sortColumn = 'id';
            sortDirection = 'asc';
            updateSortHeaders();
            applyFiltersAndRender();
            Waterlily.notifySuccess('Filters reset.');
        });

        // Sortable table headers
        $('.sortable-th').on('click keypress', function (e) {
            if (e.type === 'keypress' && e.which !== 13 && e.which !== 32) return;
            e.preventDefault();
            var col = $(this).data('sort');
            if (sortColumn === col) {
                sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
            } else {
                sortColumn = col;
                sortDirection = 'asc';
            }
            updateSortHeaders();
            applyFiltersAndRender();
        });

        // Delegated table actions: View Details
        $(document).on('click', '.btn-action-view', function () {
            lastFocusedElement = this;
            var id = $(this).data('id');
            openDetailsModal(id);
        });

        // Delegated table actions: Edit Product
        $(document).on('click', '.btn-action-edit', function () {
            lastFocusedElement = this;
            var id = $(this).data('id');
            openEditModal(id);
        });

        // Delegated table actions: Delete Product
        $(document).on('click', '.btn-action-delete', function () {
            lastFocusedElement = this;
            var id = $(this).data('id');
            var name = $(this).data('name');
            confirmDeleteProduct(id, name);
        });

        // Input restrictions and real-time validation feedback
        $('#productName').on('input', function () {
            var val = $(this).val();
            if (/[^a-zA-Z0-9\s-]/.test(val)) {
                $(this).addClass('is-invalid');
                $('#nameError').text('Special characters are not allowed. Only letters, numbers, spaces, and hyphens are permitted.').show();
            } else {
                $(this).removeClass('is-invalid');
                $('#nameError').hide();
            }
        });

        $('#productCategory').on('change', function () {
            if ($(this).val()) {
                $(this).removeClass('is-invalid');
                $('#categoryError').hide();
            }
        });

        // Disallow 'e', 'E', '+', '-' on Price input
        $('#productPrice').on('keydown', function (e) {
            if (['e', 'E', '+', '-'].indexOf(e.key) !== -1) {
                e.preventDefault();
            }
        });

        $('#productPrice').on('input', function () {
            if (this.validity && this.validity.badInput) {
                $(this).addClass('is-invalid');
                $('#priceError').text('Letters and invalid characters are not allowed in price.').show();
            } else {
                $(this).removeClass('is-invalid');
                $('#priceError').hide();
            }
        });

        // Disallow 'e', 'E', '+', '-', '.' on Stock input (integers only)
        $('#productStock').on('keydown', function (e) {
            if (['e', 'E', '+', '-', '.'].indexOf(e.key) !== -1) {
                e.preventDefault();
            }
        });

        $('#productStock').on('input', function () {
            if (this.validity && this.validity.badInput) {
                $(this).addClass('is-invalid');
                $('#stockError').text('Letters and invalid characters are not allowed in stock quantity.').show();
            } else {
                $(this).removeClass('is-invalid');
                $('#stockError').hide();
            }
        });

        // Form submission for Add / Edit
        $('#productForm').on('submit', function (e) {
            e.preventDefault();
            submitProductForm();
        });

        // Edit button inside Details Modal
        $('#btnEditFromDetails').on('click', function () {
            var id = parseInt($('#detailsId').text().replace('#', ''), 10);
            if (detailsModal) detailsModal.hide();
            if (id > 0) openEditModal(id);
        });

        // Keyboard focus return on modal close
        if (productModalEl) {
            productModalEl.addEventListener('hidden.bs.modal', function () {
                if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
                    lastFocusedElement.focus();
                }
            });
            productModalEl.addEventListener('shown.bs.modal', function () {
                $('#productName').focus();
            });
        }

        if (detailsModalEl) {
            detailsModalEl.addEventListener('hidden.bs.modal', function () {
                if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
                    lastFocusedElement.focus();
                }
            });
        }

        if (bulkModalEl) {
            bulkModalEl.addEventListener('hidden.bs.modal', function () {
                resetBulkModal();
                if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
                    lastFocusedElement.focus();
                }
            });
        }

        // Bulk Upload: File Browse Click
        $('#btnBrowseFile').on('click', function (e) {
            e.stopPropagation();
            $('#bulkFileInput').trigger('click');
        });

        $('#bulkDropZone').on('click', function () {
            $('#bulkFileInput').trigger('click');
        });

        // Bulk Upload: Drag & Drop handlers
        $('#bulkDropZone')
            .on('dragover dragenter', function (e) {
                e.preventDefault();
                e.stopPropagation();
                $(this).addClass('dragover');
            })
            .on('dragleave dragend drop', function (e) {
                e.preventDefault();
                e.stopPropagation();
                $(this).removeClass('dragover');
            })
            .on('drop', function (e) {
                var dt = e.originalEvent.dataTransfer;
                if (dt && dt.files && dt.files.length > 0) {
                    handleBulkFileSelection(dt.files[0]);
                }
            });

        // Bulk Upload: Input File Change
        $('#bulkFileInput').on('change', function () {
            if (this.files && this.files.length > 0) {
                handleBulkFileSelection(this.files[0]);
            }
        });

        // Bulk Upload: Remove File
        $('#btnRemoveSelectedFile').on('click', function () {
            resetBulkFile();
        });

        // Bulk Upload: Extract & Validate Data button
        $('#btnExtractAndValidate').on('click', function () {
            extractAndValidateBulkFile();
        });

        // Bulk Preview: Tabs Switcher
        $('#tabAllRecords').on('click', function () {
            currentBulkFilterTab = 'all';
            setActivePreviewTab($(this));
            renderBulkPreviewTable('all');
        });

        $('#tabValidRecords').on('click', function () {
            currentBulkFilterTab = 'valid';
            setActivePreviewTab($(this));
            renderBulkPreviewTable('valid');
        });

        $('#tabInvalidRecords').on('click', function () {
            currentBulkFilterTab = 'invalid';
            setActivePreviewTab($(this));
            renderBulkPreviewTable('invalid');
        });

        // Bulk Preview: Back to Upload step
        $('#btnBulkBackToUpload').on('click', function () {
            $('#bulkPreviewStep').hide();
            $('#bulkUploadStep').show();
            $('#btnBulkBackToUpload').addClass('d-none');
            $('#btnApproveAndImport').addClass('d-none');
        });

        // Bulk Preview: Approve & Import Valid Records
        $('#btnApproveAndImport').on('click', function () {
            executeBulkImport();
        });
    }

    /**
     * Loads product list from server via GET /products/data
     */
    function loadProducts(preserveState) {
        if (!preserveState) {
            $('#productsTableBody').html(
                '<tr><td colspan="7" class="text-center py-5 text-muted">' +
                '<div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>' +
                '<span>Loading product inventory...</span></td></tr>'
            );
        }

        $.ajax({
            url: '/products/data',
            type: 'GET',
            dataType: 'json'
        }).done(function (res) {
            if (res && res.isSuccess && Array.isArray(res.data)) {
                allProducts = res.data;
                populateCategoryFilter();
                applyFiltersAndRender();
            } else {
                showTableError('Failed to load product data.');
            }
        }).fail(function (jqXHR) {
            showTableError('Unable to connect to server. Please try again.');
            Waterlily.handleAjaxError(jqXHR);
        });
    }

    /**
     * Populates the Category dropdown from distinct categories in allProducts
     */
    function populateCategoryFilter() {
        var categories = [];
        allProducts.forEach(function (p) {
            if (p.category && categories.indexOf(p.category) === -1) {
                categories.push(p.category);
            }
        });
        categories.sort();

        var $select = $('#categoryFilterSelect');
        var selected = $select.val();
        $select.find('option:not(:first)').remove();

        categories.forEach(function (cat) {
            var $opt = $('<option>').val(cat).text(cat);
            if (cat === selected) $opt.prop('selected', true);
            $select.append($opt);
        });
    }

    /**
     * Applies search, category filter, stock status filter, sorting, and pagination
     */
    function applyFiltersAndRender() {
        // 1. Filter
        filteredProducts = allProducts.filter(function (p) {
            // Search filter
            if (currentSearch) {
                var nameMatch = p.name && p.name.toLowerCase().indexOf(currentSearch) !== -1;
                var catMatch = p.category && p.category.toLowerCase().indexOf(currentSearch) !== -1;
                if (!nameMatch && !catMatch) return false;
            }

            // Category filter
            if (currentCategory && p.category !== currentCategory) {
                return false;
            }

            // Stock status filter (Threshold = 10)
            if (currentStockStatus) {
                if (currentStockStatus === 'in_stock' && p.stock < 10) return false;
                if (currentStockStatus === 'low_stock' && (p.stock <= 0 || p.stock >= 10)) return false;
                if (currentStockStatus === 'out_of_stock' && p.stock !== 0) return false;
            }

            return true;
        });

        // 2. Sort
        filteredProducts.sort(function (a, b) {
            var valA = a[sortColumn];
            var valB = b[sortColumn];

            if (typeof valA === 'string') {
                valA = valA.toLowerCase();
                valB = (valB || '').toLowerCase();
                return sortDirection === 'asc' ? valA.localeCompare(valB) : valB.localeCompare(valA);
            }

            valA = valA || 0;
            valB = valB || 0;
            return sortDirection === 'asc' ? (valA - valB) : (valB - valA);
        });

        // 3. Reconcile pagination
        var totalItems = filteredProducts.length;
        var totalPages = Math.ceil(totalItems / pageSize) || 1;
        if (currentPage > totalPages) {
            currentPage = totalPages;
        }
        if (currentPage < 1) {
            currentPage = 1;
        }

        var startIndex = (currentPage - 1) * pageSize;
        var pageItems = filteredProducts.slice(startIndex, startIndex + pageSize);

        // 4. Render Table
        renderTableRows(pageItems, totalItems);

        // 5. Render Pagination
        renderPagination(totalItems, totalPages);

        // 6. Update counts and summaries
        $('#productCountBadge').text(totalItems + ' of ' + allProducts.length + ' products');
    }

    /**
     * Renders rows into the table using safe DOM methods (.text())
     */
    function renderTableRows(items, totalItems) {
        var $tbody = $('#productsTableBody');
        $tbody.empty();

        if (totalItems === 0) {
            var emptyMsg = allProducts.length === 0
                ? 'No products available in the database.'
                : 'No products match the selected filters.';
            $tbody.html(
                '<tr><td colspan="7" class="text-center py-5 text-muted">' +
                '<p class="mb-1 fw-semibold">' + emptyMsg + '</p>' +
                '<small>Try adjusting your search query or filters.</small></td></tr>'
            );
            return;
        }

        items.forEach(function (p) {
            var $tr = $('<tr>');

            // ID
            $tr.append($('<td>').addClass('text-muted small fw-semibold').text('#' + p.id));

            // Name
            $tr.append($('<td>').addClass('fw-semibold').text(p.name));

            // Category
            $tr.append($('<td>').text(p.category));

            // Price (formatted)
            $tr.append($('<td>').addClass('text-end fw-semibold text-nowrap').text(Waterlily.formatCurrency(p.price)));

            // Stock
            $tr.append($('<td>').addClass('text-end fw-semibold').text(p.stock.toLocaleString()));

            // Status Badge
            var $badgeTd = $('<td>').addClass('text-center');
            var $badge = $('<span>').addClass('stock-badge');

            if (p.stock === 0) {
                $badge.addClass('stock-out-of-stock').text('Out of Stock (0)');
            } else if (p.stock < 10) {
                $badge.addClass('stock-low-stock').text('Low Stock (' + p.stock + ')');
            } else {
                $badge.addClass('stock-in-stock').text('In Stock (' + p.stock + ')');
            }
            $badgeTd.append($badge);
            $tr.append($badgeTd);

            // Action Buttons
            var $actionsTd = $('<td>').addClass('text-center');
            var $btnGroup = $('<div>').addClass('action-btn-group');

            // View Details button
            var $btnView = $('<button>')
                .addClass('btn-action btn-action-view')
                .attr('type', 'button')
                .attr('title', 'View details for ' + p.name)
                .attr('data-id', p.id)
                .html('<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>');

            // Edit button
            var $btnEdit = $('<button>')
                .addClass('btn-action btn-action-edit')
                .attr('type', 'button')
                .attr('title', 'Edit ' + p.name)
                .attr('data-id', p.id)
                .html('<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path></svg>');

            // Delete button
            var $btnDelete = $('<button>')
                .addClass('btn-action btn-action-delete')
                .attr('type', 'button')
                .attr('title', 'Delete ' + p.name)
                .attr('data-id', p.id)
                .attr('data-name', p.name)
                .html('<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>');

            $btnGroup.append($btnView, $btnEdit, $btnDelete);
            $actionsTd.append($btnGroup);
            $tr.append($actionsTd);

            $tbody.append($tr);
        });
    }

    /**
     * Renders pagination buttons and footer summary
     */
    function renderPagination(totalItems, totalPages) {
        var $info = $('#paginationInfo');
        var $controls = $('#paginationControls');
        $controls.empty();

        if (totalItems === 0) {
            $info.text('Showing 0 to 0 of 0 entries');
            return;
        }

        var start = (currentPage - 1) * pageSize + 1;
        var end = Math.min(currentPage * pageSize, totalItems);
        $info.text('Showing ' + start + ' to ' + end + ' of ' + totalItems + ' entries');

        // Previous Button
        var $prevLi = $('<li>').addClass('page-item').toggleClass('disabled', currentPage === 1);
        var $prevLink = $('<a>').addClass('page-link').attr('href', '#').text('Previous').on('click', function (e) {
            e.preventDefault();
            if (currentPage > 1) {
                currentPage--;
                applyFiltersAndRender();
            }
        });
        $prevLi.append($prevLink);
        $controls.append($prevLi);

        // Page Number Buttons
        for (var p = 1; p <= totalPages; p++) {
            (function (pageNumber) {
                var $pageLi = $('<li>').addClass('page-item').toggleClass('active', pageNumber === currentPage);
                var $pageLink = $('<a>').addClass('page-link').attr('href', '#').text(pageNumber).on('click', function (e) {
                    e.preventDefault();
                    if (currentPage !== pageNumber) {
                        currentPage = pageNumber;
                        applyFiltersAndRender();
                    }
                });
                $pageLi.append($pageLink);
                $controls.append($pageLi);
            })(p);
        }

        // Next Button
        var $nextLi = $('<li>').addClass('page-item').toggleClass('disabled', currentPage === totalPages);
        var $nextLink = $('<a>').addClass('page-link').attr('href', '#').text('Next').on('click', function (e) {
            e.preventDefault();
            if (currentPage < totalPages) {
                currentPage++;
                applyFiltersAndRender();
            }
        });
        $nextLi.append($nextLink);
        $controls.append($nextLi);
    }

    /**
     * Updates visual sorting indicators on table headers
     */
    function updateSortHeaders() {
        $('.sortable-th').each(function () {
            var col = $(this).data('sort');
            var $icon = $(this).find('.sort-icon');
            $(this).removeClass('sorted-asc sorted-desc');

            if (col === sortColumn) {
                if (sortDirection === 'asc') {
                    $(this).addClass('sorted-asc');
                    $icon.html('&uparrow;');
                } else {
                    $(this).addClass('sorted-desc');
                    $icon.html('&downarrow;');
                }
            } else {
                $icon.html('&updownarrow;');
            }
        });
    }

    /**
     * Shows error state inside table body
     */
    function showTableError(msg) {
        $('#productsTableBody').html(
            '<tr><td colspan="7" class="text-center py-5 text-danger">' +
            '<p class="fw-semibold mb-1">' + msg + '</p>' +
            '<button type="button" class="btn btn-outline-custom btn-sm mt-2" id="btnRetryLoad">Retry</button>' +
            '</td></tr>'
        );
        $('#btnRetryLoad').on('click', function () { loadProducts(); });
    }

    /**
     * Opens Add Product Modal
     */
    function openAddModal() {
        resetFormErrors();
        $('#productId').val(0);
        $('#productName').val('');
        $('#productCategory').val('');
        $('#productPrice').val('');
        $('#productStock').val('');
        $('#productModalLabel').text('Add New Product');
        $('#saveBtnText').text('Save Product');
        if (productModal) productModal.show();
    }

    /**
     * Helper to set Category dropdown value, matching exact value, text,
     * or alias (e.g. Stationery vs Stationary).
     */
    function setCategorySelectValue(cat) {
        if (!cat) {
            $('#productCategory').val('');
            return;
        }
        $('#productCategory').val(cat);
        if (!$('#productCategory').val()) {
            $('#productCategory option').each(function () {
                var optVal = $(this).val();
                var optText = $(this).text();
                var target = cat.toLowerCase();
                if (optVal.toLowerCase() === target ||
                    optText.toLowerCase() === target ||
                    (target.indexOf('station') === 0 && (optVal.toLowerCase().indexOf('station') === 0 || optText.toLowerCase().indexOf('station') === 0))) {
                    $('#productCategory').val(optVal);
                    return false;
                }
            });
        }
    }

    /**
     * Opens Edit Product Modal after loading authoritative state via GET /products/{id}/data
     */
    function openEditModal(id) {
        resetFormErrors();

        // Retrieve product from server (CachedLong path)
        $.ajax({
            url: '/products/' + id + '/data',
            type: 'GET',
            dataType: 'json'
        }).done(function (res) {
            if (res && res.isSuccess && res.data) {
                var p = res.data;
                $('#productId').val(p.id);
                $('#productName').val(p.name);
                setCategorySelectValue(p.category);
                $('#productPrice').val(p.price);
                $('#productStock').val(p.stock);
                $('#productModalLabel').text('Edit Product #' + p.id);
                $('#saveBtnText').text('Update Product');
                if (productModal) productModal.show();
            } else {
                Waterlily.notifyError('Failed to load product. It may have been deleted.');
                loadProducts(true);
            }
        }).fail(function (jqXHR) {
            Waterlily.notifyError('Unable to load product #' + id + '. ' + (jqXHR.responseJSON ? jqXHR.responseJSON.message : ''));
            loadProducts(true);
        });
    }

    /**
     * Opens Details Modal
     */
    function openDetailsModal(id) {
        $.ajax({
            url: '/products/' + id + '/data',
            type: 'GET',
            dataType: 'json'
        }).done(function (res) {
            if (res && res.isSuccess && res.data) {
                var p = res.data;
                $('#detailsId').text('#' + p.id);
                $('#detailsName').text(p.name);
                $('#detailsCategory').text(p.category);
                $('#detailsPrice').text(Waterlily.formatCurrency(p.price));
                $('#detailsStock').text(p.stock.toLocaleString() + ' units');

                var $badge = $('<span>').addClass('stock-badge');
                if (p.stock === 0) {
                    $badge.addClass('stock-out-of-stock').text('Out of Stock (0)');
                } else if (p.stock < 10) {
                    $badge.addClass('stock-low-stock').text('Low Stock (' + p.stock + ')');
                } else {
                    $badge.addClass('stock-in-stock').text('In Stock (' + p.stock + ')');
                }
                $('#detailsStatusBadge').empty().append($badge);

                if (detailsModal) detailsModal.show();
            } else {
                Waterlily.notifyError('Unable to view product. It may no longer exist.');
            }
        }).fail(function (jqXHR) {
            Waterlily.handleAjaxError(jqXHR);
        });
    }

    /**
     * Submits Add or Edit product form via AJAX
     */
    function submitProductForm() {
        resetFormErrors();

        var id = parseInt($('#productId').val(), 10) || 0;
        var name = $('#productName').val().trim();
        var category = $('#productCategory').val();
        var priceStr = $('#productPrice').val();
        var stockStr = $('#productStock').val();

        // Allowed categories
        var allowedCategories = ['Accessories', 'Electronics', 'Office Furniture', 'Stationery', 'Stationary'];

        // Client-side validation
        var hasError = false;

        // Name validation: required, max 150, no special characters (only letters, numbers, spaces, and hyphens)
        var nameRegex = /^[a-zA-Z0-9\s-]+$/;
        if (!name) {
            $('#productName').addClass('is-invalid');
            $('#nameError').text('Product name is required.').show();
            hasError = true;
        } else if (name.length > 150) {
            $('#productName').addClass('is-invalid');
            $('#nameError').text('Product name cannot exceed 150 characters.').show();
            hasError = true;
        } else if (!nameRegex.test(name) || !/[a-zA-Z0-9]/.test(name)) {
            $('#productName').addClass('is-invalid');
            $('#nameError').text('Product name cannot contain special characters. Only letters, numbers, spaces, and hyphens are allowed.').show();
            hasError = true;
        }

        // Category validation: required, must be in allowed categories dropdown
        var catValid = category && allowedCategories.some(function (c) {
            return c.toLowerCase() === category.toLowerCase();
        });
        if (!category) {
            $('#productCategory').addClass('is-invalid');
            $('#categoryError').text('Please select a category from the dropdown.').show();
            hasError = true;
        } else if (!catValid) {
            $('#productCategory').addClass('is-invalid');
            $('#categoryError').text('Category must be one of: Accessories, Electronics, Office Furniture, Stationary.').show();
            hasError = true;
        }

        // Price validation: required, no letters, >= 0, max 2 decimal places
        var priceInput = document.getElementById('productPrice');
        if (priceInput && priceInput.validity && priceInput.validity.badInput) {
            $('#productPrice').addClass('is-invalid');
            $('#priceError').text('Price cannot contain letters or invalid characters. Enter a valid numeric amount.').show();
            hasError = true;
        } else if (priceStr === '') {
            $('#productPrice').addClass('is-invalid');
            $('#priceError').text('Price is required.').show();
            hasError = true;
        } else {
            var price = parseFloat(priceStr);
            if (isNaN(price)) {
                $('#productPrice').addClass('is-invalid');
                $('#priceError').text('Price must be a valid numeric amount without letters.').show();
                hasError = true;
            } else if (price < 0) {
                $('#productPrice').addClass('is-invalid');
                $('#priceError').text('Price must be zero or positive.').show();
                hasError = true;
            } else if (Math.round(price * 100) !== price * 100) {
                $('#productPrice').addClass('is-invalid');
                $('#priceError').text('Price cannot have more than 2 decimal places.').show();
                hasError = true;
            }
        }

        // Stock validation: required, no letters or decimals, >= 0
        var stockInput = document.getElementById('productStock');
        if (stockInput && stockInput.validity && stockInput.validity.badInput) {
            $('#productStock').addClass('is-invalid');
            $('#stockError').text('Stock quantity cannot contain letters or invalid characters. Enter a whole number.').show();
            hasError = true;
        } else if (stockStr === '') {
            $('#productStock').addClass('is-invalid');
            $('#stockError').text('Stock quantity is required.').show();
            hasError = true;
        } else if (!/^\d+$/.test(stockStr)) {
            $('#productStock').addClass('is-invalid');
            $('#stockError').text('Stock quantity must be a non-negative whole number without letters or decimals.').show();
            hasError = true;
        } else {
            var stock = parseInt(stockStr, 10);
            if (isNaN(stock) || stock < 0) {
                $('#productStock').addClass('is-invalid');
                $('#stockError').text('Stock quantity must be zero or positive.').show();
                hasError = true;
            }
        }

        if (hasError) return;

        // Prepare request
        var url = id > 0 ? '/products/' + id + '/update' : '/products';
        var postData = {
            Name: name,
            Category: category,
            Price: price,
            Stock: stock
        };

        // Disable submit button while request is in flight
        var $btnSave = $('#btnSaveProduct');
        var $spinner = $('#saveSpinner');
        $btnSave.prop('disabled', true);
        $spinner.removeClass('d-none');

        $.ajax({
            url: url,
            type: 'POST',
            data: postData,
            dataType: 'json'
        }).done(function (res) {
            if (res && res.isSuccess) {
                if (productModal) productModal.hide();
                Waterlily.notifySuccess(res.message || (id > 0 ? 'Product updated successfully.' : 'Product created successfully.'));

                // Refetch product data to refresh table and exercise cache invalidation
                loadProducts(true);
            } else {
                handleFormError(res);
            }
        }).fail(function (jqXHR) {
            var res = jqXHR.responseJSON;
            if (res) {
                handleFormError(res);
            } else {
                $('#modalGeneralError').text('A server error occurred. Please try again.').removeClass('d-none');
            }
        }).always(function () {
            $btnSave.prop('disabled', false);
            $spinner.addClass('d-none');
        });
    }

    /**
     * Handles server-side validation and conflict errors returned in JSON envelope
     */
    function handleFormError(res) {
        if (!res) return;

        if (res.validationErrors) {
            for (var field in res.validationErrors) {
                if (res.validationErrors.hasOwnProperty(field)) {
                    var msgs = res.validationErrors[field].join(' ');
                    var fieldLower = field.toLowerCase();
                    if (fieldLower === 'name') {
                        $('#productName').addClass('is-invalid');
                        $('#nameError').text(msgs).show();
                    } else if (fieldLower === 'category') {
                        $('#productCategory').addClass('is-invalid');
                        $('#categoryError').text(msgs).show();
                    } else if (fieldLower === 'price') {
                        $('#productPrice').addClass('is-invalid');
                        $('#priceError').text(msgs).show();
                    } else if (fieldLower === 'stock') {
                        $('#productStock').addClass('is-invalid');
                        $('#stockError').text(msgs).show();
                    }
                }
            }
        }

        if (res.message) {
            $('#modalGeneralError').text(res.message).removeClass('d-none');
        }
    }

    /**
     * Clears modal validation styling and error text
     */
    function resetFormErrors() {
        $('#productForm input, #productForm select').removeClass('is-invalid');
        $('.invalid-feedback').hide();
        $('#modalGeneralError').addClass('d-none').text('');
    }

    /**
     * Prompts for product deletion with SweetAlert2 and safely escaped text
     */
    function confirmDeleteProduct(id, name) {
        // Naming product as safe text via SweetAlert2
        Swal.fire({
            title: 'Delete Product?',
            text: 'Are you sure you want to permanently delete "' + (name || 'Product #' + id) + '"? This will remove the item from inventory.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#123F8A',
            cancelButtonColor: '#667383',
            confirmButtonText: 'Yes, delete it',
            cancelButtonText: 'Cancel',
            reverseButtons: true
        }).then(function (result) {
            if (result.isConfirmed) {
                executeDeleteProduct(id);
            }
        });
    }

    /**
     * Executes product deletion via POST /products/{id}/delete
     */
    function executeDeleteProduct(id) {
        $.ajax({
            url: '/products/' + id + '/delete',
            type: 'POST',
            dataType: 'json'
        }).done(function (res) {
            if (res && res.isSuccess) {
                Waterlily.notifySuccess(res.message || 'Product deleted successfully.');
                // Reconcile and reload products
                loadProducts(true);
            } else {
                Waterlily.notifyError(res ? res.message : 'Failed to delete product.');
            }
        }).fail(function (jqXHR) {
            Waterlily.handleAjaxError(jqXHR);
        });
    }

    /**
     * Opens the Bulk Add Products Modal
     */
    function openBulkModal() {
        resetBulkModal();
        if (bulkModal) bulkModal.show();
    }

    /**
     * Resets the entire Bulk Modal state
     */
    function resetBulkModal() {
        resetBulkFile();
        bulkPreviewData = null;
        currentBulkFilterTab = 'all';

        $('#bulkUploadStep').show();
        $('#bulkPreviewStep').hide();
        $('#btnBulkBackToUpload').addClass('d-none');
        $('#btnApproveAndImport').addClass('d-none');
        $('#previewTableBody').empty();
        $('#previewTotalCount').text('0');
        $('#previewValidCount').text('0');
        $('#previewInvalidCount').text('0');
        $('#badgeTabAll').text('0');
        $('#badgeTabValid').text('0');
        $('#badgeTabInvalid').text('0');
        setActivePreviewTab($('#tabAllRecords'));
    }

    /**
     * Resets the uploaded file input and selection card
     */
    function resetBulkFile() {
        currentBulkFile = null;
        $('#bulkFileInput').val('');
        $('#selectedFileInfo').addClass('d-none');
        $('#bulkUploadError').addClass('d-none').text('');
        $('#btnExtractAndValidate').prop('disabled', true);
    }

    /**
     * Handles file selection from file input or drag-and-drop
     */
    function handleBulkFileSelection(file) {
        if (!file) return;

        var ext = file.name.split('.').pop().toLowerCase();
        if (ext !== 'csv' && ext !== 'xlsx' && ext !== 'txt') {
            $('#bulkUploadError')
                .text('Unsupported file format. Please upload a .csv or .xlsx file.')
                .removeClass('d-none');
            resetBulkFile();
            return;
        }

        // Check file size (max 10MB)
        if (file.size > 10 * 1024 * 1024) {
            $('#bulkUploadError')
                .text('File size cannot exceed 10MB.')
                .removeClass('d-none');
            resetBulkFile();
            return;
        }

        currentBulkFile = file;
        $('#bulkUploadError').addClass('d-none').text('');

        // Update file info display
        $('#selectedFileName').text(file.name);
        var sizeKb = (file.size / 1024).toFixed(1);
        var sizeMb = (file.size / (1024 * 1024)).toFixed(2);
        $('#selectedFileSize').text(file.size > 1024 * 1024 ? sizeMb + ' MB' : sizeKb + ' KB');
        $('#selectedFileInfo').removeClass('d-none');
        $('#btnExtractAndValidate').prop('disabled', false);
    }

    /**
     * Sends file to /products/bulk-preview to extract and validate records
     */
    function extractAndValidateBulkFile() {
        if (!currentBulkFile) return;

        var $btn = $('#btnExtractAndValidate');
        var $spinner = $('#validateSpinner');
        var $btnText = $('#validateBtnText');

        $btn.prop('disabled', true);
        $spinner.removeClass('d-none');
        $btnText.text('Extracting & Validating...');
        $('#bulkUploadError').addClass('d-none').text('');

        var formData = new FormData();
        formData.append('file', currentBulkFile);

        $.ajax({
            url: '/products/bulk-preview',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            dataType: 'json'
        }).done(function (res) {
            if (res && res.isSuccess && res.data) {
                bulkPreviewData = res.data;
                displayBulkPreview(res.data);
            } else {
                var errorMsg = res && res.message ? res.message : 'Failed to extract and validate records.';
                $('#bulkUploadError').text(errorMsg).removeClass('d-none');
            }
        }).fail(function (jqXHR) {
            var msg = jqXHR.responseJSON && jqXHR.responseJSON.message
                ? jqXHR.responseJSON.message
                : 'Server error while processing spreadsheet.';
            $('#bulkUploadError').text(msg).removeClass('d-none');
        }).always(function () {
            $btn.prop('disabled', false);
            $spinner.addClass('d-none');
            $btnText.text('Extract & Validate Data');
        });
    }

    /**
     * Renders preview summary metrics and activates preview step
     */
    function displayBulkPreview(preview) {
        $('#previewTotalCount').text(preview.totalRecords);
        $('#previewValidCount').text(preview.validRecordsCount);
        $('#previewInvalidCount').text(preview.invalidRecordsCount);

        $('#badgeTabAll').text(preview.totalRecords);
        $('#badgeTabValid').text(preview.validRecordsCount);
        $('#badgeTabInvalid').text(preview.invalidRecordsCount);

        // Update alert message and approve button state
        var $alert = $('#previewStatusAlert');
        var $alertText = $('#previewStatusAlertText');
        var $btnImport = $('#btnApproveAndImport');
        var $importBtnText = $('#importBtnText');

        if (preview.validRecordsCount > 0) {
            $alert.removeClass('alert-danger').addClass('alert-info');
            $alertText.html(
                '<strong>' + preview.validRecordsCount + ' acceptable records</strong> ready to import. ' +
                (preview.invalidRecordsCount > 0 ? (preview.invalidRecordsCount + ' rejected records will be skipped. ') : '') +
                'Click <strong>Approve &amp; Import</strong> to add them to your inventory database.'
            );
            $btnImport.prop('disabled', false).removeClass('d-none');
            $importBtnText.text('Approve & Import (' + preview.validRecordsCount + ') Valid Records');
        } else {
            $alert.removeClass('alert-info').addClass('alert-danger');
            $alertText.html(
                '<strong>No acceptable records found.</strong> All ' + preview.totalRecords +
                ' records have validation errors. Please review the rejected reasons below, fix your spreadsheet, and upload again.'
            );
            $btnImport.prop('disabled', true).addClass('d-none');
        }

        // Switch to preview view
        $('#bulkUploadStep').hide();
        $('#bulkPreviewStep').show();
        $('#btnBulkBackToUpload').removeClass('d-none');

        // Set default tab and render table
        currentBulkFilterTab = 'all';
        setActivePreviewTab($('#tabAllRecords'));
        renderBulkPreviewTable('all');
    }

    /**
     * Sets active preview tab pill styling
     */
    function setActivePreviewTab($activeTab) {
        $('#previewTabs .nav-link').removeClass('active');
        $activeTab.addClass('active');
    }

    /**
     * Renders the Preview Table filtered by 'all', 'valid', or 'invalid'
     */
    function renderBulkPreviewTable(filter) {
        if (!bulkPreviewData) return;

        var rows = [];
        if (filter === 'valid') {
            rows = bulkPreviewData.validRecords || [];
        } else if (filter === 'invalid') {
            rows = bulkPreviewData.invalidRecords || [];
        } else {
            rows = bulkPreviewData.allRecords || [];
        }

        var $tbody = $('#previewTableBody');
        $tbody.empty();

        if (rows.length === 0) {
            $tbody.html('<tr><td colspan="6" class="text-center py-4 text-muted">No records in this category.</td></tr>');
            return;
        }

        rows.forEach(function (r) {
            var $tr = $('<tr>');

            // ID / Row number
            var rowIdText = r.rawId ? r.rawId : ('#' + r.rowNumber);
            $tr.append($('<td>').addClass('fw-semibold text-muted').text(rowIdText));

            // Name (highlight cell in red if Name has error)
            var $tdName = $('<td>').text(r.name || '—');
            if (r.fieldErrors && r.fieldErrors.Name) {
                $tdName.addClass('cell-invalid').attr('title', r.fieldErrors.Name);
            }
            $tr.append($tdName);

            // Category (highlight cell in red if Category has error)
            var $tdCat = $('<td>').text(r.category || '—');
            if (r.fieldErrors && r.fieldErrors.Category) {
                $tdCat.addClass('cell-invalid').attr('title', r.fieldErrors.Category);
            }
            $tr.append($tdCat);

            // Price (highlight cell in red if Price has error)
            var priceDisplay = r.price !== null && r.price !== undefined
                ? Waterlily.formatCurrency(r.price)
                : (r.rawPrice ? r.rawPrice : '—');
            var $tdPrice = $('<td>').addClass('text-end text-nowrap').text(priceDisplay);
            if (r.fieldErrors && r.fieldErrors.Price) {
                $tdPrice.addClass('cell-invalid').attr('title', r.fieldErrors.Price);
            }
            $tr.append($tdPrice);

            // Stock (highlight cell in red if Stock has error)
            var stockDisplay = r.stock !== null && r.stock !== undefined
                ? r.stock.toLocaleString()
                : (r.rawStock ? r.rawStock : '—');
            var $tdStock = $('<td>').addClass('text-end text-nowrap').text(stockDisplay);
            if (r.fieldErrors && r.fieldErrors.Stock) {
                $tdStock.addClass('cell-invalid').attr('title', r.fieldErrors.Stock);
            }
            $tr.append($tdStock);

            // Status and Error Details
            var $tdStatus = $('<td>');
            if (r.isValid) {
                $tdStatus.html(
                    '<span class="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 px-2 py-1">' +
                    '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="me-1"><polyline points="20 6 9 17 4 12"></polyline></svg>' +
                    'Ready to Import</span>'
                );
            } else {
                var errHtml = '<span class="badge bg-danger bg-opacity-10 text-danger border border-danger border-opacity-25 px-2 py-1 mb-1">' +
                              '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="me-1"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>' +
                              'Rejected</span>';

                if (r.errors && r.errors.length > 0) {
                    errHtml += '<div class="text-danger small mt-1">';
                    r.errors.forEach(function (err) {
                        errHtml += '<div>&bull; ' + $('<div>').text(err).html() + '</div>';
                    });
                    errHtml += '</div>';
                }
                $tdStatus.html(errHtml);
            }
            $tr.append($tdStatus);

            $tbody.append($tr);
        });
    }

    /**
     * Executes bulk import of validated records via POST /products/bulk-import
     */
    function executeBulkImport() {
        if (!bulkPreviewData || !bulkPreviewData.validRecords || bulkPreviewData.validRecords.length === 0) {
            Waterlily.notifyError('No valid records to import.');
            return;
        }

        var validCount = bulkPreviewData.validRecordsCount;
        var invalidCount = bulkPreviewData.invalidRecordsCount;

        var confirmTitle = 'Import ' + validCount + ' Products?';
        var confirmText = 'Are you sure you want to add ' + validCount + ' valid product(s) to the inventory database?' +
            (invalidCount > 0 ? (' ' + invalidCount + ' rejected record(s) will be skipped.') : '');

        Waterlily.confirmAction(confirmTitle, confirmText, 'Yes, Import Now').then(function (result) {
            if (!result.isConfirmed) return;

            var $btn = $('#btnApproveAndImport');
            var $spinner = $('#importSpinner');
            var $btnText = $('#importBtnText');

            $btn.prop('disabled', true);
            $spinner.removeClass('d-none');
            $btnText.text('Importing Products...');

            var payload = {
                Products: bulkPreviewData.validRecords.map(function (r) {
                    return {
                        Name: r.name,
                        Category: r.category,
                        Price: r.price,
                        Stock: r.stock
                    };
                })
            };

            $.ajax({
                url: '/products/bulk-import',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload),
                dataType: 'json'
            }).done(function (res) {
                if (res && res.isSuccess) {
                    if (bulkModal) bulkModal.hide();
                    Waterlily.notifySuccess(res.message || 'Successfully imported ' + validCount + ' products.');
                    // Reload products table and reflect new records immediately
                    loadProducts(true);
                } else {
                    Waterlily.notifyError(res && res.message ? res.message : 'Bulk import failed.');
                }
            }).fail(function (jqXHR) {
                Waterlily.handleAjaxError(jqXHR);
            }).always(function () {
                $btn.prop('disabled', false);
                $spinner.addClass('d-none');
                $btnText.text('Approve & Import Valid Records');
            });
        });
    }

});

