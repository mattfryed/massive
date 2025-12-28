// Wireframe Shader <https://u3d.as/26T8>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.WireframeShader.Editor.WireframeMeshGenerator
{
    internal class VertexAttributePopup : PopupWindowContent
    {
        [Flags]
        public enum AttributeFlags
        {
            None = 0,

            UV0 = 1,
            UV1 = 2,
            UV2 = 4,
            UV3 = 8,
            UV4 = 16,
            UV5 = 32,
            UV6 = 64,
            UV7 = 128,

            Normal = 256,
            Tangent = 512,
            Color = 1024,

            Skin = 2048,


            All = UV0 | UV1 | UV2 | UV3 | UV4 | UV5 | UV6 | UV7 | Normal | Tangent | Color | Skin
        }

        AttributeFlags usedAttributes;
        public VertexAttributePopup(AttributeFlags attributeFlags)
        {
            this.usedAttributes = attributeFlags;
        }
        public override Vector2 GetWindowSize()
        {
            return new Vector2(120, 15 * 20 + 3 * 4 + 10);
        }
        public override void OnGUI(Rect rect)
        {
            EditorGUI.BeginChangeCheck();
            EditorWindow.active.editorSettings.saveUseDefaultFlagsForVertexAttribute = EditorGUILayout.ToggleLeft("Default", EditorWindow.active.editorSettings.saveUseDefaultFlagsForVertexAttribute);
            if (EditorGUI.EndChangeCheck())
            {
                EditorWindow.active.Repaint();
            }

            if (GUILayout.Button("All"))
            {
                EditorWindow.active.editorSettings.saveUseDefaultFlagsForVertexAttribute = false;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags = (AttributeFlags)~0;

                EditorWindow.active.Repaint();
            }
            if (GUILayout.Button("None"))
            {
                EditorWindow.active.editorSettings.saveUseDefaultFlagsForVertexAttribute = false;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags = (AttributeFlags)0;

                EditorWindow.active.Repaint();
            }

            GUILayout.Space(4);


            AttributeFlags flags = EditorWindow.active.editorSettings.saveVertexAttributeFlags;

            bool UV0 = flags.HasFlag(AttributeFlags.UV0);
            bool UV1 = flags.HasFlag(AttributeFlags.UV1);
            bool UV2 = flags.HasFlag(AttributeFlags.UV2);
            bool UV3 = flags.HasFlag(AttributeFlags.UV3);
            bool UV4 = flags.HasFlag(AttributeFlags.UV4);
            bool UV5 = flags.HasFlag(AttributeFlags.UV5);
            bool UV6 = flags.HasFlag(AttributeFlags.UV6);
            bool UV7 = flags.HasFlag(AttributeFlags.UV7);

            bool normal = flags.HasFlag(AttributeFlags.Normal);
            bool tangent = flags.HasFlag(AttributeFlags.Tangent);
            bool color = flags.HasFlag(AttributeFlags.Color);

            bool skin = flags.HasFlag(AttributeFlags.Skin);


            EditorGUI.BeginChangeCheck();
            {
                using (new EditorGUIHelper.GUIEnabled(EditorWindow.active.editorSettings.saveUseDefaultFlagsForVertexAttribute == false))
                {
                    DrawFlag(ref UV0, AttributeFlags.UV0);
                    DrawFlag(ref UV1, AttributeFlags.UV1);
                    DrawFlag(ref UV2, AttributeFlags.UV2);
                    DrawFlag(ref UV3, AttributeFlags.UV3);
                    DrawFlag(ref UV4, AttributeFlags.UV4);
                    DrawFlag(ref UV5, AttributeFlags.UV5);
                    DrawFlag(ref UV6, AttributeFlags.UV6);
                    DrawFlag(ref UV7, AttributeFlags.UV7);

                    DrawSeparator();

                    DrawFlag(ref normal, AttributeFlags.Normal);
                    DrawFlag(ref tangent, AttributeFlags.Tangent);
                    DrawFlag(ref color, AttributeFlags.Color);

                    DrawSeparator();

                    skin = EditorGUILayout.ToggleLeft("Skin", skin);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                EditorWindow.active.editorSettings.saveVertexAttributeFlags = 0;

                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV0 ? AttributeFlags.UV0 : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV1 ? AttributeFlags.UV1 : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV2 ? AttributeFlags.UV2 : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV3 ? AttributeFlags.UV3 : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV4 ? AttributeFlags.UV4 : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV5 ? AttributeFlags.UV5 : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV6 ? AttributeFlags.UV6 : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= UV7 ? AttributeFlags.UV7 : 0;

                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= normal ? AttributeFlags.Normal : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= tangent ? AttributeFlags.Tangent : 0;
                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= color ? AttributeFlags.Color : 0;

                EditorWindow.active.editorSettings.saveVertexAttributeFlags |= skin ? AttributeFlags.Skin : 0;

                EditorWindow.active.Repaint();
            }
        }

        void DrawFlag(ref bool value, AttributeFlags flag)
        {
            if (usedAttributes.HasFlag(flag))
            {
                Rect rect = EditorGUILayout.GetControlRect();
                EditorGUI.DrawRect(rect, Color.gray);

                using (new EditorGUIHelper.GUIEnabled(false))
                {
                    EditorGUI.ToggleLeft(rect, flag.ToString(), true);
                }
            }
            else
            {
                value = EditorGUILayout.ToggleLeft(flag.ToString(), value);
            }
        }

        private void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 4);
            EditorGUI.DrawRect(new Rect(rect.xMin + 3, rect.yMin + 2, rect.width - 6, 1), Color.gray * 0.85f);
        }
        static public string GetLabelName(AttributeFlags editorAttributeFlags, AttributeFlags usedAttributeFlags)
        {
            usedAttributeFlags |= editorAttributeFlags;

            int bytesCount = 0;

            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV0) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV1) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV2) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV3) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV4) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV5) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV6) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.UV7) ? 1 : 0;

            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.Normal) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.Tangent) ? 1 : 0;
            bytesCount += usedAttributeFlags.HasFlag(AttributeFlags.Color) ? 1 : 0;


            if (bytesCount == 0)
                return "None";
            else if (bytesCount == 1)   //One
                return usedAttributeFlags.ToString();
            else if (bytesCount == 11)  //All
                return "All";
            else
                return "Mixed";
        }
    }
}