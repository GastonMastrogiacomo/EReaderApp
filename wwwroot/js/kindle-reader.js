// Archivo específico para controlar el comportamiento de los temas

document.addEventListener('DOMContentLoaded', function () {
    // Toggle de controles
    const toggleControlsBtn = document.getElementById('toggle-controls');
    const controlsPanel = document.getElementById('reader-controls-panel');

    if (toggleControlsBtn && controlsPanel) {
        toggleControlsBtn.addEventListener('click', function () {
            if (controlsPanel.style.display === 'none' || !controlsPanel.style.display) {
                controlsPanel.style.display = 'block';
            } else {
                controlsPanel.style.display = 'none';
            }
        });
    }

    // Control del menú de temas
    const themeButton = document.getElementById('theme-button');
    const themeMenu = document.getElementById('theme-menu');

    if (themeButton && themeMenu) {
        themeButton.addEventListener('click', function (e) {
            e.stopPropagation();
            if (themeMenu.style.display === 'block') {
                themeMenu.style.display = 'none';
            } else {
                themeMenu.style.display = 'block';
            }
        });

        // Cerrar el menú de temas al hacer clic fuera
        document.addEventListener('click', function (e) {
            if (themeMenu.style.display === 'block' && !themeMenu.contains(e.target) && e.target !== themeButton) {
                themeMenu.style.display = 'none';
            }
        });
    }

    // Aplicar tema al hacer clic en una opción
    document.querySelectorAll('.theme-option').forEach(function (option) {
        option.addEventListener('click', function () {
            const theme = this.dataset.theme;
            if (!theme) return;

            // Actualizar clase activa en el menú
            document.querySelectorAll('.theme-option').forEach(opt => {
                opt.classList.remove('active');
            });
            this.classList.add('active');

            // Obtener el bookId
            const bookId = document.getElementById('book-container')?.dataset.bookId || 1;

            // Actualizar configuración global
            if (window.ReaderApp && window.ReaderApp.readerSettings) {
                window.ReaderApp.readerSettings.theme = theme;
                localStorage.setItem(`settings_${bookId}`, JSON.stringify(window.ReaderApp.readerSettings));
            } else if (typeof readerSettings !== 'undefined') {
                readerSettings.theme = theme;
                localStorage.setItem(`settings_${bookId}`, JSON.stringify(readerSettings));
            }

            // Apply theme immediately
            applyThemeManually(theme);

            // Re-render pages immediately with new theme
            if (typeof renderCurrentPages === 'function' && window.pdfDoc) {
                // Cancel any ongoing renders
                if (renderTaskLeft) renderTaskLeft.cancel();
                if (renderTaskRight) renderTaskRight.cancel();
                if (renderTaskSingle) renderTaskSingle.cancel();

                // Force immediate re-render
                setTimeout(() => {
                    renderCurrentPages();
                }, 50);
            }

            // Notificación
            if (typeof showNotification === 'function') {
                const themeNames = {
                    'light': 'Light',
                    'sepia': 'Sepia',
                    'dark': 'Dark'
                };
                showNotification(`Theme changed to ${themeNames[theme] || theme}`, 'info');
            }

            // Cerrar el menú después de seleccionar
            if (themeMenu) {
                themeMenu.style.display = 'none';
            }
        });
    });

    // Función para aplicar tema manualmente
    function applyThemeManually(theme) {
        // Aplicar al body
        document.body.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
        document.body.classList.add(`theme-${theme}`);

        // Aplicar al contenedor del libro
        const bookContainer = document.getElementById('book-container');
        const bookView = document.getElementById('book-view');

        if (bookContainer) {
            bookContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
            bookContainer.classList.add(`theme-${theme}`);
            bookContainer.dataset.theme = theme;
        }

        if (bookView) {
            bookView.classList.remove('light-theme', 'sepia-theme', 'dark-theme');
            bookView.classList.add(`${theme}-theme`);
        }

        // Aplicar filtros a los canvas según el tema
        const canvases = document.querySelectorAll('.page-canvas');
        canvases.forEach(canvas => {
            if (theme === 'sepia') {
                canvas.style.filter = 'sepia(0.5)';
            } else if (theme === 'dark') {
                canvas.style.filter = 'invert(0.85) hue-rotate(180deg)';
            } else {
                canvas.style.filter = 'none';
            }
        });
    }

    // Actualizar el porcentaje de progreso
    function updateProgressText() {
        const progressBar = document.getElementById('reading-progress');
        const progressText = document.getElementById('progress-percentage');

        if (progressBar && progressText) {
            const percentage = parseInt(progressBar.getAttribute('aria-valuenow')) || 0;
            progressText.textContent = `${percentage}%`;
        }
    }

    // Observar cambios en la barra de progreso para actualizar el texto
    const progressBar = document.getElementById('reading-progress');
    if (progressBar) {
        const progressObserver = new MutationObserver(updateProgressText);
        progressObserver.observe(progressBar, {
            attributes: true,
            attributeFilter: ['aria-valuenow', 'style']
        });
    }

    // Inicializar estado de los temas
    function initThemes() {
        let activeTheme = 'light';

        // Intentar obtener el tema guardado
        try {
            const bookId = document.getElementById('book-container')?.dataset.bookId || 1;
            const savedSettings = localStorage.getItem(`settings_${bookId}`);

            if (savedSettings) {
                const settings = JSON.parse(savedSettings);
                activeTheme = settings.theme || 'light';
            }
        } catch (e) {
            console.error('Error al cargar ajustes de tema:', e);
        }

        // Marcar la opción activa
        document.querySelectorAll('.theme-option').forEach(opt => {
            if (opt.dataset.theme === activeTheme) {
                opt.classList.add('active');
            } else {
                opt.classList.remove('active');
            }
        });

        // Aplicar el tema guardado
        applyThemeManually(activeTheme);
    }

    // Asegurar tema por defecto
    function ensureLightThemeDefault() {
        const bookId = document.getElementById('book-container')?.dataset.bookId || 1;
        let savedSettings = localStorage.getItem(`settings_${bookId}`);

        if (!savedSettings) {
            // Si no hay configuración guardada, crear una con tema claro por defecto
            const defaultSettings = {
                theme: 'light',
                fontFamily: 'Arial',
                fontSize: 16,
                viewMode: 'double'
            };

            localStorage.setItem(`settings_${bookId}`, JSON.stringify(defaultSettings));
            console.log("Creando configuración por defecto con tema claro");

            // Aplicar tema claro inmediatamente
            applyThemeManually('light');
        } else {
            try {
                // Verificar si el tema está definido
                const settings = JSON.parse(savedSettings);
                if (!settings.theme) {
                    settings.theme = 'light';
                    localStorage.setItem(`settings_${bookId}`, JSON.stringify(settings));
                    console.log("Configuración existente sin tema, estableciendo tema claro");
                }
            } catch (e) {
                console.error("Error analizando configuración guardada:", e);
            }
        }
    }

    // Llamada a la función para asegurar tema por defecto
    ensureLightThemeDefault();

    // Inicializar temas
    initThemes();

    // Llamar a updateProgressText inicialmente
    updateProgressText();
});