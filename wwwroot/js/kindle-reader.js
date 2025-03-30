﻿// Archivo específico para controlar el comportamiento del tema Kindle
document.addEventListener('DOMContentLoaded', function () {
    // Toggle de controles
    const toggleControlsBtn = document.getElementById('toggle-controls');
    const controlsPanel = document.getElementById('reader-controls-panel');

    toggleControlsBtn.addEventListener('click', function () {
        if (controlsPanel.style.display === 'none') {
            controlsPanel.style.display = 'block';
        } else {
            controlsPanel.style.display = 'none';
        }
    });

    // Control del menú de temas
    const themeButton = document.getElementById('theme-button');
    const themeMenu = document.getElementById('theme-menu');

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

    // Aplicar tema al hacer clic en una opción
    document.querySelectorAll('.theme-option').forEach(function (option) {
        option.addEventListener('click', function () {
            const theme = this.dataset.theme;
            if (!theme) return;

            // Actualizar clase activa
            document.querySelectorAll('.theme-option').forEach(opt => {
                opt.classList.remove('active');
            });
            this.classList.add('active');

            // Guardar y aplicar el tema
            const bookId = document.getElementById('book-container')?.dataset.bookId || 1;

            // Intentar usar el objeto global si existe
            if (window.ReaderApp) {
                window.ReaderApp.readerSettings.theme = theme;
                localStorage.setItem(`settings_${window.ReaderApp.getBookId()}`, JSON.stringify(window.ReaderApp.readerSettings));

                if (typeof window.ReaderApp.applyTheme === 'function') {
                    window.ReaderApp.applyTheme();
                }
            }
            // Si no, intentar con las funciones directas
            else {
                try {
                    // Acceder a las variables globales del lector original
                    readerSettings.theme = theme;
                    localStorage.setItem(`settings_${bookId}`, JSON.stringify(readerSettings));

                    // Llamar a la función applyTheme si existe
                    if (typeof applyTheme === 'function') {
                        applyTheme();
                    } else {
                        // Aplicación manual de tema si no existe función
                        const bookContainer = document.getElementById('book-container');
                        if (bookContainer) {
                            bookContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
                            bookContainer.classList.add(`theme-${theme}`);

                            // Aplicar filtros para páginas
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
                    }

                    // Notificación
                    if (typeof showNotification === 'function') {
                        showNotification(`Tema cambiado a ${theme === 'light' ? 'claro' : theme === 'dark' ? 'oscuro' : 'sepia'}`, 'info');
                    }
                } catch (error) {
                    console.error('Error al aplicar tema:', error);
                }
            }

            // Cerrar el menú después de seleccionar
            themeMenu.style.display = 'none';
        });
    });

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
    const progressObserver = new MutationObserver(updateProgressText);
    const progressBar = document.getElementById('reading-progress');

    if (progressBar) {
        progressObserver.observe(progressBar, {
            attributes: true,
            attributeFilter: ['aria-valuenow', 'style']
        });
    }

    // Inicializar estado de los temas
    function initThemes() {
        // Marcar el tema activo
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
    }
    function ensureLightThemeDefault() {
        // Verificar si ya hay un tema guardado
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
            const bookContainer = document.getElementById('book-container');
            if (bookContainer) {
                bookContainer.classList.remove('theme-light', 'theme-sepia', 'theme-dark');
                bookContainer.classList.add('theme-light');
            }
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