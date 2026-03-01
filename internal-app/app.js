const API = 'http://localhost:5009';

function getToken() { return localStorage.getItem('adminToken'); }
function getRole()  { return localStorage.getItem('adminRole') || 'admin'; }

// Which roles can access each page
const NAV_ACCESS = {
  'index.html':       ['admin', 'marketing', 'sustainability'],
  'emissions.html':   ['admin', 'sustainability'],
  'rvm-impact.html':  ['admin', 'sustainability'],
  'ai-insights.html': ['admin', 'marketing', 'sustainability'],
  'users.html':       ['admin', 'marketing'],
};

function checkAuth() {
  if (!getToken()) { window.location.href = 'login.html'; return false; }
  return true;
}

// Call on restricted pages — redirects to dashboard if role not allowed
function checkPageAccess(allowedRoles) {
  if (!allowedRoles.includes(getRole())) {
    window.location.href = 'index.html';
    return false;
  }
  return true;
}

// Hide nav links the current role can't access; hide admin-only export buttons
function filterNav() {
  const role = getRole();
  document.querySelectorAll('.navbar-nav .nav-item').forEach(li => {
    const href = li.querySelector('a')?.getAttribute('href');
    if (href && NAV_ACCESS[href] && !NAV_ACCESS[href].includes(role)) {
      li.style.display = 'none';
    }
  });
  // Hide export buttons for non-admin users
  if (role !== 'admin') {
    document.querySelectorAll('[data-admin-only]').forEach(el => el.style.display = 'none');
  }
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

function showNavUser() {
  const el = document.getElementById('adminName');
  if (!el) return;
  const name = localStorage.getItem('adminName') || '';
  const role = getRole();
  const colors = { admin: 'bg-danger', marketing: 'bg-primary', sustainability: 'bg-success' };
  el.innerHTML = `${name} <span class="badge ${colors[role] || 'bg-secondary'} ms-1">${role}</span>`;
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
  filterNav();
}

/** Populate an area <select> with optgroups by type (Countries, States/Provinces, Cities). */
function populateAreaSelectWithGroups(select, areas, placeholder = 'Choose an area...') {
  const typeOrder = ['country', 'state', 'city'];
  const typeLabels = { country: 'Countries', state: 'States / Provinces', city: 'Cities' };
  select.innerHTML = '';
  const first = document.createElement('option');
  first.value = '';
  first.textContent = placeholder;
  select.appendChild(first);
  typeOrder.forEach(type => {
    const group = areas.filter(a => a.type === type);
    if (group.length === 0) return;
    const optgroup = document.createElement('optgroup');
    optgroup.label = typeLabels[type] || type;
    group.forEach(a => {
      const opt = document.createElement('option');
      opt.value = a.id;
      opt.textContent = a.name;
      optgroup.appendChild(opt);
    });
    select.appendChild(optgroup);
  });
  const other = areas.filter(a => !typeOrder.includes(a.type));
  if (other.length) {
    const optgroup = document.createElement('optgroup');
    optgroup.label = 'Other';
    other.forEach(a => {
      const opt = document.createElement('option');
      opt.value = a.id;
      opt.textContent = a.name;
      optgroup.appendChild(opt);
    });
    select.appendChild(optgroup);
  }
}
