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

// 初始状态：未加载项目时禁用“添加语种”
setAddLanguageEnabled(false);
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

        mainLanguage = payload.mainLanguage || (languages.length > 0 ? languages[0] : "");
        dirtyLanguages = new Set(payload.dirtyLanguages || []);

        projectLoaded = true;
        setAddLanguageEnabled(true);
        updateDirtyUI();
        
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
    else if (message.startsWith("LANG_FILE:")) {
        const path = message.substring("LANG_FILE:".length);
        const input = document.getElementById('new-lang-path');
        if (input) input.value = path;
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
        details.open = true;
        const summary = document.createElement('summary');
        summary.textContent = keyName;
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
    document.getElementById('modal-overlay').style.display = 'flex';
}

function closeAddLangModal() {
    document.getElementById('modal-overlay').style.display = 'none';
    document.getElementById('new-lang-key').value = '';
    document.getElementById('new-lang-path').value = '';
    document.getElementById('is-main-lang').checked = false;
}

function submitAddLang() {
    const key = document.getElementById('new-lang-key').value.trim();
    const path = document.getElementById('new-lang-path').value.trim();
    const isMain = document.getElementById('is-main-lang').checked;

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