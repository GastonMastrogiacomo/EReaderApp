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

// Nuevas variables para el modo de doble página
let viewMode = 'double'; 
let currentLeftPage = 1;
let currentRightPage = 2;
let pagesRendering = false;
let pageNumPending = null;

// Variables para zoom y ajustes
let isFitWidth = false;
let isFitPage = false;

let renderTaskLeft = null;
let renderTaskRight = null;
let renderTaskSingle = null;

let readingStartTime = Date.now();
let lastPageTracked = currentPage;

// Variables para configuración del lector
let readerSettings = {
    theme: 'light',
    fontFamily: 'Arial',
    fontSize: 16,
    viewMode: 'double'
};

const bookId = document.getElementById('book-container')?.dataset.bookId || 1;

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
            else renderCurrentPages();
        }
    }, 300));
});

// Cargar el PDF
async function loadPDF() {
    try {
        // Mostrar indicador de carga
        document.getElementById('loading-indicator').style.display = 'block';
        document.getElementById('single-page-container').style.display = 'none';
        document.getElementById('double-page-container').style.display = 'none';

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

        // Mostrar u ocultar los contenedores según el modo de vista
        updateViewModeDisplay();

        // Calcular las páginas actuales para modo doble página
        if (viewMode === 'double') {
            calculateDoublePagesForPage(currentPage);
        }

        // Renderizar la página actual según el modo
        renderCurrentPages();

        // Ocultar indicador de carga
        document.getElementById('loading-indicator').style.display = 'none';
    } catch (error) {
        console.error('Error al cargar el PDF:', error);
        document.getElementById('loading-indicator').innerHTML = `
            <div class="alert alert-danger">
                <i class="fas fa-exclamation-triangle me-2"></i>
                Error al cargar el PDF: ${error.message}
            </div>`;
    }

    const autoSaveInterval = setInterval(function () {
        if (pdfDoc) {
            console.log("Auto-saving position...");
            savePosition();
        }
    }, 150000); // Every 15 seconds

    // Also save when user leaves the page
    window.addEventListener('beforeunload', function () {
        if (pdfDoc) {
            savePosition();
        }
        clearInterval(autoSaveInterval);
    });

}

// Calcular páginas para modo doble
function calculateDoublePagesForPage(page) {
    // Para el modo de doble página, queremos que la página izquierda sea impar
    // y la derecha sea par
    if (page % 2 === 0) {
        currentLeftPage = page - 1;
    } else {
        currentLeftPage = page;
    }

    currentRightPage = currentLeftPage + 1;

    // Ajustar si estamos fuera de rango
    if (currentLeftPage < 1) {
        currentLeftPage = 1;
        currentRightPage = 2;
    }

    if (currentRightPage > totalPages) {
        currentRightPage = null; // Indica página en blanco
    }

    // Actualizar página actual para mantener coherencia
    //currentPage = currentLeftPage;
}

// Renderizar las páginas actuales
async function renderCurrentPages() {
    if (!pdfDoc) return;

    // Actualizar la UI según el modo de vista
    updateViewModeDisplay();

    // Manejar el renderizado según el modo de vista
    if (viewMode === 'single') {
        return renderSinglePage();
    } else if (viewMode === 'double') {
        return renderDoublePages();
    } else if (viewMode === 'scroll') {
        return renderScrollMode();
    }

}

// Renderizar en modo página única
async function renderSinglePage() {
    pagesRendering = true;

    try {
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
        updateNavigationButtons();

        // Actualizar URL con número de página
        if (history.replaceState) {
            history.replaceState(null, null, `#${currentPage}`);
        }

        // Actualizar la barra de progreso
        updateProgressBar();

        // Guardar posición en localStorage
        savePosition();

        // Comprobar si hay una página pendiente
        pagesRendering = false;
        if (pageNumPending !== null) {
            const num = pageNumPending;
            pageNumPending = null;
            goToPage(num);
        }
    } catch (error) {
        console.error(`Error al renderizar página ${currentPage}: `, error);
        pagesRendering = false;
        document.getElementById('single-page-container').style.opacity = '1';
        showNotification('Error al renderizar la página', 'danger');
    }
}

// Renderizar en modo doble página
async function renderDoublePages() {
    pagesRendering = true;

    try {
        // Temporarily hide to avoid flickering
        document.getElementById('double-page-container').style.opacity = '0.5';

        // Clear canvases
        const leftCanvas = document.getElementById('left-canvas');
        const rightCanvas = document.getElementById('right-canvas');
        const leftCtx = leftCanvas.getContext('2d');
        const rightCtx = rightCanvas.getContext('2d');
        leftCtx.clearRect(0, 0, leftCanvas.width, leftCanvas.height);
        rightCtx.clearRect(0, 0, rightCanvas.width, rightCanvas.height);

        // Render both pages sequentially to avoid race conditions
        await renderPage(currentLeftPage, leftCanvas);

        // Only render right page if it exists
        if (currentRightPage && currentRightPage <= totalPages) {
            await renderPage(currentRightPage, rightCanvas);
        } else {
            // If no right page, show a blank page
            await renderBlankPage(rightCanvas);
        }

        // Show container
        document.getElementById('double-page-container').style.opacity = '1';

        // Update current page (show both pages)
        document.getElementById('current-page-input').value = currentLeftPage;
        document.getElementById('bookmark-page').textContent = currentPage;

        // Update active TOC item
        updateActiveTocItem();

        // Update navigation button states
        updateNavigationButtons();

        // Update URL with page number
        if (history.replaceState) {
            history.replaceState(null, null, `#${currentLeftPage}`);
        }

        // Update progress bar
        updateProgressBar();

        // Save position in localStorage
        savePosition();

        pagesRendering = false;
        if (pageNumPending !== null) {
            const num = pageNumPending;
            pageNumPending = null;
            goToPage(num);
        }
    } catch (error) {
        if (error.message !== 'Rendering cancelled') {
            console.error(`Error al renderizar páginas ${currentLeftPage}-${currentRightPage}: `, error);
        }
        pagesRendering = false;
        document.getElementById('double-page-container').style.opacity = '1';
        showNotification('Error al renderizar las páginas', 'danger');
    }
}

// 4. Add initialization code to connect everything
document.addEventListener('DOMContentLoaded', function () {
    // The original initialization will run, but we need to make sure our fixes
    // are applied. Let's set a short timeout to ensure the original code has run.
    setTimeout(() => {
        console.log("Applying PDF Reader fixes...");
        // We can override specific functions here if needed
    }, 100);
});

// Renderizar página en modo de desplazamiento (scroll)
async function renderScrollMode() {
    // Esta es una implementación básica, se podría mejorar para cargar solo las páginas visibles
    pagesRendering = true;

    try {
        // Por ahora, simplemente redirige al modo de página única
        viewMode = 'single';
        await renderSinglePage();

        // Mostrar notificación
        showNotification('El modo de desplazamiento está en desarrollo. Se ha cambiado a modo de página única.', 'info');
    } catch (error) {
        console.error(`Error al renderizar modo scroll: `, error);
        pagesRendering = false;
        showNotification('Error al renderizar en modo desplazamiento', 'danger');
    }
}

// Renderizar una página en un canvas
async function renderPage(pageNum, canvas) {
    try {
        // Cancel any existing render task for this canvas
        if (canvas.id === 'left-canvas' && renderTaskLeft) {
            await renderTaskLeft.cancel();
            renderTaskLeft = null;
        } else if (canvas.id === 'right-canvas' && renderTaskRight) {
            await renderTaskRight.cancel();
            renderTaskRight = null;
        } else if (canvas.id === 'single-canvas' && renderTaskSingle) {
            await renderTaskSingle.cancel();
            renderTaskSingle = null;
        }

        // Obtain the page
        const page = await pdfDoc.getPage(pageNum);

        // Get container dimensions
        const container = document.getElementById('book-container');
        const containerWidth = container.clientWidth - 40; // margin
        const containerHeight = container.clientHeight - 40; // margin

        // Get original viewport
        const viewport = page.getViewport({ scale: 1.0 });

        // Calculate appropriate scale
        let pageScale = scale;

        if (isFitWidth) {
            // Adjust to width, considering mode
            if (viewMode === 'double') {
                pageScale = (containerWidth / 2 - 5) / viewport.width;
            } else {
                pageScale = containerWidth / viewport.width;
            }
        } else if (isFitPage) {
            // Adjust to page
            let scaleX;
            if (viewMode === 'double') {
                scaleX = (containerWidth / 2 - 5) / viewport.width;
            } else {
                scaleX = containerWidth / viewport.width;
            }

            const scaleY = containerHeight / viewport.height;
            pageScale = Math.min(scaleX, scaleY);
        } else {
            // Verify scale isn't too large
            const maxScaleX = containerWidth / viewport.width;
            const maxScaleY = containerHeight / viewport.height;
            const maxScale = Math.min(maxScaleX, maxScaleY);

            if (scale > maxScale * 1.5) {
                pageScale = maxScale * 1.5;
            }
        }

        const scaledViewport = page.getViewport({ scale: pageScale });

        // Setup canvas dimensions
        canvas.width = scaledViewport.width;
        canvas.height = scaledViewport.height;

        // Setup page container dimensions
        const pageDiv = canvas.parentElement;
        pageDiv.style.width = `${scaledViewport.width}px`;
        pageDiv.style.height = `${scaledViewport.height}px`;

        // Apply theme
        applyThemeToElement(pageDiv);

        // Create render context
        const renderContext = {
            canvasContext: canvas.getContext('2d'),
            viewport: scaledViewport
        };

        // Store the render task reference based on canvas ID
        const renderTask = page.render(renderContext);

        if (canvas.id === 'left-canvas') {
            renderTaskLeft = renderTask;
        } else if (canvas.id === 'right-canvas') {
            renderTaskRight = renderTask;
        } else if (canvas.id === 'single-canvas') {
            renderTaskSingle = renderTask;
        }

        // Wait for rendering to complete
        await renderTask.promise;

        // Update zoom buttons state
        document.getElementById('zoom-in').disabled = scale >= 2.0;
        document.getElementById('zoom-out').disabled = scale <= 0.5;

        return page;
    } catch (error) {
        // Only log non-cancellation errors
        if (error.message !== 'Rendering cancelled') {
            console.error(`Error al renderizar página ${pageNum}: `, error);
        }
        throw error;
    }
}

function getElementSafely(id) {
    const element = document.getElementById(id);
    return element;
}

// Renderizar una página en blanco
function renderBlankPage(canvas) {
    return new Promise((resolve) => {
        const ctx = canvas.getContext('2d');

        // Usar las dimensiones de la página izquierda como referencia
        const leftCanvas = document.getElementById('left-canvas');

        if (leftCanvas.width > 0 && leftCanvas.height > 0) {
            canvas.width = leftCanvas.width;
            canvas.height = leftCanvas.height;

            // Dibujar fondo blanco
            ctx.fillStyle = getComputedStyle(document.documentElement)
                .getPropertyValue('--reader-page-color').trim() || '#ffffff';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
        } else {
            // Si no hay referencia, usar dimensiones estándar
            canvas.width = 595; // Tamaño A4 estándar en puntos
            canvas.height = 842;

            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
        }

        resolve();
    });
}

// Función para cargar la tabla de contenidos del PDF
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
            const tocLoading = document.getElementById('toc-loading');
            const tocEmpty = document.getElementById('toc-empty');

            if (tocLoading) tocLoading.style.display = 'none';
            if (tocEmpty) tocEmpty.style.display = 'block';
        }
    } catch (error) {
        console.error('Error al cargar la tabla de contenidos:', error);
        const tocLoading = document.getElementById('toc-loading');
        const tocEmpty = document.getElementById('toc-empty');

        if (tocLoading) tocLoading.style.display = 'none';
        if (tocEmpty) {
            tocEmpty.style.display = 'block';
            tocEmpty.textContent = 'Error al cargar la tabla de contenidos';
        }
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
    if (!tocList) return;

    // Ocultar indicador de carga
    const tocLoading = document.getElementById('toc-loading');
    if (tocLoading) tocLoading.style.display = 'none';

    if (tocItems.length === 0) {
        const tocEmpty = document.getElementById('toc-empty');
        if (tocEmpty) tocEmpty.style.display = 'block';
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

            // Determinar si la página actual está en un rango visible según el modo
            let isActive = false;

            if (viewMode === 'single') {
                isActive = pageNum === currentPage;
            } else if (viewMode === 'double') {
                isActive = (pageNum === currentLeftPage || pageNum === currentRightPage);
            } else {
                isActive = pageNum === currentPage;
            }

            if (isActive) {
                item.classList.add('active');

                // Scroll para mostrar el elemento activo
                const panel = document.getElementById('toc-content');
                if (panel && typeof panel.scrollTop !== 'undefined') {
                    panel.scrollTop = item.offsetTop - panel.offsetTop - 10;
                }
            }
        }
    });
}

// Función para actualizar la visualización según el modo seleccionado
function updateViewModeDisplay() {
    // Actualizar el texto del botón
    const viewModeText = document.getElementById('view-mode-text');
    if (viewModeText) {
        if (viewMode === 'single') {
            viewModeText.textContent = 'Página única';
        } else if (viewMode === 'double') {
            viewModeText.textContent = 'Doble página';
        }
    }

    // Actualizar el modo seleccionado en configuración
    const settingsViewMode = document.getElementById('settings-view-mode');
    if (settingsViewMode) {
        settingsViewMode.value = viewMode;
    }

    // Mostrar u ocultar los contenedores adecuados
    const singleContainer = document.getElementById('single-page-container');
    const doubleContainer = document.getElementById('double-page-container');

    if (viewMode === 'single') {
        singleContainer.style.display = 'flex';
        doubleContainer.style.display = 'none';
    } else if (viewMode === 'double') {
        singleContainer.style.display = 'none';
        doubleContainer.style.display = 'flex';
    }

    // Resaltar el tema activo
    document.querySelectorAll('.theme-option').forEach(option => {
        if (option.dataset.theme === readerSettings.theme) {
            option.classList.add('active');
        } else {
            option.classList.remove('active');
        }
    });
}

// Función para cambiar de modo de visualización
function changeViewMode(mode) {
    if (mode === viewMode) return;

    viewMode = mode;

    // Guardar preferencia en localStorage
    localStorage.setItem(`viewMode_${bookId}`, viewMode);
    readerSettings.viewMode = viewMode;

    // Recalcular páginas para el modo doble
    if (viewMode === 'double') {
        calculateDoublePagesForPage(currentPage);
    }

    // Actualizar visualización
    renderCurrentPages();
}

// Aplicar tema a un elemento específico
function applyThemeToElement(element) {
    element.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
    element.classList.add(`theme-${readerSettings.theme}`);
}

// Aplicar tema general
function applyTheme() {
    // Use the correct settings object
    const currentSettings = window.ReaderApp ? window.ReaderApp.readerSettings : readerSettings;
    console.log("Aplicandos tema:", currentSettings.theme);

    // Apply theme to body element (for CSS compatibility)
    document.body.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
    document.body.classList.add(`theme-${currentSettings.theme}`);

    const bookContainer = document.getElementById('book-container');
    const bookView = document.getElementById('book-view');

    if (bookContainer) {
        bookContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
        bookContainer.classList.add(`theme-${currentSettings.theme}`);
        bookContainer.dataset.theme = currentSettings.theme;
    }

    if (bookView) {
        bookView.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
        bookView.classList.add(`theme-${currentSettings.theme}`);
    }

    // Update theme option states
    document.querySelectorAll('.theme-option').forEach(option => {
        if (option.dataset.theme === currentSettings.theme) {
            option.classList.add('active');
        } else {
            option.classList.remove('active');
        }
    });

    // Apply theme to preview
    updatePreview();
}

// Función para ajustar al ancho
function fitToWidth() {
    isFitWidth = true;
    isFitPage = false;

    scale = 1.0; // Reset scale
    document.getElementById('zoom-text').textContent = 'Ancho';
    renderCurrentPages();
}

// Función para ajustar a la página
function fitToPage() {
    isFitWidth = false;
    isFitPage = true;

    scale = 1.0; // Reset scale
    document.getElementById('zoom-text').textContent = 'Página';
    renderCurrentPages();
}

// Actualizar la barra de progreso
function updateProgressBar() {
    let progress;

    if (viewMode === 'single') {
        progress = (currentPage / totalPages) * 100;
    } else if (viewMode === 'double') {
        progress = (currentLeftPage / totalPages) * 100;
    } else {
        progress = (currentPage / totalPages) * 100;
    }

    const progressBar = document.getElementById('reading-progress');
    progressBar.style.width = `${progress}%`;
    progressBar.setAttribute('aria-valuenow', progress);
}

// Actualizar estado de botones de navegación
function updateNavigationButtons() {
    // Determinar si estamos en la primera o última página según el modo
    let isFirstPage, isLastPage;

    if (viewMode === 'single') {
        isFirstPage = currentPage <= 1;
        isLastPage = currentPage >= totalPages;
    } else if (viewMode === 'double') {
        isFirstPage = currentLeftPage <= 1;
        isLastPage = currentRightPage === null || currentRightPage >= totalPages;
    } else {
        isFirstPage = currentPage <= 1;
        isLastPage = currentPage >= totalPages;
    }

    document.getElementById('prev-page').disabled = isFirstPage;
    document.getElementById('first-page').disabled = isFirstPage;
    document.getElementById('next-page').disabled = isLastPage;
    document.getElementById('last-page').disabled = isLastPage;
}

// Navegación - ir a una página específica
function goToPage(pageNum) {
    if (!pageNum || pageNum < 1 || pageNum > totalPages) return;

    console.log("goToPage called with page:", pageNum);
    showPageLoadingIndicator();

    const direction = pageNum > currentPage ? 'next' : 'prev';
    animatePageTurn(direction);

    currentPage = pageNum;

    // Actualizar las páginas para modo doble página
    if (viewMode === 'double') {
        calculateDoublePagesForPage(currentPage);
    }

    if (pagesRendering) {
        pageNumPending = currentPage;
    } else {
        renderCurrentPages();
    }

    savePosition();
}

// Navegación - página anterior
function previousPage() {
    if (currentPage <= 1) return;

    if (viewMode === 'single') {
        goToPage(currentPage - 1);
    } else if (viewMode === 'double') {
        // En modo doble, retroceder 2 páginas o a la página anterior disponible
        goToPage(Math.max(1, currentLeftPage - 2));
    } else {
        goToPage(currentPage - 1);
    }
}

// Navegación - página siguiente
function nextPage() {
    if (currentPage >= totalPages) return;

    if (viewMode === 'single') {
        goToPage(currentPage + 1);
    } else if (viewMode === 'double') {
        // En modo doble, avanzar 2 páginas o a la siguiente disponible
        if (currentRightPage && currentRightPage < totalPages) {
            goToPage(currentRightPage + 1);
        } else {
            goToPage(totalPages);
        }
    } else {
        goToPage(currentPage + 1);
    }
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
    // Determinar qué contenedor animar según el modo de vista
    let container;

    if (viewMode === 'single') {
        container = document.getElementById('single-page-container');
        container.classList.add(`page-turning-${direction}`);
    } else if (viewMode === 'double') {
        container = document.getElementById('double-page-container');
        container.classList.add(`page-turning-${direction}`);
    }

    setTimeout(() => {
        if (container) {
            container.classList.remove(`page-turning-${direction}`);
        }
        hidePageLoadingIndicator();
    }, 300);
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
            if (document.body.contains(notification)) {
                document.body.removeChild(notification);
            }
        }, 150);
    }, 3000);
}

// Funciones para marcadores
async function loadBookmarks() {
    try {
        // Try to fetch from server first
        const response = await fetch(`/Reader/GetBookMarks?bookId=${bookId}`);

        if (response.ok) {
            const serverBookmarks = await response.json();

            // Convert server format to client format
            bookmarks = serverBookmarks.map(bookmark => ({
                id: bookmark.id,
                page: bookmark.pageNumber,
                title: bookmark.title,
                createdAt: bookmark.createdAt
            }));

            renderBookmarks();
            return;
        }
    } catch (error) {
        console.error('Error loading bookmarks from server:', error);
    }

    // Fallback to localStorage
    const savedBookmarks = localStorage.getItem(`bookmarks_${bookId}`);
    if (savedBookmarks) {
        try {
            bookmarks = JSON.parse(savedBookmarks);
            renderBookmarks();
        } catch (e) {
            console.error('Error al cargar marcadores:', e);
            bookmarks = [];
        }
    }
}

async function saveBookmark() {
    const title = document.getElementById('bookmark-title').value.trim() || `Página ${currentPage}`;

    try {
        // Get CSRF token
        const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]')?.value;

        if (!token) {
            console.error('CSRF token not found');
            showNotification('Error de autenticación', 'danger');
            return;
        }

        // Create form data
        const formData = new FormData();
        formData.append('bookId', bookId);
        formData.append('pageNumber', currentPage);
        formData.append('title', title);

        // Call server
        const response = await fetch('/Reader/SaveBookMark', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            // Add to local array with server ID
            const bookmark = {
                id: result.bookmarkId,
                page: currentPage,
                title: title,
                createdAt: new Date().toISOString()
            };

            bookmarks.push(bookmark);
            localStorage.setItem(`bookmarks_${bookId}`, JSON.stringify(bookmarks));
            renderBookmarks();
            showNotification('Marcador guardado con éxito', 'success');
        } else {
            showNotification('Error al guardar marcador: ' + (result.message || 'Error desconocido'), 'danger');
        }
    } catch (error) {
        console.error('Error saving bookmark:', error);
        showNotification('Error de conexión al guardar marcador', 'danger');
    }
}

function renderBookmarks() {
    const bookmarksList = document.getElementById('bookmarks-list');
    if (!bookmarksList) return;

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

async function deleteBookmark(id) {
    try {
        // Get CSRF token
        const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]')?.value;

        if (!token) {
            console.error('CSRF token not found');
            showNotification('Error de autenticación', 'danger');
            return;
        }

        // Call server to delete
        const response = await fetch(`/Reader/DeleteBookMark/${id}`, {
            method: 'DELETE',
            headers: {
                'RequestVerificationToken': token
            }
        });

        const result = await response.json();

        if (result.success) {
            // Remove from local array
            bookmarks = bookmarks.filter(b => b.id !== id);
            localStorage.setItem(`bookmarks_${bookId}`, JSON.stringify(bookmarks));
            renderBookmarks();
            showNotification('Marcador eliminado', 'warning');
        } else {
            showNotification('Error al eliminar marcador', 'danger');
        }
    } catch (error) {
        console.error('Error deleting bookmark:', error);
        showNotification('Error de conexión al eliminar marcador', 'danger');
    }
}

// Guardar posición
function savePosition() {
    console.log("savePosition called, currentPage:", currentPage);

    // Ensure valid page
    if (!currentPage || currentPage < 1 || isNaN(currentPage)) {
        console.error("Invalid page number detected");
        currentPage = Math.max(1, currentLeftPage || 1);
    }

    // Calculate reading time in minutes since last save
    const currentTime = Date.now();
    const elapsedTimeMinutes = Math.round((currentTime - readingStartTime) / 60000);

    // Define hasPageChanged - always check against lastPageTracked
    const hasPageChanged = currentPage !== lastPageTracked;
    console.log("lastPageTracked:", lastPageTracked, "hasPageChanged:", hasPageChanged);

    // Always update the server when page changes or enough time has passed (reduced threshold)
    if (elapsedTimeMinutes >= 0.25 || hasPageChanged) {  // 15 seconds instead of 1 minute
        console.log("Saving reading state to server:", {
            bookId, currentPage, viewMode, elapsedTimeMinutes
        });

        if (elapsedTimeMinutes >= 0.25) {
            readingStartTime = currentTime;
        }

        // Get antiforgery token - try multiple locations
        let token = null;

        // Try the dedicated form first
        const antiForgeryForm = document.getElementById('antiforgery-form');
        if (antiForgeryForm) {
            token = antiForgeryForm.querySelector('input[name="__RequestVerificationToken"]')?.value;
        }

        // If not found, try elsewhere
        if (!token) {
            token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        }

        if (!token) {
            console.error("CSRF token not found - cannot save state to server");
            // Still update localStorage even without server update
        } else {
            // Create form data
            const formData = new FormData();
            formData.append('bookId', bookId);
            formData.append('currentPage', currentPage);
            formData.append('zoomLevel', scale);
            formData.append('viewMode', viewMode);
            formData.append('readingTimeMinutes', elapsedTimeMinutes);

            // Send to server
            fetch('/Reader/SaveReadingState', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`Server returned ${response.status}`);
                    }
                    return response.json();
                })
                .then(data => {
                    console.log("Server response:", data);
                    if (data.success) {
                        console.log("Reading state saved successfully");
                    }
                })
                .catch(error => {
                    console.error("Error saving reading state:", error);
                });
        }

        // Always update last page tracked
        lastPageTracked = currentPage;
    }

    // Always save to localStorage
    localStorage.setItem(`position_${bookId}`, JSON.stringify({
        page: currentPage,
        leftPage: currentLeftPage,
        rightPage: currentRightPage,
        viewMode: viewMode,
        scale: scale,
        fitWidth: isFitWidth,
        fitPage: isFitPage,
        timestamp: new Date().toISOString()
    }));

    const positionData = {
        page: currentPage,
        leftPage: currentLeftPage,
        rightPage: currentRightPage,
        viewMode: viewMode,
        scale: scale,
        fitWidth: isFitWidth,
        fitPage: isFitPage,
        timestamp: new Date().toISOString()
    };

    console.log("Saving to localStorage:", positionData);
    localStorage.setItem(`position_${bookId}`, JSON.stringify(positionData));


}

function loadPosition() {
    const readingState = document.getElementById('book-view').dataset.readingState;

    if (readingState && readingState !== 'null' && readingState !== '') {
        try {
            const state = JSON.parse(readingState);
            console.log("Parsed reading state:", state);
            currentPage = state.currentPage || 1; 
            console.log("Setting currentPage to:", currentPage);
            currentLeftPage = state.currentPage || 1;  
            currentRightPage = currentLeftPage + 1;
            scale = state.zoomLevel || 1.0;  
            viewMode = state.viewMode || 'double';

            // Also set lastPageTracked to currentPage
            lastPageTracked = currentPage;

            return;
        } catch (e) {
            console.error('Error parsing reading state:', e);
        }
    } else {
        console.log("No reading state found in data attribute");
    }

    // Try localStorage if no reading state in data attribute
    const savedPosition = localStorage.getItem(`position_${bookId}`);
    if (savedPosition) {
        try {
            const position = JSON.parse(savedPosition);
            console.log("Loaded position from localStorage:", position);
            currentPage = position.page || 1;
            currentLeftPage = position.leftPage || 1;
            currentRightPage = position.rightPage || 2;
            viewMode = position.viewMode || localStorage.getItem(`viewMode_${bookId}`) || 'double';
            scale = position.scale || 1.0;

            // Also set lastPageTracked
            lastPageTracked = currentPage;
        } catch (e) {
            console.error('Error loading saved position:', e);
            setDefaultPosition();
        }
    } else {
        console.log("No position in localStorage, using defaults");
        setDefaultPosition();
    }
}

function setDefaultPosition() {
    currentPage = 1;
    currentLeftPage = 1;
    currentRightPage = 2;
    viewMode = localStorage.getItem(`viewMode_${bookId}`) || 'double';
    scale = 1.0;
    isFitWidth = false;
    isFitPage = true;
}

// Funciones para cargar y guardar configuración
async function loadReaderSettings() {
    try {
        // Load theme from localStorage FIRST (synchronously)
        let savedTheme = 'light'; // default
        let localSettings = null;

        try {
            const savedSettings = localStorage.getItem(`settings_${bookId}`);
            if (savedSettings) {
                localSettings = JSON.parse(savedSettings);
                savedTheme = localSettings.theme || 'light';
            }
        } catch (e) {
            console.error('Error loading theme from localStorage:', e);
        }

        // Configure with the saved theme
        readerSettings = {
            theme: savedTheme,
            fontFamily: 'Arial',
            fontSize: 16,
            viewMode: 'double'
        };

        console.log("Initial theme set to:", readerSettings.theme);

        // Update with full localStorage settings
        if (localSettings) {
            Object.assign(readerSettings, localSettings);
            console.log("Full settings loaded from localStorage:", readerSettings.theme);
        }

        // Store the localStorage theme BEFORE server overwrites it
        const preservedTheme = readerSettings.theme;

        // Then try to load from server if authenticated
        if (document.querySelector('#antiforgery-form')) {
            try {
                const response = await fetch('/ReaderSettings/GetSettings');
                if (response.ok) {
                    const serverSettings = await response.json();
                    console.log("Server returned theme:", serverSettings.theme);

                    // Apply server settings but preserve localStorage theme
                    Object.assign(readerSettings, serverSettings);
                    readerSettings.theme = preservedTheme; // Restore localStorage theme

                    console.log("Final theme after preserving localStorage:", readerSettings.theme);
                    localStorage.setItem(`settings_${bookId}`, JSON.stringify(readerSettings));
                }
            } catch (serverError) {
                console.error('Error getting server settings:', serverError);
            }
        }

        // Apply theme immediately after loading
        applyTheme();

        // Actualizar controles en el modal de configuración (si existe)
        const themeRadio = document.querySelector(`input[name="theme"][value="${readerSettings.theme}"]`);
        if (themeRadio) themeRadio.checked = true;

        const viewModeSelect = document.getElementById('settings-view-mode');
        if (viewModeSelect) viewModeSelect.value = readerSettings.viewMode || viewMode;

        // Configurar el zoom según las preferencias guardadas
        const savedZoom = localStorage.getItem(`zoom_${bookId}`);
        if (savedZoom) {
            try {
                const zoomConfig = JSON.parse(savedZoom);
                if (zoomConfig.fitWidth) {
                    fitToWidth();
                    const defaultZoom = document.getElementById('default-zoom');
                    if (defaultZoom) defaultZoom.value = 'width';
                } else if (zoomConfig.fitPage) {
                    fitToPage();
                    const defaultZoom = document.getElementById('default-zoom');
                    if (defaultZoom) defaultZoom.value = 'page';
                } else {
                    setZoom(zoomConfig.scale || 1.0);
                    const defaultZoom = document.getElementById('default-zoom');
                    if (defaultZoom) defaultZoom.value = zoomConfig.scale || '1.0';
                }
            } catch (e) {
                console.error('Error al analizar la configuración de zoom:', e);
            }
        } else {
            // Si no hay configuración de zoom, usar ajuste a página por defecto
            const defaultZoom = document.getElementById('default-zoom');
            if (defaultZoom) defaultZoom.value = 'page';
            fitToPage();
        }

        // Actualizar vista previa
        updatePreview();

        // Asegurarse de que el tema se aplica al contenedor principal
        document.querySelectorAll('.theme-option').forEach(option => {
            if (option.dataset.theme === readerSettings.theme) {
                option.classList.add('active');
            } else {
                option.classList.remove('active');
            }
        });

        // Forzar la aplicación del tema al contenedor del libro
        const bookContainer = document.getElementById('book-container');
        if (bookContainer) {
            bookContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
            bookContainer.classList.add(`theme-${readerSettings.theme}`);
        }
    } catch (error) {
        console.error('Error loading reader settings:', error);
        readerSettings.theme = 'light';
        applyTheme();
    }
}

// Actualizar vista previa del tema en la configuración
function updatePreview() {
    const previewContainer = document.getElementById('preview-container');
    const previewElement = document.getElementById('theme-preview');

    if (!previewContainer || !previewElement) return;

    // Obtener el tema seleccionado
    const selectedTheme = document.querySelector('input[name="theme"]:checked')?.value || readerSettings.theme || 'light';

    // Limpiar clases anteriores
    previewContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');

    // Aplicar tema
    previewContainer.classList.add(`theme-${selectedTheme}`);

    // Aplicar estilos según el tema
    if (selectedTheme === 'light') {
        previewElement.style.backgroundColor = 'var(--theme-light-bg)';
        previewElement.style.color = 'var(--theme-light-text)';
    } else if (selectedTheme === 'sepia') {
        previewElement.style.backgroundColor = 'var(--theme-sepia-bg)';
        previewElement.style.color = 'var(--theme-sepia-text)';
    } else if (selectedTheme === 'dark') {
        previewElement.style.backgroundColor = 'var(--theme-dark-bg)';
        previewElement.style.color = 'var(--theme-dark-text)';
    }
}

// Guardar configuración del lector
async function saveReaderSettings() {
    const theme = document.querySelector('input[name="theme"]:checked')?.value || readerSettings.theme || 'light';
    const viewModeValue = document.getElementById('settings-view-mode')?.value || viewMode;
    const defaultZoom = document.getElementById('default-zoom')?.value || 'page';

    // Actualizar configuración
    readerSettings.theme = theme;
    readerSettings.viewMode = viewModeValue;

    // Guardar localmente
    localStorage.setItem(`settings_${bookId}`, JSON.stringify(readerSettings));

    // Guardar configuración de zoom
    const zoomConfig = {
        scale: 1.0,
        fitWidth: false,
        fitPage: false
    };

    // Aplicar el zoom según la opción seleccionada
    if (defaultZoom === 'width') {
        zoomConfig.fitWidth = true;
    } else if (defaultZoom === 'page') {
        zoomConfig.fitPage = true;
    } else {
        zoomConfig.scale = parseFloat(defaultZoom);
    }

    localStorage.setItem(`zoom_${bookId}`, JSON.stringify(zoomConfig));

    try {
        // Guardar en el servidor si está autenticado
        if (document.querySelector('#antiforgery-form')) {
            // Obtener el token de antiforgery
            const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]')?.value;

            // Verificar si tenemos el token
            if (token) {
                // Usar FormData para compatibilidad con ASP.NET Core
                const formData = new FormData();
                formData.append('theme', theme);
                formData.append('fontFamily', readerSettings.fontFamily || 'Arial');
                formData.append('fontSize', readerSettings.fontSize || '16');

                await fetch('/ReaderSettings/UpdateSettings', {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': token
                    },
                    body: formData
                });
            }
        }
    } catch (error) {
        console.error('Error al guardar configuración en el servidor:', error);
        // No mostrar error al usuario, ya se guardó localmente
    }

    // Cambiar el modo de visualización si es necesario
    if (viewModeValue !== viewMode) {
        changeViewMode(viewModeValue);
    } else {
        // Si solo cambió el tema, aplicarlo
        applyTheme();
    }

    // Aplicar zoom
    if (defaultZoom === 'width') {
        fitToWidth();
    } else if (defaultZoom === 'page') {
        fitToPage();
    } else {
        setZoom(parseFloat(defaultZoom));
    }

    // Cerrar el modal
    const modal = bootstrap.Modal.getInstance(document.getElementById('settings-modal'));
    if (modal) modal.hide();

    showNotification('Configuración guardada correctamente', 'success');
}

// Esta función es un reemplazo seguro para renderizar las páginas cuando hay errores
// Maneja correctamente los casos donde faltan elementos o funciones
async function safeRenderDoublePages() {
    pagesRendering = true;

    try {
        // Ocultar temporalmente para evitar parpadeos
        const doublePageContainer = document.getElementById('double-page-container');
        if (doublePageContainer) {
            doublePageContainer.style.opacity = '0.5';
        }

        // Limpiar los canvas
        const leftCanvas = document.getElementById('left-canvas');
        const rightCanvas = document.getElementById('right-canvas');

        if (!leftCanvas || !rightCanvas) {
            console.error('No se encontraron los canvas necesarios');
            pagesRendering = false;
            return;
        }

        const leftCtx = leftCanvas.getContext('2d');
        const rightCtx = rightCanvas.getContext('2d');
        leftCtx.clearRect(0, 0, leftCanvas.width, leftCanvas.height);
        rightCtx.clearRect(0, 0, rightCanvas.width, rightCanvas.height);

        // Renderizar ambas páginas
        const leftPromise = renderPage(currentLeftPage, leftCanvas);

        // Solo renderizar la página derecha si existe
        let rightPromise;
        if (currentRightPage && currentRightPage <= totalPages) {
            rightPromise = renderPage(currentRightPage, rightCanvas);
        } else {
            // Si no hay página derecha, mostrar una página en blanco
            rightPromise = renderBlankPage(rightCanvas);
        }

        await Promise.all([leftPromise, rightPromise]);

        // Mostrar el contenedor
        if (doublePageContainer) {
            doublePageContainer.style.opacity = '1';
        }

        // Actualizar número de página actual (mostrar ambas páginas)
        const pageInput = document.getElementById('current-page-input');
        const bookmarkPage = document.getElementById('bookmark-page');

        if (pageInput) pageInput.value = currentLeftPage;
        if (bookmarkPage) bookmarkPage.textContent = currentPage;

        // Actualizar elemento activo en TOC
        if (typeof updateActiveTocItem === 'function') {
            updateActiveTocItem();
        }

        // Actualizar estado de botones de navegación
        if (typeof updateNavigationButtons === 'function') {
            updateNavigationButtons();
        } else {
            // Implementación de respaldo básica
            const prevBtn = document.getElementById('prev-page');
            const nextBtn = document.getElementById('next-page');
            const firstBtn = document.getElementById('first-page');
            const lastBtn = document.getElementById('last-page');

            if (prevBtn) prevBtn.disabled = currentLeftPage <= 1;
            if (firstBtn) firstBtn.disabled = currentLeftPage <= 1;
            if (nextBtn) nextBtn.disabled = currentRightPage > totalPages;
            if (lastBtn) lastBtn.disabled = currentRightPage > totalPages;
        }

        // Actualizar URL con número de página
        if (history.replaceState) {
            history.replaceState(null, null, `#${currentLeftPage}`);
        }

        // Actualizar la barra de progreso
        const progressBar = document.getElementById('reading-progress');
        if (progressBar) {
            const progress = (currentLeftPage / totalPages) * 100;
            progressBar.style.width = `${progress}%`;
            progressBar.setAttribute('aria-valuenow', progress);
        }

        // Guardar posición en localStorage si la función existe
        if (typeof savePosition === 'function') {
            savePosition();
        } else {
            // Implementación de respaldo básica
            localStorage.setItem(`position_${bookId}`, JSON.stringify({
                page: currentPage,
                leftPage: currentLeftPage,
                rightPage: currentRightPage,
                viewMode: viewMode,
                timestamp: new Date().toISOString()
            }));
        }

        pagesRendering = false;
        if (pageNumPending !== null) {
            const num = pageNumPending;
            pageNumPending = null;
            goToPage(num);
        }
    } catch (error) {
        console.error(`Error al renderizar páginas ${currentLeftPage}-${currentRightPage}: `, error);
        pagesRendering = false;
        const doublePageContainer = document.getElementById('double-page-container');
        if (doublePageContainer) {
            doublePageContainer.style.opacity = '1';
        }

        // Mostrar notificación si la función existe
        if (typeof showNotification === 'function') {
            showNotification('Error al renderizar las páginas. Reintentando...', 'danger');
        } else {
            // Crear una notificación básica
            alert('Error al renderizar las páginas. Por favor, recarga la página.');
        }

        // Intentar recuperarse del error
        setTimeout(() => {
            // Intentar cargar solo la página izquierda como respaldo
            const leftCanvas = document.getElementById('left-canvas');
            if (leftCanvas && pdfDoc) {
                renderPage(currentLeftPage, leftCanvas).catch(e =>
                    console.error('Error en recuperación:', e));
            }
        }, 1000);
    }
}

// Función para renderizar una página en blanco (versión robusta)
function safeRenderBlankPage(canvas) {
    return new Promise((resolve) => {
        if (!canvas) {
            console.error('Canvas no encontrado para página en blanco');
            resolve();
            return;
        }

        const ctx = canvas.getContext('2d');

        // Usar las dimensiones de la página izquierda como referencia o usar valores por defecto
        const leftCanvas = document.getElementById('left-canvas');

        let width = 595; // Ancho A4 por defecto
        let height = 842; // Alto A4 por defecto

        if (leftCanvas && leftCanvas.width > 0 && leftCanvas.height > 0) {
            width = leftCanvas.width;
            height = leftCanvas.height;
        }

        canvas.width = width;
        canvas.height = height;

        // Intentar obtener el color del tema o usar un color por defecto
        let bgColor = '#ffffff';
        try {
            bgColor = getComputedStyle(document.documentElement)
                .getPropertyValue('--reader-page-color').trim() || '#ffffff';
        } catch (e) {
            console.warn('No se pudo obtener el color del tema:', e);
        }

        // Dibujar fondo
        ctx.fillStyle = bgColor;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        resolve();
    });
}

// Reemplazar renderCurrentPages para asegurarse de manejar todos los errores
async function safeRenderCurrentPages() {
    if (!pdfDoc) return;

    try {
        // Actualizar la UI según el modo de vista
        if (typeof updateViewModeDisplay === 'function') {
            updateViewModeDisplay();
        } else {
            // Implementación básica de respaldo
            const singleContainer = document.getElementById('single-page-container');
            const doubleContainer = document.getElementById('double-page-container');

            if (viewMode === 'single') {
                if (singleContainer) singleContainer.style.display = 'flex';
                if (doubleContainer) doubleContainer.style.display = 'none';
            } else if (viewMode === 'double') {
                if (singleContainer) singleContainer.style.display = 'none';
                if (doubleContainer) doubleContainer.style.display = 'flex';
            }
        }

        // Manejar el renderizado según el modo de vista
        if (viewMode === 'single') {
            if (typeof renderSinglePage === 'function') {
                return renderSinglePage();
            } else {
                console.error('Función renderSinglePage no definida, usando modo doble como respaldo');
                return safeRenderDoublePages();
            }
        } else if (viewMode === 'double') {
            return safeRenderDoublePages();
        } else if (viewMode === 'scroll') {
            if (typeof renderScrollMode === 'function') {
                return renderScrollMode();
            } else {
                console.error('Función renderScrollMode no definida, usando modo doble como respaldo');
                return safeRenderDoublePages();
            }
        }
    } catch (error) {
        console.error('Error en renderCurrentPages:', error);
        // Intentar modo básico como último recurso
        try {
            const leftCanvas = document.getElementById('left-canvas');
            if (leftCanvas && pdfDoc) {
                leftCanvas.style.display = 'block';
                const page = await pdfDoc.getPage(currentPage);
                const viewport = page.getViewport({ scale: 1.0 });
                leftCanvas.width = viewport.width;
                leftCanvas.height = viewport.height;
                await page.render({
                    canvasContext: leftCanvas.getContext('2d'),
                    viewport: viewport
                }).promise;
            }
        } catch (e) {
            console.error('Error en recuperación de emergencia:', e);
        }
    }
}

// Funciones para notas
async function loadNotes() {
    try {
        // Mostrar indicador de carga en el panel de notas
        const notesList = document.getElementById('notes-list');
        if (!notesList) return;

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
        const notesList = document.getElementById('notes-list');
        if (notesList) {
            notesList.innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-circle me-2"></i>
                    Error al cargar las notas: ${error.message}
                </div>
            `;
        }
    }
}

function renderNotes() {
    const notesList = document.getElementById('notes-list');
    if (!notesList) return;

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

        if (editButton) {
            editButton.addEventListener('click', () => {
                editNote(note);
            });
        }

        if (deleteButton) {
            deleteButton.addEventListener('click', () => {
                deleteNote(note.id);
            });
        }

        notesList.appendChild(noteElement);
    });
}

window.ReaderApp = {
    readerSettings: readerSettings,
    applyTheme: applyTheme,
    getBookId: function () { return bookId; }
};

// Utilidad para debounce de eventos
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        const context = this;
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(context, args), wait);
    };
}

// Configurar todos los eventos
function setupEventListeners() {
    // Función auxiliar para agregar event listeners de manera segura
    function addSafeEventListener(elementId, eventType, handler) {
        const element = document.getElementById(elementId);
        if (element) {
            element.addEventListener(eventType, handler);
        }
    }

    // Navegación de páginas
    addSafeEventListener('first-page', 'click', () => goToPage(1));
    addSafeEventListener('prev-page', 'click', previousPage);
    addSafeEventListener('next-page', 'click', nextPage);
    addSafeEventListener('last-page', 'click', () => goToPage(totalPages));

    // Input de página actual
    addSafeEventListener('current-page-input', 'change', function () {
        const page = parseInt(this.value);
        if (page >= 1 && page <= totalPages) {
            goToPage(page);
        }
    });

    // Controles de zoom
    addSafeEventListener('zoom-in', 'click', () => {
        setZoom(Math.min(scale + 0.15, 2.0));
    });

    addSafeEventListener('zoom-out', 'click', () => {
        setZoom(Math.max(scale - 0.15, 0.5));
    });

    // Presets de zoom
    const zoomPresets = document.querySelectorAll('.zoom-preset');
    if (zoomPresets.length > 0) {
        zoomPresets.forEach(preset => {
            preset.addEventListener('click', function () {
                setZoom(parseFloat(this.dataset.zoom));
            });
        });
    }

    // Opciones de ajuste
    addSafeEventListener('zoom-fit-width', 'click', fitToWidth);
    addSafeEventListener('zoom-fit-page', 'click', fitToPage);

    // Configurar eventos para opciones de modo de visualización
    document.querySelectorAll('.view-mode-option').forEach(option => {
        option.addEventListener('click', function () {
            changeViewMode(this.dataset.mode);
        });
    });

    // Botón de marcadores
    addSafeEventListener('bookmark-button', 'click', function () {
        const bookmarkPage = document.getElementById('bookmark-page');
        if (bookmarkPage) {
            bookmarkPage.textContent = currentPage;
        }
        const modalElement = document.getElementById('bookmark-modal');
        if (modalElement) {
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    });

    // Guardar marcador
    addSafeEventListener('save-bookmark', 'click', saveBookmark);

    // Toggle de paneles laterales
    addSafeEventListener('toggle-bookmarks', 'click', function () {
        const panel = document.getElementById('side-panel');
        const bookmarksTab = document.getElementById('bookmarks-tab');
        const notesPanel = document.getElementById('notes-panel');

        if (panel && bookmarksTab) {
            if (panel.style.display === 'none') {
                panel.style.display = 'block';
                bookmarksTab.click();

                // Ocultar otros paneles
                if (notesPanel) {
                    notesPanel.style.display = 'none';
                }
            } else {
                if (bookmarksTab.classList.contains('active')) {
                    panel.style.display = 'none';
                } else {
                    bookmarksTab.click();
                }
            }
        }
    });

    addSafeEventListener('toggle-toc', 'click', function () {
        const panel = document.getElementById('side-panel');
        const tocTab = document.getElementById('toc-tab');
        const notesPanel = document.getElementById('notes-panel');

        if (panel && tocTab) {
            if (panel.style.display === 'none') {
                panel.style.display = 'block';
                tocTab.click();

                // Ocultar otros paneles
                if (notesPanel) {
                    notesPanel.style.display = 'none';
                }
            } else {
                if (tocTab.classList.contains('active')) {
                    panel.style.display = 'none';
                } else {
                    tocTab.click();
                }
            }
        }
    });

    // Botones para cerrar paneles
    addSafeEventListener('close-side-panel', 'click', function () {
        const panel = document.getElementById('side-panel');
        if (panel) {
            panel.style.display = 'none';
        }
    });

    addSafeEventListener('close-side-panel-toc', 'click', function () {
        const panel = document.getElementById('side-panel');
        if (panel) {
            panel.style.display = 'none';
        }
    });

    // Eventos para notas
    addSafeEventListener('toggle-notes', 'click', function () {
        const panel = document.getElementById('notes-panel');
        const sidePanel = document.getElementById('side-panel');

        if (panel) {
            if (panel.style.display === 'none') {
                panel.style.display = 'block';
                // Ocultar otros paneles si están abiertos
                if (sidePanel) {
                    sidePanel.style.display = 'none';
                }
            } else {
                panel.style.display = 'none';
            }
        }
    });

    addSafeEventListener('close-notes-panel', 'click', function () {
        const panel = document.getElementById('notes-panel');
        if (panel) {
            panel.style.display = 'none';
        }
    });

    addSafeEventListener('save-new-note', 'click', createNote);

    // Permitir usar Ctrl+Enter para guardar la nota
    const noteContent = document.getElementById('new-note-content');
    if (noteContent) {
        noteContent.addEventListener('keydown', function (e) {
            if (e.ctrlKey && e.key === 'Enter') {
                createNote();
            }
        });
    }

    /* Comentado por conflictos con logica preexistente en kindle-reader.js
    // Eventos para configuración del lector (handle missing settings elements)
    const themeButton = document.getElementById('theme-button');
    if (themeButton) {
        themeButton.addEventListener('click', function () {
            // Use the kindle-reader.js theme menu functionality
            const themeMenu = document.getElementById('theme-menu');
            if (themeMenu) {
                if (themeMenu.style.display === 'block') {
                    themeMenu.style.display = 'none';
                } else {
                    themeMenu.style.display = 'block';
                }
            }
        });
    }
    */

    // Guardar configuración
    addSafeEventListener('save-settings', 'click', saveReaderSettings);

    // Pantalla completa
    addSafeEventListener('toggle-fullscreen', 'click', function () {
        const elem = document.documentElement;

        if (!document.fullscreenElement) {
            if (elem.requestFullscreen) {
                elem.requestFullscreen();
            } else if (elem.webkitRequestFullscreen) { /* Safari */
                elem.webkitRequestFullscreen();
            } else if (elem.msRequestFullscreen) { /* IE11 */
                elem.msRequestFullscreen();
            }
        } else {
            if (document.exitFullscreen) {
                document.exitFullscreen();
            } else if (document.webkitExitFullscreen) { /* Safari */
                document.webkitExitFullscreen();
            } else if (document.msExitFullscreen) { /* IE11 */
                document.msExitFullscreen();
            }
        }
    });

    // Gestos táctiles para dispositivos móviles
    let touchStartX = 0;
    let touchEndX = 0;

    const bookContainer = document.getElementById('book-container');
    if (bookContainer) {
        bookContainer.addEventListener('touchstart', function (e) {
            touchStartX = e.changedTouches[0].screenX;
        });

        bookContainer.addEventListener('touchend', function (e) {
            touchEndX = e.changedTouches[0].screenX;
            handleSwipe();
        });
    }

    function handleSwipe() {
        const minSwipeDistance = 50;
        if (touchEndX < touchStartX - minSwipeDistance) {
            nextPage();
        }

        if (touchEndX > touchStartX + minSwipeDistance) {
            previousPage();
        }

        // Ensure position is saved even if navigation was blocked (e.g., at last page)
        savePosition();
    }

    // Teclado
    document.addEventListener('keydown', function (e) {
        // No capturar teclas si el foco está en un input o textarea
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        let pageChanged = false;

        switch (e.key) {
            case 'ArrowLeft':
                previousPage();
                pageChanged = true;
                break;
            case 'ArrowRight':
                nextPage();
                pageChanged = true;
                break;
            case 'Home':
                goToPage(1);
                pageChanged = true;
                break;
            case 'End':
                goToPage(totalPages);
                pageChanged = true;
                break;
            // Other cases...
        }

        // If page changed and we didn't call goToPage (which would call savePosition)
        if (pageChanged) {
            savePosition();
        }
    });

    // Evento para pantalla completa - Versión mejorada
    document.addEventListener('fullscreenchange', function () {
        const bookContainer = document.getElementById('book-container');
        const kindleTopBar = document.querySelector('.kindle-top-bar');
        const kindleProgress = document.querySelector('.kindle-progress');
        const readerControlsPanel = document.getElementById('reader-controls-panel');

        if (document.fullscreenElement) {
            // Activar modo fullscreen
            if (bookContainer) bookContainer.classList.add('fullscreen-mode');

            // Ocultar elementos del navbar
            if (kindleTopBar) kindleTopBar.style.display = 'none';
            //if (kindleProgress) kindleProgress.style.display = 'none';
            //if (readerControlsPanel) readerControlsPanel.style.display = 'none';

            showNotification('Presiona ESC para salir de pantalla completa', 'info');
        } else {
            // Desactivar modo fullscreen
            if (bookContainer) bookContainer.classList.remove('fullscreen-mode');

            // Restaurar elementos del navbar
            if (kindleTopBar) kindleTopBar.style.display = 'flex';
            if (kindleProgress) kindleProgress.style.display = 'flex';

            // Controles se mantienen ocultos para ser consistente con la UX
            // El usuario puede mostrarlos usando el botón de toggle
        }

        // Re-renderizar para ajustar tamaño
        if (pdfDoc) {
            if (isFitWidth) fitToWidth();
            else if (isFitPage) fitToPage();
            else renderCurrentPages();
        }
    });
}

// Función para crear notas
async function createNote() {
    const content = document.getElementById('new-note-content').value.trim();
    if (!content) {
        showNotification('El contenido de la nota no puede estar vacío', 'warning');
        return;
    }

    // Mostrar indicador de carga
    const saveButton = document.getElementById('save-new-note');
    if (!saveButton) return;

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
            showNotification('Error de autenticación al guardar la nota', 'danger');
            saveButton.disabled = false;
            saveButton.innerHTML = originalText;
            return;
        }

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

            if (result.success) {
                notes.push(result.note);
                renderNotes();
                document.getElementById('new-note-content').value = '';
                showNotification('Nota guardada correctamente', 'success');
            } else {
                showNotification('Error al guardar la nota: ' + (result.message || 'Respuesta inválida'), 'danger');
            }
        } else {
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

// Función para editar nota
async function editNote(note) {
    const noteElement = document.querySelector(`.note-item[data-note-id="${note.id}"]`);
    if (!noteElement) return;

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

// Función para eliminar nota
async function deleteNote(id) {

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
            showNotification('Error al eliminar la nota', 'danger');
        }
    } catch (error) {
        console.error('Error al eliminar la nota:', error);
        showNotification('Error al eliminar la nota', 'danger');
    }
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
        showNotification('Use las flechas del teclado o deslice para cambiar de página', 'info');
        localStorage.setItem(`visited_${bookId}`, 'true');
    }
}, 500);