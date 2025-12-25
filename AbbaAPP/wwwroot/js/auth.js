// auth.js - Логика авторизации и модального окна

// Хранилище пользователей (в реальном приложении это будет база данных)
let users = JSON.parse(localStorage.getItem('tooldrop_users')) || [];
let currentUser = JSON.parse(localStorage.getItem('tooldrop_currentUser')) || null;

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function() {
    updateAuthButton();
});

// Открыть модальное окно авторизации
function openAuthModal() {
    const modal = new bootstrap.Modal(document.getElementById('authModal'));
    resetForms();
    modal.show();
}

// Закрыть модальное окно
function closeAuthModal() {
    const modal = bootstrap.Modal.getInstance(document.getElementById('authModal'));
    if (modal) {
        modal.hide();
    }
}

// ========== ПЕРЕКЛЮЧЕНИЕ МЕЖДУ ФОРМАМИ ==========

function switchToRegister() {
    document.getElementById('loginForm').style.display = 'none';
    document.getElementById('registerForm').style.display = 'block';
    document.getElementById('forgotPasswordForm').style.display = 'none';
}

function switchToLogin() {
    document.getElementById('loginForm').style.display = 'block';
    document.getElementById('registerForm').style.display = 'none';
    document.getElementById('forgotPasswordForm').style.display = 'none';
}

function showForgotPassword() {
    document.getElementById('loginForm').style.display = 'none';
    document.getElementById('registerForm').style.display = 'none';
    document.getElementById('forgotPasswordForm').style.display = 'block';
}

function showForgotPasswordClose() {
    switchToLogin();
}

// ========== ОБРАБОТКА ВХОДА ==========

function handleLogin() {
    const login = document.getElementById('loginInput').value.trim();
    const password = document.getElementById('passwordInput').value;

    // Валидация
    if (!login || !password) {
        showAlert('Пожалуйста, заполните оба поля', 'warning');
        return;
    }

    // Поиск пользователя
    const user = users.find(u => u.username === login && u.password === password);

    if (user) {
        // Успешная авторизация
        currentUser = {
            id: user.id,
            username: user.username,
            email: user.email,
            isAdmin: user.isAdmin || false
        };

        localStorage.setItem('tooldrop_currentUser', JSON.stringify(currentUser));
        
        showAlert('Успешная авторизация!', 'success');
        
        setTimeout(() => {
            closeAuthModal();
            updateAuthButton();
            location.reload(); // Перезагрузить страницу для обновления интерфейса
        }, 1000);
    } else {
        showAlert('Неверный логин или пароль', 'danger');
    }
}

// ========== ОБРАБОТКА РЕГИСТРАЦИИ ==========

function handleRegister() {
    const username = document.getElementById('regUsername').value.trim();
    const email = document.getElementById('regEmail').value.trim();
    const password = document.getElementById('regPassword').value;
    const passwordConfirm = document.getElementById('regPasswordConfirm').value;

    // Валидация
    if (!username || !email || !password || !passwordConfirm) {
        showAlert('Пожалуйста, заполните все поля', 'warning');
        return;
    }

    if (password !== passwordConfirm) {
        showAlert('Пароли не совпадают', 'warning');
        return;
    }

    if (password.length < 6) {
        showAlert('Пароль должен содержать минимум 6 символов', 'warning');
        return;
    }

    if (!isValidEmail(email)) {
        showAlert('Пожалуйста, введите корректный email', 'warning');
        return;
    }

    // Проверка, не существует ли уже такой пользователь
    if (users.find(u => u.username === username)) {
        showAlert('Пользователь с таким логином уже существует', 'warning');
        return;
    }

    if (users.find(u => u.email === email)) {
        showAlert('Пользователь с таким email уже зарегистрирован', 'warning');
        return;
    }

    // Создание нового пользователя
    const newUser = {
        id: Date.now(),
        username: username,
        email: email,
        password: password, // ВАЖНО: В реальном приложении пароли должны быть захеширован!
        isAdmin: false,
        createdAt: new Date().toISOString()
    };

    users.push(newUser);
    localStorage.setItem('tooldrop_users', JSON.stringify(users));

    // Автоматический вход после регистрации
    currentUser = {
        id: newUser.id,
        username: newUser.username,
        email: newUser.email,
        isAdmin: false
    };

    localStorage.setItem('tooldrop_currentUser', JSON.stringify(currentUser));

    showAlert('Вы успешно зарегистрировались!', 'success');

    setTimeout(() => {
        closeAuthModal();
        updateAuthButton();
        location.reload();
    }, 1000);
}

// ========== ОБНОВЛЕНИЕ КНОПКИ АВТОРИЗАЦИИ ==========

function updateAuthButton() {
    const authBtn = document.getElementById('authBtn');
    const adminNavItem = document.getElementById('adminNavItem');

    if (currentUser) {
        // Пользователь авторизован
        authBtn.textContent = currentUser.username;
        authBtn.classList.remove('btn-outline-light');
        authBtn.classList.add('btn-light');
        authBtn.onclick = openUserMenu;

        // Показать кнопку редактирования для админов
        if (currentUser.isAdmin) {
            adminNavItem.style.display = 'block';
        }
    } else {
        // Пользователь не авторизован
        authBtn.textContent = 'Авторизоваться';
        authBtn.classList.add('btn-outline-light');
        authBtn.classList.remove('btn-light');
        authBtn.onclick = openAuthModal;

        if (adminNavItem) {
            adminNavItem.style.display = 'none';
        }
    }
}

// ========== МЕНЮ ПОЛЬЗОВАТЕЛЯ ==========

function openUserMenu() {
    // Создаем простое контекстное меню
    const dropdown = document.createElement('div');
    dropdown.className = 'dropdown-menu show';
    dropdown.style.position = 'absolute';
    dropdown.style.right = '0';
    dropdown.style.top = '100%';
    dropdown.style.minWidth = '200px';
    dropdown.innerHTML = `
        <a class="dropdown-item" href="/profile">Мой профиль</a>
        <a class="dropdown-item" href="/catalog">Мои покупки</a>
        <hr class="dropdown-divider">
        <a class="dropdown-item" onclick="handleLogout()">Выход</a>
    `;

    // Удалить предыдущее меню если есть
    const existingDropdown = document.querySelector('.dropdown-menu.show');
    if (existingDropdown && existingDropdown !== dropdown) {
        existingDropdown.remove();
    }

    const authBtn = document.getElementById('authBtn');
    authBtn.parentElement.appendChild(dropdown);

    // Закрыть меню при клике вне его
    document.addEventListener('click', function(event) {
        if (!event.target.closest('#authBtn') && !event.target.closest('.dropdown-menu')) {
            const menu = document.querySelector('.dropdown-menu.show');
            if (menu) menu.remove();
        }
    });
}

// ========== ВЫХОД ИЗ СИСТЕМЫ ==========

function handleLogout() {
    currentUser = null;
    localStorage.removeItem('tooldrop_currentUser');
    
    showAlert('Вы вышли из системы', 'info');
    
    setTimeout(() => {
        updateAuthButton();
        location.reload();
    }, 500);
}

// ========== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ==========

function resetForms() {
    document.getElementById('loginForm').style.display = 'block';
    document.getElementById('registerForm').style.display = 'none';
    document.getElementById('forgotPasswordForm').style.display = 'none';

    // Очистить поля
    document.getElementById('loginInput').value = '';
    document.getElementById('passwordInput').value = '';
    document.getElementById('regUsername').value = '';
    document.getElementById('regEmail').value = '';
    document.getElementById('regPassword').value = '';
    document.getElementById('regPasswordConfirm').value = '';
}

function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

function showAlert(message, type = 'info') {
    // Создание временного алерта
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
    alertDiv.style.position = 'fixed';
    alertDiv.style.top = '80px';
    alertDiv.style.right = '20px';
    alertDiv.style.zIndex = '9999';
    alertDiv.style.minWidth = '300px';
    alertDiv.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;

    document.body.appendChild(alertDiv);

    // Автоматически удалить алерт через 4 секунды
    setTimeout(() => {
        alertDiv.remove();
    }, 4000);
}

// ========== ОТЛАДКА ==========

// Функция для создания тестовых данных
function createTestUser() {
    if (users.find(u => u.username === 'testuser')) {
        console.log('Тестовый пользователь уже существует');
        return;
    }

    const testUser = {
        id: 1,
        username: 'testuser',
        email: 'test@example.com',
        password: 'password123',
        isAdmin: true,
        createdAt: new Date().toISOString()
    };

    users.push(testUser);
    localStorage.setItem('tooldrop_users', JSON.stringify(users));
    console.log('Тестовый пользователь создан. Логин: testuser, Пароль: password123');
}

// Вызовите в консоли: createTestUser()
// Затем попробуйте войти с логином testuser, пароль password123