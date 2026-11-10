/* eslint-disable no-underscore-dangle */
const PreLoadKeys = '$PreLoadKeys'; 
const storage = {
    _cacheData: {},
    _handleList: [],
    isRunning: false,
    isCallDeletedAll: false,
    getData(key, defaultValue) {
        let v = this._cacheData[key];
        if (v === null) {
            return defaultValue;
        }
        if (typeof v !== 'undefined') {
            return v;
        }
        if (this.isCallDeletedAll) {
            return defaultValue;
        }
        try {
            v = wx.getStorageSync(key);
            this._cacheData[key] = v !== '' ? v : null;
            return v === '' ? defaultValue : v;
        }
        catch (e) {
            // console.error(e);
            return defaultValue;
        }
    },
    setData(key, value) {
        this._cacheData[key] = value;
        this._handleList.push({
            type: 'setData',
            key,
            value,
        });
        this._doRun();
    },
    deleteKey(key) {
        this._cacheData[key] = null;
        this._handleList.push({
            type: 'deleteKey',
            key,
        });
        this._doRun();
    },
    deleteAll() {
        Object.keys(this._cacheData).forEach((key) => {
            this._cacheData[key] = null;
        });
        this.isCallDeletedAll = true;
        this._handleList.push({
            type: 'deleteAll',
        });
        this._doRun();
    },
    _doRun() {
        if (this.isRunning || this._handleList.length === 0) {
            return false;
        }
        this.isRunning = true;
        const task = this._handleList.shift();
        if (!task) {
            this.isRunning = false;
            this._doRun();
            return;
        }
        if (task.type === 'setData') {
            wx.setStorage({
                key: task.key || 'defaultKey',
                data: task.value,
                fail({ errMsg }) {
                    console.error(errMsg);
                },
                complete: () => {
                    this.isRunning = false;
                    this._doRun();
                },
            });
        }
        else if (task.type === 'deleteKey') {
            wx.removeStorage({
                key: task.key || 'defaultKey',
                fail({ errMsg }) {
                    console.error(errMsg);
                },
                complete: () => {
                    this.isRunning = false;
                    this._doRun();
                },
            });
        }
        else if (task.type === 'deleteAll') {
            wx.clearStorage({
                fail({ errMsg }) {
                    console.error(errMsg);
                },
                complete: () => {
                    this.isRunning = false;
                    this._doRun();
                },
            });
        }
        else {
            this.isRunning = false;
            this._doRun();
        }
    },
    init() {
        if (Array.isArray(PreLoadKeys) && PreLoadKeys.length > 0) {
            const key = PreLoadKeys.shift();
            wx.getStorage({
                key,
                success(res) {
                    storage._cacheData[key] = res.data;
                    storage.init();
                },
                fail() {
                    storage._cacheData[key] = null;
                    storage.init();
                },
            });
        }
    },
};
setTimeout(() => {
    storage.init();
}, 0);
export default {
    WXStorageGetIntSync(key, defaultValue) {
        return +(storage.getData(key, defaultValue) || '');
    },
    WXStorageSetIntSync(key, value) {
        storage.setData(key, value);
    },
    WXStorageGetFloatSync(key, defaultValue) {
        return +(storage.getData(key, defaultValue) || '');
    },
    WXStorageSetFloatSync(key, value) {
        storage.setData(key, value);
    },
    WXStorageGetStringSync(key, defaultValue) {
        return storage.getData(key, defaultValue) || '';
    },
    WXStorageSetStringSync(key, value) {
        storage.setData(key, value);
    },
    WXStorageDeleteAllSync() {
        storage.deleteAll();
    },
    WXStorageDeleteKeySync(key) {
        storage.deleteKey(key);
    },
    WXStorageHasKeySync(key) {
        return storage.getData(key, '') !== '';
    },
    // ---- 确认型存储（直连微信原生 wx.*StorageSync，不经本文件上方的缓存与异步队列）----
    // 供 C# 侧 WX.CallJSFunctionWithReturn('WXWASMSDK', '<方法名>', ...) 同步调用：
    // 返回 { ok: true, data? } 或 { ok: false, err }，JS 异常不再穿透 P/Invoke 导致 abort。
    // wx.getStorageSync 对不存在的 key 返回 ''（不抛错），与读取错误经由 ok/err 区分。
    StorageSetConfirmed(key, value) {
        try {
            wx.setStorageSync(key || 'defaultKey', value);
            return { ok: true };
        }
        catch (e) {
            return { ok: false, err: (e && e.errMsg) || String(e) };
        }
    },
    StorageGetConfirmed(key) {
        try {
            return { ok: true, data: wx.getStorageSync(key || 'defaultKey') };
        }
        catch (e) {
            return { ok: false, err: (e && e.errMsg) || String(e) };
        }
    },
    StorageRemoveConfirmed(key) {
        try {
            wx.removeStorageSync(key || 'defaultKey');
            return { ok: true };
        }
        catch (e) {
            return { ok: false, err: (e && e.errMsg) || String(e) };
        }
    },
};
