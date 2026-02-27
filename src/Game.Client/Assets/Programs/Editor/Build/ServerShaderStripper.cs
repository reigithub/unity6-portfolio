using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor.Build
{
    /// <summary>
    /// Dedicated Server ビルド時にシェーダーバリアントを全てストリップする
    /// </summary>
    public class ServerShaderStripper : IPreprocessShaders
    {
        /// <summary>
        /// 他のストリッパーより先に実行
        /// </summary>
        public int callbackOrder => -100;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Server)
            {
                return;
            }

            data.Clear();
        }
    }
}
