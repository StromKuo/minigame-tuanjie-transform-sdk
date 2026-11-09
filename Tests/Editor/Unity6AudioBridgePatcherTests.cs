using System;
using System.Reflection;
using NUnit.Framework;
using WeChatWASM;

public class Unity6AudioBridgePatcherTests
{
    private const string Updater = "function updateMemoryViews(){var b=wasmMemory.buffer;HEAPU8=new Uint8Array(b);}";

    private static string Patch(string code)
    {
        var type = typeof(WXConvertCore).Assembly.GetType("WeChatWASM.Unity6AudioBridgePatcher", true);
        try
        {
            return (string)type.GetMethod("Patch", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { code });
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException;
        }
    }

    [Test]
    public void FunctionExpressionInitializerRoutesToSdk()
    {
        var result = Patch("var _JS_Sound_Init=function(){if(true){nativeInit();}};" + Updater);
        StringAssert.Contains("var _JS_Sound_Init = window.WXWASMSDK._JS_Sound_Init;", result);
        StringAssert.DoesNotContain("nativeInit", result);
    }

    [Test]
    public void ExportsAllHeapViewsInsideMemoryUpdater()
    {
        var result = Patch(Updater);
        foreach (string name in new[] { "HEAP8", "HEAPU8", "HEAP16", "HEAPU16", "HEAP32", "HEAPU32", "HEAPF32", "HEAPF64" })
            StringAssert.Contains("Module[\"" + name + "\"]=" + name + ";", result);
        Assert.That(result.EndsWith("Module[\"HEAPF64\"]=HEAPF64;}"), Is.True);
    }

    [Test]
    public void RepeatedPatchIsIdempotent()
    {
        var once = Patch("var _JS_Sound_Init=function(){nativeInit();};" + Updater);
        Assert.That(Patch(once), Is.EqualTo(once));
    }

    [Test]
    public void MissingMemoryUpdaterFailsExplicitly()
    {
        Assert.Throws<InvalidOperationException>(() => Patch("var unrelated=1;"));
    }

    [Test]
    public void UnsupportedInitializerTerminatorFailsExplicitly()
    {
        Assert.Throws<InvalidOperationException>(() => Patch("var _JS_Sound_Init=function(){} , next=1;" + Updater));
    }
}
