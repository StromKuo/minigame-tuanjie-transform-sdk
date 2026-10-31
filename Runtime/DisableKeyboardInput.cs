#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
using System;
using UnityEngine;
using WeChatWASM;


internal class DisableKeyboardInput : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnGameLaunch()
    {
#if !UNITY_EDITOR
#if PLATFORM_PLAYABLEADS
        PlayableAdsInput.mobileKeyboardSupport = false;
#elif PLATFORM_WEIXINMINIGAME
        WeixinMiniGameInput.mobileKeyboardSupport = false;
#elif PLATFORM_WEBGL
#if UNITY_2022_1_OR_NEWER && !UNITY_6000_5_OR_NEWER
        // Unity 6000.5+ WebGL 播放器不再为运行时程序集引用 WebGLModule（WebGLInput 不可见）
        WebGLInput.mobileKeyboardSupport = false;
#endif
#endif
#endif
    }
}
#endif