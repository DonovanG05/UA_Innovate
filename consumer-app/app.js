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
