#if UNITY_6000_0_OR_NEWER
using System;
using System.Text.RegularExpressions;

namespace WeChatWASM
{
    // Unity 6 emits the audio initializer as a variable and no longer exports every heap view.
    // Keep SDK audio initialization and its memory access on the same runtime.
    internal static class Unity6AudioBridgePatcher
    {
        internal static string Patch(string code)
        {
            var initializer = Regex.Match(code, @"\bvar\s+_JS_Sound_Init\s*=\s*function\s*\(");
            if (initializer.Success)
            {
                int end = WXConvertCore.SeekFunctionEnd(code, initializer.Index + initializer.Length - 1);
                if (end >= code.Length || code[end] != ';')
                    throw new InvalidOperationException("Unsupported Unity 6 audio initializer shape.");
                const string bridge = "var _JS_Sound_Init = window.WXWASMSDK._JS_Sound_Init;";
                code = code.Substring(0, initializer.Index) + bridge + code.Substring(end + 1);
            }

            const string marker = "function updateMemoryViews(){";
            int memoryStart = code.IndexOf(marker, StringComparison.Ordinal);
            if (memoryStart < 0)
                throw new InvalidOperationException("Unity 6 updateMemoryViews was not found.");
            int memoryEnd = WXConvertCore.SeekFunctionEnd(code, memoryStart + marker.Length - 1);
            if (memoryEnd > code.Length || code[memoryEnd - 1] != '}')
                throw new InvalidOperationException("Unsupported Unity 6 memory updater shape.");

            // Export inside the updater so memory growth cannot leave stale SDK typed arrays.
            const string exports = ";Module[\"HEAP8\"]=HEAP8;Module[\"HEAPU8\"]=HEAPU8;" +
                "Module[\"HEAP16\"]=HEAP16;Module[\"HEAPU16\"]=HEAPU16;" +
                "Module[\"HEAP32\"]=HEAP32;Module[\"HEAPU32\"]=HEAPU32;" +
                "Module[\"HEAPF32\"]=HEAPF32;Module[\"HEAPF64\"]=HEAPF64;";
            if (!code.Substring(memoryStart, memoryEnd - memoryStart).Contains(exports))
                code = code.Insert(memoryEnd - 1, exports);
            return code;
        }
    }
}
#endif
