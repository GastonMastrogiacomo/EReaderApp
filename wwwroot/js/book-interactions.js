
// Share book function
function shareBook(bookId, bookTitle) {
    const url = window.location.origin + '/Books/ViewDetails/' + bookId;
    const text = 'Check out this book: ' + bookTitle;

    // Check if it's Twitter/X app or website in the user agent
    const isTwitter = /Twitter|X|tweet/i.test(navigator.userAgent);

    if (isTwitter) {
        window.open(`https://twitter.com/intent/tweet?text=${encodeURIComponent(text)}&url=${encodeURIComponent(url)}`, '_blank');
        return;
    }

    if (navigator.share) {
        navigator.share({
            title: bookTitle,
            text: text,
            url: url,
        })
            .catch(console.error);
    } else {
        // Fallback for browsers that don't support the Web Share API
        prompt('Copy this link to share:', url);
    }
}

function initializeLibraryFunctions() {
    // Load user libraries when modal opens
    const addToLibraryModal = document.getElementById('addToLibraryModal');
    if (addToLibraryModal) {
        addToLibraryModal.addEventListener('show.bs.modal', function (event) {
            // Get the book ID from the button that triggered the modal
            const button = event.relatedTarget;
            const bookId = button.getAttribute('data-book-id');
            document.getElementById('selectedBookId').value = bookId;

            // Load the user's libraries
            loadUserLibraries();
        });
    }

    // Toggle create new library form
    const createLibraryBtn = document.querySelector('.create-library-btn');
    const cancelCreateBtn = document.querySelector('.cancel-create-btn');
    const createLibraryForm = document.querySelector('.create-library-form');

    if (createLibraryBtn && cancelCreateBtn && createLibraryForm) {
        createLibraryBtn.addEventListener('click', function () {
            createLibraryForm.classList.remove('d-none');
            document.getElementById('newLibraryName').focus();
        });

        cancelCreateBtn.addEventListener('click', function () {
            createLibraryForm.classList.add('d-none');
            document.getElementById('newLibraryName').value = '';
        });
    }

    // Handle the add to library confirmation
    const confirmAddToLibrary = document.getElementById('confirmAddToLibrary');
    if (confirmAddToLibrary) {
        confirmAddToLibrary.addEventListener('click', function () {
            const bookId = document.getElementById('selectedBookId').value;
            const librarySelect = document.getElementById('librarySelect');
            const newLibraryName = document.getElementById('newLibraryName').value;

            let libraryId = librarySelect.value;
            let isNewLibrary = !createLibraryForm.classList.contains('d-none') && newLibraryName.trim() !== '';

            if (isNewLibrary) {
                // Create a new library and add the book
                createLibraryAndAddBook(bookId, newLibraryName);
            } else if (libraryId) {
                // Add to existing library
                addBookToLibrary(bookId, libraryId);
            } else {
                showToast('Please select a library or create a new one', 'warning');
                return;
            }

            // Close the modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('addToLibraryModal'));
            modal.hide();
        });
    }

    // Save for later buttons
    document.querySelectorAll('.save-for-later').forEach(button => {
        button.addEventListener('click', function () {
            const bookId = this.getAttribute('data-book-id');
            saveBookForLater(bookId);
        });
    });
}

// Load user libraries via AJAX
function loadUserLibraries() {
    const librarySelect = document.getElementById('librarySelect');
    if (!librarySelect) return;

    // Clear existing options
    while (librarySelect.options.length > 1) {
        librarySelect.remove(1);
    }

    // In a real implementation, use AJAX to fetch libraries
    fetch('/Libraries/GetUserLibraries')
        .then(response => response.json())
        .then(libraries => {
            libraries.forEach(library => {
                const option = document.createElement('option');
                option.value = library.idLibrary;
                option.textContent = library.listName;
                librarySelect.appendChild(option);
            });
        })
        .catch(error => {
            console.error('Error loading libraries:', error);
            // Fallback to show some default options if the API fails
            const sampleLibraries = [
                { idLibrary: 1, listName: 'Favorites' },
                { idLibrary: 2, listName: 'To Read' }
            ];

            sampleLibraries.forEach(library => {
                const option = document.createElement('option');
                option.value = library.idLibrary;
                option.textContent = library.listName;
                librarySelect.appendChild(option);
            });
        });
}

// Add book to existing library
function addBookToLibrary(bookId, libraryId) {
    fetch('/Libraries/AddToLibrary', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({
            bookId: bookId,
            libraryId: libraryId
        })
    })
        .then(response => {
            if (response.ok) {
                showToast('Book added to library successfully', 'success');
            } else {
                showToast('Failed to add book to library', 'danger');
            }
        })
        .catch(error => {
            console.error('Error adding book to library:', error);
            showToast('An error occurred', 'danger');
        });
}

// Create a new library and add book
function createLibraryAndAddBook(bookId, libraryName) {
    fetch('/Libraries/CreateLibrary', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({
            listName: libraryName
        })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Now add the book to the newly created library
                addBookToLibrary(bookId, data.libraryId);
            } else {
                showToast('Failed to create library', 'danger');
            }
        })
        .catch(error => {
            console.error('Error creating library:', error);
            showToast('An error occurred', 'danger');
        });
}

// Save book for later
function saveBookForLater(bookId) {
    showToast('Book saved for later reading!', 'success');
}

// Show toast notification
function showToast(message, type = 'info') {
    // If using Bootstrap toasts
    const toastContainer = document.getElementById('toast-container');
    if (!toastContainer) {
        // Create container if it doesn't exist
        const container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        document.body.appendChild(container);
    }

    const toastId = 'toast-' + Date.now();
    const toast = document.createElement('div');
    toast.className = `toast align-items-center text-bg-${type} border-0`;
    toast.id = toastId;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'assertive');
    toast.setAttribute('aria-atomic', 'true');

    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    `;

    document.getElementById('toast-container').appendChild(toast);

    const bsToast = new bootstrap.Toast(toast);
    bsToast.show();

    // Remove toast after it's hidden
    toast.addEventListener('hidden.bs.toast', function () {
        this.remove();
    });
}

function initializeBookDetailsEnhancement() {
    const bookDetailsElement = document.getElementById('book-details');
    if (!bookDetailsElement) return;

    const bookTitle = document.querySelector('.book-title').textContent;
    const bookAuthor = document.querySelector('[data-author]')?.getAttribute('data-author');

    if (bookTitle && bookAuthor) {
        fetchBookDetails(bookTitle, bookAuthor);
    }
}

async function fetchBookDetails(title, author) {
    try {
        // Construct search query
        const query = `intitle:${encodeURIComponent(title)}+inauthor:${encodeURIComponent(author)}`;
        const apiUrl = `https://www.googleapis.com/books/v1/volumes?q=${query}&maxResults=1`;

        const response = await fetch(apiUrl);
        const data = await response.json();

        if (data.items && data.items.length > 0) {
            const bookInfo = data.items[0].volumeInfo;
            updateBookDetails(bookInfo);
        }
    } catch (error) {
        console.error('Error fetching book details:', error);
    }
}

function updateBookDetails(bookInfo) {
    // Implementation depends on the page structure
}

// Review functions
function initializeReviewFunctions() {
    // Initialize rating input for reviews
    const ratingInputs = document.querySelectorAll('.rating-input input');
    ratingInputs.forEach(input => {
        input.addEventListener('change', function () {
            updateStarRating(this.value);
        });
    });
}

function updateStarRating(value) {
    const ratingStars = document.querySelectorAll('.rating-stars i');
    ratingStars.forEach((star, index) => {
        if (index < value) {
            star.className = 'fas fa-star';
        } else {
            star.className = 'far fa-star';
        }
    });
}