const API = 'http://localhost:5009';

function getToken() { return localStorage.getItem('adminToken'); }

function checkAuth() {
  if (!getToken()) { window.location.href = 'login.html'; return false; }
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
  if (res.status === 401) { localStorage.clear(); window.location.href = 'login.html'; return null; }
  return res;
}

function logout() {
  localStorage.clear();
  window.location.href = 'login.html';
}

function gradeColor(grade) {
  return grade === 'A' ? '#198754' : grade === 'B' ? '#ffc107' : '#dc3545';
}

function gradeBadge(grade) {
  const cls = grade === 'A' ? 'bg-success' : grade === 'B' ? 'bg-warning text-dark' : 'bg-danger';
  return `<span class="badge ${cls} fs-6">${grade}</span>`;
}

function fmt(n) { return Number(n).toLocaleString(undefined, { maximumFractionDigits: 0 }); }

function setActivePage() {
  const page = location.pathname.split('/').pop();
  document.querySelectorAll('.nav-link').forEach(a => {
    a.classList.toggle('active', a.getAttribute('href') === page);
  });
}
