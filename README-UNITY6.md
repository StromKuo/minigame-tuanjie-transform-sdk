# Unity 6（6000.5+）使用说明

本分支（`v0.1.34-unity6`）使 SDK 可在 Unity 6000.5+（Unity 6.5 / 6.6）下完成微信小游戏转换。改动三处：

- `Runtime/WXRuntimeExtDef.cs`：`GetInstanceID()` 在 6000.5+ 为 error 级废弃，且 `EntityId → int` 隐式转换同样废弃，改用 `EntityId.ToULong()`
- `Runtime/DisableKeyboardInput.cs` / `Runtime/WXTouchInputOverride.cs`：6000.5+ 的 WebGL 播放器不再为运行时程序集暴露 `WebGLInput`（CS0103），宏排除
- `Editor/WXConvertCore.cs`：Unity 6 内置 Emscripten 4.0，与 2022 预编译静态库（Emscripten 3.1 编译）存在 libc++ ABI 冲突，禁用其链接（官方 issue #79 / #71 / #30）

已在 6000.6.7f1 实测：编辑器编译、WebGL 构建（含 IL2CPP / wasm-ld 链接）全部通过。

## 转换面板配置（微信小游戏 → 转换小游戏）

| 选项 | 状态 | 级别与原因 |
|:---|:---:|:---|
| `EmscriptenGLX(?)` | ☐ | **必须不勾**。渲染线程变体库（`libemscriptenglx-mt*.a`）不受本分支补丁拦截，勾选并开启多线程仍会链接旧库，`wasm-ld` 报 undefined symbol |
| `WebAssembly 2023(?)` | ☐ | **必须不勾**。微信运行环境尚未完整支持；SDK 转换时也会强制关闭（双保险），不要手动对抗 |
| `iOSMetal(?)` | ☐ | **必须不勾**。仅支持团结引擎，Unity 国际版不可用 |
| `WebGL2.0` | ☑ | 推荐。小游戏 Graphics API 使用 WebGL 2.0（暂不启用 WebGPU） |
| `Il2Cpp Optimize Size(?)` | ☑ | 推荐。生成代码小约 15%；游戏大量泛型集合高频访问或使用 HybridCLR 时改为不勾（OptimizeSpeed） |
| `brotli多线程压缩(?)` | 按需 | 出包更快但压缩率降低；未使用 wasm 代码分包的上线包不建议开启 |
| `Development Build`、`Auto connect Profiler`、`Profiling Funcs / Memory`、`显示性能面板`、`显示渲染日志` | 按需 | 调试选项，与 Unity 6 兼容性无关 |
| 其余选项（首包压缩、好友关系链、字体、触控、自适应屏幕等） | 按需 | 与 Unity 版本无关，按项目需求配置 |

> 注：「集成性能分析工具」（`enablePerfAnalysis`，会链接 `wx_perf` 静态库）在 v0.1.34 面板中已被隐藏（`m_EnablePerfTool = false`），默认关闭；即使配置残留开启状态，其静态库在 Unity 6 下也会被本分支补丁拦截，不会参与链接。

## 面板之外的一个必做步骤：crypto polyfill

Unity 6（Emscripten 4.0）构建时不会把 SDK 自带的 `Runtime/Plugins/crypto.jspre` 拼入 framework.js，而微信运行环境（尤其 iOS JSC）缺少 `crypto.getRandomValues`，启动会黑屏报错（官方 issue #30）。**每次转换完成后，在产物 `game.js` 最顶部注入：**

```javascript
if (typeof crypto === 'undefined' || !crypto.getRandomValues) {
    var g = typeof window !== 'undefined' ? window : (typeof GameGlobal !== 'undefined' ? GameGlobal : this);
    g.crypto = {
        getRandomValues: function(array) {
            for (var i = 0; i < array.length; i++) array[i] = (Math.random() * 256) | 0;
            return array;
        }
    };
}
```

## 已知边界

- **✅ 全链路已验证（2026-11-02，iOS + Android 真机全绿）**：引擎启动、WebGL2 直连渲染、触摸交互、OnGUI、VibrateShort、crypto polyfill（iOS JSC 实证生效）、无弹窗。
- **质量档案**：`UNITY6_REVIEW.md` ~ `UNITY6_REVIEW_V5.md`（7 轮独立审查，29 项发现全部修复关闭）
- **使用步骤**（全自动，无需手动改产物）：
  1. 导出前重置工程 Graphics API 为 Auto、清空 emscriptenArgs（历史 SDK 版本写入的旧值会毒害构建）
  2. 构建设置 `ManagedStrippingLevel=Disabled` + `stripEngineCode=false`
  3. 转换面板取消勾选 `EmscriptenGLX`
  4. 直接导出——SDK 自动完成全部补丁（含 crypto polyfill、弹窗拦截、canvas 抢锁），并有 6 项契约断言验证产物完整性
- **版本要求**：Unity 6000.6.7f1（已验证）；微信开发者工具 2.02+；基础库 3.17.5
- **仍不可用**：EmscriptenGLX 加速、性能分析插件、TextureMin 压缩纹理适配（待为 4.0 重写）；Profiler、自定义 linker 参数
- 微信官方发布 Unity 6.5+ 适配后，可 rebase 回官方分支并删除本分支补丁


### 2026-11-09 Unity 6 音频桥修复

CubeAway Unity 6000.6.8f1 验收发现：`_JS_Sound_Init` 生成为 `var ... = function(){...}`，原转换器只扫描具名 `function `，导致初始化留在 Unity 原实现，而加载已转发微信 SDK。接通初始化后又确认 SDK 读取 `Module.HEAPU8` 时为空；Emscripten 4 默认只导出部分 heap views。

`Unity6AudioBridgePatcher` 在既有转换之后将该初始化表达式连接到 SDK，并在 `updateMemoryViews` 内同步导出八种 heap views，使内存增长后引用随之更新，不改 Android 原生运行时或音频资源导入设置。未知内存更新函数形态明确失败，避免静默生成缺少桥接的产物。

已验证：完整 WxEditor 源码使用 Unity 6000.6.8f1 实际引用编译通过；真实 framework 转换和 AST 解析通过，重复转换保持一致，八种视图首次初始化及内存增长后更新测试通过。`Tests/Editor` 含五项回归测试。诊断产物经用户确认有音效、画面正常；完整源码正式提交接入后的重新导出、真机与跨机型听感仍未验收，不将临时修改导出文件视为正式接入。
