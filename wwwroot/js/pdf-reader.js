// Configuración de PDF.js
pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.5.141/pdf.worker.min.js';

// Variables globales
let pdfDoc = null;
let currentPage = 1;
let totalPages = 0;
let scale = 1.0;
let bookmarks = [];
let tocItems = [];
const bookId = document.getElementById('book-container').dataset.bookId || 1; // Obtener el ID del libro
let isDoublePage = true; // Modo libro (dos páginas) por defecto
let isFitWidth = false;
let isFitPage = false;
let pageRendering = false;
let pageNumPending = null;

// Función de inicialización
document.addEventListener('DOMContentLoaded', function () {
    console.log("PDF Reader initialized");

    // Cargar el PDF
    loadPDF();

    // Configurar eventos
    setupEventListeners();

    // Cargar marcadores guardados
    loadBookmarks();

    // Ajustar tamaño al redimensionar
    window.addEventListener('resize', debounce(function () {
        if (pdfDoc) {
            if (isFitWidth) fitToWidth();
            else if (isFitPage) fitToPage();
            else renderCurrentPages();
        }
    }, 300));
});

// Cargar el PDF
async function loadPDF() {
    try {
        // Mostrar indicador de carga
        document.getElementById('loading-indicator').style.display = 'block';
        document.getElementById('pages-container').style.display = 'none';
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

        // Cargar preferencia de modo
        loadViewMode();

        // Intentar cargar posición guardada
        loadPosition();

        // O renderizar página inicial
        if (currentPage < 1) {
            currentPage = 1;
            renderCurrentPages();
        }

        // Ocultar indicador de carga
        document.getElementById('loading-indicator').style.display = 'none';

        // Mostrar el contenedor apropiado según el modo
        if (isDoublePage) {
            document.getElementById('pages-container').style.display = 'flex';
        } else {
            document.getElementById('single-page-container').style.display = 'flex';
        }
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
            tocElement.style.setProperty('--toc-level', item.level);

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
            if (pageNum === currentPage || (isDoublePage && (pageNum === currentPage + 1))) {
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
        renderCurrentPages();
    }
}

// Renderizar las páginas actuales
async function renderCurrentPages() {
    if (!pdfDoc) return;

    pageRendering = true;

    try {
        if (isDoublePage) {
            // Modo libro (dos páginas)
            // Ocultar temporalmente para evitar parpadeos
            document.getElementById('pages-container').style.opacity = '0.5';

            // Obtener la página izquierda (par)
            let leftPageNum = currentPage;
            if (currentPage % 2 === 0) {
                leftPageNum = currentPage - 1;
            }
            const rightPageNum = leftPageNum + 1;

            // Obtener canvases
            const leftCanvas = document.getElementById('left-canvas');
            const rightCanvas = document.getElementById('right-canvas');

            // Limpiar canvases
            const leftCtx = leftCanvas.getContext('2d');
            const rightCtx = rightCanvas.getContext('2d');
            leftCtx.clearRect(0, 0, leftCanvas.width, leftCanvas.height);
            rightCtx.clearRect(0, 0, rightCanvas.width, rightCanvas.height);

            // Renderizar página izquierda si existe
            if (leftPageNum >= 1) {
                await renderPage(leftPageNum, leftCanvas, true);
            }

            // Renderizar página derecha si existe
            if (rightPageNum <= totalPages) {
                await renderPage(rightPageNum, rightCanvas, true);
            } else {
                // Limpiar el canvas derecho si no hay más páginas
                rightCtx.clearRect(0, 0, rightCanvas.width, rightCanvas.height);
            }

            // Mostrar el contenedor
            document.getElementById('pages-container').style.opacity = '1';
        } else {
            // Modo página única
            // Ocultar temporalmente para evitar parpadeos
            document.getElementById('single-page-container').style.opacity = '0.5';

            const canvas = document.getElementById('single-canvas');
            const ctx = canvas.getContext('2d');
            ctx.clearRect(0, 0, canvas.width, canvas.height);

            await renderPage(currentPage, canvas, false);

            // Mostrar el contenedor
            document.getElementById('single-page-container').style.opacity = '1';
        }

        // Actualizar número de página actual
        document.getElementById('current-page-input').value = currentPage;
        document.getElementById('bookmark-page').textContent = currentPage;

        // Actualizar elemento activo en TOC
        updateActiveTocItem();

        // Actualizar estado de botones de navegación
        document.getElementById('prev-page').disabled = currentPage <= 1;
        document.getElementById('first-page').disabled = currentPage <= 1;

        if (isDoublePage) {
            const lastPageToShow = currentPage % 2 === 0 ? currentPage : currentPage + 1;
            document.getElementById('next-page').disabled = lastPageToShow >= totalPages;
            document.getElementById('last-page').disabled = lastPageToShow >= totalPages;
        } else {
            document.getElementById('next-page').disabled = currentPage >= totalPages;
            document.getElementById('last-page').disabled = currentPage >= totalPages;
        }

        // Actualizar URL con número de página
        if (history.replaceState) {
            history.replaceState(null, null, `#${currentPage}`);
        }

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
        if (isDoublePage) {
            document.getElementById('pages-container').style.opacity = '1';
        } else {
            document.getElementById('single-page-container').style.opacity = '1';
        }
        showNotification('Error al renderizar la página', 'danger');
    }
}

// Renderizar una página específica en un canvas
async function renderPage(pageNum, canvas, isDoublePage) {
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
            const pageWidth = isDoublePage ? containerWidth / 2 : containerWidth;
            pageScale = pageWidth / viewport.width;
        } else if (isFitPage) {
            // Ajustar a la página
            const pageWidth = isDoublePage ? containerWidth / 2 : containerWidth;
            const pageHeight = containerHeight;

            const scaleX = pageWidth / viewport.width;
            const scaleY = pageHeight / viewport.height;

            pageScale = Math.min(scaleX, scaleY);
        } else {
            if (isDoublePage) {
                const maxScale = containerWidth / (viewport.width * 2);
                if (scale > maxScale * 1.5) {
                    pageScale = maxScale * 1.5;
                }
            } else {
                const maxScaleX = containerWidth / viewport.width;
                const maxScaleY = containerHeight / viewport.height;
                const maxScale = Math.min(maxScaleX, maxScaleY);

                if (scale > maxScale * 1.5) {
                    pageScale = maxScale * 1.5;
                }
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

// Función para ajustar al ancho
function fitToWidth() {
    isFitWidth = true;
    isFitPage = false;

    document.getElementById('zoom-text').textContent = 'Ancho';
    renderCurrentPages();
}

// Función para ajustar a la página
function fitToPage() {
    isFitWidth = false;
    isFitPage = true;
    document.getElementById('zoom-text').textContent = 'Página';
    renderCurrentPages();
}

// Funciones de navegación
function goToPage(pageNum) {
    if (!pageNum || pageNum < 1 || pageNum > totalPages) return;

    if (isDoublePage) {
        if (pageNum % 2 === 0 && pageNum < currentPage) {
            // No ajustar
        } else if (pageNum % 2 === 0) {
            pageNum = pageNum - 1;
        }
    }

    const direction = pageNum > currentPage ? (isDoublePage ? 'forward' : 'next') : (isDoublePage ? 'backward' : 'prev');
    animatePageTurn(direction);

    currentPage = pageNum;
    queueRenderPage(currentPage);
}

function previousPages() {
    if (currentPage <= 1) return;

    let prevPage;
    if (isDoublePage) {
        prevPage = Math.max(1, currentPage - 2);
    } else {
        prevPage = currentPage - 1;
    }

    goToPage(prevPage);
}

function nextPages() {
    const lastPage = isDoublePage ?
        (totalPages % 2 === 0 ? totalPages - 1 : totalPages) :
        totalPages;

    if (currentPage >= lastPage) return;

    let nextPage;
    if (isDoublePage) {
        nextPage = Math.min(lastPage, currentPage + 2);
    } else {
        nextPage = currentPage + 1;
    }

    goToPage(nextPage);
}

// Animación de cambio de página
function animatePageTurn(direction) {
    if (isDoublePage) {
        const container = document.getElementById('pages-container');
        container.classList.add(`page-turning-${direction}`);
        setTimeout(() => {
            container.classList.remove(`page-turning-${direction}`);
        }, 600);
    } else {
        const container = document.getElementById('single-page-container');
        container.classList.add(`page-turning-${direction}`);
        setTimeout(() => {
            container.classList.remove(`page-turning-${direction}`);
        }, 300);
    }
}

// Cambiar entre modo libro y página única
function toggleBookMode() {
    document.getElementById('book-view').classList.add('mode-transition');

    setTimeout(() => {
        isDoublePage = !isDoublePage;

        document.getElementById('pages-container').style.display = 'none';
        document.getElementById('single-page-container').style.display = 'none';

        if (isDoublePage) {
            document.getElementById('pages-container').style.display = 'flex';
            if (currentPage % 2 === 0) currentPage--;
        } else {
            document.getElementById('single-page-container').style.display = 'flex';
        }

        renderCurrentPages();

        localStorage.setItem(`book_mode_${bookId}`, isDoublePage ? 'double' : 'single');

        setTimeout(() => {
            document.getElementById('book-view').classList.remove('mode-transition');
        }, 500);
    }, 250);
}

// Funciones de zoom
function setZoom(newScale) {
    scale = newScale;
    isFitWidth = false;
    isFitPage = false;
    document.getElementById('zoom-text').textContent = `${Math.round(scale * 100)}%`;
    renderCurrentPages();
}

// Función para mostrar notificaciones
function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = `alert alert-${type} alert-dismissible fade show notification-toast`;
    notification.role = 'alert';
    notification.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Cerrar"></button>
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
        mode: isDoublePage ? 'double' : 'single',
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

    renderCurrentPages();
}

// Cargar preferencia de modo (libro o página única)
function loadViewMode() {
    const savedMode = localStorage.getItem(`book_mode_${bookId}`);
    if (savedMode) {
        isDoublePage = savedMode === 'double';
    }
}

// Configurar eventos
function setupEventListeners() {
    document.getElementById('first-page').addEventListener('click', () => goToPage(1));
    document.getElementById('prev-page').addEventListener('click', previousPages);
    document.getElementById('next-page').addEventListener('click', nextPages);
    document.getElementById('last-page').addEventListener('click', () => {
        const lastPage = isDoublePage ?
            (totalPages % 2 === 0 ? totalPages - 1 : totalPages) :
            totalPages;
        goToPage(lastPage);
    });

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

    document.getElementById('toggle-mode').addEventListener('click', toggleBookMode);

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
            nextPages();
        }

        if (touchEndX > touchStartX + minSwipeDistance) {
            previousPages();
        }
    }

    // Teclado
    document.addEventListener('keydown', function (e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        switch (e.key) {
            case 'ArrowLeft':
                previousPages();
                break;
            case 'ArrowRight':
                nextPages();
                break;
            case 'Home':
                goToPage(1);
                break;
            case 'End':
                goToPage(isDoublePage ? (totalPages % 2 === 0 ? totalPages - 1 : totalPages) : totalPages);
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
            case 'm':
                document.getElementById('toggle-mode').click();
                break;
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
