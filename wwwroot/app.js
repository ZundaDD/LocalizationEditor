document.addEventListener('click', function(e) {
    // 处理点击菜单标题展开/收起
    if (e.target.classList.contains('menu-title')) {
        const parent = e.target.parentElement;
        const isActive = parent.classList.contains('active');
        
        // 先关闭所有已经打开的菜单
        document.querySelectorAll('.menu-dropdown').forEach(m => m.classList.remove('active'));
        
        // 如果刚才没打开，就打开它
        if (!isActive) {
            parent.classList.add('active');
        }
    } else {
        // 如果点击的不是菜单标题，说明点到了外面或者具体的菜单项，全部收起
        document.querySelectorAll('.menu-dropdown').forEach(m => m.classList.remove('active'));
    }
});

document.addEventListener('keydown', function(e) {
    if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
        e.preventDefault();
        saveProject();
    }
});

let languages = []; 
let allData = {};   
let mergedTree = {}; 
let currentEditingPath = [];
let currentLang = "";
let projectLoaded = false;
let mainLanguage = "";
let dirtyLanguages = new Set();
let languageFiles = {}; // { [langKey]: filePath }

let addLangIsMain = false;

let compileSelectedLang = "";
let compileSetMain = false;

let contextMenuState = null; // { path: string[], isObject: boolean }

// 记录树节点展开状态（key 为 path 的 JSON 字符串）
let expandedNodes = new Set();

function updateAddLangMainCheckboxState() {
    const btn = document.getElementById('toggle-main-lang-btn');
    const hint = document.getElementById('toggle-main-lang-hint');
    if (!btn) return;

    // 当当前不存在任何语言时，添加第一个语种必须是主语言
    const mustBeMain = Array.isArray(languages) && languages.length === 0;
    if (mustBeMain) addLangIsMain = true;

    btn.disabled = mustBeMain;
    btn.textContent = addLangIsMain ? '设为主语言' : '不设为主语言';
    btn.classList.toggle('btn-toggle-active', addLangIsMain);
    if (hint) hint.textContent = mustBeMain ? '当前无语言：必须设为主语言' : '';
}

function toggleAddLangMain() {
    // 手动切换“主语言”状态（无语言时会被禁用）
    addLangIsMain = !addLangIsMain;
    updateAddLangMainCheckboxState();
}

function setMenuEnabled(id, enabled) {
    const el = document.getElementById(id);
    if (!el) return;
    if (enabled) {
        el.classList.remove('disabled');
        el.setAttribute('aria-disabled', 'false');
    } else {
        el.classList.add('disabled');
        el.setAttribute('aria-disabled', 'true');
    }
}

function updateDirtyUI() {
    const hasDirty = dirtyLanguages.size > 0;
    setMenuEnabled('save-project-menu-item', projectLoaded && hasDirty);
    const label = document.getElementById('save-project-label');
    if (label) label.textContent = '保存项目';
}

function setAddLanguageEnabled(enabled) {
    const el = document.getElementById('add-lang-menu-item');
    if (!el) return;
    if (enabled) {
        el.classList.remove('disabled');
        el.setAttribute('aria-disabled', 'false');
    } else {
        el.classList.add('disabled');
        el.setAttribute('aria-disabled', 'true');
    }
}

function setCompileLanguageEnabled(enabled) {
    const el = document.getElementById('compile-lang-menu-item');
    if (!el) return;
    if (enabled) {
        el.classList.remove('disabled');
        el.setAttribute('aria-disabled', 'false');
    } else {
        el.classList.add('disabled');
        el.setAttribute('aria-disabled', 'true');
    }
}

// 初始状态：未加载项目时禁用“添加语种”
setAddLanguageEnabled(false);
setCompileLanguageEnabled(false);
setMenuEnabled('save-project-menu-item', false);

// 发送创建项目指令
function createProject() {
    window.external.sendMessage("CREATE_PROJECT");
}

function openProject() {
    window.external.sendMessage("OPEN_PROJECT");
}

function saveProject() {
    if (!projectLoaded) return;
    window.external.sendMessage("SAVE_PROJECT");
}

function pickLanguageFile() {
    if (!projectLoaded) return;
    window.external.sendMessage("PICK_LANGUAGE_FILE");
}

window.external.receiveMessage(message => {
    if (message.startsWith("LOAD:")) {
        const payload = JSON.parse(message.substring(5));
        languages = payload.languages;
        allData = payload.data;
        languageFiles = payload.files || {};

        mainLanguage = payload.mainLanguage || (languages.length > 0 ? languages[0] : "");
        dirtyLanguages = new Set(payload.dirtyLanguages || []);

        projectLoaded = true;
        setAddLanguageEnabled(true);
        setCompileLanguageEnabled(true);
        updateDirtyUI();
        updateAddLangMainCheckboxState();
        
        if (languages.length > 0) currentLang = mainLanguage || languages[0];
        
        buildMergedTree();
        renderTree();

        // 项目加载完成后：保持空状态提示，直到用户选择具体节点
        const empty = document.getElementById('empty-state');
        if (empty) {
            empty.style.display = 'flex';
            empty.textContent = '请选择一个节点开始编辑';
        }
        const editorContent = document.getElementById('editor-content');
        if (editorContent) editorContent.style.display = 'none';
    }
    else if (message.startsWith("ADD_KEY_OK:")) {
        const payload = JSON.parse(message.substring("ADD_KEY_OK:".length));
        const parentPath = payload.parentPath || [];
        const key = (payload.key || "").trim();
        const kind = (payload.kind || "").trim().toLowerCase();
        if (!key) return;
        if (kind !== 'object' && kind !== 'string') return;

        for (const lang of languages) {
            if (!allData[lang]) allData[lang] = {};
            const parent = ensureObjectByPath(allData[lang], parentPath);
            if (parent && typeof parent === 'object' && !(key in parent)) {
                insertKeyAtEndPreserveOrder(parent, key, (kind === 'object') ? {} : "");
            }
        }

        buildMergedTree();
        renderTree();
    }
    else if (message.startsWith("DELETE_KEY_OK:")) {
        const payload = JSON.parse(message.substring("DELETE_KEY_OK:".length));
        const path = payload.path || [];
        if (!path.length) return;

        for (const lang of languages) {
            if (!allData[lang]) continue;
            deleteKeyInObject(allData[lang], path);
        }

        // 如果删的是当前正在编辑的节点或其祖先，关闭编辑器（与前端预处理一致，防止边界情况）
        const isPrefix = (full, prefix) => {
            if (!full || !prefix) return false;
            if (prefix.length > full.length) return false;
            for (let i = 0; i < prefix.length; i++) if (full[i] !== prefix[i]) return false;
            return true;
        };
        if (currentEditingPath && isPrefix(currentEditingPath, path)) {
            currentEditingPath = [];
            const empty = document.getElementById('empty-state');
            if (empty) {
                empty.style.display = 'flex';
                empty.textContent = '请选择一个节点开始编辑';
            }
            const editorContent = document.getElementById('editor-content');
            if (editorContent) editorContent.style.display = 'none';
        }

        buildMergedTree();
        renderTree();
    }
    else if (message.startsWith("RENAME_KEY_OK:")) {
        const payload = JSON.parse(message.substring("RENAME_KEY_OK:".length));
        const oldPath = payload.oldPath || [];
        const newPath = payload.newPath || [];
        if (!oldPath.length || !newPath.length) return;

        // 更新本地数据（所有语言）并保持 key 的顺序不变
        for (const lang of languages) {
            if (!allData[lang]) continue;
            renameKeyInObjectPreserveOrder(allData[lang], oldPath, newPath[newPath.length - 1]);
        }

        // 更新 mergedTree（树结构以主语言为准）
        buildMergedTree();
        renderTree();

        // 如果当前正在编辑该节点，校正路径与 UI
        if (currentEditingPath && currentEditingPath.join('\0') === oldPath.join('\0')) {
            currentEditingPath = newPath;
            const title = document.getElementById('key-title');
            if (title) title.textContent = newPath[newPath.length - 1];
            const pathInfo = document.getElementById('pathInfo');
            if (pathInfo) pathInfo.textContent = "路径: " + currentEditingPath.join(" > ");
        }
    }
    else if (message.startsWith("LANG_FILE:")) {
        const path = message.substring("LANG_FILE:".length);
        const input = document.getElementById('new-lang-path');
        if (input) input.value = path;
    }
    else if (message.startsWith("LANG_FILE2:")) {
        try {
            const payload = JSON.parse(message.substring("LANG_FILE2:".length));
            if (payload && payload.target === 'compile') {
                const input = document.getElementById('compile-lang-path');
                if (input) input.value = payload.path || '';
            }
        } catch { }
    }
    else if (message.startsWith("DIRTY_STATE:")) {
        const payload = JSON.parse(message.substring("DIRTY_STATE:".length));
        dirtyLanguages = new Set(payload.dirtyLanguages || []);
        updateDirtyUI();
    }
    else if (message === "SAVE_SUCCESS") {
        dirtyLanguages = new Set();
        updateDirtyUI();
        const msg = document.getElementById('save-msg');
        if (msg) {
            msg.textContent = "已保存到磁盘";
            msg.style.opacity = '1';
            setTimeout(() => msg.style.opacity = '0', 1500);
        }
    }
});

function deleteKeyInObject(rootObj, path) {
    const info = getParentAndKey(path);
    if (!info) return false;
    const parent = getObjectByPath(rootObj, info.parentPath);
    if (!parent || typeof parent !== 'object') return false;
    const key = info.key;
    if (!(key in parent)) return false;
    delete parent[key];
    return true;
}

function insertKeyAtEndPreserveOrder(obj, key, value) {
    const keys = Object.keys(obj);
    const next = {};
    for (const k of keys) next[k] = obj[k];
    next[key] = value;
    for (const k of keys) delete obj[k];
    for (const k of Object.keys(next)) obj[k] = next[k];
}

function renameKeyInObjectPreserveOrder(rootObj, oldPath, newKey) {
    const info = getParentAndKey(oldPath);
    if (!info) return false;
    const parent = getObjectByPath(rootObj, info.parentPath);
    if (!parent || typeof parent !== 'object') return false;
    const oldKey = info.key;
    if (!(oldKey in parent)) return false;
    if (newKey in parent) return false;

    const keys = Object.keys(parent);
    const newParent = {};
    for (const k of keys) {
        if (k === oldKey) newParent[newKey] = parent[oldKey];
        else newParent[k] = parent[k];
    }

    // 原地更新：先删除，再按顺序重建
    for (const k of keys) delete parent[k];
    for (const k of Object.keys(newParent)) parent[k] = newParent[k];
    return true;
}

function buildMergedTree() {
    // 根据“主语言”的 hjson 对象结构展示树
    mergedTree = {};
    if (!mainLanguage || !allData[mainLanguage]) return;
    mergeNode(mergedTree, allData[mainLanguage]);
}

function mergeNode(target, source) {
    if (typeof source !== 'object' || source === null) return;
    for (const key in source) {
        if (typeof source[key] === 'object' && source[key] !== null) {
            if (!target[key]) target[key] = {};
            mergeNode(target[key], source[key]);
        } else {
            target[key] = null;
        }
    }
}

function buildTreeDOM(obj, keyName, path) {
    if (obj !== null) { 
        const details = document.createElement('details');
        const pathKey = JSON.stringify(path);
        details.dataset.path = pathKey;
        details.open = expandedNodes.has(pathKey); // 默认收起；用户手动展开后记忆
        details.addEventListener('toggle', () => {
            if (details.open) expandedNodes.add(pathKey);
            else expandedNodes.delete(pathKey);
        });
        const summary = document.createElement('summary');
        summary.textContent = keyName;
        summary.dataset.path = JSON.stringify(path);
        summary.dataset.nodeType = 'object';
        details.appendChild(summary);
        for (const key in obj) {
            details.appendChild(buildTreeDOM(obj[key], key, [...path, key]));
        }
        return details;
    } else {
        const div = document.createElement('div');
        div.className = 'leaf';
        div.textContent = keyName;
        div.onclick = () => openEditor(keyName, path);
        div.dataset.path = JSON.stringify(path);
        div.dataset.nodeType = 'leaf';
        return div;
    }
}

function renderTree() {
    const container = document.getElementById('tree-container');
    if (!container) return;
    container.innerHTML = '';
    for (const key in mergedTree) {
        container.appendChild(buildTreeDOM(mergedTree[key], key, [key]));
    }
}

function collapseAll() {
    expandedNodes = new Set();
    renderTree();
}

function addRootKey(kind) {
    if (!projectLoaded) return;
    addChildKey([], kind);
}

function openEditor(key, path) {
    currentEditingPath = path;
    const empty = document.getElementById('empty-state');
    if (empty) empty.style.display = 'none';
    const editorContent = document.getElementById('editor-content');
    if (editorContent) editorContent.style.display = 'flex';
    document.getElementById('key-title').textContent = key;
    document.getElementById('pathInfo').textContent = "路径: " + path.join(" > ");
    
    renderLangSelectors();
    loadValueIntoEditor();
}

function hideTreeContextMenu() {
    const menu = document.getElementById('tree-context-menu');
    if (!menu) return;
    menu.style.display = 'none';
    menu.innerHTML = '';
    contextMenuState = null;
}

document.addEventListener('click', function(e) {
    const menu = document.getElementById('tree-context-menu');
    if (!menu) return;
    if (menu.style.display === 'none') return;
    if (!menu.contains(e.target)) hideTreeContextMenu();
});

document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') hideTreeContextMenu();
});

// 在树区域统一接管右键菜单（屏蔽浏览器默认菜单）
document.addEventListener('contextmenu', function(e) {
    const tree = document.getElementById('tree-container');
    if (!tree) return;
    if (!tree.contains(e.target)) return;

    // 树区域内：不出现浏览器默认右键菜单
    e.preventDefault();

    // 找到最近的可操作节点（Object 的 summary / 叶子 div.leaf）
    const nodeEl = e.target.closest('summary,[data-node-type="leaf"]');
    if (!nodeEl || !tree.contains(nodeEl)) {
        showRootContextMenu(e.clientX, e.clientY);
        return;
    }

    const rawPath = nodeEl.dataset.path;
    if (!rawPath) return;
    let path;
    try { path = JSON.parse(rawPath); } catch { return; }

    const isObject = nodeEl.tagName.toLowerCase() === 'summary' || nodeEl.dataset.nodeType === 'object';
    showTreeContextMenu(e.clientX, e.clientY, path, isObject);
}, true);

function showRootContextMenu(x, y) {
    if (!projectLoaded) return;
    const menu = document.getElementById('tree-context-menu');
    if (!menu) return;

    contextMenuState = { path: [], isObject: true };
    menu.innerHTML = '';

    const addItem = (label, onClick, opts = {}) => {
        const item = document.createElement('div');
        item.className = 'cm-item' + (opts.danger ? ' danger' : '') + (opts.disabled ? ' disabled' : '');
        item.textContent = label;
        if (!opts.disabled) {
            item.onclick = () => {
                hideTreeContextMenu();
                onClick();
            };
        }
        menu.appendChild(item);
    };

    const addSep = () => {
        const sep = document.createElement('div');
        sep.className = 'cm-sep';
        menu.appendChild(sep);
    };

    addItem('根目录添加 Object', () => addChildKey([], 'object'));
    addItem('根目录添加 string', () => addChildKey([], 'string'));
    addSep();
    addItem('全部收起', () => collapseAll());

    // 定位与防止出屏
    menu.style.display = 'block';
    menu.style.left = x + 'px';
    menu.style.top = y + 'px';

    const rect = menu.getBoundingClientRect();
    let nx = x, ny = y;
    if (rect.right > window.innerWidth) nx = Math.max(8, window.innerWidth - rect.width - 8);
    if (rect.bottom > window.innerHeight) ny = Math.max(8, window.innerHeight - rect.height - 8);
    menu.style.left = nx + 'px';
    menu.style.top = ny + 'px';
}

function showTreeContextMenu(x, y, path, isObject) {
    if (!projectLoaded) return;
    const menu = document.getElementById('tree-context-menu');
    if (!menu) return;

    contextMenuState = { path, isObject };
    menu.innerHTML = '';

    const addItem = (label, onClick, opts = {}) => {
        const item = document.createElement('div');
        item.className = 'cm-item' + (opts.danger ? ' danger' : '') + (opts.disabled ? ' disabled' : '');
        item.textContent = label;
        if (!opts.disabled) {
            item.onclick = () => {
                hideTreeContextMenu();
                onClick();
            };
        }
        menu.appendChild(item);
    };

    const addSep = () => {
        const sep = document.createElement('div');
        sep.className = 'cm-sep';
        menu.appendChild(sep);
    };

    addItem('重命名', () => renameKeyAtPath(path));

    if (isObject) {
        addSep();
        addItem('添加子对象', () => addChildKey(path, 'object'));
        addItem('添加翻译项', () => addChildKey(path, 'string'));
    }

    addSep();
    addItem('删除', () => deleteKeyAtPath(path), { danger: true });

    // 定位与防止出屏
    menu.style.display = 'block';
    menu.style.left = x + 'px';
    menu.style.top = y + 'px';

    const rect = menu.getBoundingClientRect();
    let nx = x, ny = y;
    if (rect.right > window.innerWidth) nx = Math.max(8, window.innerWidth - rect.width - 8);
    if (rect.bottom > window.innerHeight) ny = Math.max(8, window.innerHeight - rect.height - 8);
    menu.style.left = nx + 'px';
    menu.style.top = ny + 'px';
}

function getParentAndKey(path) {
    if (!path || path.length === 0) return null;
    const parentPath = path.slice(0, -1);
    const key = path[path.length - 1];
    return { parentPath, key };
}

function getObjectByPath(dataObj, path) {
    let target = dataObj;
    for (let i = 0; i < path.length; i++) {
        if (target === undefined || target === null) return null;
        target = target[path[i]];
    }
    return target;
}

function ensureObjectByPath(dataObj, path) {
    let target = dataObj;
    for (let i = 0; i < path.length; i++) {
        const k = path[i];
        if (typeof target[k] !== 'object' || target[k] === null) target[k] = {};
        target = target[k];
    }
    return target;
}

function renameKeyAtPath(path) {
    const info = getParentAndKey(path);
    if (!info) return;
    const oldKey = info.key;

    const newKey = prompt('请输入新的键名', oldKey);
    if (!newKey) return;
    const trimmed = newKey.trim();
    if (!trimmed) return;
    if (trimmed === oldKey) return;
    if (trimmed.includes('.') || trimmed.includes(':')) {
        alert('键名不支持包含 "." 或 ":"（请使用普通键名）');
        return;
    }

    // 如果正在编辑该节点，先更新本地路径/UI（后端会回推 LOAD 再校正）
    if (currentEditingPath && currentEditingPath.join('\0') === path.join('\0')) {
        currentEditingPath = [...info.parentPath, trimmed];
        const title = document.getElementById('key-title');
        if (title) title.textContent = trimmed;
        const pathInfo = document.getElementById('pathInfo');
        if (pathInfo) pathInfo.textContent = "路径: " + currentEditingPath.join(" > ");
    }

    const payload = { path: path, newKey: trimmed };
    window.external.sendMessage("RENAME_KEY:" + JSON.stringify(payload));
}

function deleteKeyAtPath(path) {
    const info = getParentAndKey(path);
    if (!info) return;
    const key = info.key;

    const ok = confirm(`确认删除键：${path.join(' > ')} ？`);
    if (!ok) return;

    // 如果删的是当前正在编辑的节点或其祖先，先关闭编辑器
    const isPrefix = (full, prefix) => {
        if (!full || !prefix) return false;
        if (prefix.length > full.length) return false;
        for (let i = 0; i < prefix.length; i++) if (full[i] !== prefix[i]) return false;
        return true;
    };
    if (currentEditingPath && isPrefix(currentEditingPath, path)) {
        currentEditingPath = [];
        const empty = document.getElementById('empty-state');
        if (empty) {
            empty.style.display = 'flex';
            empty.textContent = '请选择一个节点开始编辑';
        }
        const editorContent = document.getElementById('editor-content');
        if (editorContent) editorContent.style.display = 'none';
    }

    const payload = { path: path };
    window.external.sendMessage("DELETE_KEY:" + JSON.stringify(payload));
}

function addChildKey(parentPath, kind) {
    const key = prompt(`请输入要添加的子键名（${kind === 'object' ? 'Object' : 'string'}）`, '');
    if (!key) return;
    const trimmed = key.trim();
    if (!trimmed) return;
    if (trimmed.includes('.') || trimmed.includes(':')) {
        alert('键名不支持包含 "." 或 ":"（请使用普通键名）');
        return;
    }

    const payload = { parentPath: parentPath, key: trimmed, kind: kind };
    window.external.sendMessage("ADD_KEY:" + JSON.stringify(payload));
}

// 根目录添加：希望新 key 插入到末尾（保持 UI 预期），所以直接走后端创建并在成功回执里再同步本地，
// 这里不做任何本地预插入，避免顺序被“重建树”时扰动。

function renderLangSelectors() {
    const container = document.getElementById('lang-container');
    container.innerHTML = '';
    languages.forEach(lang => {
        const label = document.createElement('label');
        const radio = document.createElement('input');
        radio.type = 'radio';
        radio.name = 'language';
        radio.value = lang;
        radio.checked = (lang === currentLang);
        radio.onchange = (e) => {
            currentLang = e.target.value;
            loadValueIntoEditor();
        };
        label.appendChild(radio);
        label.appendChild(document.createTextNode(lang));
        container.appendChild(label);
    });
}

function getValueByPath(dataObj, path) {
    let target = dataObj;
    for (let i = 0; i < path.length; i++) {
        if (target === undefined || target === null) return "";
        target = target[path[i]];
    }
    return target === undefined ? "" : target;
}

function setValueByPath(dataObj, path, value) {
    let target = dataObj;
    for (let i = 0; i < path.length - 1; i++) {
        if (typeof target[path[i]] !== 'object' || target[path[i]] === null) {
            target[path[i]] = {}; 
        }
        target = target[path[i]];
    }
    target[path[path.length - 1]] = value;
}

function loadValueIntoEditor() {
    const val = getValueByPath(allData[currentLang], currentEditingPath);
    document.getElementById('valInput').value = val;
}

function editKey() {
    if (currentEditingPath.length === 0) return;
    if (!projectLoaded) return;

    const newValue = document.getElementById('valInput').value;
    setValueByPath(allData[currentLang], currentEditingPath, newValue);

    const payload = {
        lang: currentLang,
        path: currentEditingPath,
        value: newValue
    };
    window.external.sendMessage("EDIT_KEY:" + JSON.stringify(payload));

    dirtyLanguages.add(currentLang);
    updateDirtyUI();

    const msg = document.getElementById('save-msg');
    if (msg) {
        msg.textContent = "已暂存更改，选择文件-保存项目写入文件";
        msg.style.opacity = '1';
        setTimeout(() => msg.style.opacity = '0', 1500);
    }
}

function showAddLangModal() {
    if (!projectLoaded) return;
    updateAddLangMainCheckboxState();
    document.getElementById('modal-overlay').style.display = 'flex';
}

function closeAddLangModal() {
    document.getElementById('modal-overlay').style.display = 'none';
    document.getElementById('new-lang-key').value = '';
    document.getElementById('new-lang-path').value = '';
    addLangIsMain = false;
    const hint = document.getElementById('toggle-main-lang-hint');
    if (hint) hint.textContent = '';
    const btn = document.getElementById('toggle-main-lang-btn');
    if (btn) {
        btn.disabled = false;
        btn.classList.remove('btn-toggle-active');
        btn.textContent = '不设为主语言';
    }
}

function submitAddLang() {
    const key = document.getElementById('new-lang-key').value.trim();
    const path = document.getElementById('new-lang-path').value.trim();
    // 兜底：无语言时必定为主语言
    const mustBeMain = Array.isArray(languages) && languages.length === 0;
    const isMain = mustBeMain ? true : !!addLangIsMain;

    if (!key || !path) {
        alert("请填写语言标识符，并选择一个已存在的语言文件路径");
        return;
    }

    const payload = {
        key: key,
        path: path,
        isMain: isMain
    };

    window.external.sendMessage("ADD_LANGUAGE:" + JSON.stringify(payload));
    
    closeAddLangModal();
}

function showCompileLangModal() {
    if (!projectLoaded) return;

    compileSetMain = false;
    compileSelectedLang = mainLanguage || ((languages && languages.length) ? languages[0] : "");
    renderCompileLangList();

    const pathInput = document.getElementById('compile-lang-path');
    if (pathInput) pathInput.value = (languageFiles && compileSelectedLang) ? (languageFiles[compileSelectedLang] || '') : '';

    document.getElementById('compile-modal-overlay').style.display = 'flex';
}

function closeCompileLangModal() {
    document.getElementById('compile-modal-overlay').style.display = 'none';
    const pathInput = document.getElementById('compile-lang-path');
    if (pathInput) pathInput.value = '';
    compileSelectedLang = "";
    compileSetMain = false;
}

function renderCompileLangList() {
    const list = document.getElementById('compile-lang-list');
    if (!list) return;
    list.innerHTML = '';

    (languages || []).forEach(lang => {
        const row = document.createElement('div');
        row.className = 'select-item' + (lang === compileSelectedLang ? ' selected' : '');
        row.tabIndex = 0;
        row.role = 'button';

        const left = document.createElement('div');
        left.textContent = lang;

        const right = document.createElement('div');
        right.className = 'subtle';
        right.textContent = (lang === mainLanguage) ? '主语言' : '';

        const applySelect = () => {
            compileSelectedLang = lang;
            compileSetMain = false;
            renderCompileLangList();
            const pathInput = document.getElementById('compile-lang-path');
            if (pathInput) pathInput.value = (languageFiles && compileSelectedLang) ? (languageFiles[compileSelectedLang] || '') : '';
        };

        row.onclick = applySelect;
        row.onkeydown = (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                applySelect();
            }
        };

        row.appendChild(left);
        row.appendChild(right);
        list.appendChild(row);
    });
}

function pickCompileLanguageFile() {
    if (!projectLoaded) return;
    window.external.sendMessage("PICK_LANGUAGE_FILE2:" + JSON.stringify({ target: "compile" }));
}

function markCompileAsMain() {
    if (!compileSelectedLang) {
        alert('请先选择一个语种');
        return;
    }
    compileSetMain = true;
}

function submitCompileLang() {
    if (!projectLoaded) return;
    if (!compileSelectedLang) {
        alert('请选择一个语种');
        return;
    }

    const path = (document.getElementById('compile-lang-path').value || '').trim();
    if (!path) {
        alert('请选择语种文件路径');
        return;
    }

    const payload = {
        key: compileSelectedLang,
        path: path,
        setMain: !!compileSetMain
    };

    window.external.sendMessage("EDIT_LANGUAGE_CONFIG:" + JSON.stringify(payload));
    closeCompileLangModal();
}

function escapeHtml(s) {
    return (s ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

function renderMarkdown(md) {
    // 轻量渲染：支持 标题/段落/列表/粗体/行内代码/代码块/链接
    const parts = String(md ?? '').split('```');
    let html = '';

    for (let i = 0; i < parts.length; i++) {
        const chunk = parts[i];
        if (i % 2 === 1) {
            // code fence（忽略语言标识）
            const lines = chunk.replaceAll('\r\n', '\n').split('\n');
            if (lines.length > 0 && /^[a-zA-Z0-9_-]+$/.test(lines[0].trim())) lines.shift();
            html += `<pre><code>${escapeHtml(lines.join('\n'))}</code></pre>`;
            continue;
        }

        const lines = chunk.replaceAll('\r\n', '\n').split('\n');
        let inList = false;
        for (const raw of lines) {
            const line = raw.trimEnd();
            const t = line.trim();

            if (!t) {
                if (inList) { html += '</ul>'; inList = false; }
                continue;
            }

            const h3 = t.match(/^###\s+(.+)$/);
            const h2 = t.match(/^##\s+(.+)$/);
            const h1 = t.match(/^#\s+(.+)$/);
            if (h3 || h2 || h1) {
                if (inList) { html += '</ul>'; inList = false; }
                const level = h3 ? 3 : h2 ? 2 : 1;
                const text = escapeHtml((h3 || h2 || h1)[1]);
                html += `<h${level}>${text}</h${level}>`;
                continue;
            }

            const li = t.match(/^[-*]\s+(.+)$/);
            if (li) {
                if (!inList) { html += '<ul>'; inList = true; }
                html += `<li>${escapeHtml(li[1])}</li>`;
                continue;
            }

            if (inList) { html += '</ul>'; inList = false; }

            let p = escapeHtml(t);
            // inline code
            p = p.replace(/`([^`]+)`/g, (_, c) => `<code>${escapeHtml(c)}</code>`);
            // bold
            p = p.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
            // links
            p = p.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, text, url) => `<a href="${escapeHtml(url)}" target="_blank" rel="noreferrer">${escapeHtml(text)}</a>`);
            html += `<p>${p}</p>`;
        }
        if (inList) html += '</ul>';
    }

    return html || '<p>(空)</p>';
}

async function loadHelpMarkdown() {
    // 从 wwwroot/tutorial.md 读取，方便用户直接编辑文件来更新内容
    const res = await fetch('tutorial.md', { cache: 'no-store' });
    if (!res.ok) throw new Error('tutorial.md not found');
    return await res.text();
}

async function showHelpModal() {
    const overlay = document.getElementById('help-modal-overlay');
    const content = document.getElementById('help-content');
    if (!overlay || !content) return;
    overlay.style.display = 'flex';
    content.textContent = '正在加载教程...';

    try {
        const md = await loadHelpMarkdown();
        content.innerHTML = renderMarkdown(md);
    } catch (e) {
        content.innerHTML = renderMarkdown(`# 教程未找到\n\n请创建文件：\`wwwroot/tutorial.md\`\n`);
    }
}

function closeHelpModal() {
    const overlay = document.getElementById('help-modal-overlay');
    if (overlay) overlay.style.display = 'none';
}

window.external.sendMessage("APP_READY");