// Admin panel functionality
document.addEventListener('DOMContentLoaded', function () {
    // Sidebar toggle functionality for mobile devices
    const toggleSidebarBtn = document.getElementById('toggleSidebar');
    const closeSidebarBtn = document.getElementById('closeSidebar');
    const sidebar = document.getElementById('sidebar');

    if (toggleSidebarBtn && sidebar) {
        toggleSidebarBtn.addEventListener('click', function () {
            sidebar.classList.toggle('show');
        });
    }

    if (closeSidebarBtn && sidebar) {
        closeSidebarBtn.addEventListener('click', function () {
            sidebar.classList.remove('show');
        });
    }

    // Close sidebar when clicking outside on mobile
    document.addEventListener('click', function (event) {
        const isClickInsideSidebar = sidebar.contains(event.target);
        const isClickToggleButton = toggleSidebarBtn.contains(event.target);

        if (!isClickInsideSidebar && !isClickToggleButton && sidebar.classList.contains('show')) {
            sidebar.classList.remove('show');
        }
    });

    // Initialize tooltips if Bootstrap is available
    if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    // DataTable initialization if it exists on the page
    if (typeof $.fn.DataTable !== 'undefined') {
        $('.datatable').DataTable({
            responsive: true,
            language: {
                search: "_INPUT_",
                searchPlaceholder: "Search...",
                lengthMenu: "Show _MENU_ entries",
                info: "Showing _START_ to _END_ of _TOTAL_ entries",
                infoEmpty: "Showing 0 to 0 of 0 entries",
                infoFiltered: "(filtered from _MAX_ total entries)",
                zeroRecords: "No matching records found",
                paginate: {
                    first: '<i class="fas fa-angle-double-left"></i>',
                    previous: '<i class="fas fa-angle-left"></i>',
                    next: '<i class="fas fa-angle-right"></i>',
                    last: '<i class="fas fa-angle-double-right"></i>'
                }
            }
        });
    }

    // Confirmation for delete actions
    /*document.querySelectorAll('.confirm-delete').forEach(function (button) {
        button.addEventListener('click', function (e) {
            if (!confirm('Are you sure you want to delete this item? This action cannot be undone.')) {
                e.preventDefault();
            }
        });
    });
    */

    // Auto-hide alerts after 5 seconds
    setTimeout(function () {
        document.querySelectorAll('.alert').forEach(function (alert) {
            // Create a Bootstrap alert instance and call hide
            if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                const bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            } else {
                // Fallback if Bootstrap is not available
                alert.style.display = 'none';
            }
        });
    }, 5000);

    // Function to handle dynamic form fields
    function setupDynamicFormFields() {
        // Add new form field
        document.querySelectorAll('.add-field-btn').forEach(function (button) {
            button.addEventListener('click', function () {
                const container = document.querySelector(this.dataset.target);
                const template = document.querySelector(this.dataset.template);

                if (container && template) {
                    const clone = template.content.cloneNode(true);
                    container.appendChild(clone);

                    // Setup remove button for the new field
                    const removeButtons = container.querySelectorAll('.remove-field-btn');
                    const newRemoveButton = removeButtons[removeButtons.length - 1];

                    if (newRemoveButton) {
                        newRemoveButton.addEventListener('click', function () {
                            this.closest('.dynamic-field').remove();
                        });
                    }
                }
            });
        });

        // Remove existing form fields
        document.querySelectorAll('.remove-field-btn').forEach(function (button) {
            button.addEventListener('click', function () {
                this.closest('.dynamic-field').remove();
            });
        });
    }

    // Initialize dynamic form fields if they exist
    setupDynamicFormFields();
});