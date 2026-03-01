const API = 'http://localhost:5009';

function getToken() { return localStorage.getItem('userToken'); }

function checkAuth() {
  if (!getToken()) {
    // Preserve current URL params for scan flow
    const params = window.location.search;
    localStorage.setItem('redirectAfterLogin', window.location.pathname + params);
    window.location.href = 'login.html'; 
    return false; 
  }
  return true;
}

async function apiFetch(path, options = {}) {
  const res = await fetch(API + path, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ' + getToken(),
      ...(options.headers || {})
    }
  });
  if (res.status === 401) { 
    localStorage.clear(); 
    window.location.href = 'login.html'; 
    return null; 
  }
  return res;
}

function logout() {
  localStorage.clear();
  window.location.href = 'login.html';
}

function fmtDate(s) {
  return new Date(s).toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function setActivePage() {
  const page = location.pathname.split('/').pop() || 'index.html';
  document.querySelectorAll('.nav-link').forEach(a => {
    const href = a.getAttribute('href');
    a.classList.toggle('active', href === page);
  });
}

const CART_KEY = 'cokeRewardsCart';

function getCart() {
  try {
    const raw = localStorage.getItem(CART_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch (_) {
    return [];
  }
}

function addToCart(item) {
  const cart = getCart();
  cart.push({
    id: Date.now() + '-' + Math.random().toString(36).slice(2),
    rewardName: item.rewardName,
    points: item.points,
    addedAt: new Date().toISOString()
  });
  localStorage.setItem(CART_KEY, JSON.stringify(cart));
  return cart;
}

function setCart(items) {
  localStorage.setItem(CART_KEY, JSON.stringify(items));
}
