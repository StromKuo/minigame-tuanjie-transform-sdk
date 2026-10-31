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

- **✅ 全链路已打通（2026-10-31 实测）**：引擎启动（callMain 130ms）、3D 渲染、IMGUI、触摸输入桥接、音频初始化在微信开发者工具中全部正常（60fps）。关键修复见提交历史：强制 WebGL2 + 跳过 3.1 时代 emscriptenArgs（GL 库裁剪真凶）、代码剥离双禁用（构建侧设置）、一组运行时转换 stub。
- **使用注意**：
  - 首次使用请重置工程的 Graphics API（Auto）与 emscriptenArgs（清空）——历史 SDK 版本写入的旧值会毒害构建；
  - 构建侧需设置 `ManagedStrippingLevel=Disabled` + `stripEngineCode=false`（Unity 6.6 默认剥离会裁掉引擎启动必需代码）；
  - 每次导出后向产物 `game.js` 顶部注入 crypto polyfill（见上文）。
- 仍不可用：EmscriptenGLX 加速、性能分析插件（静态库 ABI 冲突）；TextureMin 压缩纹理适配待为 4.0 重写；真机验证待做。
- 微信官方发布 Unity 6.5+ 适配后，可 rebase 回官方分支并删除本分支补丁。
