// Configuración de PDF.js
pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.5.141/pdf.worker.min.js';

// Variables globales
let pdfDoc = null;
let currentPage = 1;
let totalPages = 0;
let scale = 1.0;
let bookmarks = [];
let tocItems = [];
let notes = [];
const bookId = document.getElementById('book-container').dataset.bookId || 1;
let isFitWidth = false;
let isFitPage = false;
let pageRendering = false;
let pageNumPending = null;
let readerSettings = {
    theme: 'light',
    fontFamily: 'Arial',
    fontSize: 16
};

// Función de inicialización
document.addEventListener('DOMContentLoaded', function () {
    console.log("PDF Reader initialized");

    // Cargar el PDF
    loadPDF();

    // Configurar eventos
    setupEventListeners();

    // Cargar marcadores guardados
    loadBookmarks();

    // Cargar notas
    if (bookId) {
        loadNotes();
    }

    // Cargar configuración del lector
    loadReaderSettings();

    // Ajustar tamaño al redimensionar
    window.addEventListener('resize', debounce(function () {
        if (pdfDoc) {
            if (isFitWidth) fitToWidth();
            else if (isFitPage) fitToPage();
            else renderCurrentPage();
        }
    }, 300));
});

// Cargar el PDF
async function loadPDF() {
    try {
        // Mostrar indicador de carga
        document.getElementById('loading-indicator').style.display = 'block';
        document.getElementById('single-page-container').style.display = 'none';

        // Obtener ruta del PDF del atributo data-pdf-path
        const pdfPath = document.querySelector('#book-container').dataset.pdfPath;

        if (!pdfPath) {
            console.error('Error: No se encontró la ruta del PDF');
            document.getElementById('loading-indicator').innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-triangle me-2"></i>
                    Error: No se encontró la ruta del PDF
                </div>`;
            return;
        }

        console.log("Cargando PDF desde:", pdfPath);
        pdfDoc = await pdfjsLib.getDocument(pdfPath).promise;
        totalPages = pdfDoc.numPages;

        console.log("PDF cargado, páginas totales:", totalPages);

        // Actualizar interfaz
        document.getElementById('page-count').textContent = totalPages;
        document.getElementById('current-page-input').max = totalPages;

        // Intentar cargar la tabla de contenidos
        await loadTableOfContents();

        // Intentar cargar posición guardada
        loadPosition();

        // O renderizar página inicial
        if (currentPage < 1) {
            currentPage = 1;
            renderCurrentPage();
        }

        // Ocultar indicador de carga
        document.getElementById('loading-indicator').style.display = 'none';

        // Mostrar el contenedor de página única
        document.getElementById('single-page-container').style.display = 'flex';
    } catch (error) {
        console.error('Error al cargar el PDF:', error);
        document.getElementById('loading-indicator').innerHTML = `
            <div class="alert alert-danger">
                <i class="fas fa-exclamation-triangle me-2"></i>
                Error al cargar el PDF: ${error.message}
            </div>`;
    }
}

// Cargar tabla de contenidos
async function loadTableOfContents() {
    try {
        // Intentar obtener el outline del PDF
        const outline = await pdfDoc.getOutline();

        if (outline && outline.length > 0) {
            // Procesar items del outline
            tocItems = await processTocItems(outline);

            // Renderizar la tabla de contenidos
            renderTableOfContents();
        } else {
            // No hay tabla de contenidos
            document.getElementById('toc-loading').style.display = 'none';
            document.getElementById('toc-empty').style.display = 'block';
        }
    } catch (error) {
        console.error('Error al cargar la tabla de contenidos:', error);
        document.getElementById('toc-loading').style.display = 'none';
        document.getElementById('toc-empty').style.display = 'block';
        document.getElementById('toc-empty').textContent = 'Error al cargar la tabla de contenidos';
    }
}

// Procesar items de la tabla de contenidos y resolver destinos
async function processTocItems(outline, level = 0) {
    const items = [];

    for (const item of outline) {
        if (!item.dest && !item.url) continue;

        let pageNumber = null;

        // Obtener número de página
        if (item.dest) {
            try {
                if (typeof item.dest === 'string') {
                    // Necesita resolverse
                    const dest = await pdfDoc.getDestination(item.dest);
                    const index = await pdfDoc.getPageIndex(dest[0]);
                    pageNumber = index + 1;
                } else if (Array.isArray(item.dest)) {
                    // Ya es un array de destino
                    const index = await pdfDoc.getPageIndex(item.dest[0]);
                    pageNumber = index + 1;
                }
            } catch (error) {
                console.error('Error al resolver destino:', error);
            }
        }

        // Añadir el item
        const tocItem = {
            title: item.title,
            page: pageNumber,
            level: level,
            children: []
        };

        // Procesar hijos si existen
        if (item.items && item.items.length > 0) {
            tocItem.children = await processTocItems(item.items, level + 1);
        }

        items.push(tocItem);
    }

    return items;
}

// Renderizar tabla de contenidos
function renderTableOfContents() {
    const tocList = document.getElementById('toc-list');

    // Ocultar indicador de carga
    document.getElementById('toc-loading').style.display = 'none';

    if (tocItems.length === 0) {
        document.getElementById('toc-empty').style.display = 'block';
        return;
    }

    // Función recursiva para renderizar items
    function renderItems(items, container) {
        for (const item of items) {
            if (!item.title) continue;

            const tocElement = document.createElement('a');
            tocElement.className = 'list-group-item list-group-item-action toc-item';
            tocElement.style.paddingLeft = `${(item.level * 1.0) + 1}rem`;

            // Texto con sangría según nivel
            tocElement.innerHTML = `
                <div class="d-flex w-100 justify-content-between">
                    <span>${item.title}</span>
                    ${item.page ? `<small>Pág. ${item.page}</small>` : ''}
                </div>
            `;

            // Si tiene página, añadir evento para navegar
            if (item.page) {
                tocElement.addEventListener('click', function () {
                    goToPage(item.page);

                    // Actualizar estado activo
                    document.querySelectorAll('.toc-item').forEach(i => {
                        i.classList.remove('active');
                    });
                    this.classList.add('active');

                    // Si estamos en móvil, cerrar el panel
                    if (window.innerWidth < 992) {
                        document.getElementById('side-panel').style.display = 'none';
                    }
                });
            }

            container.appendChild(tocElement);

            // Renderizar hijos recursivamente
            if (item.children && item.children.length > 0) {
                renderItems(item.children, container);
            }
        }
    }

    // Limpiar y renderizar
    tocList.innerHTML = '';
    renderItems(tocItems, tocList);

    // Actualizar el estado activo para la página actual
    updateActiveTocItem();
}

// Actualizar el elemento activo en la tabla de contenidos
function updateActiveTocItem() {
    document.querySelectorAll('.toc-item').forEach(item => {
        item.classList.remove('active');

        // Extraer el número de página del elemento
        const pageText = item.querySelector('small');
        if (pageText) {
            const pageNum = parseInt(pageText.textContent.replace('Pág. ', ''));
            if (pageNum === currentPage) {
                item.classList.add('active');

                // Scroll para mostrar el elemento activo
                const panel = document.getElementById('toc-content');
                if (panel.scrollTop !== undefined) {
                    panel.scrollTop = item.offsetTop - panel.offsetTop - 10;
                }
            }
        }
    });
}

// Sistema de cola para renderizado
function queueRenderPage(num) {
    if (pageRendering) {
        pageNumPending = num;
    } else {
        renderCurrentPage();
    }
}

// Renderizar la página actual
async function renderCurrentPage() {
    if (!pdfDoc) return;

    pageRendering = true;

    try {
        // Modo página única siempre
        // Ocultar temporalmente para evitar parpadeos
        document.getElementById('single-page-container').style.opacity = '0.5';

        const canvas = document.getElementById('single-canvas');
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        await renderPage(currentPage, canvas);

        // Mostrar el contenedor
        document.getElementById('single-page-container').style.opacity = '1';

        // Actualizar número de página actual
        document.getElementById('current-page-input').value = currentPage;
        document.getElementById('bookmark-page').textContent = currentPage;

        // Actualizar elemento activo en TOC
        updateActiveTocItem();

        // Actualizar estado de botones de navegación
        document.getElementById('prev-page').disabled = currentPage <= 1;
        document.getElementById('first-page').disabled = currentPage <= 1;
        document.getElementById('next-page').disabled = currentPage >= totalPages;
        document.getElementById('last-page').disabled = currentPage >= totalPages;

        // Actualizar URL con número de página
        if (history.replaceState) {
            history.replaceState(null, null, `#${currentPage}`);
        }

        // Actualizar la barra de progreso
        updateProgressBar();

        // Guardar posición en localStorage
        savePosition();

        // Comprobar si hay una página pendiente
        pageRendering = false;
        if (pageNumPending !== null) {
            const num = pageNumPending;
            pageNumPending = null;
            goToPage(num);
        }
    } catch (error) {
        console.error(`Error al renderizar página ${currentPage}: `, error);
        pageRendering = false;
        document.getElementById('single-page-container').style.opacity = '1';
        showNotification('Error al renderizar la página', 'danger');
    }
}

// Renderizar una página específica en un canvas
async function renderPage(pageNum, canvas) {
    try {
        // Obtener la página
        const page = await pdfDoc.getPage(pageNum);

        // Obtener dimensiones del contenedor
        const container = document.getElementById('book-container');
        const containerWidth = container.clientWidth - 40; // margen
        const containerHeight = container.clientHeight - 40; // margen

        // Obtener viewport original
        const viewport = page.getViewport({ scale: 1.0 });

        // Calcular la escala adecuada
        let pageScale = scale;

        if (isFitWidth) {
            // Ajustar al ancho
            pageScale = containerWidth / viewport.width;
        } else if (isFitPage) {
            // Ajustar a la página
            const scaleX = containerWidth / viewport.width;
            const scaleY = containerHeight / viewport.height;

            pageScale = Math.min(scaleX, scaleY);
        } else {
            // Verificar que la escala no sea demasiado grande
            const maxScaleX = containerWidth / viewport.width;
            const maxScaleY = containerHeight / viewport.height;
            const maxScale = Math.min(maxScaleX, maxScaleY);

            if (scale > maxScale * 1.5) {
                pageScale = maxScale * 1.5;
            }
        }

        const scaledViewport = page.getViewport({ scale: pageScale });

        // Configurar dimensiones del canvas
        canvas.width = scaledViewport.width;
        canvas.height = scaledViewport.height;

        // Configurar dimensiones del contenedor de la página
        const pageDiv = canvas.parentElement;
        pageDiv.style.width = `${scaledViewport.width}px`;
        pageDiv.style.height = `${scaledViewport.height}px`;

        // Aplicar configuración de tema
        applyTheme(pageDiv);

        // Renderizar
        const renderContext = {
            canvasContext: canvas.getContext('2d'),
            viewport: scaledViewport
        };

        await page.render(renderContext).promise;

        // Actualizar estado de botones de zoom
        document.getElementById('zoom-in').disabled = scale >= 2.0;
        document.getElementById('zoom-out').disabled = scale <= 0.5;

        return page;
    } catch (error) {
        console.error(`Error al renderizar página ${pageNum}: `, error);
        throw error;
    }
}

// Función para aplicar el tema al lector
function applyTheme(element) {
    element.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
    element.classList.add(`theme-${readerSettings.theme}`);
}

// Función para ajustar al ancho
function fitToWidth() {
    isFitWidth = true;
    isFitPage = false;

    document.getElementById('zoom-text').textContent = 'Ancho';
    renderCurrentPage();
}

// Función para ajustar a la página
function fitToPage() {
    isFitWidth = false;
    isFitPage = true;
    document.getElementById('zoom-text').textContent = 'Página';
    renderCurrentPage();
}

// Actualizar la barra de progreso
function updateProgressBar() {
    const progress = (currentPage / totalPages) * 100;
    const progressBar = document.getElementById('reading-progress');
    progressBar.style.width = `${progress}%`;
    progressBar.setAttribute('aria-valuenow', progress);
}

// Funciones de navegación
function goToPage(pageNum) {
    if (!pageNum || pageNum < 1 || pageNum > totalPages) return;

    showPageLoadingIndicator();

    const direction = pageNum > currentPage ? 'next' : 'prev';
    animatePageTurn(direction);

    currentPage = pageNum;
    queueRenderPage(currentPage);
}

function previousPage() {
    if (currentPage <= 1) return;
    goToPage(currentPage - 1);
}

function nextPage() {
    if (currentPage >= totalPages) return;
    goToPage(currentPage + 1);
}

// Indicador de carga para cambios de página
function showPageLoadingIndicator() {
    // Si ya existe, no crear otro
    if (document.getElementById('page-loading-indicator')) return;

    const loadingIndicator = document.createElement('div');
    loadingIndicator.id = 'page-loading-indicator';
    loadingIndicator.className = 'position-absolute top-50 start-50 translate-middle';
    loadingIndicator.innerHTML = `
        <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Cargando página...</span>
        </div>
    `;

    document.getElementById('book-container').appendChild(loadingIndicator);
}

function hidePageLoadingIndicator() {
    const indicator = document.getElementById('page-loading-indicator');
    if (indicator) {
        indicator.remove();
    }
}

// Animación de cambio de página
function animatePageTurn(direction) {
    const container = document.getElementById('single-page-container');
    container.classList.add(`page-turning-${direction}`);
    setTimeout(() => {
        container.classList.remove(`page-turning-${direction}`);
        hidePageLoadingIndicator();
    }, 300);
}

// Funciones de zoom
function setZoom(newScale) {
    scale = newScale;
    isFitWidth = false;
    isFitPage = false;
    document.getElementById('zoom-text').textContent = `${Math.round(scale * 100)}%`;
    renderCurrentPage();
}

// Función para mostrar notificaciones
function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = `alert alert-${type} alert-dismissible fade show notification-toast`;
    notification.role = 'alert';
    notification.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;

    notification.style.position = 'fixed';
    notification.style.bottom = '20px';
    notification.style.right = '20px';
    notification.style.zIndex = '9999';
    notification.style.minWidth = '250px';
    notification.style.boxShadow = '0 4px 8px rgba(0,0,0,0.1)';

    document.body.appendChild(notification);

    setTimeout(() => {
        notification.classList.remove('show');
        setTimeout(() => {
            document.body.removeChild(notification);
        }, 150);
    }, 3000);
}

// Funciones para marcadores
function loadBookmarks() {
    const savedBookmarks = localStorage.getItem(`bookmarks_${bookId}`);
    if (savedBookmarks) {
        bookmarks = JSON.parse(savedBookmarks);
        renderBookmarks();
    }
}

function saveBookmark() {
    const title = document.getElementById('bookmark-title').value.trim() || `Página ${currentPage}`;

    const bookmark = {
        id: Date.now(),
        page: currentPage,
        title: title,
        createdAt: new Date().toISOString()
    };

    bookmarks.push(bookmark);
    localStorage.setItem(`bookmarks_${bookId}`, JSON.stringify(bookmarks));

    const modalElement = document.getElementById('bookmark-modal');
    const modal = bootstrap.Modal.getInstance(modalElement);
    if (modal) {
        modal.hide();
    } else {
        modalElement.classList.remove('show');
        modalElement.style.display = 'none';
        document.body.classList.remove('modal-open');
        const backdrop = document.querySelector('.modal-backdrop');
        if (backdrop) backdrop.remove();
    }

    document.getElementById('bookmark-title').value = '';
    renderBookmarks();
    showNotification('Marcador guardado con éxito', 'success');
}

function renderBookmarks() {
    const bookmarksList = document.getElementById('bookmarks-list');

    if (bookmarks.length === 0) {
        bookmarksList.innerHTML = `
            <div class="text-center p-3 text-muted small">
                No hay marcadores guardados
            </div>`;
        return;
    }

    bookmarks.sort((a, b) => a.page - b.page);
    bookmarksList.innerHTML = '';

    bookmarks.forEach(bookmark => {
        const item = document.createElement('a');
        item.className = `list-group-item list-group-item-action bookmark-item ${bookmark.page === currentPage ? 'active' : ''}`;
        item.innerHTML = `
            <div class="d-flex w-100 justify-content-between">
                <h6 class="mb-1">${bookmark.title}</h6>
                <small>Pág. ${bookmark.page}</small>
            </div>
            <div class="d-flex justify-content-between align-items-center">
                <small class="text-muted">${new Date(bookmark.createdAt).toLocaleDateString()}</small>
                <button class="btn btn-sm btn-outline-danger delete-bookmark" data-bookmark-id="${bookmark.id}">
                    <i class="fas fa-trash-alt"></i>
                </button>
            </div>
        `;

        item.addEventListener('click', function (e) {
            if (e.target.closest('.delete-bookmark')) return;

            goToPage(bookmark.page);

            document.querySelectorAll('.bookmark-item').forEach(i => {
                i.classList.remove('active');
            });
            this.classList.add('active');

            if (window.innerWidth < 992) {
                document.getElementById('side-panel').style.display = 'none';
            }
        });

        bookmarksList.appendChild(item);
    });

    document.querySelectorAll('.delete-bookmark').forEach(button => {
        button.addEventListener('click', function (e) {
            e.stopPropagation();
            const id = parseInt(this.getAttribute('data-bookmark-id'));
            deleteBookmark(id);
        });
    });
}

function deleteBookmark(id) {
    bookmarks = bookmarks.filter(b => b.id !== id);
    localStorage.setItem(`bookmarks_${bookId}`, JSON.stringify(bookmarks));
    renderBookmarks();
    showNotification('Marcador eliminado', 'warning');
}

// Guardar posición
function savePosition() {
    localStorage.setItem(`position_${bookId}`, JSON.stringify({
        page: currentPage,
        scale: scale,
        fitWidth: isFitWidth,
        fitPage: isFitPage,
        timestamp: new Date().toISOString()
    }));
}

// Cargar posición guardada
function loadPosition() {
    const savedPosition = localStorage.getItem(`position_${bookId}`);
    if (savedPosition) {
        try {
            const position = JSON.parse(savedPosition);

            currentPage = position.page || 1;
            scale = position.scale || 1.0;

            isFitWidth = position.fitWidth || false;
            isFitPage = position.fitPage || false;

            if (isFitWidth) {
                document.getElementById('zoom-text').textContent = 'Ancho';
            } else if (isFitPage) {
                document.getElementById('zoom-text').textContent = 'Página';
            } else {
                document.getElementById('zoom-text').textContent = `${Math.round(scale * 100)}%`;
            }
        } catch (e) {
            console.error('Error al cargar la posición guardada:', e);
            currentPage = 1;
            scale = 1.0;
        }
    }

    renderCurrentPage();
}

// Funciones para notas
async function loadNotes() {
    try {
        // Mostrar indicador de carga en el panel de notas
        const notesList = document.getElementById('notes-list');
        notesList.innerHTML = `
            <div class="text-center p-3">
                <div class="spinner-border spinner-border-sm text-primary" role="status">
                    <span class="visually-hidden">Cargando notas...</span>
                </div>
                <span class="ms-2">Cargando notas...</span>
            </div>
        `;

        // Realizar la solicitud al servidor
        const response = await fetch(`/Notes/GetBookNotes?bookId=${bookId}`);

        if (response.ok) {
            notes = await response.json();
            console.log('Notas cargadas:', notes);
            renderNotes();
        } else {
            console.error('Error al cargar las notas:', response.statusText);
            notesList.innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-circle me-2"></i>
                    Error al cargar las notas: ${response.statusText}
                </div>
            `;
        }
    } catch (error) {
        console.error('Error al cargar las notas:', error);
        document.getElementById('notes-list').innerHTML = `
            <div class="alert alert-danger">
                <i class="fas fa-exclamation-circle me-2"></i>
                Error al cargar las notas: ${error.message}
            </div>
        `;
    }
}

function renderNotes() {
    const notesList = document.getElementById('notes-list');

    if (notes.length === 0) {
        notesList.innerHTML = `
            <div class="text-center p-3 text-muted small notes-empty">
                No hay notas para este libro
            </div>`;
        return;
    }

    // Ordenar notas por fecha de creación (más recientes primero)
    notes.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
    notesList.innerHTML = '';

    notes.forEach(note => {
        const noteElement = document.createElement('div');
        noteElement.className = 'card mb-2 note-item';
        noteElement.dataset.noteId = note.id;

        const formattedDate = new Date(note.createdAt).toLocaleDateString(
            'es-ES',
            { year: 'numeric', month: 'short', day: 'numeric' }
        );

        const updatedInfo = note.updatedAt ?
            `<small class="text-muted">(Editada: ${new Date(note.updatedAt).toLocaleDateString()})</small>` : '';

        noteElement.innerHTML = `
            <div class="card-body py-2 px-3">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <small class="text-muted">${formattedDate}</small>
                    <div class="note-actions">
                        <button class="btn btn-sm btn-link p-0 me-2 edit-note" title="Editar">
                            <i class="fas fa-edit"></i>
                        </button>
                        <button class="btn btn-sm btn-link p-0 text-danger delete-note" title="Eliminar">
                            <i class="fas fa-trash-alt"></i>
                        </button>
                    </div>
                </div>
                <p class="note-content mb-0">${note.content}</p>
                ${updatedInfo}
            </div>
        `;

        // Agregar eventos para editar y eliminar
        const editButton = noteElement.querySelector('.edit-note');
        const deleteButton = noteElement.querySelector('.delete-note');

        editButton.addEventListener('click', () => {
            editNote(note);
        });

        deleteButton.addEventListener('click', () => {
            deleteNote(note.id);
        });

        notesList.appendChild(noteElement);
    });
}

async function createNote() {
    const content = document.getElementById('new-note-content').value.trim();
    if (!content) {
        showNotification('El contenido de la nota no puede estar vacío', 'warning');
        return;
    }

    // Mostrar indicador de carga
    const saveButton = document.getElementById('save-new-note');
    const originalText = saveButton.innerHTML;
    saveButton.disabled = true;
    saveButton.innerHTML = `
        <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
        Guardando...
    `;

    try {
        // Obtener el token de antiforgery del formulario oculto
        const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]')?.value;

        // Verificar si tenemos el token
        if (!token) {
            console.error('No se pudo encontrar el token de antiforgery');
            console.log('Formulario:', document.getElementById('antiforgery-form'));
            console.log('Token input:', document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]'));
            showNotification('Error de autenticación al guardar la nota', 'danger');

            // Restaurar el botón
            saveButton.disabled = false;
            saveButton.innerHTML = originalText;
            return;
        }

        console.log('Enviando nota con token:', token);
        console.log('Contenido:', content);
        console.log('ID del libro:', bookId);

        // Crear un objeto FormData
        const formData = new FormData();
        formData.append('bookId', bookId);
        formData.append('content', content);

        // Realizar la solicitud al servidor con FormData
        const response = await fetch('/Notes/CreateNote', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        // Restaurar el botón
        saveButton.disabled = false;
        saveButton.innerHTML = originalText;

        // Analizar la respuesta
        if (response.ok) {
            const result = await response.json();
            console.log('Respuesta del servidor:', result);

            if (result.success) {
                notes.push(result.note);
                renderNotes();
                document.getElementById('new-note-content').value = '';
                showNotification('Nota guardada correctamente', 'success');
            } else {
                showNotification('Error al guardar la nota: ' + (result.message || 'Respuesta inválida'), 'danger');
            }
        } else {
            const errorText = await response.text();
            console.error('Error al guardar la nota - Respuesta:', response.status, errorText);
            showNotification(`Error al guardar la nota (${response.status})`, 'danger');
        }
    } catch (error) {
        console.error('Error al crear la nota:', error);
        showNotification('Error al guardar la nota: ' + error.message, 'danger');

        // Restaurar el botón
        saveButton.disabled = false;
        saveButton.innerHTML = originalText;
    }
}

function editNote(note) {
    const noteElement = document.querySelector(`.note-item[data-note-id="${note.id}"]`);
    const contentElement = noteElement.querySelector('.note-content');
    const currentContent = contentElement.textContent;

    // Crear campo de texto para editar
    const textarea = document.createElement('textarea');
    textarea.className = 'form-control mb-2';
    textarea.value = currentContent;
    textarea.rows = 3;

    // Crear botones de acción
    const actions = document.createElement('div');
    actions.className = 'd-flex justify-content-end';
    actions.innerHTML = `
        <button class="btn btn-sm btn-outline-secondary me-2 cancel-edit">Cancelar</button>
        <button class="btn btn-sm btn-primary save-edit">Guardar</button>
    `;

    // Reemplazar el contenido con el editor
    contentElement.replaceWith(textarea);
    noteElement.querySelector('.note-actions').style.display = 'none';
    noteElement.appendChild(actions);

    // Enfocar el textarea
    textarea.focus();

    // Evento para cancelar la edición
    noteElement.querySelector('.cancel-edit').addEventListener('click', () => {
        textarea.replaceWith(contentElement);
        actions.remove();
        noteElement.querySelector('.note-actions').style.display = '';
    });

    // Evento para guardar la edición
    noteElement.querySelector('.save-edit').addEventListener('click', async () => {
        const newContent = textarea.value.trim();
        if (!newContent) {
            showNotification('El contenido de la nota no puede estar vacío', 'warning');
            return;
        }

        try {
            // Obtener el token de antiforgery del formulario oculto
            const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]')?.value;

            // Verificar si tenemos el token
            if (!token) {
                console.error('No se pudo encontrar el token de antiforgery');
                showNotification('Error de autenticación al actualizar la nota', 'danger');
                renderNotes();
                return;
            }

            // Usar FormData para compatibilidad con ASP.NET Core
            const formData = new FormData();
            formData.append('id', note.id);
            formData.append('content', newContent);

            const response = await fetch(`/Notes/UpdateNote`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            if (response.ok) {
                const result = await response.json();

                // Actualizar la nota en el array
                const index = notes.findIndex(n => n.id === note.id);
                if (index !== -1) {
                    notes[index] = result.note;
                }

                renderNotes();
                showNotification('Nota actualizada correctamente', 'success');
            } else {
                const errorText = await response.text();
                console.error('Error al actualizar la nota:', errorText);
                showNotification('Error al actualizar la nota', 'danger');
                renderNotes();
            }
        } catch (error) {
            console.error('Error al actualizar la nota:', error);
            showNotification('Error al actualizar la nota', 'danger');
            renderNotes();
        }
    });
}

async function deleteNote(id) {
    if (!confirm('¿Estás seguro de que deseas eliminar esta nota?')) {
        return;
    }

    try {
        // Obtener el token de antiforgery del formulario oculto
        const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]')?.value;

        // Verificar si tenemos el token
        if (!token) {
            console.error('No se pudo encontrar el token de antiforgery');
            showNotification('Error de autenticación al eliminar la nota', 'danger');
            return;
        }

        // Usar FormData para compatibilidad con ASP.NET Core
        const formData = new FormData();
        formData.append('id', id);

        const response = await fetch(`/Notes/DeleteNote`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        if (response.ok) {
            // Eliminar la nota del array
            notes = notes.filter(n => n.id !== id);
            renderNotes();
            showNotification('Nota eliminada correctamente', 'success');
        } else {
            const errorText = await response.text();
            console.error('Error al eliminar la nota:', errorText);
            showNotification('Error al eliminar la nota', 'danger');
        }
    } catch (error) {
        console.error('Error al eliminar la nota:', error);
        showNotification('Error al eliminar la nota', 'danger');
    }
}

// Funciones para la configuración del lector
async function loadReaderSettings() {
    try {
        const response = await fetch('/ReaderSettings/GetSettings');
        if (response.ok) {
            const settings = await response.json();
            readerSettings = settings;

            // Aplicar configuración
            applyReaderSettings();

            // Actualizar controles en el modal
            document.querySelector(`input[name="theme"][value="${settings.theme}"]`).checked = true;
            document.getElementById('font-family').value = settings.fontFamily;
            document.getElementById('font-size').value = settings.fontSize;
            document.getElementById('font-size-value').textContent = `${settings.fontSize}px`;

            updatePreview();
        }
    } catch (error) {
        console.error('Error al cargar la configuración del lector:', error);
    }
}

function applyReaderSettings() {
    // Aplicar tema al contenedor
    const bookContainer = document.getElementById('book-container');
    bookContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
    bookContainer.classList.add(`theme-${readerSettings.theme}`);

    // Aplicar filtros según el tema
    const canvas = document.getElementById('single-canvas');
    if (readerSettings.theme === 'sepia') {
        canvas.style.filter = 'sepia(0.5)';
    } else if (readerSettings.theme === 'dark') {
        canvas.style.filter = 'invert(0.85) hue-rotate(180deg)';
    } else {
        canvas.style.filter = 'none';
    }
}

function updatePreview() {
    const previewText = document.getElementById('preview-text');
    const previewContainer = document.getElementById('preview-container');

    // Aplicar fuente y tamaño
    previewText.style.fontFamily = document.getElementById('font-family').value;
    previewText.style.fontSize = `${document.getElementById('font-size').value}px`;

    // Aplicar tema
    const theme = document.querySelector('input[name="theme"]:checked').value;

    previewContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
    previewContainer.classList.add(`theme-${theme}`);

    // Actualizar el valor mostrado
    document.getElementById('font-size-value').textContent = `${document.getElementById('font-size').value}px`;
}

async function saveReaderSettings() {
    const theme = document.querySelector('input[name="theme"]:checked').value;
    const fontFamily = document.getElementById('font-family').value;
    const fontSize = parseInt(document.getElementById('font-size').value);

    try {
        // Obtener el token de antiforgery del formulario oculto
        const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]')?.value;

        // Verificar si tenemos el token
        if (!token) {
            console.error('No se pudo encontrar el token de antiforgery');
            showNotification('Error de autenticación al guardar la configuración', 'danger');
            return;
        }

        // Usar FormData para compatibilidad con ASP.NET Core
        const formData = new FormData();
        formData.append('theme', theme);
        formData.append('fontFamily', fontFamily);
        formData.append('fontSize', fontSize);

        const response = await fetch('/ReaderSettings/UpdateSettings', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        if (response.ok) {
            const result = await response.json();

            // Guardar la configuración localmente para uso inmediato
            readerSettings = {
                theme: theme,
                fontFamily: fontFamily,
                fontSize: fontSize
            };

            // Aplicar la configuración
            applyReaderSettings();

            // Cerrar el modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('settings-modal'));
            modal.hide();

            showNotification('Configuración guardada correctamente', 'success');
        } else {
            const errorText = await response.text();
            console.error('Error al guardar la configuración:', errorText);
            showNotification('Error al guardar la configuración', 'danger');
        }
    } catch (error) {
        console.error('Error al guardar la configuración del lector:', error);
        showNotification('Error al guardar la configuración', 'danger');
    }
}

// Configurar eventos
function setupEventListeners() {
    document.getElementById('first-page').addEventListener('click', () => goToPage(1));
    document.getElementById('prev-page').addEventListener('click', previousPage);
    document.getElementById('next-page').addEventListener('click', nextPage);
    document.getElementById('last-page').addEventListener('click', () => goToPage(totalPages));

    document.getElementById('current-page-input').addEventListener('change', function () {
        const page = parseInt(this.value);
        if (page >= 1 && page <= totalPages) {
            goToPage(page);
        }
    });

    document.getElementById('zoom-in').addEventListener('click', () => {
        setZoom(Math.min(scale + 0.15, 2.0));
    });

    document.getElementById('zoom-out').addEventListener('click', () => {
        setZoom(Math.max(scale - 0.15, 0.5));
    });

    document.querySelectorAll('.zoom-preset').forEach(preset => {
        preset.addEventListener('click', function () {
            setZoom(parseFloat(this.dataset.zoom));
        });
    });

    document.getElementById('zoom-fit-width').addEventListener('click', fitToWidth);
    document.getElementById('zoom-fit-page').addEventListener('click', fitToPage);

    document.getElementById('bookmark-button').addEventListener('click', function () {
        document.getElementById('bookmark-page').textContent = currentPage;
        const modal = new bootstrap.Modal(document.getElementById('bookmark-modal'));
        modal.show();
    });

    document.getElementById('save-bookmark').addEventListener('click', saveBookmark);

    document.getElementById('toggle-bookmarks').addEventListener('click', function () {
        const panel = document.getElementById('side-panel');
        if (panel.style.display === 'none') {
            panel.style.display = 'block';
            document.getElementById('bookmarks-tab').click();

            // Ocultar otros paneles
            document.getElementById('notes-panel').style.display = 'none';
        } else {
            if (document.getElementById('bookmarks-tab').classList.contains('active')) {
                panel.style.display = 'none';
            } else {
                document.getElementById('bookmarks-tab').click();
            }
        }
    });

    document.getElementById('toggle-toc').addEventListener('click', function () {
        const panel = document.getElementById('side-panel');
        if (panel.style.display === 'none') {
            panel.style.display = 'block';
            document.getElementById('toc-tab').click();

            // Ocultar otros paneles
            document.getElementById('notes-panel').style.display = 'none';
        } else {
            if (document.getElementById('toc-tab').classList.contains('active')) {
                panel.style.display = 'none';
            } else {
                document.getElementById('toc-tab').click();
            }
        }
    });

    document.getElementById('close-side-panel').addEventListener('click', function () {
        document.getElementById('side-panel').style.display = 'none';
    });

    document.getElementById('close-side-panel-toc').addEventListener('click', function () {
        document.getElementById('side-panel').style.display = 'none';
    });

    // Eventos para notas
    document.getElementById('toggle-notes').addEventListener('click', function () {
        const panel = document.getElementById('notes-panel');
        if (panel.style.display === 'none') {
            panel.style.display = 'block';
            // Ocultar otros paneles si están abiertos
            document.getElementById('side-panel').style.display = 'none';
        } else {
            panel.style.display = 'none';
        }
    });

    document.getElementById('close-notes-panel').addEventListener('click', function () {
        document.getElementById('notes-panel').style.display = 'none';
    });

    document.getElementById('save-new-note').addEventListener('click', createNote);

    // Permitir usar Ctrl+Enter para guardar la nota
    document.getElementById('new-note-content').addEventListener('keydown', function (e) {
        if (e.ctrlKey && e.key === 'Enter') {
            createNote();
        }
    });

    // Eventos para configuración del lector
    document.getElementById('toggle-settings').addEventListener('click', function () {
        const modal = new bootstrap.Modal(document.getElementById('settings-modal'));
        modal.show();
    });

    // Actualizar vista previa en tiempo real
    document.getElementById('font-family').addEventListener('change', updatePreview);
    document.getElementById('font-size').addEventListener('input', updatePreview);
    document.querySelectorAll('input[name="theme"]').forEach(radio => {
        radio.addEventListener('change', updatePreview);
    });

    // Guardar configuración
    document.getElementById('save-settings').addEventListener('click', saveReaderSettings);

    document.getElementById('toggle-fullscreen').addEventListener('click', function () {
        const elem = document.getElementById('book-container');
        if (!document.fullscreenElement) {
            elem.requestFullscreen().catch(err => {
                console.error(`Error al entrar en pantalla completa: ${err.message}`);
            });
        } else {
            document.exitFullscreen();
        }
    });

    // Gestos táctiles para dispositivos móviles
    let touchStartX = 0;
    let touchEndX = 0;

    document.getElementById('book-container').addEventListener('touchstart', function (e) {
        touchStartX = e.changedTouches[0].screenX;
    });

    document.getElementById('book-container').addEventListener('touchend', function (e) {
        touchEndX = e.changedTouches[0].screenX;
        handleSwipe();
    });

    function handleSwipe() {
        const minSwipeDistance = 50;
        if (touchEndX < touchStartX - minSwipeDistance) {
            nextPage();
        }

        if (touchEndX > touchStartX + minSwipeDistance) {
            previousPage();
        }
    }

    // Teclado
    document.addEventListener('keydown', function (e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        switch (e.key) {
            case 'ArrowLeft':
                previousPage();
                break;
            case 'ArrowRight':
                nextPage();
                break;
            case 'Home':
                goToPage(1);
                break;
            case 'End':
                goToPage(totalPages);
                break;
            case '+':
                document.getElementById('zoom-in').click();
                break;
            case '-':
                document.getElementById('zoom-out').click();
                break;
            case 'f':
                document.getElementById('toggle-fullscreen').click();
                break;
        }
    });

    // Evento para pantalla completa
    document.addEventListener('fullscreenchange', function () {
        const bookContainer = document.getElementById('book-container');

        if (document.fullscreenElement) {
            bookContainer.classList.add('fullscreen-mode');
            showNotification('Presiona ESC para salir de pantalla completa', 'info');
        } else {
            bookContainer.classList.remove('fullscreen-mode');
        }

        // Re-renderizar para ajustar tamaño
        if (pdfDoc) {
            if (isFitWidth) fitToWidth();
            else if (isFitPage) fitToPage();
            else renderCurrentPage();
        }
    });
}

// Función de debounce para eventos rápidos
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        const context = this;
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(context, args), wait);
    };
}

// Al iniciar el documento, cargar la página después de un breve retraso
setTimeout(() => {
    if (window.location.hash) {
        const pageNum = parseInt(window.location.hash.substring(1));
        if (!isNaN(pageNum) && pageNum > 0 && pageNum <= totalPages) {
            goToPage(pageNum);
        }
    }

    if (!localStorage.getItem(`visited_${bookId}`)) {
        showNotification('Usa las flechas del teclado o desliza para cambiar de página', 'info');
        localStorage.setItem(`visited_${bookId}`, 'true');
    }
}, 500);