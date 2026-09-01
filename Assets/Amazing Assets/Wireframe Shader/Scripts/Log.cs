// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System.Runtime.CompilerServices;

using UnityEngine;


[assembly: InternalsVisibleTo("AmazingAssets.WireframeShader.Editor")]
[assembly: InternalsVisibleTo("AmazingAssets.WireframeShaderEditor")]
namespace AmazingAssets.WireframeShader
{
    static internal class WireframeShaderDebug
    {
        static public void Log(string message)
        {
            Log(LogType.Log, message, null, null);
        }
        static public void Log(LogType logType, string message) 
        {
            Log(logType, message, null, null);
        }
        static public void Log(LogType logType, string message, System.Exception exception, UnityEngine.Object context = null)
        {
            message = $"[{WireframeShaderAbout.name}]\n" + message;

            StackTraceLogType save = Application.GetStackTraceLogType(logType);
            Application.SetStackTraceLogType(logType, StackTraceLogType.None);

            switch (logType)
            {
                case LogType.Assert:
                    {
                        if (context == null)
                            UnityEngine.Debug.LogAssertion(message);
                        else
                            UnityEngine.Debug.LogAssertion(message, context);

                    }
                    break;

                case LogType.Error:
                    {
                        if (context == null)
                            UnityEngine.Debug.LogError(message);
                        else
                            UnityEngine.Debug.LogError(message, context);
                    }
                    break;

                case LogType.Exception:
                    {
                        if (context == null)
                            UnityEngine.Debug.LogException(exception);
                        else
                            UnityEngine.Debug.LogException(exception, context);
                    }
                    break;

                case LogType.Log:
                    {
                        if (context == null)
                            UnityEngine.Debug.Log(message);
                        else
                            UnityEngine.Debug.Log(message, context);

                    }
                    break;

                case LogType.Warning:
                    {
                        if (context == null)
                            UnityEngine.Debug.LogWarning(message);
                        else
                            UnityEngine.Debug.LogWarning(message, context);

                    }
                    break;
            }

            Application.SetStackTraceLogType(logType, save);
        }
    }
}