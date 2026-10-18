// TEMPORARY one-shot (deleted after it runs): points Unity's External Tools to C:\Users\brand\UnityAndroid.
using System.IO;
using UnityEditor;
using UnityEditor.Android;

[InitializeOnLoad]
static class __AndroidToolsSetup
{
    const string Dir = "Temp/AndroidTools1";
    const string Root = "C:/Users/brand/UnityAndroid";

    static __AndroidToolsSetup()
    {
        Directory.CreateDirectory(Dir);
        if (File.Exists(Dir + "/done.txt")) return;
        EditorApplication.update += Run;
    }

    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!File.Exists(Root + "/_extract.done")) return; // wait until everything is unpacked
        EditorApplication.update -= Run;

        string log;
        try
        {
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetBool("NdkUseEmbedded", false);
            AndroidExternalToolsSettings.jdkRootPath = Root + "/OpenJDK";
            AndroidExternalToolsSettings.sdkRootPath = Root + "/SDK";
            AndroidExternalToolsSettings.ndkRootPath = Root + "/NDK";
            log = "jdk " + AndroidExternalToolsSettings.jdkRootPath
                + "\nsdk " + AndroidExternalToolsSettings.sdkRootPath
                + "\nndk " + AndroidExternalToolsSettings.ndkRootPath
                + "\ngradle " + AndroidExternalToolsSettings.gradlePath
                + "\nembedded jdk/sdk/ndk/gradle " + EditorPrefs.GetBool("JdkUseEmbedded", true) + "/" + EditorPrefs.GetBool("SdkUseEmbedded", true)
                + "/" + EditorPrefs.GetBool("NdkUseEmbedded", true) + "/" + EditorPrefs.GetBool("GradleUseEmbedded", true);
        }
        catch (System.Exception e) { log = "EXC " + e; }
        File.WriteAllText(Dir + "/result.txt", log);
        File.WriteAllText(Dir + "/done.txt", "done");
    }
}
